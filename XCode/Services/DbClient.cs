using NewLife.Data;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Serialization;
using XCode.DataAccessLayer;

namespace XCode.Services;

/// <summary>数据库HTTP客户端。封装ApiHttpClient，提供数据库远程操作</summary>
/// <remarks>
/// 连接字符串格式：Server=http://127.0.0.1:3305;Database=Membership;Password=token123;Provider=Network
/// 
/// 所有数据库操作通过HTTP接口转发到远端DbServer执行：
/// - Query → POST /Db/Query
/// - Execute → POST /Db/Execute
/// - InsertAndGetIdentity → POST /Db/InsertAndGetIdentity
/// - QueryCount → GET /Db/QueryCount
/// - GetTables → GET /Db/GetTables
/// </remarks>
public class DbClient : DisposeBase, ILogFeature
{
    #region 属性
    /// <summary>服务端地址。支持多地址负载均衡</summary>
    public String? Server { get; set; }

    /// <summary>数据库连接名</summary>
    public String? Db { get; set; }

    /// <summary>令牌</summary>
    public String? Token { get; set; }

    /// <summary>最后一次登录成功后的消息</summary>
    public LoginInfo? Info { get; private set; }

    /// <summary>HTTP客户端</summary>
    public ApiHttpClient? Client { get; private set; }

    /// <summary>是否已登录</summary>
    public Boolean Logined { get; private set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;
    #endregion

    #region 构造
    /// <summary>实例化数据库客户端</summary>
    public DbClient() { }

    /// <summary>根据连接字符串实例化数据库客户端</summary>
    /// <param name="server">服务端地址</param>
    /// <param name="db">数据库连接名</param>
    /// <param name="token">令牌</param>
    public DbClient(String server, String db, String? token = null)
    {
        Server = server;
        Db = db;
        Token = token;
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        Client.TryDispose();
        Client = null;
    }
    #endregion

    #region 方法
    /// <summary>打开连接，创建HTTP客户端</summary>
    public void Open()
    {
        if (Client != null) return;

        var server = Server;
        if (server.IsNullOrEmpty()) throw new InvalidOperationException("未设置服务端地址Server");

        var client = new ApiHttpClient(server)
        {
            Log = Log,
        };

        if (!Token.IsNullOrEmpty())
            client.Token = Token;

        Client = client;
    }

    /// <summary>登录到远端数据库服务</summary>
    /// <returns></returns>
    public async Task<LoginInfo?> LoginAsync()
    {
        Open();

        var client = Client ?? throw new InvalidOperationException("客户端未初始化");
        var rs = await client.PostAsync<LoginInfo>("Db/Login", new { db = Db, token = Token }).ConfigureAwait(false);
        if (rs != null)
        {
            Info = rs;
            Logined = true;
            Log?.Info("登录成功！DbType={0} Version={1}", rs.DbType, rs.Version);
        }

        return rs;
    }

    /// <summary>确保已登录</summary>
    private async Task EnsureLogin()
    {
        if (!Logined)
            await LoginAsync().ConfigureAwait(false);
    }
    #endregion

    #region 核心方法
    /// <summary>异步查询，返回DbTable结果集</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="ps">参数集合</param>
    /// <returns></returns>
    public async Task<DbTable?> QueryAsync(String sql, IDictionary<String, Object?>? ps = null)
    {
        await EnsureLogin().ConfigureAwait(false);

        var client = Client ?? throw new InvalidOperationException("客户端未初始化");

        var args = BuildArgs(sql, ps);
        var packet = await client.PostAsync<IPacket>("Db/Query", args).ConfigureAwait(false);
        if (packet == null || packet.Total <= 0) return null;

        var dt = new DbTable();
        dt.Read(packet);

        return dt;
    }

    /// <summary>异步执行SQL，返回受影响行数</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="ps">参数集合</param>
    /// <returns></returns>
    public async Task<Int64> ExecuteAsync(String sql, IDictionary<String, Object?>? ps = null)
    {
        await EnsureLogin().ConfigureAwait(false);

        var client = Client ?? throw new InvalidOperationException("客户端未初始化");

        var args = BuildArgs(sql, ps);
        return await client.PostAsync<Int64>("Db/Execute", args).ConfigureAwait(false);
    }

    /// <summary>异步执行插入语句并返回自增ID</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="ps">参数集合</param>
    /// <returns></returns>
    public async Task<Int64> InsertAndGetIdentityAsync(String sql, IDictionary<String, Object?>? ps = null)
    {
        await EnsureLogin().ConfigureAwait(false);

        var client = Client ?? throw new InvalidOperationException("客户端未初始化");

        var args = BuildArgs(sql, ps);
        return await client.PostAsync<Int64>("Db/InsertAndGetIdentity", args).ConfigureAwait(false);
    }

    /// <summary>异步查询单表记录数</summary>
    /// <param name="tableName">表名</param>
    /// <returns></returns>
    public async Task<Int64> QueryCountAsync(String tableName)
    {
        await EnsureLogin().ConfigureAwait(false);

        var client = Client ?? throw new InvalidOperationException("客户端未初始化");

        return await client.GetAsync<Int64>("Db/QueryCount", new { tableName, db = Db, token = Token }).ConfigureAwait(false);
    }

    /// <summary>异步获取表结构</summary>
    /// <returns></returns>
    public async Task<IDataTable[]?> GetTablesAsync()
    {
        await EnsureLogin().ConfigureAwait(false);

        var client = Client ?? throw new InvalidOperationException("客户端未初始化");

        var json = await client.GetAsync<String>("Db/GetTables", new { db = Db, token = Token }).ConfigureAwait(false);
        if (json.IsNullOrEmpty()) return null;

        return JsonHelper.Convert<IDataTable[]>(json);
    }
    #endregion

    #region 辅助
    /// <summary>构建请求参数，包含 sql/parameters/db/token</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="ps">参数字典</param>
    /// <returns></returns>
    private Object BuildArgs(String sql, IDictionary<String, Object?>? ps)
    {
        // 控制器方法需要 db 和 token 参数做鉴权
        if (ps == null || ps.Count == 0)
            return new { sql, db = Db, token = Token };

        // 控制器期望 parameters 为 JSON 字符串
        var parameters = JsonHelper.ToJson(ps);

        return new { sql, parameters, db = Db, token = Token };
    }

    #endregion
}