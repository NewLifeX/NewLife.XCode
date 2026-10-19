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
            XTrace.WriteLine("SQL logging is currently enabled. To reduce log volume, set [Config/XCode.config:ShowSQL=false].");
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
    public static IDataTable CreateTable() => ObjectContainer.Current.GetService<IDataTable>() ?? throw new InvalidDataException(L.T("IDataTable is not registered."));

    /// <summary>批量操作能力。不同数据库支持不同批量操作</summary>
    public BatchCapability BatchCapabilities => Db.BatchCapability;

    /// <summary>是否支持批操作</summary>
    [Obsolete("请使用 BatchCapabilities 属性，通过 BatchCapability 枚举判断具体批操作支持情况")]
    public Boolean SupportBatch => Db.BatchCapability == BatchCapability.Insert;

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