using System.Collections.Concurrent;
using NewLife.Data;
using NewLife.Log;
using XCode.DataAccessLayer;

namespace XCode.Services;

/// <summary>数据库服务层。实际执行 DAL 操作，可被 DbServer 和 ASP.NET 控制器共用</summary>
/// <remarks>
/// 核心业务逻辑层，负责：
/// 1. 令牌验证与数据库连接管理
/// 2. SQL 查询与执行
/// 3. 表结构元数据获取
/// 
/// 设计为可注入服务，支持两种部署方式：
/// - 独立部署：通过 DbServer + DbController 使用
/// - ASP.NET 集成：直接注入到 ASP.NET 控制器使用
/// </remarks>
public class DbService
{
    #region 属性
    /// <summary>令牌字典。Key 为令牌，Value 为允许访问的数据库连接名列表（空列表表示允许所有）</summary>
    public IDictionary<String, String[]> Tokens { get; set; } = new ConcurrentDictionary<String, String[]>(StringComparer.OrdinalIgnoreCase);

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;
    #endregion

    #region 方法
    /// <summary>验证令牌是否可访问指定数据库</summary>
    /// <param name="token">令牌</param>
    /// <param name="db">数据库连接名</param>
    public void ValidateToken(String token, String db)
    {
        if (token.IsNullOrEmpty()) throw new ArgumentException("令牌不能为空", nameof(token));
        if (db.IsNullOrEmpty()) throw new ArgumentException("数据库名称不能为空", nameof(db));

        if (Tokens.Count <= 0) return;

        if (!Tokens.TryGetValue(token, out var dbs))
            throw new UnauthorizedAccessException("无效令牌");

        if (dbs != null && dbs.Length > 0 && !dbs.Contains(db, StringComparer.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException($"令牌无权访问数据库[{db}]");
    }

    /// <summary>验证令牌并获取 DAL 实例</summary>
    /// <param name="token">令牌</param>
    /// <param name="db">数据库连接名</param>
    /// <returns></returns>
    public DAL Login(String token, String db)
    {
        ValidateToken(token, db);

        var dal = DAL.Create(db);

        Log?.Info("登录成功 db={0} type={1} version={2}", db, dal.DbType, dal.Db.ServerVersion);

        return dal;
    }

    /// <summary>执行 SQL 查询，返回结果集</summary>
    /// <param name="dal">数据访问层</param>
    /// <param name="sql">SQL 语句</param>
    /// <param name="parameters">参数字典</param>
    /// <returns></returns>
    public DbTable? Query(DAL dal, String sql, IDictionary<String, Object?>? parameters)
    {
        if (dal == null) throw new ArgumentNullException(nameof(dal));
        if (sql.IsNullOrEmpty()) throw new ArgumentException("SQL不能为空", nameof(sql));

        var ps = ConvertToDictionary(parameters);

        return dal.Query(sql, ps);
    }

    /// <summary>执行 SQL 语句，返回受影响的行数</summary>
    /// <param name="dal">数据访问层</param>
    /// <param name="sql">SQL 语句</param>
    /// <param name="parameters">参数字典</param>
    /// <returns></returns>
    public Int64 Execute(DAL dal, String sql, IDictionary<String, Object?>? parameters)
    {
        if (dal == null) throw new ArgumentNullException(nameof(dal));
        if (sql.IsNullOrEmpty()) throw new ArgumentException("SQL不能为空", nameof(sql));

        var ps = ConvertToDictionary(parameters);
        var dps = ps != null ? dal.Db.CreateParameters(ps) : null;

        return dal.Execute(sql, System.Data.CommandType.Text, dps);
    }

    /// <summary>执行插入语句并返回新增行的自动编号</summary>
    /// <param name="dal">数据访问层</param>
    /// <param name="sql">SQL 语句</param>
    /// <param name="parameters">参数字典</param>
    /// <returns></returns>
    public Int64 InsertAndGetIdentity(DAL dal, String sql, IDictionary<String, Object?>? parameters)
    {
        if (dal == null) throw new ArgumentNullException(nameof(dal));
        if (sql.IsNullOrEmpty()) throw new ArgumentException("SQL不能为空", nameof(sql));

        var ps = ConvertToDictionary(parameters);
        var dps = ps != null ? dal.Db.CreateParameters(ps) : null;

        return dal.InsertAndGetIdentity(sql, System.Data.CommandType.Text, dps);
    }

    /// <summary>快速查询单表记录数</summary>
    /// <param name="dal">数据访问层</param>
    /// <param name="tableName">表名</param>
    /// <returns></returns>
    public Int64 QueryCount(DAL dal, String tableName)
    {
        if (dal == null) throw new ArgumentNullException(nameof(dal));
        if (tableName.IsNullOrEmpty()) throw new ArgumentException("表名不能为空", nameof(tableName));

        return dal.Session.QueryCountFast(tableName);
    }

    /// <summary>获取远端数据库的表结构</summary>
    /// <param name="dal">数据访问层</param>
    /// <returns></returns>
    public IDataTable[]? GetTables(DAL dal)
    {
        if (dal == null) throw new ArgumentNullException(nameof(dal));

        return dal.Tables?.ToArray();
    }
    #endregion

    #region 辅助
    /// <summary>将可空值字典转换为非空值字典，同时去掉参数名的@前缀避免CreateParameters重复添加</summary>
    /// <param name="parameters">参数字典</param>
    /// <returns></returns>
    private static IDictionary<String, Object>? ConvertToDictionary(IDictionary<String, Object?>? parameters)
    {
        if (parameters == null || parameters.Count == 0) return null;

        var dic = new Dictionary<String, Object>();
        foreach (var item in parameters)
        {
            if (item.Value != null)
            {
                var key = item.Key.TrimStart('@');
                dic[key] = item.Value;
            }
        }
        return dic.Count > 0 ? dic : null;
    }
    #endregion
}
