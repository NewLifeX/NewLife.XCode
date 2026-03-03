using NewLife.Caching;
using NewLife.Data;
using NewLife.Remoting;
using NewLife.Serialization;
using XCode.DataAccessLayer;

namespace XCode.Services;

/// <summary>数据库HTTP控制器。提供数据库远程操作的REST接口</summary>
/// <remarks>
/// 路由映射到 /Db/*，支持以下接口：
/// - POST /Db/Login - 登录认证
/// - POST /Db/Query - SQL查询
/// - POST /Db/Execute - SQL执行
/// - POST /Db/InsertAndGetIdentity - 插入并返回自增ID
/// - GET  /Db/QueryCount - 快速查询记录数
/// - GET  /Db/GetTables - 获取表结构
/// 
/// 控制器不直接包含业务逻辑，所有操作委托给 DbService 执行。
/// 可用于 HttpServer（MapController）或 ASP.NET 场景。
/// </remarks>
public class DbController
{
    #region 属性
    /// <summary>数据库服务。处理实际的数据库操作</summary>
    public DbService Service { get; set; } = null!;

    /// <summary>令牌校验缓存。Key 为 token:db</summary>
    public ICache TokenCache { get; set; } = new MemoryCache { Expire = 600 };

    /// <summary>令牌校验缓存有效期，单位秒</summary>
    public Int32 TokenCacheExpire { get; set; } = 600;
    #endregion

    #region 构造
    /// <summary>实例化数据库控制器</summary>
    public DbController() { }

    /// <summary>实例化数据库控制器</summary>
    /// <param name="service">数据库服务</param>
    public DbController(DbService service) => Service = service;
    #endregion

    #region 登录
    /// <summary>登录认证</summary>
    /// <param name="db">数据库连接名</param>
    /// <param name="token">令牌</param>
    /// <returns></returns>
    public LoginInfo Login(String db, String token)
    {
        if (db.IsNullOrEmpty()) throw new ApiException(ApiCode.BadRequest, "数据库名称不能为空");
        if (token.IsNullOrEmpty()) throw new ApiException(ApiCode.BadRequest, "令牌不能为空");

        ValidateToken(token, db);
        var dal = DAL.Create(db);

        return new LoginInfo
        {
            DbType = dal.DbType,
            Version = dal.Db.ServerVersion,
        };
    }
    #endregion

    #region 查询
    /// <summary>执行SQL查询，返回结果集</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数JSON</param>
    /// <param name="token">令牌（从Header或参数传入）</param>
    /// <param name="db">数据库名（从Header或参数传入）</param>
    /// <returns></returns>
    public IPacket? Query(String sql, String? parameters, String? token, String? db)
    {
        var dal = GetDal(token, db);

        var ps = DeserializeParameters(parameters);
        var rs = Service.Query(dal, sql, ps);
        if (rs == null) return null;

        // 使用二进制包返回结果集，减少序列化开销
        return rs.ToPacket();
    }

    /// <summary>快速查询单表记录数</summary>
    /// <param name="tableName">表名</param>
    /// <param name="token">令牌</param>
    /// <param name="db">数据库名</param>
    /// <returns></returns>
    public Object QueryCount(String tableName, String? token, String? db)
    {
        var dal = GetDal(token, db);

        return new { data = Service.QueryCount(dal, tableName) };
    }
    #endregion

    #region 执行
    /// <summary>执行SQL语句，返回受影响行数</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数JSON</param>
    /// <param name="token">令牌</param>
    /// <param name="db">数据库名</param>
    /// <returns></returns>
    public Object Execute(String sql, String? parameters, String? token, String? db)
    {
        var dal = GetDal(token, db);

        var ps = DeserializeParameters(parameters);
        return new { data = Service.Execute(dal, sql, ps) };
    }

    /// <summary>执行插入语句并返回自增ID</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数JSON</param>
    /// <param name="token">令牌</param>
    /// <param name="db">数据库名</param>
    /// <returns></returns>
    public Object InsertAndGetIdentity(String sql, String? parameters, String? token, String? db)
    {
        var dal = GetDal(token, db);

        var ps = DeserializeParameters(parameters);
        return new { data = Service.InsertAndGetIdentity(dal, sql, ps) };
    }
    #endregion

    #region 元数据
    /// <summary>获取表结构</summary>
    /// <param name="token">令牌</param>
    /// <param name="db">数据库名</param>
    /// <returns></returns>
    public IDataTable[]? GetTables(String? token, String? db)
    {
        var dal = GetDal(token, db);

        var tables = Service.GetTables(dal);
        if (tables == null) return null;

        return tables;
    }
    #endregion

    #region 辅助
    /// <summary>根据令牌和数据库名获取DAL</summary>
    /// <param name="token">令牌</param>
    /// <param name="db">数据库名</param>
    /// <returns></returns>
    private DAL GetDal(String? token, String? db)
    {
        if (db.IsNullOrEmpty()) throw new ApiException(ApiCode.BadRequest, "数据库名称不能为空");

        ValidateToken(token, db);

        return DAL.Create(db);
    }

    /// <summary>验证令牌并检查是否可访问指定数据库</summary>
    /// <param name="token">令牌</param>
    /// <param name="db">数据库名</param>
    private void ValidateToken(String? token, String db)
    {
        if (token.IsNullOrEmpty()) throw new ApiException(ApiCode.Unauthorized, "未登录或令牌无效");

        var key = $"db:token:{token}:{db}";
        if (TokenCache.TryGetValue<Boolean>(key, out var ok) && ok) return;

        try
        {
            Service.ValidateToken(token, db);
        }
        catch (UnauthorizedAccessException)
        {
            throw new ApiException(ApiCode.Unauthorized, "未登录或令牌无效");
        }

        TokenCache.Set(key, true, TokenCacheExpire);
    }

    /// <summary>反序列化参数字典</summary>
    /// <param name="json">参数JSON</param>
    /// <returns></returns>
    private static IDictionary<String, Object?>? DeserializeParameters(String? json)
    {
        if (json.IsNullOrEmpty()) return null;

        var dic = JsonParser.Decode(json);
        if (dic is IDictionary<String, Object?> result) return result;

        return null;
    }

    #endregion
}