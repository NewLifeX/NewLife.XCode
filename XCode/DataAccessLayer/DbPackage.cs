using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Threading;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Serialization;
using XCode.Transform;

namespace XCode.DataAccessLayer;

/// <summary>数据包。数据的备份与恢复</summary>
public class DbPackage
{
    #region 属性
    /// <summary>
    /// 数据库连接
    /// </summary>
    public DAL Dal { get; set; } = null!;

    /// <summary>数据页事件</summary>
    public event EventHandler<PageEventArgs>? OnPage;

    /// <summary>批大小。用于批量操作数据，默认5000</summary>
    public Int32 BatchSize { get; set; }

    /// <summary>批量处理时，忽略单表错误，继续处理下一个。默认true</summary>
    public Boolean IgnoreError { get; set; } = true;

    /// <summary>批量处理时，忽略单页错误，继续处理下一个。默认false</summary>
    public Boolean IgnorePageError { get; set; }

    /// <summary>批量插入，提供最好吞吐，默认true。为false时关闭批量，采用逐行插入并忽略单行错误</summary>
    public Boolean BatchInsert { get; set; } = true;

    /// <summary>数据保存模式。默认Insert</summary>
    public SaveModes Mode { get; set; } = SaveModes.Insert;

    /// <summary>数据抽取器的创建回调，支持外部自定义</summary>
    public Func<IDataTable, IExtracter<DbTable>>? CreateExtracterCallback { get; set; }

    /// <summary>写文件Actor的创建回调，支持外部自定义</summary>
    public Func<WriteFileActor>? WriteFileCallback { get; set; }

    /// <summary>写数据库Actor的创建回调，支持外部自定义</summary>
    public Func<WriteDbActor>? WriteDbCallback { get; set; }

    /// <summary>性能追踪器</summary>
    public ITracer? Tracer { get; set; } = DAL.GlobalTracer;
    #endregion

    #region 构造
    /// <summary>实例化数据包</summary>
    public DbPackage()
    {
        CreateExtracterCallback = GetExtracter;
    }
    #endregion

    #region 备份
    /// <summary>备份单表数据，抽取数据和写入文件双线程</summary>
    /// <remarks>
    /// 最大支持21亿行
    /// </remarks>
    /// <param name="table">数据表</param>
    /// <param name="stream">目标数据流</param>
    /// <returns></returns>
    public virtual Int32 Backup(IDataTable table, Stream stream) => Backup(table, stream, default);

    /// <summary>备份单表数据，抽取数据和写入文件双线程</summary>
    /// <remarks>
    /// 最大支持21亿行
    /// </remarks>
    /// <param name="table">数据表</param>
    /// <param name="stream">目标数据流</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual Int32 Backup(IDataTable table, Stream stream, CancellationToken cancellationToken = default)
    {
        using var span = Tracer?.NewSpan($"db:{Dal.ConnName}:Backup", table.Name);

        // 并行写入文件，提升吞吐
        using var writeFile = WriteFileCallback?.Invoke() ?? new WriteFileActor { BoundedCapacity = 4, };

        writeFile.Stream = stream;
        writeFile.TracerParent = span;
        writeFile.Tracer = Tracer;
        writeFile.Log = Log;

        var tableName = Dal.Db.FormatName(table);
        var sb = new SelectBuilder { Table = tableName };
        var connName = Dal.ConnName;

        var extracer = CreateExtracterCallback?.Invoke(table) ?? GetExtracter(table);

        // 总行数
        writeFile.Total = Dal.SelectCount(sb);
        WriteLog("备份[{0}/{1}]开始，共[{2:n0}]行，抽取器{3}", table, connName, writeFile.Total, extracer);
        writeFile.Start(cancellationToken);

        // 临时关闭日志
        var old = Dal.Db.ShowSQL;
        Dal.Db.ShowSQL = false;
        Dal.Session.ShowSQL = false;

        var total = 0;
        var sw = Stopwatch.StartNew();
        try
        {
            foreach (var dt in extracer.Fetch())
            {
                if (cancellationToken.IsCancellationRequested) break;

                var row = extracer.TotalCount;
                var count = dt.Rows.Count;
                WriteLog("备份[{0}/{1}]数据 {2:n0} + {3:n0}", table, connName, row, count);
                if (count == 0) break;

                // 字段名更换为属性名
                for (var i = 0; i < dt.Columns.Length; i++)
                {
                    var dc = table.GetColumn(dt.Columns[i]);
                    if (dc != null) dt.Columns[i] = dc.Name;
                }

                // 进度报告、消费数据
                OnProcess(table, row, dt, writeFile);

                total += count;
                if (span != null) span.Value = total;
            }

            // 通知写入完成
            writeFile.Stop(-1);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, table);
            throw;
        }
        finally
        {
            Dal.Db.ShowSQL = old;
            Dal.Session.ShowSQL = old;
        }

        sw.Stop();
        var ms = sw.Elapsed.TotalMilliseconds;
        WriteLog("备份[{0}/{1}]完成，共[{2:n0}]行，耗时{3:n0}ms，速度{4:n0}tps", table, connName, total, ms, total * 1000L / ms);

        // 返回总行数
        return total;
    }

    /// <summary>处理核心。数据抽取后，需要报告进度，以及写入Actor</summary>
    /// <param name="table">正在处理的数据表</param>
    /// <param name="row">进度</param>
    /// <param name="page">当前数据页</param>
    /// <param name="actor">处理数据Actor</param>
    /// <returns></returns>
    protected virtual Boolean OnProcess(IDataTable table, Int64 row, DbTable page, Actor actor)
    {
        // 进度报告
        OnPage?.Invoke(this, new PageEventArgs { Table = table, Row = row, Page = page });

        // 消费数据。克隆对象，避免被修改
        actor.Tell(page.Clone());

        return true;
    }

    /// <summary>获取数据抽取器。自增/数字主键->时间索引->主键分页->索引分页->默认分页</summary>
    /// <param name="table"></param>
    /// <returns></returns>
    public virtual IExtracter<DbTable> GetExtracter(IDataTable table)
    {
        var tableName = Dal.Db.FormatName(table);

        // 自增抽取，或数字主键
        var id = table.Columns.FirstOrDefault(e => e.Identity);
        if (id == null)
        {
            var pks = table.PrimaryKeys;
            if (pks.Length >= 1 && pks[0].DataType.IsInt()) id = pks[0];
        }
        if (id != null)
            return new IdExtracter(Dal, tableName, id);

        // 时间索引抽取
        IDataColumn? time = null;
        foreach (var dx in table.Indexes)
        {
            var column = table.GetColumn(dx.Columns[0]);
            if (column != null && column.DataType == typeof(DateTime))
            {
                time = column;
                break;
            }
        }
        //var time = table.Indexes.FirstOrDefault(e => table.GetColumn(e.Columns[0])?.DataType == typeof(DateTime));
        if (time != null)
            return new TimeExtracter(Dal, tableName, time);

        // 主键分页功能
        var pk = table.Columns.FirstOrDefault(e => e.PrimaryKey);
        if (pk != null)
            return new PagingExtracter(Dal, tableName, pk.ColumnName);

        // 索引分页功能
        var index = table.Indexes.FirstOrDefault();
        if (index != null)
        {
            var i_dc = index.Columns.FirstOrDefault();
            if (i_dc != null)
                return new PagingExtracter(Dal, tableName, i_dc);
        }

        // 默认第一个字段
        var dc = table.Columns.FirstOrDefault();
        return new PagingExtracter(Dal, tableName, dc?.ColumnName);
    }

    /// <summary>备份单表数据到文件</summary>
    /// <param name="table">数据表</param>
    /// <param name="file">文件。.gz后缀时采用压缩</param>
    /// <returns></returns>
    public Int32 Backup(IDataTable table, String? file = null)
    {
        if (file.IsNullOrEmpty()) file = table + ".table";

        var file2 = file.GetFullPath();
        file2.EnsureDirectory(true);

        WriteLog("备份[{0}/{1}]到文件 {2}", table, Dal.ConnName, file2);

        using var fs = new FileStream(file2, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        var rs = 0;
        if (file.EndsWithIgnoreCase(".gz"))
        {
            using var gs = new GZipStream(fs, CompressionLevel.Optimal, true);
            rs = Backup(table, gs, default);
        }
        else
        {
            rs = Backup(table, fs, default);
        }

        // 截断文件
        fs.SetLength(fs.Position);

        return rs;
    }

    /// <summary>备份一批表到指定压缩文件</summary>
    /// <param name="tables">数据表集合</param>
    /// <param name="file">zip压缩文件</param>
    /// <param name="backupSchema">备份架构</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Int32 BackupAll(IList<IDataTable> tables, String file, Boolean backupSchema = true, CancellationToken cancellationToken = default)
    {
        if (tables == null) throw new ArgumentNullException(nameof(tables));

        using var span = Tracer?.NewSpan($"db:{Dal.ConnName}:BackupAll", file);

        // 过滤不存在的表
        var ts = Dal.Tables.Select(e => e.TableName).ToArray();
        tables = tables.Where(e => e.TableName.EqualIgnoreCase(ts)).ToList();
        var connName = Dal.ConnName;

        var count = 0;
        //if (tables == null) tables = Tables;
        if (tables.Count > 0)
        {
            var file2 = file.GetFullPath();
            file2.EnsureDirectory(true);

            WriteLog("备份[{0}]到文件 {1}。{2}", connName, file2, tables.Join(",", e => e.Name));

            using var fs = new FileStream(file2, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Create, true, Encoding.UTF8);

            try
            {
                // 备份架构
                if (backupSchema)
                {
                    var xml = DAL.Export(tables);
                    var entry = zip.CreateEntry(connName + ".xml");
                    using var ms = entry.Open();
                    ms.Write(xml.GetBytes());
                }

                foreach (var item in tables)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    try
                    {
                        var entry = zip.CreateEntry(item.Name + ".table");
                        using var ms = entry.Open();
                        Backup(item, ms, cancellationToken);

                        count++;
                        if (span != null) span.Value = count;
                    }
                    catch (Exception ex)
                    {
                        if (!IgnoreError) throw;
                        XTrace.WriteException(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);
                throw;
            }
        }

        return count;
    }
    #endregion

    #region 恢复
    /// <summary>从数据流恢复数据</summary>
    /// <param name="stream">数据流</param>
    /// <param name="table">数据表</param>
    /// <returns></returns>
    public virtual Int32 Restore(Stream stream, IDataTable table) => Restore(stream, table, default);

    /// <summary>从数据流恢复数据</summary>
    /// <param name="stream">数据流</param>
    /// <param name="table">数据表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual Int32 Restore(Stream stream, IDataTable table, CancellationToken cancellationToken = default)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (table == null) throw new ArgumentNullException(nameof(table));

        using var span = Tracer?.NewSpan($"db:{Dal.ConnName}:Restore", table.Name);

        using var writeDb = WriteDbCallback?.Invoke() ?? new WriteDbActor { BoundedCapacity = 4 };
        writeDb.Host = this;
        writeDb.Dal = Dal;
        writeDb.Table = table;
        writeDb.TracerParent = span;
        writeDb.Start(cancellationToken);

        var connName = Dal.ConnName;

        // 临时关闭日志
        var old = Dal.Db.ShowSQL;
        Dal.Db.ShowSQL = false;
        Dal.Session.ShowSQL = false;
        var total = 0;
        var sw = Stopwatch.StartNew();
        try
        {
            // 二进制读写器
            var binary = new Binary
            {
                FullTime = true,
                EncodeInt = true,
                Stream = stream,
            };

            var dt = new DbTable();
            dt.ReadHeader(binary);
            WriteLog("恢复[{0}/{1}]开始，共[{2:n0}]行", table.Name, connName, dt.Total);

            // 输出日志
            var cs = dt.Columns;
            var ts = dt.Types;
            for (var i = 0; i < cs.Length; i++)
            {
                if (ts[i] == null || ts[i] == typeof(Object))
                {
                    var dc = table.GetColumn(cs[i]);
                    if (dc != null) ts[i] = dc.DataType;
                }
            }
            WriteLog("字段[{0}]：{1}", cs.Length, cs.Join());
            WriteLog("类型[{0}]：{1}", ts.Length, ts.Join(",", e => e?.Name));

            var row = 0;
            var pageSize = BatchSize;
            if (pageSize <= 0) pageSize = Dal.GetBatchSize();
            while (!cancellationToken.IsCancellationRequested)
            {
                //修复总行数是pageSize的倍数无法退出循环的情况
                if (dt.Total == row) break;
                // 读取数据
                dt.ReadData(binary, Math.Min(dt.Total - row, pageSize));

                var rs = dt.Rows;
                if (rs == null || rs.Count == 0) break;

                WriteLog("恢复[{0}/{1}]数据 {2:n0} + {3:n0}", table.Name, connName, row, rs.Count);

                // 进度报告，批量写入数据库
                OnProcess(table, row, dt, writeDb);

                // 下一页
                total += rs.Count;
                if (span != null) span.Value = total;

                if (rs.Count < pageSize) break;
                row += pageSize;
            }

            // 通知写入完成
            writeDb.Stop(-1);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
        finally
        {
            Dal.Db.ShowSQL = old;
            Dal.Session.ShowSQL = old;
        }

        sw.Stop();
        var ms = sw.Elapsed.TotalMilliseconds;
        WriteLog("恢复[{0}/{1}]完成，共[{2:n0}]行，耗时{3:n0}ms，速度{4:n0}tps", table.Name, connName, total, ms, total * 1000L / ms);

        // 返回总行数
        return total;
    }

    /// <summary>从文件恢复数据</summary>
    /// <param name="file">zip压缩文件</param>
    /// <param name="table">数据表</param>
    /// <param name="setSchema">是否设置数据表模型，自动建表</param>
    /// <returns></returns>
    public Int64 Restore(String file, IDataTable table, Boolean setSchema = true)
    {
        if (file.IsNullOrEmpty()) throw new ArgumentNullException(nameof(file));
        if (table == null) throw new ArgumentNullException(nameof(table));

        var file2 = file.GetFullPath();
        if (!File.Exists(file2)) return 0;
        file2.EnsureDirectory(true);

        WriteLog("恢复[{2}]到[{0}/{1}]", table, Dal.ConnName, file);

        if (setSchema) Dal.SetTables(table);

        // 返回恢复行数
        var compressed = file.EndsWithIgnoreCase(".gz");
        var rs = 0;
        file2.AsFile().OpenRead(compressed, s => { rs = Restore(s, table); });

        return rs;
    }

    /// <summary>从指定压缩文件恢复一批数据到目标库</summary>
    /// <param name="file">zip压缩文件</param>
    /// <param name="tables">数据表。为空时从压缩包读取xml模型文件</param>
    /// <param name="setSchema">是否设置数据表模型，自动建表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public IDataTable[]? RestoreAll(String file, IDataTable[]? tables = null, Boolean setSchema = true, CancellationToken cancellationToken = default)
    {
        if (file.IsNullOrEmpty()) throw new ArgumentNullException(nameof(file));
        //if (tables == null) throw new ArgumentNullException(nameof(tables));

        var file2 = file.GetFullPath();
        if (!File.Exists(file2)) return null;

        using var span = Tracer?.NewSpan($"db:{Dal.ConnName}:RestoreAll", file);

        using var fs = new FileStream(file2, FileMode.Open);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Read, true, Encoding.UTF8);

        // 备份架构
        if (tables == null)
        {
            var entry = zip.Entries.FirstOrDefault(e => e.Name.EndsWithIgnoreCase(".xml"));
            if (entry != null)
            {
                using var ms = entry.Open();
                tables = DAL.Import(ms.ToStr()).ToArray();
            }
        }
        if (tables == null) throw new ArgumentNullException(nameof(tables));

        WriteLog("恢复[{0}]从文件 {1}。数据表：{2}", Dal.ConnName, file2, tables.Join(",", e => e.Name));

        if (setSchema) Dal.SetTables(tables);

        try
        {
            var count = 0;
            foreach (var item in tables)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var entry = zip.GetEntry(item.Name + ".table");
                if (entry != null && entry.Length > 0)
                {
                    try
                    {
                        using var ms = entry.Open();
                        using var bs = new BufferedStream(ms);
                        Restore(bs, item, cancellationToken);

                        count++;
                        if (span != null) span.Value = count;
                    }
                    catch (Exception ex)
                    {
                        if (!IgnoreError) throw;
                        XTrace.WriteException(ex);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }

        return tables;
    }
    #endregion

    #region 同步
    /// <summary>同步单表数据</summary>
    /// <remarks>
    /// 把数据同一张表同步到另一个库
    /// </remarks>
    /// <param name="table">数据表</param>
    /// <param name="connName">目标连接名</param>
    /// <param name="syncSchema">同步架构</param>
    /// <returns></returns>
    public virtual Int32 Sync(IDataTable table, String connName, Boolean syncSchema = true) => Sync(table, connName, syncSchema, default);

    /// <summary>同步单表数据</summary>
    /// <remarks>
    /// 把数据同一张表同步到另一个库
    /// </remarks>
    /// <param name="table">数据表</param>
    /// <param name="connName">目标连接名</param>
    /// <param name="syncSchema">同步架构</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual Int32 Sync(IDataTable table, String connName, Boolean syncSchema = true, CancellationToken cancellationToken = default)
    {
        if (connName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(connName));
        if (table == null) throw new ArgumentNullException(nameof(table));

        using var span = Tracer?.NewSpan($"db:{Dal.ConnName}:Sync", $"{table.Name}->{connName}");

        var dal = DAL.Create(connName);

        using var writeDb = WriteDbCallback?.Invoke() ?? new WriteDbActor { BoundedCapacity = 4 };
        writeDb.Table = table;
        writeDb.Host = this;
        writeDb.Dal = dal;
        writeDb.TracerParent = span;
        writeDb.Start(cancellationToken);

        var extracer = CreateExtracterCallback?.Invoke(table) ?? GetExtracter(table);

        // 临时关闭日志
        var old = Dal.Db.ShowSQL;
        Dal.Db.ShowSQL = false;
        Dal.Session.ShowSQL = false;
        var total = 0;
        var sw = Stopwatch.StartNew();
        try
        {
            // 表结构
            if (syncSchema) dal.SetTables(table);

            foreach (var dt in extracer.Fetch())
            {
                if (cancellationToken.IsCancellationRequested) break;

                var row = extracer.TotalCount;
                var count = dt.Rows.Count;
                WriteLog("同步[{0}/{1}]数据 {2:n0} + {3:n0}", table.Name, Dal.ConnName, row, count);

                ////修复表的列名带下划线的会出现问题
                //for (var i=0;i< dt.Columns.Length;i++)
                //{
                //    dt.Columns[i] = dt.Columns[i].Replace("_", "");
                //}

                // 进度报告、消费数据
                OnProcess(table, row, dt, writeDb);

                total += count;
                if (span != null) span.Value = total;
            }

            // 通知写入完成
            writeDb.Stop(-1);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, table);
            throw;
        }
        finally
        {
            Dal.Db.ShowSQL = old;
            Dal.Session.ShowSQL = old;
        }

        sw.Stop();
        var ms = sw.Elapsed.TotalMilliseconds;
        WriteLog("同步[{0}/{1}]完成，共[{2:n0}]行，耗时{3:n0}ms，速度{4:n0}tps", table.Name, Dal.ConnName, total, ms, total * 1000L / ms);

        // 返回总行数
        return total;
    }

    /// <summary>备份一批表到另一个库</summary>
    /// <param name="tables">表名集合</param>
    /// <param name="connName">目标连接名</param>
    /// <param name="syncSchema">同步架构</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public IDictionary<String, Int32> SyncAll(IDataTable[] tables, String connName, Boolean syncSchema = true, CancellationToken cancellationToken = default)
    {
        if (connName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(connName));
        if (tables == null) throw new ArgumentNullException(nameof(tables));

        using var span = Tracer?.NewSpan($"db:{Dal.ConnName}:SyncAll", connName);

        var dic = new Dictionary<String, Int32>();

        if (tables.Length == 0) return dic;

        // 同步架构
        if (syncSchema) DAL.Create(connName).SetTables(tables);

        try
        {
            var count = 0;
            foreach (var item in tables)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    dic[item.Name] = Sync(item, connName, false, cancellationToken);

                    count++;
                    if (span != null) span.Value = count;
                }
                catch (Exception ex)
                {
                    if (!IgnoreError) throw;
                    XTrace.WriteException(ex);
                }
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }

        return dic;
    }
    #endregion

    #region 并行Actor
    /// <summary>
    /// 高吞吐写文件Actor
    /// </summary>
    public class WriteFileActor : Actor
    {
        /// <summary>数据流</summary>
        public Stream Stream { get; set; } = null!;

        /// <summary>总数</summary>
        public Int32 Total { get; set; }

        ///// <summary>
        ///// 日志
        ///// </summary>
        //public ILog? Log { get; set; }

        private Binary _Binary = null!;
        private Boolean _writeHeader;
        private String[] _columns = null!;

        /// <summary>开始</summary>
        /// <returns></returns>
        protected override Task OnStart(CancellationToken cancellationToken)
        {
            // 二进制读写器
            _Binary = new Binary
            {
                FullTime = true,
                EncodeInt = true,
                Stream = Stream,
            };

            return base.OnStart(cancellationToken);
        }

        /// <summary>
        /// 接收消息，写入文件
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken">取消通知</param>
        /// <returns></returns>
        protected override async Task ReceiveAsync(ActorContext context, CancellationToken cancellationToken)
        {
            if (context.Message is not DbTable dt) return;

            var bn = _Binary;
            Int32[]? fields = null;

            //using var span = Tracer?.NewSpan($"db:WriteStream", (Stream as FileStream)?.Name);

            // 写头部结构。没有数据时可以备份结构
            if (!_writeHeader)
            {
                dt.Total = Total;
                dt.WriteHeader(bn);

                // 输出日志
                var cs = dt.Columns;
                var ts = dt.Types;
                Log?.Info("字段[{0}]：{1}", cs.Length, cs.Join());
                Log?.Info("类型[{0}]：{1}", ts.Length, ts.Join(",", e => e.Name));

                _writeHeader = true;

                _columns = dt.Columns;
            }
            else
            {
                // 计算字段写入顺序，避免出现第二页开始字段变多的问题（例如rowNumber）。实际上几乎不可能出现-1，也就是首页有而后续页没有的字段
                fields = new Int32[_columns.Length];
                for (var i = 0; i < _columns.Length; i++)
                {
                    fields[i] = dt.GetColumn(_columns[i]);
                }
            }

            var rs = dt.Rows;
            if (rs == null || rs.Count == 0) return;

            // 写入文件
            if (fields != null)
                dt.WriteData(bn, fields);
            else
                dt.WriteData(bn);

            await Stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 高吞吐写数据库Actor
    /// </summary>
    public class WriteDbActor : Actor
    {
        /// <summary>父级对象</summary>
        public DbPackage Host { get; set; } = null!;

        /// <summary>
        /// 目标数据库
        /// </summary>
        public DAL Dal { get; set; } = null!;

        /// <summary>
        /// 数据表
        /// </summary>
        public IDataTable Table { get; set; } = null!;

        private IDataColumn[] _Columns = null!;

        /// <summary>
        /// 接收消息，批量插入
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken">取消通知</param>
        /// <returns></returns>
        protected override Task ReceiveAsync(ActorContext context, CancellationToken cancellationToken)
        {
            if (context.Message is not DbTable dt)
            {
#if NET45
                return Task.FromResult(0);
#else
                return Task.CompletedTask;
#endif
            }

            var dal = Dal;

            // 匹配要写入的列
            if (_Columns == null)
            {
                Tracer = Host.Tracer;

                var columns = new List<IDataColumn>();
                foreach (var item in dt.Columns)
                {
                    var dc = Table.GetColumn(item);
                    if (dc != null)
                    {
                        // 内部构建批量插入语句时，将从按照dc.Name从dt取值，Name可能与ColumnName不同，这里需要改名
                        dc = dc.Clone(Table);
                        dc.Name = item;

                        columns.Add(dc);
                    }
                }

                _Columns = columns.ToArray();

                // 这里的匹配列是目标表字段名，而DbTable数据取值是Name，两者可能不同
                Host.Log?.Info("数据表：{0}/{1}", Table.Name, Table);
                Host.Log?.Info("匹配列：{0}", _Columns.Join(",", e => e.ColumnName));
            }

            // 批量插入
            using var span = Tracer?.NewSpan($"db:{dal.ConnName}:WriteDb:{Table.TableName}", Table);
            try
            {
                if (Host.BatchInsert)
                {
                    dal.Session.Insert(Table, _Columns, dt.Cast<IModel>());
                }
                else
                {
                    foreach (var row in dt)
                    {
                        try
                        {
                            dal.Insert(row, Table, _Columns, Host.Mode);
                        }
                        catch (Exception ex)
                        {
                            span?.SetError(ex, row);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                span?.SetError(ex, dt.Rows?.Count);

                if (!Host.IgnorePageError) throw;
            }

#if NET45
            return Task.FromResult(0);
#else
            return Task.CompletedTask;
#endif
        }
    }
    #endregion

    #region 日志
    /// <summary>
    /// 日志
    /// </summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>
    /// 写日志
    /// </summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args) => Log?.Info(format, args);
    #endregion
}