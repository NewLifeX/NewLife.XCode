using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using NewLife.Data;
using NewLife.Log;
using NewLife.Threading;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
#if !NET45
using TaskEx = System.Threading.Tasks.Task;
#endif

/*
 * 检查表结构流程：
 * Create           创建实体会话，此时不能做任何操作，原则上各种操作要延迟到最后
 * Query/Execute    查询修改数据
 *      WaitForInitData     等待数据初始化
 *          Monitor.TryEnter    只会被调用一次，后续线程进入需要等待
 *          CheckModel          lock阻塞检查模型架构
 *              CheckTable      检查表
 *                  FixIndexName    修正索引名称
 *                  SetTables       设置表架构
 *              CheckTableAync  异步检查表
 *          InitData
 *          Monitor.Exit        初始化完成
 * Count            总记录数
 *      CheckModel
 *      WaitForInitData
 *
 * 缓存流程：
 * Insert/Update/Delete
 *      IEntityPersistence.Insert/Update/Delete
 *          Execute
 *              DataChange <= Tran!=null
 *                  ClearCache
 *                  OnDataChange
 *      更新缓存
 *      Tran.Completed
 *          Tran = null
 *          Success
 *              DataChange
 *          else
 *              回滚缓存
 * 
 * 缓存更新规则如下：
 * 1、直接执行SQL语句进行新增、编辑、删除操作，强制清空实体缓存、单对象缓存
 * 2、无论独占模式或非独占模式，使用实体对象或实体列表进行的对象操作，不再主动清空缓存。
 * 3、事务提交时对缓存的操作参考前两条
 * 4、事务回滚时一律强制清空缓存（因为无法判断什么异常触发回滚，回滚之前对缓存进行了哪些修改无法记录）
 * 5、强制清空缓存时传入执行更新操作记录数，如果存在更新操作清除实体缓存、单对象缓存。
 * 6、强制清空缓存时如果只有新增和删除操作，先判断当前实体类有无使用实体缓存，如果使用了实体缓存证明当前实体总记录数不大，
 *    清空实体缓存的同时清空单对象缓存，确保单对象缓存和实体缓存引用同一实体对象；没有实体缓存则不对单对象缓存进行清空操作。
 * */

namespace XCode;

/// <summary>实体会话。每个实体类、连接名和表名形成一个实体会话</summary>
public class EntitySession<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TEntity> : DisposeBase, IEntitySession where TEntity : Entity<TEntity>, new()
{
    #region 属性
    /// <summary>连接名</summary>
    public String ConnName { get; }

    /// <summary>表名</summary>
    public String TableName { get; }

    /// <summary>用于标识会话的键值</summary>
    public String Key { get; }
    #endregion

    #region 构造
    private EntitySession(String connName, String tableName)
    {
        ConnName = connName;
        TableName = tableName;
        Key = connName + "###" + tableName;

        Table = TableItem.Create(ThisType);
        var dt = DataTable = (Table.DataTable.Clone() as IDataTable)!;
        dt.ConnName = connName;
        dt.TableName = tableName;

        Queue = new EntityQueue(this)
        {
            InsertOnly = DataTable.InsertOnly
        };
    }

    /// <summary>销毁会话时，保存队列数据</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        Queue.TryDispose();
    }

    private static readonly ConcurrentDictionary<String, EntitySession<TEntity>> _es = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>创建指定表名连接名的会话</summary>
    /// <param name="connName"></param>
    /// <param name="tableName"></param>
    /// <returns></returns>
    public static EntitySession<TEntity> Create(String connName, String tableName)
    {
        if (connName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(connName));
        if (tableName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(tableName));

        // 字符串连接有较大性能损耗
        var key = connName + "###" + tableName;
        return _es.GetOrAdd(key, k => new EntitySession<TEntity>(connName, tableName));
    }
    #endregion

    #region 主要属性
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
    private Type ThisType => typeof(TEntity);

    /// <summary>数据表元数据信息。合并连接上的文件模型，可能跟Meta.Table不一致</summary>
    public TableItem Table { get; }

    /// <summary>数据表</summary>
    public IDataTable DataTable { get; }

    IEntityFactory Factory { get; } = EntityFactory.GetFactory<TEntity>();

    //private DAL _readDal;
    private DAL? _Dal;
    /// <summary>数据操作层</summary>
    public DAL Dal => _Dal ??= DAL.Create(ConnName);

    private String? _FormatedTableName;
    /// <summary>已格式化的表名，带有中括号等</summary>
    public virtual String FormatedTableName
    {
        get
        {
            if (_FormatedTableName.IsNullOrEmpty()) _FormatedTableName = Dal.Db.FormatName(DataTable);

            return _FormatedTableName;
        }
    }

    private EntitySession<TEntity>? _Default;
    /// <summary>该实体类的默认会话。</summary>
    private EntitySession<TEntity> Default
    {
        get
        {
            if (_Default != null) return _Default;

            var connName = Table.ConnName;
            var tableName = Table.TableName;

            if (ConnName == connName && TableName == tableName)
                _Default = this;
            else
                _Default = Create(connName, tableName);

            return _Default;
        }
    }

    /// <summary>用户数据</summary>
    public IDictionary<String, Object> Items { get; set; } = new Dictionary<String, Object>();
    #endregion

    #region 数据初始化
    /// <summary>记录已进行数据初始化</summary>
    Boolean hasCheckInitData = false;
    Int32 initThread = 0;
    readonly Object _wait_lock = new();

    /// <summary>检查并初始化数据。参数等待时间为0表示不等待</summary>
    /// <param name="ms">等待时间，-1表示不限，0表示不等待</param>
    /// <returns>如果等待，返回是否收到信号</returns>
    public Boolean WaitForInitData(Int32 ms = 3000)
    {
        // 已初始化
        if (hasCheckInitData || DataTable.IsView) return true;

        var tid = Thread.CurrentThread.ManagedThreadId;

        //!!! 一定一定小心堵塞的是自己
        if (initThread == tid) return true;

        if (!Monitor.TryEnter(_wait_lock, ms))
        {
            if (DAL.Debug) DAL.WriteLog("Timed out after waiting {1:n0}ms for initialization of {0} data, initThread={2}", ThisType.Name, ms, initThread);
            return false;
        }
        try
        {
            // 已初始化
            if (hasCheckInitData) return true;
            hasCheckInitData = true;

            initThread = Thread.CurrentThread.ManagedThreadId;

            // 如果该实体类是首次使用检查模型，则在这个时候检查
            Task? task = null;
            try
            {
                task = CheckModel();
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }

            try
            {
                if (Factory.Default is EntityBase entity)
                {
                    // 在初始化数据前，确保表已创建
                    // 因为CheckModel可能使用异步方式建表，导致InitData时表尚不存在
                    if (!DataTable.IsView)
                    {
                        if (task != null && !task.Wait(5_000))
                        {
                            // 表不存在，强制同步创建表
                            if (DAL.Debug) DAL.WriteLog("Table [{0}] was not found before InitData", TableName);
                        }
                    }

                    entity.InitData();
                }
            }
            catch (Exception ex)
            {
                if (XTrace.Debug) XTrace.WriteLine("Failed to initialize data! {0}", ex);
            }

            return true;
        }
        finally
        {
            initThread = 0;
            Monitor.Exit(_wait_lock);
        }
    }
    #endregion

    #region 架构检查
    private void CheckTable()
    {
        var dal = Dal;

        // 检查新表名对应的数据表
        var table = Table.DataTable;
        // 克隆一份，防止修改
        table = (table.Clone() as IDataTable)!;

        // 表名改变时，一定要检查架构，创建新表
        if (table.TableName != TableName)
        {
            // 表名去掉前缀
            var name = TableName;
            if (name.Contains(".")) name = name.Substring(".");

            table.TableName = name;
        }
        else
        {
            // 带有分表策略的实体类不参与反向工程
            if (Factory.ShardPolicy != null) return;
            if (table.Columns.Any(e => e.DataScale.StartsWithIgnoreCase("shard:", "timeShard:"))) return;
        }

        dal.SetTables(table);
    }

    private Boolean IsGenerated => ThisType.GetCustomAttribute<CompilerGeneratedAttribute>(true) != null;
    Boolean _hasCheckModel = false;
    readonly Object _checkLock = new();
    /// <summary>检查模型。依据反向工程设置、是否首次使用检查、是否已常规检查等</summary>
    private Task CheckModel()
    {
        if (_hasCheckModel || DataTable.IsView) return TaskEx.CompletedTask;
        lock (_checkLock)
        {
            if (_hasCheckModel) return TaskEx.CompletedTask;
            _hasCheckModel = true;

            var cname = ConnName;
            var tname = TableName;
            if (Dal.Db.Migration == Migration.Off || IsGenerated)
            {
                _hasCheckModel = true;
                return TaskEx.CompletedTask;
            }

            // CheckTableWhenFirstUse的实体类，在这里检查，有点意思，记下来
            var mode = Table.ModelCheckMode;
            if (DAL.Debug && mode == ModelCheckModes.CheckTableWhenFirstUse)
                DAL.WriteLog("Checking the data table schema of entity {0}, mode: {1}", ThisType.FullName, mode);

            // 第一次使用才检查的，此时检查
            var ck = false;
            // 或者前面初始化的时候没有涉及的，也在这个时候检查
            var dal = DAL.Create(cname);
            if (!dal.CheckAndAdd(tname)) ck = true;

            if (ck)
            {
                // 打开了开关，并且设置为true时，使用同步方式检查
                // 设置为false时，使用异步方式检查，因为上级的意思是不大关心数据库架构
                if (dal.Db.Migration > Migration.ReadOnly /*|| def != this*/)
                    CheckTable();
                else
                    return Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            CheckTable();
                        }
                        catch (Exception ex)
                        {
                            XTrace.WriteException(ex);
                        }
                    }, TaskCreationOptions.LongRunning);
            }

            return TaskEx.CompletedTask;
        }
    }
    #endregion

    #region 缓存
    private EntityCache<TEntity>? _cache;
    /// <summary>实体缓存</summary>
    /// <returns></returns>
    public EntityCache<TEntity> Cache
    {
        get
        {
            // 以连接名和表名为key，因为不同的库不同的表，缓存也不一样
            if (_cache == null)
            {
                var ec = new EntityCache<TEntity> { ConnName = ConnName, TableName = TableName };
                // 从默认会话复制参数
                if (Default != this) ec.CopySettingFrom(Default.Cache);
                //_cache = ec;
                Interlocked.CompareExchange(ref _cache, ec, null);
            }
            return _cache;
        }
    }

    private ISingleEntityCache<Object, TEntity>? _singleCache;
    /// <summary>单对象实体缓存。</summary>
    public ISingleEntityCache<Object, TEntity> SingleCache
    {
        get
        {
            // 以连接名和表名为key，因为不同的库不同的表，缓存也不一样
            if (_singleCache == null)
            {
                var sc = new SingleEntityCache<Object, TEntity>
                {
                    ConnName = ConnName,
                    TableName = TableName
                };

                // 从默认会话复制参数
                if (Default != this) sc.CopySettingFrom(Default.SingleCache);

                Interlocked.CompareExchange(ref _singleCache, sc, null);
            }
            return _singleCache;
        }
    }

    IEntityCache IEntitySession.Cache => Cache;
    ISingleEntityCache IEntitySession.SingleCache => SingleCache;

    /// <summary>总记录数。10 万以下精确，10 万~100 万后台校准，100 万以上为估算值；需要精确值时使用 FindCount()</summary>
    public Int32 Count
    {
        get
        {
            var v = LongCount;
            return v > Int32.MaxValue ? Int32.MaxValue : (Int32)v;
        }
    }

    private DateTime _NextCount;
    private DateTime _NextFullCount;
    /// <summary>总记录数较小时，使用静态字段，较大时增加使用Cache</summary>
    private Int64 _Count = -2L;
    readonly Object _count_lock = new();

    /// <summary>同步精确计数阈值。低于该值时直接 count(*)，不阻塞大表</summary>
    private const Int32 SyncCountLimit = 100_000;
    /// <summary>计数命令超时秒数</summary>
    private const Int32 CountCommandTimeout = 5;
    /// <summary>精确计数失败后的退避秒数</summary>
    private const Int32 CountRetrySeconds = 30 * 60;

    private Int32 _exactCountRunning;

    /// <summary>总记录数。10万以下精确，10万~100万后台校准，100万以上取索引行数并缓存</summary>
    public Int64 LongCount
    {
        get
        {
            // 当前缓存的值
            var n = _Count;

            // 如果有缓存，则考虑返回吧
            if (n >= 0)
            {
                var now = TimerX.Now;
                if (_NextCount < now)
                {
                    _NextCount = now.AddSeconds(GetCountRefreshSeconds(n));
                    // 异步更新
                    Task.Run(() =>
                    {
                        try
                        {
                            LongCount = GetCount(_Count);
                        }
                        catch (Exception ex)
                        {
                            XTrace.WriteException(ex);
                        }
                    });
                }

                return n;
            }

            if (!Monitor.TryEnter(_count_lock, 3_000))
            {
                // 另一个线程正在首次计数，返回已有缓存，避免重复慢查询
                if (_Count >= 0) return _Count;
                if (DataCache.Current.Counts.TryGetValue(CacheKey, out var cached)) return cached;
                return 0;
            }

            try
            {
                n = _Count;
                if (n >= 0) return n;

                // 来到这里，是第一次访问
                CheckModel();

                // 从配置读取
                if (n < 0)
                {
                    if (DataCache.Current.Counts.TryGetValue(CacheKey, out var c)) n = c;
                }

                // 计数诊断属于调试细节，走 Debug 级别，避免默认 Info 级别下刷屏
                if (DAL.Debug) XTrace.Log.Debug("{0}.Count fast row count (approximate) [{1}/{2}] reference value {3:n0}", ThisType.Name, TableName, ConnName, n);

                LongCount = GetCount(n);

                // 先拿到记录数再初始化，因为初始化时会用到记录数，同时也避免了死循环
                //WaitForInitData();
                InitData();

                return _Count;
            }
            finally
            {
                Monitor.Exit(_count_lock);
            }
        }
        private set
        {
            _Count = value;
            _NextCount = TimerX.Now.AddSeconds(GetCountRefreshSeconds(value));

            var dc = DataCache.Current;
            dc.Counts[CacheKey] = value;
            dc.SaveAsync();
        }
    }

    /// <summary>获取总行数，基于参考值采取不同策略</summary>
    /// <param name="count">上次缓存值，负数表示未知</param>
    /// <returns></returns>
    private Int64 GetCount(Int64 count)
    {
        var dal = Dal;
        var floor = XCodeSetting.Current.FullCountFloor;
        if (floor < 0) floor = 0;
        var syncLimit = Math.Min(SyncCountLimit, floor);

        // 视图只能按模板 SQL 精确统计，保持原有行为
        if (DataTable.IsView)
        {
            if (TimerX.Now < _NextFullCount) return count >= 0 ? count : 0;

            return ExecuteExactCount(count);
        }

        // 先取快速统计值。已缓存且未超过阈值的表交给精确计数或后台校准
        var fast = count;
        if (count <= 0 || count >= floor || dal.DbType == DatabaseType.SQLite)
            fast = QueryFastCount();

        // 快速统计不可用时，用自增主键区间兜底（SQLite 已经在 QueryFastCount 中使用区间）
        var unknown = dal.DbType == DatabaseType.SQLite ? fast < 0 : fast <= 0;
        if (unknown && dal.DbType != DatabaseType.SQLite)
        {
            var range = QueryIdentityRange();
            if (range >= 0)
            {
                fast = range;
                unknown = false;
            }
        }

        // 仍然未知时保留已有缓存值，避免把大表降级成同步精确计数
        if (unknown) fast = count > 0 ? count : 0;

        // 数据表频繁写入会重置刷新时间，计数可能被反复计算，调试级别输出以免刷屏
        if (DAL.Debug) XTrace.Log.Debug("{0}.Count fast row count (approximate) [{1}/{2}] value {3:n0}, floor {4:n0}", ThisType.Name, TableName, ConnName, fast, floor);

        // 小表同步精确；中表后台精确；大表只估算
        if (fast <= syncLimit)
        {
            if (TimerX.Now < _NextFullCount) return fast;

            return ExecuteExactCount(fast);
        }

        if (fast < floor && TimerX.Now >= _NextFullCount)
        {
            _NextFullCount = TimerX.Now.AddSeconds(GetExactRefreshSeconds(fast));
            ScheduleExactCount();
        }

        return fast;
    }

    /// <summary>快速获取表行数。失败时返回 -1，由调用方决定是否使用主键区间兜底</summary>
    /// <returns></returns>
    private Int64 QueryFastCount()
    {
        var dal = Dal;

        // SQLite 的 Select Count 很慢，用自增主键区间估算
        if (dal.DbType == DatabaseType.SQLite) return QueryIdentityRange();

        using var span = dal.Tracer?.NewSpan($"db:{ConnName}:QueryCountFast:{DAL.TrimTableName(TableName)}", TableName);
        try
        {
            var count = dal.Session.QueryCountFast(FormatedTableName);
            if (span != null) span.Value = count;
            return count;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            if (DAL.Debug) DAL.WriteLog("Failed to get fast row count for [{0}]: {1}", TableName, ex.Message);
            return -1;
        }
    }

    /// <summary>用自增主键的 MIN/MAX 区间估算行数。仅适用于有索引的单列自增主键，失败返回 -1</summary>
    /// <returns></returns>
    private Int64 QueryIdentityRange()
    {
        var fi = Table.Identity;
        if (fi == null || DataTable.IsView) return -1;

        var columnName = fi.ColumnName ?? fi.Name;

        // 必须有索引，避免 MIN/MAX 全表扫描
        var indexed = fi.PrimaryKey || Table.DataTable.Indexes.Any(ix => ix.Columns.Any(c => c.EqualIgnoreCase(columnName)));
        if (!indexed) return -1;

        try
        {
            var name = Dal.Db.FormatName(columnName);
            var builder = new SelectBuilder
            {
                Table = FormatedTableName,
                Column = $"MIN({name}), MAX({name})"
            };
            var ds = Dal.Query(builder, 0, 0);
            if (ds.Columns.Length < 2 || ds.Rows.Count == 0) return -1;

            var row = ds.Rows[0];
            if (row[0] == null || row[0] == DBNull.Value || row[1] == null || row[1] == DBNull.Value) return 0;

            var min = Convert.ToInt64(row[0]);
            var max = Convert.ToInt64(row[1]);
            if (max < min) return 0;

            var range = max - min;
            return range < Int64.MaxValue ? range + 1 : range;
        }
        catch (Exception ex)
        {
            if (DAL.Debug) DAL.WriteLog("Failed to get identity range for [{0}]: {1}", TableName, ex.Message);
            return -1;
        }
    }

    /// <summary>执行精确计数。失败时保留参考值，并按退避周期重试</summary>
    /// <param name="reference">失败时保留的估算值</param>
    /// <returns></returns>
    private Int64 ExecuteExactCount(Int64 reference)
    {
        var now = TimerX.Now;
        _NextFullCount = now.AddSeconds(GetExactRefreshSeconds(reference));

        try
        {
            var builder = new SelectBuilder
            {
                Table = FormatedTableName
            };
            FixBuilder(builder);

            using var span = Dal.Tracer?.NewSpan($"db:{ConnName}:QueryCountExact:{DAL.TrimTableName(TableName)}", TableName);
            try
            {
                var count = Dal.SelectCountLong(builder, GetCountTimeout());
                if (span != null) span.Value = count;

                _NextFullCount = now.AddSeconds(GetExactRefreshSeconds(count));
                return count;
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);
                throw;
            }
        }
        catch (Exception ex)
        {
            // 只有确认表不存在时才触发建表；超时/权限等错误保留估算值
            if (!DataTable.IsView && IsTableMissing())
            {
                try
                {
                    if (!Dal.CheckAndAdd(TableName)) CheckTable();
                }
                catch (Exception ex2)
                {
                    if (DAL.Debug) DAL.WriteLog("Failed to create table [{0}]: {1}", TableName, ex2.Message);
                }

                _NextFullCount = now.AddSeconds(60);
                return 0;
            }

            _NextFullCount = now.AddSeconds(CountRetrySeconds);
            DAL.WriteLog("Failed to get exact row count for [{0}], keep {1:n0}: {2}", TableName, reference, ex.Message);
        }

        return reference >= 0 ? reference : 0;
    }

    private Boolean IsTableMissing()
    {
        try
        {
            return !Dal.TableNames.Contains(TableName);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>后台精确计数</summary>
    private void ScheduleExactCount()
    {
        if (Interlocked.CompareExchange(ref _exactCountRunning, 1, 0) != 0) return;

        Task.Run(() =>
        {
            try
            {
                // 首次访问时可能尚未完成初始化
                WaitForInitData(3_000);

                var count = ExecuteExactCount(_Count);
                LongCount = count;
            }
            catch (Exception ex)
            {
                _NextFullCount = TimerX.Now.AddSeconds(CountRetrySeconds);
                XTrace.WriteException(ex);
            }
            finally
            {
                Interlocked.Exchange(ref _exactCountRunning, 0);
            }
        });
    }

    private static Int32 GetCountTimeout()
    {
        var timeout = XCodeSetting.Current.CommandTimeout;
        return timeout > 0 && timeout < CountCommandTimeout ? timeout : CountCommandTimeout;
    }

    private static Int32 GetCountRefreshSeconds(Int64 count) => count switch
    {
        >= 1_000_000 => 600,
        >= 100_000 => 300,
        _ => 60,
    };

    private static Int32 GetExactRefreshSeconds(Int64 count) => count switch
    {
        >= 1_000_000 => 3600,
        >= 100_000 => 600,
        _ => 60,
    };

    /// <summary>清除缓存</summary>
    /// <param name="reason">清除原因</param>
    [Obsolete]
    public void ClearCache(String reason) => ClearCache(reason, false);

    /// <summary>清除缓存</summary>
    /// <param name="reason">清除原因</param>
    /// <param name="force">强制清除，下次访问阻塞等待。默认false仅置为过期，下次访问异步更新</param>
    public void ClearCache(String reason, Boolean force)
    {
        _cache?.Clear(reason, force);

        _singleCache?.Clear(reason);

        // Count提供的是非精确数据，避免频繁更新
        //_Count = -1L;
        _NextCount = DateTime.MinValue;
    }

    String CacheKey => $"{ConnName}_{TableName}_{ThisType.Name}";
    #endregion

    #region 数据库操作
    //private String _readonlyConnName;
    ///// <summary>获取数据操作对象，根据是否查询以及事务来进行读写分离</summary>
    ///// <param name="read"></param>
    ///// <returns></returns>
    //public DAL GetDAL(Boolean read)
    //{
    //    // 如果主连接已打开事务，则直接使用
    //    var dal = Dal;
    //    if (dal.Session is DbSession ds && ds.Transaction != null) return dal;

    //    // 读写分离
    //    if (read)
    //    {
    //        if (_readDal != null) return _readDal;

    //        // 根据后缀查找只读连接名
    //        var name = ConnName + ".readonly";
    //        if (DAL.ConnStrs.ContainsKey(name))
    //        {
    //            if (_readonlyConnName.IsNullOrEmpty())
    //            {
    //                XTrace.WriteLine("[{0}]读写分离到[{1}]", ConnName, name);
    //                _readonlyConnName = name;
    //            }
    //            return _readDal = DAL.Create(name);
    //        }
    //    }

    //    return dal;
    //}

    /// <summary>初始化数据，执行反向工程检查，建库建表</summary>
    public void InitData() => WaitForInitData();

    /// <summary>执行SQL查询，返回记录集</summary>
    /// <param name="builder">SQL语句</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns></returns>
    public virtual DbTable Query(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows)
    {
        InitData();

        FixBuilder(builder);

        return Dal.Query(builder, startRowIndex, maximumRows);
    }

    /// <summary>执行SQL查询，返回记录集</summary>
    /// <param name="sql">SQL语句</param>
    /// <returns></returns>
    public virtual DbTable Query(String sql)
    {
        InitData();

        return Dal.Query(sql);
    }

    /// <summary>查询记录数</summary>
    /// <param name="builder">查询生成器</param>
    /// <returns>记录数</returns>
    public virtual Int32 QueryCount(SelectBuilder builder)
    {
        InitData();

        FixBuilder(builder);

        return Dal.SelectCount(builder);
    }

    /// <summary>查询记录数</summary>
    /// <param name="sql">SQL语句</param>
    /// <returns></returns>
    public virtual Int32 QueryCount(String sql)
    {
        InitData();

        return Dal.SelectCount(sql, CommandType.Text);
    }

    private void FixBuilder(SelectBuilder builder)
    {
        if (DataTable.IsView && builder.Table == FormatedTableName)
        {
            builder.Table = $"({Factory.Template.GetSql(Dal.DbType)}) SourceTable";
        }
    }

    /// <summary>执行</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns>影响的结果</returns>
    public Int32 Execute(String sql, CommandType type = CommandType.Text, params IDataParameter[] ps)
    {
        InitData();

        var rs = Dal.Execute(sql, type, ps);
        DataChange("Execute " + type);
        return rs;
    }

    /// <summary>执行插入语句并返回新增行的自动编号</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[] ps)
    {
        InitData();

        var rs = Dal.InsertAndGetIdentity(sql, type, ps);
        DataChange("InsertAndGetIdentity " + type);
        return rs;
    }

    /// <summary>执行SQL查询，返回记录集</summary>
    /// <param name="builder">SQL语句</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns></returns>
    public virtual Task<DbTable> QueryAsync(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows)
    {
        InitData();

        return Dal.QueryAsync(builder, startRowIndex, maximumRows);
    }

    /// <summary>查询记录数</summary>
    /// <param name="builder">查询生成器</param>
    /// <returns>记录数</returns>
    public virtual Task<Int64> QueryCountAsync(SelectBuilder builder)
    {
        InitData();

        return Dal.SelectCountAsync(builder);
    }

    /// <summary>执行</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns>影响的结果</returns>
    public Task<Int32> ExecuteAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        InitData();

        var rs = Dal.ExecuteAsync(sql, type, ps);
        DataChange("Execute " + type);
        return rs;
    }

    /// <summary>执行插入语句并返回新增行的自动编号</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        InitData();

        var rs = Dal.InsertAndGetIdentityAsync(sql, type, ps);
        DataChange("InsertAndGetIdentity " + type);
        return rs;
    }

    private void DataChange(String reason)
    {
        ClearCache(reason, true);

        _OnDataChange?.Invoke(ThisType);
    }

    private Action<Type>? _OnDataChange;
    /// <summary>数据改变后触发。参数指定触发该事件的实体类</summary>
    public event Action<Type> OnDataChange
    {
        add
        {
            if (value != null)
            {
                // 这里不能对委托进行弱引用，因为GC会回收委托，应该改为对对象进行弱引用
                //WeakReference<Action<Type>> w = value;

                // 弱引用事件，只会执行一次，一次以后自动取消注册
                _OnDataChange += new WeakAction<Type>(value, handler => { _OnDataChange -= handler; }, true);
            }
        }
        remove { }
    }
    #endregion

    #region 数据库高级操作
    /// <summary>清空数据表，标识归零</summary>
    /// <returns></returns>
    public Int32 Truncate()
    {
        var dal = Dal;

        using var span = dal.Tracer?.NewSpan($"db:{ConnName}:Truncate:{TableName}");
        try
        {
            var rs = dal.Session.Truncate(FormatedTableName);
            if (span != null) span.Value = rs;

            // 干掉所有缓存
            _cache?.Clear("Truncate", true);
            _singleCache?.Clear("Truncate");
            LongCount = 0;

            //// 重新初始化
            //hasCheckInitData = false;

            return rs;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }
    #endregion

    #region 事务保护
    //private ITransaction GetTran() => (Dal.Session as DbSession).Transaction;

    /// <summary>开始事务</summary>
    /// <returns>剩下的事务计数</returns>
    public virtual Int32 BeginTrans()
    {
        /* 这里也需要执行初始化检查架构，因为无法确定在调用此方法前是否已使用实体类进行架构检查，如下调用会造成事务不平衡而上抛异常：
         * Exception：执行SqlServer的Dispose时出错：System.InvalidOperationException: 此 SqlTransaction 已完成；它再也无法使用。
         * 
         * 如果实体的模型检查模式为CheckTableWhenFirstUse，直接执行静态操作
         * using (var trans = new EntityTransaction<TEntity>())
         * {
         *   TEntity.Delete(whereExp);
         *   TEntity.Update(new String[] { 字段1,字段2 }, new Object[] { 值1,值1 }, whereExp);
         *   trans.Commit();
         * }
         */
        InitData();

        var count = Dal.BeginTransaction();

        return count;
    }

    /// <summary>提交事务</summary>
    /// <returns>剩下的事务计数</returns>
    public virtual Int32 Commit()
    {
        var rs = Dal.Commit();

        if (rs == 0) DataChange("Commit");

        return rs;
    }

    /// <summary>回滚事务，忽略异常</summary>
    /// <returns>剩下的事务计数</returns>
    public virtual Int32 Rollback()
    {
        var rs = Dal.Rollback();

        if (rs == 0) DataChange($"Rollback");

        return rs;
    }

    /// <summary>创建事务</summary>
    public virtual EntityTransaction CreateTrans() => new(Dal);
    #endregion

    #region 参数化
    ///// <summary>格式化参数名</summary>
    ///// <param name="name">名称</param>
    ///// <returns></returns>
    //public virtual String FormatParameterName(String name) => Dal.Db.FormatParameterName(name);
    #endregion

    #region 实体操作
    /// <summary>把该对象持久化到数据库，添加/更新实体缓存和单对象缓存，增加总计数</summary>
    /// <param name="entity">实体对象</param>
    /// <returns></returns>
    public virtual Int32 Insert(IEntity entity)
    {
        if (DataTable.IsView) throw new NotSupportedException(L.T("Views cannot be inserted, updated, or deleted!"));

        var rs = Factory.Persistence.Insert(this, entity);

        var e = entity as TEntity;

        // 加入实体缓存
        _cache?.Add(e!);

        // 增加计数
        if (_Count >= 0) Interlocked.Increment(ref _Count);

        // 清空扩展属性
        //(entity as EntityBase)._Extends?.Clear();
        (entity as EntityBase)!._Extends = null;

        return rs;
    }

    /// <summary>更新数据库，同时更新实体缓存</summary>
    /// <param name="entity">实体对象</param>
    /// <returns></returns>
    public virtual Int32 Update(IEntity entity)
    {
        if (DataTable.IsView) throw new NotSupportedException(L.T("Views cannot be inserted, updated, or deleted!"));

        var rs = Factory.Persistence.Update(this, entity);

        var e = entity as TEntity;

        // 更新缓存
        _cache?.Update(e!);

        // 干掉缓存项，让它重新获取
        _singleCache?.Remove(e!);

        // 清空扩展属性
        (entity as EntityBase)!._Extends = null;

        return rs;
    }

    /// <summary>从数据库中删除该对象，同时从实体缓存和单对象缓存中删除，扣减总数量</summary>
    /// <param name="entity">实体对象</param>
    /// <returns></returns>
    public virtual Int32 Delete(IEntity entity)
    {
        if (DataTable.IsView) throw new NotSupportedException(L.T("Views cannot be inserted, updated, or deleted!"));

        var rs = Factory.Persistence.Delete(this, entity);

        var e = entity as TEntity;

        // 从实体缓存删除
        _cache?.Remove(e!);

        // 从单对象缓存删除
        _singleCache?.Remove(e!);

        // 减少计数
        if (_Count > 0) Interlocked.Decrement(ref _Count);

        // 清空扩展属性
        (entity as EntityBase)!._Extends = null;

        return rs;
    }

    /// <summary>把该对象持久化到数据库，添加/更新实体缓存和单对象缓存，增加总计数</summary>
    /// <param name="entity">实体对象</param>
    /// <returns></returns>
    public virtual async Task<Int32> InsertAsync(IEntity entity)
    {
        if (DataTable.IsView) throw new NotSupportedException(L.T("Views cannot be inserted, updated, or deleted!"));

        var rs = await Factory.Persistence.InsertAsync(this, entity).ConfigureAwait(false);

        var e = entity as TEntity;

        // 加入实体缓存
        _cache?.Add(e!);

        // 增加计数
        if (_Count >= 0) Interlocked.Increment(ref _Count);

        return rs;
    }

    /// <summary>更新数据库，同时更新实体缓存</summary>
    /// <param name="entity">实体对象</param>
    /// <returns></returns>
    public virtual Task<Int32> UpdateAsync(IEntity entity)
    {
        if (DataTable.IsView) throw new NotSupportedException(L.T("Views cannot be inserted, updated, or deleted!"));

        var rs = Factory.Persistence.UpdateAsync(this, entity);

        var e = entity as TEntity;

        // 更新缓存
        _cache?.Update(e!);

        // 干掉缓存项，让它重新获取
        _singleCache?.Remove(e!);

        return rs;
    }

    /// <summary>从数据库中删除该对象，同时从实体缓存和单对象缓存中删除，扣减总数量</summary>
    /// <param name="entity">实体对象</param>
    /// <returns></returns>
    public virtual Task<Int32> DeleteAsync(IEntity entity)
    {
        if (DataTable.IsView) throw new NotSupportedException(L.T("Views cannot be inserted, updated, or deleted!"));

        var rs = Factory.Persistence.DeleteAsync(this, entity);

        var e = entity as TEntity;

        // 从实体缓存删除
        _cache?.Remove(e!);

        // 从单对象缓存删除
        _singleCache?.Remove(e!);

        // 减少计数
        if (_Count > 0) Interlocked.Decrement(ref _Count);

        return rs;
    }
    #endregion

    #region 队列
    /// <summary>实体队列</summary>
    public EntityQueue Queue { get; set; }
    #endregion
}