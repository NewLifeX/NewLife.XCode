using System.Collections.Concurrent;
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

    /// <summary>会话字典。Key 为令牌，Value 为 DAL 实例。用于缓存登录会话</summary>
    private static readonly ConcurrentDictionary<String, DAL> _sessions = new(StringComparer.OrdinalIgnoreCase);
    #endregion

    #region 登录
    /// <summary>登录认证</summary>
    /// <param name="db">数据库连接名</param>
    /// <param name="token">令牌</param>
    /// <returns></returns>
    public Object Login(String db, String token)
    {
        if (db.IsNullOrEmpty()) return new { code = 401, message = "数据库名称不能为空" };
        if (token.IsNullOrEmpty()) return new { code = 401, message = "令牌不能为空" };

        try
        {
            var dal = Service.Login(token, db);

            // 缓存会话
            var key = $"{token}:{db}";
            _sessions[key] = dal;

            return new LoginInfo
            {
                DbType = dal.DbType,
                Version = dal.Db.ServerVersion,
            };
        }
        catch (Exception ex)
        {
            return new { code = 401, message = ex.Message };
        }
    }
    #endregion

    #region 查询
    /// <summary>执行SQL查询，返回结果集</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数JSON</param>
    /// <param name="token">令牌（从Header或参数传入）</param>
    /// <param name="db">数据库名（从Header或参数传入）</param>
    /// <returns></returns>
    public Object? Query(String sql, String? parameters, String? token, String? db)
    {
        var dal = GetDal(token, db);
        if (dal == null) return new { code = 401, message = "未登录或令牌无效" };

        try
        {
            var ps = DeserializeParameters(parameters);
            var rs = Service.Query(dal, sql, ps);
            if (rs == null) return null;

            // 返回JSON格式结果集
            return rs.ToJson();
        }
        catch (Exception ex)
        {
            return new { code = 500, message = ex.Message };
        }
    }

    /// <summary>快速查询单表记录数</summary>
    /// <param name="tableName">表名</param>
    /// <param name="token">令牌</param>
    /// <param name="db">数据库名</param>
    /// <returns></returns>
    public Object QueryCount(String tableName, String? token, String? db)
    {
        var dal = GetDal(token, db);
        if (dal == null) return new { code = 401, message = "未登录或令牌无效" };

        try
        {
            return Service.QueryCount(dal, tableName);
        }
        catch (Exception ex)
        {
            return new { code = 500, message = ex.Message };
        }
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
        if (dal == null) return new { code = 401, message = "未登录或令牌无效" };

        try
        {
            var ps = DeserializeParameters(parameters);
            return Service.Execute(dal, sql, ps);
        }
        catch (Exception ex)
        {
            return new { code = 500, message = ex.Message };
        }
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
        if (dal == null) return new { code = 401, message = "未登录或令牌无效" };

        try
        {
            var ps = DeserializeParameters(parameters);
            return Service.InsertAndGetIdentity(dal, sql, ps);
        }
        catch (Exception ex)
        {
            return new { code = 500, message = ex.Message };
        }
    }
    #endregion

    #region 元数据
    /// <summary>获取表结构</summary>
    /// <param name="token">令牌</param>
    /// <param name="db">数据库名</param>
    /// <returns></returns>
    public Object? GetTables(String? token, String? db)
    {
        var dal = GetDal(token, db);
        if (dal == null) return new { code = 401, message = "未登录或令牌无效" };

        try
        {
            var tables = Service.GetTables(dal);
            if (tables == null) return null;

            return tables.ToJson();
        }
        catch (Exception ex)
        {
            return new { code = 500, message = ex.Message };
        }
    }
    #endregion

    #region 辅助
    /// <summary>根据令牌和数据库名获取DAL</summary>
    /// <param name="token">令牌</param>
    /// <param name="db">数据库名</param>
    /// <returns></returns>
    private static DAL? GetDal(String? token, String? db)
    {
        if (token.IsNullOrEmpty() || db.IsNullOrEmpty()) return null;

        var key = $"{token}:{db}";
        _sessions.TryGetValue(key, out var dal);
        return dal;
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