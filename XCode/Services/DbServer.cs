using NewLife.Http;
using NewLife.Log;
using NewLife.Model;

namespace XCode.Services;

/// <summary>数据库HTTP服务器。继承HttpServer，提供数据库远程访问服务</summary>
/// <remarks>
/// 启动后监听HTTP端口，通过 DbController 提供数据库远程操作接口。
/// 支持独立部署场景，也可以在ASP.NET应用中直接使用DbService。
/// 
/// 用法示例：
/// <code>
/// var server = new DbServer();
/// server.Port = 3305;
/// server.Service.Tokens["mytoken"] = new[] { "Membership" };
/// server.Start();
/// </code>
/// </remarks>
public class DbServer : HttpServer
{
    #region 属性
    /// <summary>数据库服务层</summary>
    public DbService Service { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化数据库HTTP服务器</summary>
    public DbServer()
    {
        Name = "DbServer";
        Port = 3305;

        Service = new DbService();
    }

    /// <summary>实例化数据库HTTP服务器</summary>
    /// <param name="service">自定义数据库服务</param>
    public DbServer(DbService service)
    {
        Name = "DbServer";
        Port = 3305;

        Service = service ?? throw new ArgumentNullException(nameof(service));
    }
    #endregion

    #region 方法
    /// <summary>开始时调用。注册控制器路由</summary>
    protected override void OnStart()
    {
        // 注册DbService到服务容器，以便控制器通过构造函数注入获取
        var container = new ObjectContainer();
        container.AddSingleton(Service);
        ServiceProvider = container.BuildServiceProvider();

        // 注册控制器，路由映射到 /Db/*
        MapController<DbController>();

        base.OnStart();
    }
    #endregion

    #region 日志
    /// <summary>日志</summary>
    public new ILog Log
    {
        get => base.Log;
        set
        {
            base.Log = value;
            Service?.Log = value;
        }
    }
    #endregion
}