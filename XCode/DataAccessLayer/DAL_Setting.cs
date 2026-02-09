using System.Diagnostics;
using NewLife.Log;
using NewLife.Model;

namespace XCode.DataAccessLayer;

partial class DAL
{
    static DAL()
    {
        var ioc = ObjectContainer.Current;
        ioc.AddTransient<IDataTable, XTable>();

        InitLog();
        InitConnections();
    }

    #region 属性
    /// <summary>是否调试</summary>
    public static Boolean Debug { get; set; } = XCodeSetting.Current.Debug;

    /// <summary>服务提供者。用于获取配置的连接字符串，主要对接星尘配置中心</summary>
    public static IServiceProvider ServiceProvider { get; set; } = ObjectContainer.Provider;
    #endregion

    #region Sql日志输出
    /// <summary>输出日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public static void WriteLog(String format, params Object?[] args)
    {
        if (!Debug) return;

        //InitLog();
        XTrace.WriteLine(format, args);
    }

    /// <summary>输出日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    [Conditional("DEBUG")]
    public static void WriteDebugLog(String format, params Object?[] args)
    {
        if (!Debug) return;

        //InitLog();
        XTrace.WriteLine(format, args);
    }

    static Int32 hasInitLog = 0;
    internal static void InitLog()
    {
        if (Interlocked.CompareExchange(ref hasInitLog, 1, 0) > 0) return;

        // 输出当前版本
        System.Reflection.Assembly.GetExecutingAssembly().WriteVersion();

        if (XCodeSetting.Current.ShowSQL)
            XTrace.WriteLine("当前配置为输出SQL日志，如果觉得日志过多，可以修改配置关闭[Config/XCode.config:ShowSQL=false]。");
    }
    #endregion

    #region SQL拦截器
#if NET45
    private static readonly ThreadLocal<Action<String>> _filter = new();
#else
    private static readonly AsyncLocal<Action<String>> _filter = new();
#endif
    /// <summary>本地过滤器（本线程SQL拦截）</summary>
    public static Action<String> LocalFilter { get => _filter.Value; set => _filter.Value = value; }

    /// <summary>APM跟踪器</summary>
    public ITracer? Tracer { get; set; } = GlobalTracer;

    /// <summary>全局APM跟踪器</summary>
    public static ITracer? GlobalTracer { get; set; } = DefaultTracer.Instance;
    #endregion

    #region 辅助函数
    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String ToString() => ConnName;

    /// <summary>建立数据表对象</summary>
    /// <returns></returns>
    public static IDataTable CreateTable() => ObjectContainer.Current.GetService<IDataTable>() ?? throw new InvalidDataException($"未注册[IDataTable]");

    /// <summary>是否支持批操作</summary>
    /// <returns></returns>
    public Boolean SupportBatch
    {
        get
        {
            if (DbType is DatabaseType.MySql or DatabaseType.Oracle or DatabaseType.SQLite or DatabaseType.PostgreSQL or DatabaseType.PostgreSQL) return true;

            // SqlServer对批处理有BUG，将在3.0中修复
            // https://github.com/dotnet/corefx/issues/29391
            if (DbType == DatabaseType.SqlServer) return true;

            return false;
        }
    }

    /// <summary>获取批大小。优先取连接设置，再取全局，默认5000</summary>
    /// <param name="defaultSize">默认批大小</param>
    /// <returns></returns>
    public Int32 GetBatchSize(Int32 defaultSize = 5_000)
    {
        var batchSize = Db.BatchSize;
        if (batchSize <= 0) batchSize = XCodeSetting.Current.BatchSize;
        if (batchSize <= 0) batchSize = defaultSize;
        if (batchSize <= 0) batchSize = 5_000;

        return batchSize;
    }
    #endregion
}