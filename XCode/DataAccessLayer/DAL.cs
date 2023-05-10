using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Xml;
using NewLife;
using NewLife.Caching;
using NewLife.Collections;
using NewLife.Configuration;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Security;
using NewLife.Serialization;
using NewLife.Threading;

namespace XCode.DataAccessLayer;

/// <summary>数据访问层</summary>
/// <remarks>
/// 主要用于选择不同的数据库，不同的数据库的操作有所差别。
/// 每一个数据库链接字符串，对应唯一的一个DAL实例。
/// 数据库链接字符串可以写在配置文件中，然后在Create时指定名字；
/// 也可以直接把链接字符串作为AddConnStr的参数传入。
/// </remarks>
public partial class DAL
{
    #region 属性
    /// <summary>连接名</summary>
    public String ConnName { get; }

    /// <summary>实现了IDatabase接口的数据库类型</summary>
    public Type ProviderType { get; private set; }

    /// <summary>数据库类型</summary>
    public DatabaseType DbType { get; private set; }

    /// <summary>连接字符串</summary>
    /// <remarks>
    /// 内部密码字段可能处于加密状态。
    /// 修改连接字符串将会清空<see cref="Db"/>
    /// </remarks>
    public String ConnStr { get; private set; }

    /// <summary>数据保护者</summary>
    /// <remarks>
    /// 用于保护连接字符串中的密码字段，在向IDatabase设置连接字符串前解密。
    /// 默认保护密码可通过环境变量或者配置文件的ProtectedKey项进行设置。
    /// </remarks>
    public ProtectedKey ProtectedKey { get; set; } = ProtectedKey.Instance;

    private IDatabase _Db;
    /// <summary>数据库。所有数据库操作在此统一管理，强烈建议不要直接使用该属性，在不同版本中IDatabase可能有较大改变</summary>
    public IDatabase Db
    {
        get
        {
            if (_Db != null) return _Db;
            lock (this)
            {
                if (_Db != null) return _Db;

                var type = ProviderType ?? throw new XCodeException("无法识别{0}的数据提供者！", ConnName);

                //!!! 重量级更新：经常出现链接字符串为127/master的连接错误，非常有可能是因为这里线程冲突，A线程创建了实例但未来得及赋值连接字符串，就被B线程使用了
                var db = type.CreateInstance() as IDatabase;
                if (!ConnName.IsNullOrEmpty()) db.ConnName = ConnName;
                if (_infos.TryGetValue(ConnName, out var info)) db.Provider = info.Provider;
                if (db is DbBase dbBase) dbBase.Tracer = Tracer;

                // 设置连接字符串时，可能触发内部的一系列动作，因此放在最后
                if (!ConnStr.IsNullOrEmpty()) db.ConnectionString = DecodeConnStr(ConnStr);

                _Db = db;

                return _Db;
            }
        }
    }

    /// <summary>数据库会话</summary>
    public IDbSession Session => Db.CreateSession();

    /// <summary>数据库会话。为异步操作而准备，将来可能移除</summary>
    public IAsyncDbSession AsyncSession => (Db as DbBase).CreateSessionForAsync();

    private String _mapTo;
    private readonly ICache _cache = new MemoryCache();
    #endregion

    #region 创建函数
    /// <summary>构造函数</summary>
    /// <param name="connName">配置名</param>
    private DAL(String connName) => ConnName = connName;

    private Boolean _inited;
    private void Init()
    {
        if (_inited) return;
        lock (this)
        {
            if (_inited) return;

            var connName = ConnName;
            var css = ConnStrs;
            //if (!css.ContainsKey(connName)) throw new XCodeException("请在使用数据库前设置[" + connName + "]连接字符串");
            if (!css.ContainsKey(connName)) GetFromConfigCenter(connName);
            if (!css.ContainsKey(connName)) OnResolve?.Invoke(this, new ResolveEventArgs(connName));
            if (!css.ContainsKey(connName))
            {
                var cfg = NewLife.Setting.Current;
                var set = XCodeSetting.Current;
                var connstr = "Data Source=" + cfg.DataPath.CombinePath(connName + ".db");
                if (set.Migration <= Migration.On) connstr += ";Migration=On";
                WriteLog("自动为[{0}]设置SQLite连接字符串：{1}", connName, connstr);
                AddConnStr(connName, connstr, null, "SQLite");
            }

            ConnStr = css[connName];
            if (ConnStr.IsNullOrEmpty()) throw new XCodeException("请在使用数据库前设置[" + connName + "]连接字符串");

            // 连接映射
            var vs = ConnStr.SplitAsDictionary("=", ",", true);
            if (vs.TryGetValue("MapTo", out var map) && !map.IsNullOrEmpty()) _mapTo = map;

            if (_infos.TryGetValue(connName, out var t))
            {
                ProviderType = t.Type;
                DbType = DbFactory.GetDefault(t.Type)?.Type ?? DatabaseType.None;
            }

            // 读写分离
            if (!connName.EndsWithIgnoreCase(".readonly"))
            {
                var connName2 = connName + ".readonly";
                if (css.ContainsKey(connName2)) ReadOnly = Create(connName2);
            }

            _inited = true;
        }
    }
    #endregion

    #region 静态管理
    private static readonly ConcurrentDictionary<String, DAL> _dals = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>创建一个数据访问层对象。</summary>
    /// <param name="connName">配置名</param>
    /// <returns>对应于指定链接的全局唯一的数据访问层对象</returns>
    public static DAL Create(String connName)
    {
        if (String.IsNullOrEmpty(connName)) throw new ArgumentNullException(nameof(connName));

        // Dictionary.TryGetValue 在多线程高并发下有可能抛出空异常
        var dal = _dals.GetOrAdd(connName, k => new DAL(k));

        // 创建完成对象后，初始化时单独锁这个对象，避免整体加锁
        dal.Init();

        // 映射到另一个连接
        if (!dal._mapTo.IsNullOrEmpty()) dal = _dals.GetOrAdd(dal._mapTo, Create);

        return dal;
    }

    private void Reset()
    {
        _Db.TryDispose();

        _Db = null;
        _Tables = null;
        _hasCheck = false;
        HasCheckTables.Clear();
        _mapTo = null;

        GC.Collect(2);

        _inited = false;
        Init();
    }

    private static ConcurrentDictionary<String, DbInfo> _infos;
    private static void InitConnections()
    {
        var ds = new ConcurrentDictionary<String, DbInfo>(StringComparer.OrdinalIgnoreCase);

        try
        {
            LoadConfig(ds);
            //LoadAppSettings(cs, ts);
        }
        catch (Exception ex)
        {
            WriteLog("LoadConfig 失败。{0}", ex.Message);
        }

        // 联合使用 appsettings.json
        try
        {
            LoadAppSettings("appsettings.json", ds);
        }
        catch (Exception ex)
        {
            WriteLog("LoadAppSettings 失败。{0}", ex.Message);
        }
        // 读取环境变量:ASPNETCORE_ENVIRONMENT=Development
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (String.IsNullOrWhiteSpace(env)) env = "Production";
        try
        {
            LoadAppSettings($"appsettings.{env.Trim()}.json", ds);
        }
        catch (Exception ex)
        {
            WriteLog("LoadAppSettings 失败。{0}", ex.Message);
        }

        // 从环境变量加载连接字符串，优先级最高
        try
        {
            LoadEnvironmentVariable(ds, Environment.GetEnvironmentVariables());
        }
        catch (Exception ex)
        {
            WriteLog("LoadEnvironmentVariable 失败。{0}", ex.Message);
        }

        var cs = new ConcurrentDictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in ds)
        {
            cs.TryAdd(item.Key, item.Value.ConnectionString);
        }

        ConnStrs = cs;
        _infos = ds;
    }

    /// <summary>链接字符串集合</summary>
    /// <remarks>
    /// 如果需要添加连接字符串，应该使用AddConnStr，MapTo连接字符串除外（可以直接ConnStrs.TryAdd添加）；
    /// 如果需要修改一个DAL的连接字符串，不应该修改这里，而是修改DAL实例的<see cref="ConnStr"/>属性。
    /// </remarks>
    public static ConcurrentDictionary<String, String> ConnStrs { get; private set; }

    internal static void LoadConfig(IDictionary<String, DbInfo> ds)
    {
        var file = "web.config".GetFullPath();
        var fname = AppDomain.CurrentDomain.FriendlyName;
        // 2020-10-22 阴 fname可能是特殊情况，要特殊处理 "TestSourceHost: Enumerating source (E:\projects\bin\Debug\project.dll)"
        if (!File.Exists(fname) && fname.StartsWith("TestSourceHost: Enumerating"))
        {
            XTrace.WriteLine($"AppDomain.CurrentDomain.FriendlyName不太友好，处理一下：{fname}");
            fname = fname[fname.IndexOf(AppDomain.CurrentDomain.BaseDirectory, StringComparison.Ordinal)..].TrimEnd(')');
        }
        if (!File.Exists(file)) file = "app.config".GetFullPath();
        if (!File.Exists(file)) file = $"{fname}.config".GetFullPath();
        if (!File.Exists(file)) file = $"{fname}.exe.config".GetFullPath();
        if (!File.Exists(file)) file = $"{fname}.dll.config".GetFullPath();

        if (File.Exists(file))
        {
            // 读取配置文件
            var doc = new XmlDocument();
            doc.Load(file);

            var nodes = doc.SelectNodes("/configuration/connectionStrings/add");
            if (nodes != null)
            {
                foreach (XmlNode item in nodes)
                {
                    var name = item.Attributes["name"]?.Value;
                    var connstr = item.Attributes["connectionString"]?.Value;
                    var provider = item.Attributes["providerName"]?.Value;
                    if (name.IsNullOrEmpty() || connstr.IsNullOrWhiteSpace()) continue;

                    var type = DbFactory.GetProviderType(connstr, provider);
                    if (type == null) XTrace.WriteLine("无法识别{0}的提供者{1}！", name, provider);

                    ds[name] = new DbInfo
                    {
                        Name = name,
                        ConnectionString = connstr,
                        Type = type,
                        Provider = provider,
                    };
                }
            }
        }
    }

    internal static void LoadAppSettings(String fileName, IDictionary<String, DbInfo> ds)
    {
        // Asp.Net Core的Debug模式下配置文件位于项目目录而不是输出目录
        var file = fileName.GetBasePath();
        if (!File.Exists(file)) file = fileName.GetFullPath();
        if (!File.Exists(file)) file = Path.Combine(Directory.GetCurrentDirectory(), fileName);
        if (File.Exists(file))
        {
            var text = File.ReadAllText(file);

            // 预处理注释
            text = JsonConfigProvider.TrimComment(text);

            var dic = JsonParser.Decode(text);
            dic = dic?["ConnectionStrings"] as IDictionary<String, Object>;
            if (dic != null && dic.Count > 0)
            {
                foreach (var item in dic)
                {
                    var name = item.Key;
                    if (name.IsNullOrEmpty()) continue;
                    if (item.Value is IDictionary<String, Object> cfgs)
                    {
                        var connstr = cfgs["connectionString"] + "";
                        var provider = cfgs["providerName"] + "";
                        if (connstr.IsNullOrWhiteSpace()) continue;

                        var type = DbFactory.GetProviderType(connstr, provider);
                        if (type == null) XTrace.WriteLine("无法识别{0}的提供者{1}！", name, provider);

                        ds[name] = new DbInfo
                        {
                            Name = name,
                            ConnectionString = connstr,
                            Type = type,
                            Provider = provider,
                        };
                    }
                    else if (item.Value is String connstr)
                    {
                        //var connstr = cfgs["connectionString"] + "";
                        if (connstr.IsNullOrWhiteSpace()) continue;

                        var builder = new ConnectionStringBuilder(connstr);
                        var provider = builder.TryGetValue("provider", out var prv) ? prv : null;

                        var type = DbFactory.GetProviderType(connstr, provider);
                        if (type == null) XTrace.WriteLine("无法识别{0}的提供者{1}！", name, provider);

                        ds[name] = new DbInfo
                        {
                            Name = name,
                            ConnectionString = connstr,
                            Type = type,
                            Provider = provider,
                        };
                    }
                }
            }
        }
    }

    internal static void LoadEnvironmentVariable(IDictionary<String, DbInfo> ds, IDictionary envs)
    {
        foreach (DictionaryEntry item in envs)
        {
            if (item.Key is String key && item.Value is String value && key.StartsWithIgnoreCase("XCode_"))
            {
                var connName = key["XCode_".Length..];

                var type = DbFactory.GetProviderType(value, null);
                if (type == null)
                {
                    WriteLog("环境变量[{0}]设置连接[{1}]时，未通过provider指定数据库类型，使用默认类型SQLite", key, connName);
                    type = DbFactory.Create(DatabaseType.SQLite).GetType();
                }

                var dic = value.SplitAsDictionary("=", ";");
                var provider = dic["provider"];

                // 允许后来者覆盖前面设置过了的
                ds[connName] = new DbInfo
                {
                    Name = connName,
                    ConnectionString = value,
                    Type = type,
                    Provider = provider,
                };
            }
        }
    }

    /// <summary>添加连接字符串</summary>
    /// <param name="connName">连接名</param>
    /// <param name="connStr">连接字符串</param>
    /// <param name="type">实现了IDatabase接口的数据库类型</param>
    /// <param name="provider">数据库提供者，如果没有指定数据库类型，则有提供者判断使用哪一种内置类型</param>
    public static void AddConnStr(String connName, String connStr, Type type, String provider)
    {
        if (connName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(connName));
        if (connStr.IsNullOrEmpty()) return;

        //2016.01.04 @宁波-小董，加锁解决大量分表分库多线程带来的提供者无法识别错误
        lock (_infos)
        {
            if (!ConnStrs.TryGetValue(connName, out var oldConnStr)) oldConnStr = null;

            // 从连接字符串中取得提供者
            if (provider.IsNullOrEmpty())
            {
                var dic = connStr.SplitAsDictionary("=", ";");
                provider = dic["provider"];
            }

            if (type == null) type = DbFactory.GetProviderType(connStr, provider);
            if (type == null) throw new XCodeException("无法识别{0}的提供者{1}！", connName, provider);

            // 允许后来者覆盖前面设置过了的
            //var set = new ConnectionStringSettings(connName, connStr, provider);
            ConnStrs[connName] = connStr;

            var inf = _infos.GetOrAdd(connName, k => new DbInfo { Name = k });
            inf.Name = connName;
            inf.ConnectionString = connStr;
            if (type != null) inf.Type = type;
            if (!provider.IsNullOrEmpty()) inf.Provider = provider;

            // 如果连接字符串改变，则重置所有
            if (!oldConnStr.IsNullOrEmpty() && !oldConnStr.EqualIgnoreCase(connStr))
            {
                WriteLog("[{0}]的连接字符串改变，准备重置！", connName);

                var dal = _dals.GetOrAdd(connName, k => new DAL(k));
                dal.ConnStr = connStr;
                dal.Reset();
            }
        }
    }

    /// <summary>找不到连接名时调用。支持用户自定义默认连接</summary>
    [Obsolete]
    public static event EventHandler<ResolveEventArgs> OnResolve;

    /// <summary>获取连接字符串的委托。可以二次包装在连接名前后加上标识，存放在配置中心</summary>
    public static GetConfigCallback GetConfig { get; set; }

    private static IConfigProvider _configProvider;
    /// <summary>设置配置提供者。可对接配置中心，DAL内部自动从内置对象容器中取得星尘配置提供者</summary>
    /// <param name="configProvider"></param>
    public static void SetConfig(IConfigProvider configProvider)
    {
        WriteLog("DAL绑定配置提供者 {0}", configProvider);

        configProvider.Bind(new MyDAL());
        _configProvider = configProvider;
    }

    private static readonly ConcurrentHashSet<String> _conns = new();
    private static TimerX _timerGetConfig;
    /// <summary>从配置中心加载连接字符串，并支持定时刷新</summary>
    /// <param name="connName"></param>
    /// <returns></returns>
    private static Boolean GetFromConfigCenter(String connName)
    {
        var getConfig = GetConfig;

        // 自动从对象容器里取得配置提供者
        if (getConfig == null && _configProvider == null)
        {
            var prv = ObjectContainer.Provider.GetService<IConfigProvider>();
            if (prv != null)
            {
                WriteLog("DAL自动绑定配置提供者 {0}", prv);

                prv.Bind(new MyDAL());
                _configProvider = prv;
            }
        }

        if (getConfig == null) getConfig = _configProvider?.GetConfig;
        {
            var str = getConfig?.Invoke("db:" + connName);
            if (str.IsNullOrEmpty()) return false;

            AddConnStr(connName, str, null, null);

            // 加入集合，定时更新
            if (!_conns.Contains(connName)) _conns.TryAdd(connName);
        }

        // 读写分离
        if (!connName.EndsWithIgnoreCase(".readonly"))
        {
            var connName2 = connName + ".readonly";
            var str = getConfig?.Invoke("db:" + connName2);
            if (!str.IsNullOrEmpty()) AddConnStr(connName2, str, null, null);

            // 加入集合，定时更新
            if (!_conns.Contains(connName2)) _conns.TryAdd(connName2);
        }

        if (_timerGetConfig == null && GetConfig != null) _timerGetConfig = new TimerX(DoGetConfig, null, 5_000, 60_000) { Async = true };

        return true;
    }

    private static void DoGetConfig(Object state)
    {
        foreach (var item in _conns)
        {
            var str = GetConfig?.Invoke("db:" + item);
            if (!str.IsNullOrEmpty()) AddConnStr(item, str, null, null);
        }
    }

    private class MyDAL : IConfigMapping
    {
        public void MapConfig(IConfigProvider provider, IConfigSection section)
        {
            foreach (var item in _conns)
            {
                var str = section["db:" + item];
                if (!str.IsNullOrEmpty()) AddConnStr(item, str, null, null);
            }
        }
    }
    #endregion

    #region 连接字符串编码解码
    /// <summary>连接字符串编码</summary>
    /// <remarks>明文=>UTF8字节=>Base64</remarks>
    /// <param name="connstr"></param>
    /// <returns></returns>
    public static String EncodeConnStr(String connstr)
    {
        if (String.IsNullOrEmpty(connstr)) return connstr;

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(connstr));
    }

    /// <summary>连接字符串解码</summary>
    /// <remarks>Base64=>UTF8字节=>明文</remarks>
    /// <param name="connstr"></param>
    /// <returns></returns>
    private String DecodeConnStr(String connstr)
    {
        if (String.IsNullOrEmpty(connstr)) return connstr;

        connstr = ProtectedKey.Unprotect(connstr);

        // 如果包含任何非Base64编码字符，直接返回
        foreach (var c in connstr)
        {
            if (!(c >= 'a' && c <= 'z' ||
                c >= 'A' && c <= 'Z' ||
                c >= '0' && c <= '9' ||
                c == '+' || c == '/' || c == '=')) return connstr;
        }

        Byte[] bts = null;
        try
        {
            // 尝试Base64解码，如果解码失败，估计就是连接字符串，直接返回
            bts = Convert.FromBase64String(connstr);
        }
        catch { return connstr; }

        return Encoding.UTF8.GetString(bts);
    }
    #endregion

    #region 正向工程
    private IList<IDataTable> _Tables;
    /// <summary>取得所有表和视图的构架信息（异步缓存延迟1秒）。设为null可清除缓存</summary>
    /// <remarks>
    /// 如果不存在缓存，则获取后返回；否则使用线程池线程获取，而主线程返回缓存。
    /// </remarks>
    /// <returns></returns>
    public IList<IDataTable> Tables
    {
        get
        {
            // 如果不存在缓存，则获取后返回；否则使用线程池线程获取，而主线程返回缓存
            if (_Tables == null)
                _Tables = GetTables();
            else
                Task.Factory.StartNew(() => { _Tables = GetTables(); });

            return _Tables;
        }
        set =>
            //设为null可清除缓存
            _Tables = null;
    }

    private IList<IDataTable> GetTables()
    {
        if (Db is DbBase db2 && !db2.SupportSchema) return new List<IDataTable>();

        var tracer = Tracer ?? GlobalTracer;
        using var span = tracer?.NewSpan($"db:{ConnName}:GetTables", ConnName);
        try
        {
            //CheckDatabase();
            var tables = Db.CreateMetaData().GetTables();

            if (span != null) span.Tag += ": " + tables.Join(",");

            return tables;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>
    /// 获取所有表名，带缓存，不区分大小写
    /// </summary>
    public ICollection<String> TableNames => _cache.GetOrAdd("tableNames", k => new HashSet<String>(GetTableNames(), StringComparer.OrdinalIgnoreCase), 60);

    /// <summary>
    /// 快速获取所有表名，无缓存，区分大小写
    /// </summary>
    /// <returns></returns>
    public IList<String> GetTableNames()
    {
        var tracer = Tracer ?? GlobalTracer;
        using var span = tracer?.NewSpan($"db:{ConnName}:GetTableNames", ConnName);
        try
        {
            var tables = Db.CreateMetaData().GetTableNames();

            if (span != null) span.Tag += ": " + tables.Join(",");

            return tables;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>导出模型</summary>
    /// <returns></returns>
    public String Export()
    {
        var list = Tables;

        if (list == null || list.Count <= 0) return null;

        return Export(list);
    }

    /// <summary>导出模型</summary>
    /// <param name="tables"></param>
    /// <returns></returns>
    public static String Export(IEnumerable<IDataTable> tables) => ModelHelper.ToXml(tables);

    /// <summary>导入模型</summary>
    /// <param name="xml"></param>
    /// <returns></returns>
    public static List<IDataTable> Import(String xml)
    {
        if (String.IsNullOrEmpty(xml)) return null;

        return ModelHelper.FromXml(xml, CreateTable);
    }

    /// <summary>导入模型文件</summary>
    /// <param name="xmlFile"></param>
    /// <returns></returns>
    public static List<IDataTable> ImportFrom(String xmlFile)
    {
        if (xmlFile.IsNullOrEmpty()) return null;

        xmlFile = xmlFile.GetFullPath();
        if (!File.Exists(xmlFile)) return null;

        return ModelHelper.FromXml(File.ReadAllText(xmlFile), CreateTable);
    }
    #endregion

    #region 反向工程
    private Boolean _hasCheck;
    /// <summary>检查数据库，建库建表加字段</summary>
    /// <remarks>不阻塞，可能第一个线程正在检查表架构，别的线程已经开始使用数据库了</remarks>
    public void CheckDatabase()
    {
        if (_hasCheck) return;
        lock (this)
        {
            if (_hasCheck) return;
            _hasCheck = true;

            try
            {
                switch (Db.Migration)
                {
                    case Migration.Off:
                        break;
                    case Migration.ReadOnly:
                        Task.Factory.StartNew(CheckTables);
                        break;
                    case Migration.On:
                    case Migration.Full:
                        CheckTables();
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                if (Debug) WriteLog(ex.GetMessage());
            }
        }
    }

    internal List<String> HasCheckTables = new();
    /// <summary>检查是否已存在，如果不存在则添加</summary>
    /// <param name="tableName"></param>
    /// <returns></returns>
    internal Boolean CheckAndAdd(String tableName)
    {
        var tbs = HasCheckTables;
        if (tbs.Contains(tableName)) return true;
        lock (tbs)
        {
            if (tbs.Contains(tableName)) return true;

            tbs.Add(tableName);
        }

        return false;
    }

    /// <summary>检查所有数据表，建表加字段</summary>
    public void CheckTables()
    {
        var name = ConnName;
        WriteLog("开始检查连接[{0}/{1}]的数据库架构……", name, DbType);

        var sw = Stopwatch.StartNew();

        try
        {
            var list = EntityFactory.GetTables(name, true);
            if (list != null && list.Count > 0)
            {
                // 移除所有已初始化的
                list.RemoveAll(dt => CheckAndAdd(dt.TableName));

                // 过滤掉视图
                list.RemoveAll(dt => dt.IsView);

                if (list != null && list.Count > 0)
                {
                    WriteLog("[{0}]待检查表架构的实体个数：{1}", name, list.Count);

                    SetTables(list.ToArray());
                }
            }
        }
        finally
        {
            sw.Stop();

            WriteLog("检查连接[{0}/{1}]的数据库架构耗时{2:n0}ms", name, DbType, sw.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>检查指定数据表，建表加字段</summary>
    /// <param name="tables"></param>
    public void SetTables(params IDataTable[] tables)
    {
        if (Db is DbBase db2 && !db2.SupportSchema) return;

        var tracer = Tracer ?? GlobalTracer;
        using var span = tracer?.NewSpan($"db:{ConnName}:SetTables", tables.Join());
        try
        {
            //// 构建DataTable时也要注意表前缀，避免反向工程用错
            //var pf = Db.TablePrefix;
            //if (!pf.IsNullOrEmpty())
            //{
            //    foreach (var tbl in tables)
            //    {
            //        if (!tbl.TableName.StartsWithIgnoreCase(pf)) tbl.TableName = pf + tbl.TableName;
            //    }
            //}

            foreach (var item in tables)
            {
                FixIndexName(item);
            }

            Db.CreateMetaData().SetTables(Db.Migration, tables);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    private void FixIndexName(IDataTable table)
    {
        // 修改一下索引名，否则，可能因为同一个表里面不同的索引冲突
        if (table.Indexes != null)
        {
            var pf = Db.TablePrefix;
            foreach (var di in table.Indexes)
            {
                if (!di.Name.IsNullOrEmpty() && pf.IsNullOrEmpty()) continue;

                var sb = Pool.StringBuilder.Get();
                sb.AppendFormat("IX_{0}", Db.FormatName(table, false));
                foreach (var item in di.Columns)
                {
                    sb.Append('_');
                    sb.Append(item);
                }

                di.Name = sb.Put(true);
            }
        }
    }
    #endregion
}