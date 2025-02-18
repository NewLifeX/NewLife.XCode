﻿using System.Collections;
using System.Data;
using System.Data.Common;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using NewLife;
using NewLife.Collections;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Web;

namespace XCode.DataAccessLayer;

/// <summary>数据库基类</summary>
/// <remarks>
/// 数据库类的职责是抽象不同数据库的共同点，理应最小化，保证原汁原味，因此不做缓存等实现。
/// 对于每一个连接字符串配置，都有一个数据库实例，而不是每个数据库类型一个实例，因为同类型数据库不同版本行为不同。
/// </remarks>
abstract class DbBase : DisposeBase, IDatabase
{
    #region 构造函数
    static DbBase()
    {
        var root = AppDomain.CurrentDomain.BaseDirectory;
        if (Runtime.IsWeb) root = root.CombinePath("bin");

        // 根据进程版本，设定x86或者x64为DLL目录
        var dir = Environment.Is64BitProcess ? "x64" : "x86";
        dir = root.CombinePath(dir);
        //if (Directory.Exists(dir)) SetDllDirectory(dir);
        // 不要判断是否存在，因为可能目录还不存在，一会下载驱动后将创建目录
        if (Runtime.Windows) SetDllDirectory(dir);

        var set = NewLife.Setting.Current;
        root = set.GetPluginPath();
        //dir = Environment.Is64BitProcess ? "x64" : "x86";
        //dir = root.CombinePath(dir);
        if (Runtime.Windows) SetDllDirectory(root);
    }

    /// <summary>实例化</summary>
    public DbBase()
    {
        var set = XCodeSetting.Current;
        Migration = set.Migration;
        TraceSQLTime = set.TraceSQLTime;
        RetryOnFailure = set.RetryOnFailure;
        NameFormat = set.NameFormat;
        ShowSQL = set.ShowSQL;
        SQLMaxLength = set.SQLMaxLength;
        UseParameter = set.UseParameter;
    }

    /// <summary>销毁资源时，回滚未提交事务，并关闭数据库连接</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        if (_metadata != null)
        {
            // 销毁本数据库的元数据对象
            try
            {
                _metadata.Dispose();
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
            _metadata = null;
        }
    }

    /// <summary>释放所有会话</summary>
    internal void ReleaseSession()
    {
        var st = _store;
        if (st != null) _store = new ThreadLocal<IDbSession>();

#if NET45
        var st2 = _store2;
        if (st2 != null) _store2 = new ThreadLocal<IAsyncDbSession>();
#else
        var st2 = _store2;
        if (st2 != null) _store2 = new AsyncLocal<IAsyncDbSession>();
#endif
    }
    #endregion

    #region 属性
    /// <summary>返回数据库类型。外部DAL数据库类请使用Other</summary>
    public virtual DatabaseType Type => DatabaseType.None;

    /// <summary>提供者工厂</summary>
    private DbProviderFactory? _providerFactory;
    /// <summary>数据库提供者工厂。支持外部修改</summary>
    public DbProviderFactory Factory
    {
        get => _providerFactory ??= GetFactory(true)!;
        set => _providerFactory = value;
    }

    private static Dictionary<Type, DbProviderFactory> _factories = new();
    private Boolean _inited;
    protected DbProviderFactory? GetFactory(Boolean create)
    {
        try
        {
            var type = GetType();
            if (_factories.TryGetValue(type, out var factory)) return factory;

            if (!create) return null;

            // 如果已经初始化过，还没拿到工厂，那么没有必要创建第二次
            if (_inited) return null;

            // 先创建，再加锁加入集合。允许重复创建，避免死锁
            factory = CreateFactory();
            if (factory == null) return null;

            lock (_factories)
            {
                if (_factories.TryGetValue(type, out var factory2)) return factory2;

                if (factory != null) _factories.Add(type, factory);

                return factory;
            }
        }
        finally
        {
            _inited = true;
        }
    }

    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected virtual DbProviderFactory? CreateFactory() => null;

    /// <summary>连接名</summary>
    public String ConnName { get; set; } = null!;

    protected internal String _ConnectionString = null!;
    /// <summary>链接字符串</summary>
    public virtual String ConnectionString
    {
        get => _ConnectionString;
        set
        {
#if DEBUG
            XTrace.WriteLine("{0} 设定 {1}", ConnName, value);
#endif
            var builder = new ConnectionStringBuilder(value);

            OnSetConnectionString(builder);

            // 只有连接字符串改变，才释放会话
            var connStr = builder.ConnectionString;
#if DEBUG
            XTrace.WriteLine("{0} 格式 {1}", ConnName, connStr);
#endif
            if (_ConnectionString != connStr)
            {
                _ConnectionString = connStr;
                _newStr = null;

                ReleaseSession();
            }
        }
    }

    /// <summary>数据库提供者。用于选择驱动</summary>
    public String? Provider { get; set; }

    protected void CheckConnStr()
    {
        if (ConnectionString.IsNullOrWhiteSpace())
            throw new XCodeException("[{0}]未指定连接字符串！", ConnName);
    }

    private String? _newStr;
    private String GetConnectionString()
    {
        if (_newStr != null) return _newStr;

        var builder = new ConnectionStringBuilder(_ConnectionString);
        OnGetConnectionString(builder);
        _newStr = builder.ConnectionString;
        _ConnectionString = _newStr;

        // 显示当前数据库的地址和用户名
        if (DAL.Debug)
        {
            var server = builder["DataSource"] ?? builder["Data Source"] ?? builder["Server"];
            var db = builder["Database"] ?? builder["Db"];
            var uid = builder["User"] ?? builder["Uid"];

            XTrace.WriteLine("[{0}] DataSource={1} Database={2} User={3}", ConnName, server, db, uid);
        }

        return _newStr;
    }

    /// <summary>设置连接字符串时允许从中取值或修改，基类用于读取拥有者Owner，子类重写时应调用基类</summary>
    /// <param name="builder"></param>
    protected virtual void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        if (builder.TryGetAndRemove(nameof(Owner), out var value) && !value.IsNullOrEmpty()) Owner = value;
        if (builder.TryGetAndRemove(nameof(ShowSQL), out value) && !value.IsNullOrEmpty()) ShowSQL = value.ToBoolean();

        // 参数化，需要兼容写错了一年的UserParameter
        if (builder.TryGetAndRemove(nameof(UseParameter), out value) && !value.IsNullOrEmpty()) UseParameter = value.ToBoolean();
        //if (builder.TryGetAndRemove("UserParameter", out value) && !value.IsNullOrEmpty()) UseParameter = value.ToBoolean();

        if (builder.TryGetAndRemove(nameof(Migration), out value) && !value.IsNullOrEmpty()) Migration = (Migration)Enum.Parse(typeof(Migration), value, true);
        if (builder.TryGetAndRemove(nameof(TablePrefix), out value) && !value.IsNullOrEmpty()) TablePrefix = value;
        if (builder.TryGetAndRemove(nameof(Readonly), out value) && !value.IsNullOrEmpty()) Readonly = value.ToBoolean();
        if (builder.TryGetAndRemove(nameof(DataCache), out value) && !value.IsNullOrEmpty()) DataCache = value.ToInt();
        // 反向工程生成sql中表名和字段名称大小写
        if (builder.TryGetAndRemove(nameof(NameFormat), out value) && !value.IsNullOrEmpty()) NameFormat = (NameFormats)Enum.Parse(typeof(NameFormats), value, true);
        if (builder.TryGetAndRemove(nameof(BatchSize), out value) && !value.IsNullOrEmpty()) BatchSize = value.ToInt();
        if (builder.TryGetAndRemove(nameof(CommandTimeout), out value) && !value.IsNullOrEmpty()) CommandTimeout = value.ToInt();

        // 连接字符串去掉provider，可能有些数据库不支持这个属性
        if (builder.TryGetAndRemove("provider", out value) && !value.IsNullOrEmpty()) { }

        // 数据库名称
        var db = builder["Database"] ?? builder["Db"];
        if (db.IsNullOrEmpty()) db = builder["Initial Catalog"];
        DatabaseName = db;
    }

    /// <summary>获取连接字符串时取值或修改</summary>
    /// <remarks>此时Factory已就绪，可根据驱动调整连接字符串参数</remarks>
    /// <param name="builder"></param>
    protected virtual void OnGetConnectionString(ConnectionStringBuilder builder) { }

    /// <summary>拥有者</summary>
    public virtual String? Owner { get; set; }

    /// <summary>数据库名</summary>
    public String? DatabaseName { get; set; }

    internal protected String? _ServerVersion;
    /// <summary>数据库服务器版本</summary>
    public virtual String ServerVersion
    {
        get
        {
            var ver = _ServerVersion;
            if (ver != null) return ver;

            _ServerVersion = String.Empty;

            using var conn = OpenConnection();
            return _ServerVersion = conn.ServerVersion;
        }
    }

    /// <summary>反向工程。Off 关闭；ReadOnly 只读不执行；On 打开，新建；Full 完全，修改删除</summary>
    public Migration Migration { get; set; }

    /// <summary>跟踪SQL执行时间，大于该阀值将输出日志</summary>
    public Int32 TraceSQLTime { get; set; }

    /// <summary>本连接数据只读</summary>
    public Boolean Readonly { get; set; }

    /// <summary>失败重试。执行命令超时后的重试次数，默认0不重试</summary>
    public Int32 RetryOnFailure { get; set; }

    /// <summary>数据层缓存有效期。单位秒</summary>
    public Int32 DataCache { get; set; }

    /// <summary>表前缀。所有在该连接上的表名都自动增加该前缀</summary>
    public String? TablePrefix { get; set; }

    /// <summary>反向工程表名、字段名大小写设置</summary>
    public NameFormats NameFormat { get; set; }

    /// <summary>批大小。用于批量操作数据，抽取、删除、备份、恢复</summary>
    public Int32 BatchSize { get; set; }

    /// <summary>命令超时。查询执行超时时间，默认0秒不限制</summary>
    public Int32 CommandTimeout { get; set; }

    /// <summary>性能追踪器</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>数据库引擎默认的反向工程表名、字段名大小写设置</summary>
    public virtual NameFormats DefaultNameFormat => NameFormats.None;
    #endregion

    #region 方法
    private ThreadLocal<IDbSession> _store = new();
#if NET45
    private ThreadLocal<IAsyncDbSession> _store2 = new();
#else
    private AsyncLocal<IAsyncDbSession> _store2 = new();
#endif

    /// <summary>创建数据库会话，数据库在每一个线程都有唯一的一个实例</summary>
    /// <returns></returns>
    public IDbSession CreateSession()
    {
        // 会话可能已经被销毁
        var session = _store.Value;
        if (session != null && !session.Disposed) return session;

        session = OnCreateSession();

        if (session is DbSession ds)
        {
            if (ds.Log != null && ds.Log.Level <= LogLevel.Debug) ds.Tracer = Tracer;
        }

        CheckConnStr();

        _store.Value = session;

        return session;
    }

    /// <summary>为异步操作创建数据库会话，数据库在每一个异步上下文都有唯一的一个实例</summary>
    /// <returns></returns>
    public IAsyncDbSession CreateSessionForAsync()
    {
        // 会话可能已经被销毁
        var session = _store2.Value;
        if (session != null && !session.Disposed) return session;

        session = (OnCreateSession() as IAsyncDbSession)!;

        if (session is DbSession ds)
        {
            if (ds.Log != null && ds.Log.Level <= LogLevel.Debug) ds.Tracer = Tracer;
        }

        CheckConnStr();

        _store2.Value = session;

        return session;
    }

    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected abstract IDbSession OnCreateSession();

    /// <summary>唯一实例</summary>
    private IMetaData? _metadata;

    /// <summary>创建元数据对象，唯一实例</summary>
    /// <returns></returns>
    public IMetaData CreateMetaData()
    {
        if (_metadata != null && !_metadata.Disposed) return _metadata;
        lock (this)
        {
            if (_metadata != null && !_metadata.Disposed) return _metadata;

            _metadata = OnCreateMetaData();
            // 减少一步类型转换
            if (_metadata is DbMetaData meta) meta.Database = this;

            return _metadata;
        }
    }

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected abstract IMetaData OnCreateMetaData();

    /// <summary>打开连接</summary>
    /// <returns></returns>
    public virtual DbConnection OpenConnection()
    {
        if (Factory == null) throw new InvalidOperationException($"无法找到{Type}的ADO.NET驱动，需要从Nuget引用！");

        var conn = Factory.CreateConnection();
        conn.ConnectionString = GetConnectionString();
        conn.Open();

        return conn;
    }

    /// <summary>打开连接</summary>
    /// <returns></returns>
    public virtual async Task<DbConnection> OpenConnectionAsync()
    {
        if (Factory == null) throw new InvalidOperationException($"无法找到{Type}的ADO.NET驱动，需要从Nuget引用！");

        var conn = Factory.CreateConnection();
        conn.ConnectionString = GetConnectionString();
        await conn.OpenAsync().ConfigureAwait(false);

        return conn;
    }

    /// <summary>是否支持该提供者所描述的数据库</summary>
    /// <param name="providerName">提供者</param>
    /// <returns></returns>
    public virtual Boolean Support(String providerName) => !providerName.IsNullOrEmpty() && providerName.ToLower().Contains(Type.ToString().ToLower());
    #endregion

    #region 下载驱动
    protected static IList<String> GetLinkNames(String name, Boolean strict = false)
    {
        var links = new List<String>();
        //var name = Path.GetFileNameWithoutExtension(assemblyFile);
        if (!name.IsNullOrEmpty())
        {
#if !NETFRAMEWORK
            var arch = (RuntimeInformation.OSArchitecture + "").ToLower();
            // 可能是在x64架构上跑x86
            if (arch == "x64" && !Environment.Is64BitProcess) arch = "x86";
#endif

            var platform = "";
            if (Runtime.Linux)
                platform = "linux";
            else if (Runtime.OSX)
                platform = "osx";
            else
                platform = "win";

            // 多目标匹配，不区分先后顺序，统一匹配后按照版本和时间排序
#if !NETFRAMEWORK
            links.Add($"{name}.{platform}-{arch}");
#endif
            links.Add($"{name}.{platform}");

            var ver = Environment.Version;
            if (ver.Major <= 3)
                links.Add($"{name}_netcore{ver.Major}{ver.Minor}");
            else
            {
                links.Add($"{name}_net{ver.Major}{ver.Minor}");
                for (var i = ver.Major - 1; i >= 5; i--)
                {
                    links.Add($"{name}_net{i}0");
                }
            }

            if (ver.Major >= 3) links.Add($"{name}_netstandard21");
            links.Add($"{name}_netstandard20");

            // 有些数据库驱动不区分x86/x64，并且逐步以Fx4为主，所以来一个默认
            if (!strict && !links.Contains(name)) links.Add(name);
        }

        return links;
    }

    /// <summary>获取提供者工厂</summary>
    /// <param name="name"></param>
    /// <param name="assemblyFile"></param>
    /// <param name="className"></param>
    /// <param name="strict"></param>
    /// <param name="ignoreError"></param>
    /// <returns></returns>
    public static DbProviderFactory? GetProviderFactory(String? name, String assemblyFile, String className, Boolean strict = false, Boolean ignoreError = false)
    {
        try
        {
            if (name.IsNullOrEmpty()) name = Path.GetFileNameWithoutExtension(assemblyFile);
            var links = GetLinkNames(name, strict);
            var type = PluginHelper.LoadPlugin(className, null, assemblyFile, links.Join(","));

            var factory = GetProviderFactory(type);
            if (factory != null) return factory;

            // 反射实现获取数据库工厂
            var file = assemblyFile;
            var plugin = NewLife.Setting.Current.GetPluginPath();
            file = plugin.CombinePath(file);

            // 如果还没有，就写异常
            if (type == null)
            {
                if (assemblyFile.IsNullOrEmpty()) return null;
                if (!File.Exists(file)) throw new FileNotFoundException("缺少文件" + file + "！", file);
            }

            if (type == null)
            {
                XTrace.WriteLine("驱动文件[{0}]无效或不适用于当前环境，准备删除后重新加载[{1}]！", assemblyFile, file);

                try
                {
                    File.Delete(file);
                }
                catch (UnauthorizedAccessException) { }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }

                type = PluginHelper.LoadPlugin(className, null!, assemblyFile, links.Join(","));

                // 如果还没有，就写异常
                if (!File.Exists(file)) throw new FileNotFoundException("缺少文件" + file + "！", file);
            }

            if (type == null) throw new XCodeException("无法加载驱动[{0}]，请从nuget正确引入数据库驱动！", assemblyFile);

            return GetProviderFactory(type);
        }
        catch
        {
            if (ignoreError) return null;

            throw;
        }
    }

    /// <summary>
    /// 从类型加载驱动
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static DbProviderFactory? GetProviderFactory(Type? type)
    {
        if (type == null) return null;

        var asm = type.Assembly;
        if (DAL.Debug) DAL.WriteLog("[{2}]驱动 {0} 版本v{1}", asm.Location, asm.GetName().Version, type.FullName.TrimEnd("Client", "Factory"));

        var field = type.GetFieldEx("Instance");
        if (field == null) return Activator.CreateInstance(type) as DbProviderFactory;

        return Reflect.GetValue(null, field) as DbProviderFactory;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern Int32 SetDllDirectory(String pathName);
    #endregion

    #region 分页
    /// <summary>构造分页SQL，优先选择max/min，然后选择not in</summary>
    /// <remarks>
    /// 两个构造分页SQL的方法，区别就在于查询生成器能够构造出来更好的分页语句，尽可能的避免子查询。
    /// MS体系的分页精髓就在于唯一键，当唯一键带有Asc/Desc/Unkown等排序结尾时，就采用最大最小值分页，否则使用较次的TopNotIn分页。
    /// TopNotIn分页和MaxMin分页的弊端就在于无法完美的支持GroupBy查询分页，只能查到第一页，往后分页就不行了，因为没有主键。
    /// </remarks>
    /// <param name="sql">SQL语句</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <param name="keyColumn">唯一键。用于not in分页</param>
    /// <returns>分页SQL</returns>
    public abstract String PageSplit(String sql, Int64 startRowIndex, Int64 maximumRows, String? keyColumn);

    private static readonly Regex reg_SimpleSQL = new(@"^\s*select\s+\*\s+from\s+([\w\[\]\""\""\']+)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    /// <summary>检查简单SQL语句，比如Select * From table</summary>
    /// <param name="sql">待检查SQL语句</param>
    /// <returns>如果是简单SQL语句则返回表名，否则返回子查询(sql) XCode_Temp_a</returns>
    internal protected static String CheckSimpleSQL(String sql)
    {
        if (String.IsNullOrEmpty(sql)) return sql;

        var ms = reg_SimpleSQL.Matches(sql);
        if (ms == null || ms.Count <= 0 || ms[0].Groups.Count < 2 ||
            String.IsNullOrEmpty(ms[0].Groups[1].Value)) return $"({sql}) XCode_Temp_a";
        return ms[0].Groups[1].Value;
    }

    private static readonly Regex reg_Order = new(@"\border\s*by\b([^)]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    /// <summary>检查是否以Order子句结尾，如果是，分割sql为前后两部分</summary>
    /// <param name="sql"></param>
    /// <returns></returns>
    internal protected static String? CheckOrderClause(ref String sql)
    {
        if (!sql.ToLower().Contains("order")) return null;

        // 使用正则进行严格判断。必须包含Order By，并且它右边没有右括号)，表明有order by，且不是子查询的，才需要特殊处理
        var ms = reg_Order.Matches(sql);
        if (ms == null || ms.Count <= 0 || ms[0].Index < 1) return null;

        var orderBy = sql[ms[0].Index..].Trim();
        sql = sql[..ms[0].Index].Trim();

        return orderBy;
    }

    /// <summary>构造分页SQL</summary>
    /// <remarks>
    /// 两个构造分页SQL的方法，区别就在于查询生成器能够构造出来更好的分页语句，尽可能的避免子查询。
    /// MS体系的分页精髓就在于唯一键，当唯一键带有Asc/Desc/Unkown等排序结尾时，就采用最大最小值分页，否则使用较次的TopNotIn分页。
    /// TopNotIn分页和MaxMin分页的弊端就在于无法完美的支持GroupBy查询分页，只能查到第一页，往后分页就不行了，因为没有主键。
    /// </remarks>
    /// <param name="builder">查询生成器</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns>分页SQL</returns>
    public virtual SelectBuilder PageSplit(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows)
    {
        // 从第一行开始，不需要分页
        if (startRowIndex <= 0 && maximumRows < 1) return builder;

        var sql = PageSplit(builder.ToString(), startRowIndex, maximumRows, builder.Key);
        var sb = new SelectBuilder();
        sb.Parse(sql);
        return sb;
    }
    #endregion

    #region 数据库特性
    /// <summary>长文本长度</summary>
    public virtual Int32 LongTextLength => 4000;

    /// <summary>
    /// 保留字字符串，其实可以在首次使用时动态从Schema中加载
    /// </summary>
    protected virtual String ReservedWordsStr => "";

    private Dictionary<String, Boolean>? _ReservedWords = null;
    /// <summary>
    /// 保留字
    /// </summary>
    private Dictionary<String, Boolean> ReservedWords
    {
        get
        {
            if (_ReservedWords == null)
            {
                var dic = new Dictionary<String, Boolean>(StringComparer.OrdinalIgnoreCase);
                var ss = (ReservedWordsStr + "").Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var item in ss)
                {
                    var key = item.Trim();
                    if (!dic.ContainsKey(key)) dic.Add(key, true);
                }
                _ReservedWords = dic;
            }
            return _ReservedWords;
        }
    }

    /// <summary>是否保留字</summary>
    /// <param name="word"></param>
    /// <returns></returns>
    internal Boolean IsReservedWord(String word) => !word.IsNullOrEmpty() && ReservedWords.ContainsKey(word);

    /// <summary>格式化时间为SQL字符串</summary>
    /// <remarks>
    /// 优化DateTime转为全字符串，平均耗时从25.76ns降为15.07。
    /// 调用非常频繁，每分钟都有数百万次调用。
    /// </remarks>
    /// <param name="dateTime">时间值</param>
    /// <returns></returns>
    public virtual String FormatDateTime(DateTime dateTime) => FormatDateTime(null!, dateTime);

    /// <summary>格式化时间为SQL字符串</summary>
    /// <remarks>
    /// 优化DateTime转为全字符串，平均耗时从25.76ns降为15.07。
    /// 调用非常频繁，每分钟都有数百万次调用。
    /// </remarks>
    /// <param name="column">字段</param>
    /// <param name="dateTime">时间值</param>
    /// <returns></returns>
    public virtual String FormatDateTime(IDataColumn column, DateTime dateTime)
    {
        if (dateTime.Ticks % 10_000_000 == 0)
            return $"'{dateTime:yyyy-MM-dd HH:mm:ss}'";
        else
            return $"'{dateTime:yyyy-MM-dd HH:mm:ss.fffffff}'";
    }

    /// <summary>格式化关键字</summary>
    /// <param name="keyWord">表名</param>
    /// <returns></returns>
    public virtual String FormatKeyWord(String keyWord) => keyWord;

    /// <summary>格式化名称，如果是关键字，则格式化后返回，否则原样返回</summary>
    /// <param name="name">名称</param>
    /// <returns></returns>
    public virtual String FormatName(String name)
    {
        if (name.IsNullOrEmpty()) return name;

        // 优先使用内置关键字
        var rws = ReservedWords;
        if (rws.Count > 0)
        {
            if (rws.ContainsKey(name)) return FormatKeyWord(name);
        }
        else
        {
            if (CreateMetaData() is DbMetaData md && md.ReservedWords.Contains(name)) return FormatKeyWord(name);
        }

        return name;
    }

    /// <summary>格式化表名，考虑表前缀和Owner</summary>
    /// <param name="table">表</param>
    /// <returns></returns>
    public virtual String FormatName(IDataTable table) => FormatName(table, true);

    /// <summary>格式化表名，考虑表前缀和Owner</summary>
    /// <param name="table">表</param>
    /// <param name="formatKeyword">是否格式化关键字</param>
    /// <returns></returns>
    public virtual String FormatName(IDataTable table, Boolean formatKeyword)
    {
        //if (table == null) return null;

        var name = table.TableName;

        // 检查自动表前缀
        var pf = TablePrefix;
        if (!pf.IsNullOrEmpty()) name = pf + name;

        // 名称格式化，只有表名跟名称相同时才处理。否则认为用户指定了表名
        if (table.TableName == table.Name)
        {
            var fmt = DefaultNameFormat;
            if (NameFormat != NameFormats.Default) fmt = NameFormat;
            switch (fmt)
            {
                case NameFormats.Upper:
                    name = name.ToUpperInvariant();
                    break;
                case NameFormats.Lower:
                    name = name.ToLowerInvariant();
                    break;
                case NameFormats.Underline:
                    name = ChangeUnderline(name).ToLowerInvariant();
                    break;
                case NameFormats.Default:
                case NameFormats.None:
                default:
                    break;
            }
        }

        return formatKeyword ? FormatName(name) : name;
    }

    /// <summary>格式化字段名，考虑大小写</summary>
    /// <param name="column">字段</param>
    /// <returns></returns>
    public virtual String FormatName(IDataColumn column)
    {
        //if (column == null) return null;

        var name = column.ColumnName;

        // 名称格式化，只有字段名名跟名称相同时才处理。否则认为用户指定了字段名
        if (column.ColumnName == column.Name)
        {
            var fmt = DefaultNameFormat;
            if (NameFormat != NameFormats.Default) fmt = NameFormat;
            switch (fmt)
            {
                case NameFormats.Upper:
                    name = name.ToUpperInvariant();
                    break;
                case NameFormats.Lower:
                    name = name.ToLowerInvariant();
                    break;
                case NameFormats.Underline:
                    name = ChangeUnderline(name).ToLowerInvariant();
                    break;
                case NameFormats.Default:
                case NameFormats.None:
                default:
                    break;
            }
        }

        return FormatName(name);
    }

    /// <summary>把驼峰命名转为下划线</summary>
    /// <param name="name"></param>
    /// <returns></returns>
    private static String ChangeUnderline(String name)
    {
        var sb = Pool.StringBuilder.Get();

        // 遇到大写字母时，表示新一段开始，增加下划线
        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (i > 0 && Char.IsUpper(ch))
            {
                // 前一个小写字母，新的开始
                if (Char.IsLower(name[i - 1]))
                    sb.Append('_');
                // 后一个字母小写，新的开始
                else if (i < name.Length - 1 && Char.IsLower(name[i + 1]) && Char.IsLower(name[i - 1]))
                    sb.Append('_');
            }
            sb.Append(ch);
        }

        return sb.Return(true);
    }

    /// <summary>格式化数据为SQL数据</summary>
    /// <param name="column">字段</param>
    /// <param name="value">数值</param>
    /// <returns></returns>
    public virtual String FormatValue(IDataColumn column, Object? value)
    {
        var isNullable = true;
        Type? type = null;
        if (column != null)
        {
            type = column.DataType;
            isNullable = column.Nullable;
        }
        else if (value != null)
            type = value.GetType();

        // 如果类型是Nullable的，则获取对应的类型
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(String))
        {
            if (value == null) return isNullable ? "null" : "''";
            //!!! 为SQL格式化数值时，如果字符串是Empty，将不再格式化为null
            //if (String.IsNullOrEmpty(value.ToString()) && isNullable) return "null";

            return "'" + value.ToString().Replace('\0', ' ').Replace("'", "''") + "'";
        }
        else if (type == typeof(DateTime))
        {
            if (value == null) return isNullable ? "null" : "''";
            var dt = Convert.ToDateTime(value);

            //if (dt <= DateTime.MinValue || dt >= DateTime.MaxValue) return isNullable ? "null" : "''";

            if (isNullable && (dt <= DateTime.MinValue || dt >= DateTime.MaxValue)) return "null";

            return FormatDateTime(column!, dt);
        }
        else if (type == typeof(Boolean))
        {
            if (value == null) return isNullable ? "null" : "";
            return Convert.ToBoolean(value) ? "1" : "0";
        }
        else if (type == typeof(Byte[]))
        {
            if (value is not Byte[] bts || bts.Length <= 0) return isNullable ? "null" : "0x0";

            return "0x" + BitConverter.ToString(bts).Replace("-", null);
        }
        else if (type == typeof(Guid))
        {
            if (value == null) return isNullable ? "null" : "''";

            return $"'{value}'";
        }

        if (value == null) return isNullable ? "null" : "";

        // 枚举
        if (type != null && type.IsEnum)
        {
            type = Enum.GetUnderlyingType(type);
        }

        // 转为目标类型，比如枚举转为数字
        if (type != null) value = value.ChangeType(type);
        if (value == null) return isNullable ? "null" : "";

        return value.ToString();
    }

    private static readonly Char[] _likeKeys = new[] { '\'', '%', '_' };
    /// <summary>格式化模糊搜索的字符串。处理转义字符</summary>
    /// <param name="column">字段</param>
    /// <param name="format">格式化字符串</param>
    /// <param name="value">数值</param>
    /// <returns></returns>
    public virtual String FormatLike(IDataColumn column, String format, String value)
    {
        if (value.IsNullOrEmpty()) return value;

        if (value.IndexOfAny(_likeKeys) >= 0)
            value = value
                .Replace("'", "''")
                .Replace("%", "\\%")
                .Replace("_", "\\_");

        return String.Format(format, FormatName(column), value);
    }
    /// <summary>(参数化)格式化模糊搜索的字符串。处理转义字符</summary>
    /// <param name="column">字段</param>
    /// <param name="format">格式化字符串</param>
    /// <returns></returns>
    public virtual String FormatLike(IDataColumn column, String format)
        => String.Format(format, FormatName(column), FormatParameterName(column.ColumnName));

    /// <summary>格式化参数名</summary>
    /// <param name="name">名称</param>
    /// <returns></returns>
    public virtual String FormatParameterName(String name)
    {
        if (name.IsNullOrEmpty()) return name;

        return ParamPrefix + name;
    }

    internal protected virtual String ParamPrefix => "@";

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public virtual String StringConcat(String left, String right) => (!left.IsNullOrEmpty() ? left : "\'\'") + "+" + (!right.IsNullOrEmpty() ? right : "\'\'");

    /// <summary>创建参数</summary>
    /// <param name="name">名称</param>
    /// <param name="value">值</param>
    /// <param name="field">字段</param>
    /// <returns></returns>
    public virtual IDataParameter CreateParameter(String name, Object? value, IDataColumn? field) => CreateParameter(name, value, field?.DataType);

    /// <summary>创建参数</summary>
    /// <param name="name">名称</param>
    /// <param name="value">值</param>
    /// <param name="type">类型</param>
    /// <returns></returns>
    public virtual IDataParameter CreateParameter(String name, Object? value, Type? type = null)
    {
        if (value == null && type == null) throw new ArgumentNullException(nameof(value));

        var dp = Factory.CreateParameter();
        dp.ParameterName = FormatParameterName(name);
        dp.SourceColumn = name;
        dp.Direction = ParameterDirection.Input;

        try
        {
            if (type == null)
            {
                type = value?.GetType();
                // 参数可能是数组
                if (type != null && type != typeof(Byte[]) && type.IsArray) type = type.GetElementTypeEx();
            }
            else
            {
                // 可空类型
                type = Nullable.GetUnderlyingType(type) ?? type;

                if (value is not null and not IList) value = value.ChangeType(type);
            }
            if (type == null) throw new ArgumentNullException(nameof(type));

            // 写入数据类型
            switch (type.GetTypeCode())
            {
                case TypeCode.Boolean:
                    dp.DbType = DbType.Boolean;
                    break;
                case TypeCode.Char:
                case TypeCode.SByte:
                case TypeCode.Byte:
                    dp.DbType = DbType.Byte;
                    break;
                case TypeCode.Int16:
                case TypeCode.UInt16:
                    dp.DbType = DbType.Int16;
                    break;
                case TypeCode.Int32:
                    dp.DbType = DbType.Int32;
                    break;
                case TypeCode.UInt32:
                    dp.DbType = DbType.UInt32;
                    break;
                case TypeCode.Int64:
                    dp.DbType = DbType.Int64;
                    break;
                case TypeCode.UInt64:
                    dp.DbType = DbType.UInt64;
                    break;
                case TypeCode.Single:
                    dp.DbType = DbType.Double;
                    break;
                case TypeCode.Double:
                    dp.DbType = DbType.Double;
                    break;
                case TypeCode.Decimal:
                    dp.DbType = DbType.Decimal;
                    break;
                case TypeCode.DateTime:
                    dp.DbType = DbType.DateTime;
                    break;
                case TypeCode.String:
                    dp.DbType = DbType.String;
                    break;
                default:
                    if (type == typeof(Byte[]))
                    {
                        dp.DbType = DbType.Binary;
                    }
                    break;
            }
            dp.Value = value;
        }
        catch (Exception ex)
        {
            throw new Exception($"创建字段{name}/{type?.Name}的参数时出错", ex);
        }

        return dp;
    }

    /// <summary>创建参数数组</summary>
    /// <param name="ps"></param>
    /// <returns></returns>
    public virtual IDataParameter[] CreateParameters(IDictionary<String, Object>? ps) => ps?.Select(e => CreateParameter(e.Key, e.Value)).ToArray() ?? [];

    /// <summary>根据对象成员创建参数数组</summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public virtual IDataParameter[] CreateParameters(Object? model)
    {
        if (model == null) return new IDataParameter[0];
        if (model is IDataParameter[] dps) return dps;
        if (model is IDataParameter dp) return new[] { dp };
        if (model is IDictionary<String, Object> dic) return CreateParameters(dic);

        var list = new List<IDataParameter>();
        foreach (var pi in model.GetType().GetProperties(true))
        {
            list.Add(CreateParameter(pi.Name, pi.GetValue(model, null), pi.PropertyType));
        }

        return list.ToArray();
    }

    /// <summary>生成批量删除SQL。部分数据库支持分批删除</summary>
    /// <param name="tableName"></param>
    /// <param name="where"></param>
    /// <param name="batchSize"></param>
    /// <returns>不支持分批删除时返回null</returns>
    public virtual String? BuildDeleteSql(String tableName, String where, Int32 batchSize)
    {
        if (batchSize > 0) return null;

        var sql = $"Delete From {FormatName(tableName)}";
        if (!where.IsNullOrEmpty()) sql += " Where " + where;

        return sql;
    }

    /// <summary>是否支持Schema。默认true</summary>
    public Boolean SupportSchema { get; set; } = true;
    #endregion

    #region 辅助函数
    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String ToString() => $"[{ConnName}] {Type} {ServerVersion}";

    protected static String ResolveFile(String file)
    {
        if (file.IsNullOrEmpty()) return file;

        var cfg = NewLife.Setting.Current;
        file = file.Replace("|DataDirectory|", cfg.DataPath);
        file = file.Replace(@"~\App_Data", cfg.DataPath);
        file = file.TrimStart("~");

        // 过滤掉不必要的符号
        file = new FileInfo(file.GetBasePath()).FullName;

        return file;
    }

    //internal ICache _SchemaCache = new MemoryCache { Expire = 10, Period = 10 * 60, };
    #endregion

    #region Sql日志输出
    /// <summary>是否输出SQL语句，默认为XCode调试开关XCode.Debug</summary>
    public Boolean ShowSQL { get; set; }

    /// <summary>SQL最大长度，输出日志时的SQL最大长度，超长截断，默认4096，不截断用0</summary>
    public Int32 SQLMaxLength { get; set; }
    #endregion

    #region 参数化
    /// <summary>参数化添删改查。默认关闭</summary>
    public Boolean UseParameter { get; set; }
    #endregion
}