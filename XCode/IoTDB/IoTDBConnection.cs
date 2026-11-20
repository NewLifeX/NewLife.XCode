using System.Data;
using System.Data.Common;
using System.Net.Http;
using System.Text;
using NewLife.Serialization;
using XCode.DataAccessLayer;

namespace XCode.IoTDB;

/// <summary>Apache IoTDB 连接</summary>
/// <remarks>
/// 使用 IoTDB REST v2 接口，连接字符串示例：
/// Server=http://localhost:18080;User=root;Password=root;RowLimit=10000
/// </remarks>
public class IoTDBConnection : DbConnection
{
    #region 属性
    private String _connectionString = String.Empty;
    private ConnectionState _state = ConnectionState.Closed;
    private static HttpClient? _sharedHttpClient;
    private HttpClient? _httpClient;
    private String _dataSource = String.Empty;

    /// <summary>连接字符串</summary>
    public override String ConnectionString
    {
        get => _connectionString;
        set => _connectionString = value;
    }

    /// <summary>数据库名。IoTDB 使用路径式数据库名</summary>
    public override String Database => String.Empty;

    /// <summary>数据源</summary>
    public override String DataSource => _dataSource;

    /// <summary>服务器版本</summary>
    public override String ServerVersion => "IoTDB";

    /// <summary>连接状态</summary>
    public override ConnectionState State => _state;

    /// <summary>用户名</summary>
    public String User { get; private set; } = "root";

    /// <summary>密码</summary>
    public String Password { get; private set; } = "root";

    /// <summary>查询返回的最大行数。0 表示使用服务端默认值</summary>
    public Int32 RowLimit { get; private set; }

    /// <summary>HTTP客户端</summary>
    internal HttpClient? HttpClient => _httpClient;

    /// <summary>基础地址</summary>
    internal String BaseUrl => _dataSource.TrimEnd('/');
    #endregion

    #region 构造函数
    /// <summary>实例化</summary>
    public IoTDBConnection() { }

    /// <summary>实例化</summary>
    /// <param name="connectionString">连接字符串</param>
    public IoTDBConnection(String connectionString) => ConnectionString = connectionString;
    #endregion

    #region 方法
    /// <summary>打开连接</summary>
    public override void Open()
    {
        if (_state == ConnectionState.Open) return;

        ParseConnectionString();

        if (_sharedHttpClient == null)
        {
            _sharedHttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        _httpClient = _sharedHttpClient;
        _state = ConnectionState.Open;
    }

    /// <summary>关闭连接</summary>
    public override void Close()
    {
        if (_state == ConnectionState.Closed) return;

        // 不要释放共享的 HttpClient
        _httpClient = null;
        _state = ConnectionState.Closed;
    }

    /// <summary>开始事务</summary>
    /// <param name="isolationLevel">隔离级别</param>
    /// <returns></returns>
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException(L.T("This operation is not supported!"));

    /// <summary>改变数据库</summary>
    /// <param name="databaseName">数据库名</param>
    public override void ChangeDatabase(String databaseName) { }

    /// <summary>创建命令</summary>
    /// <returns></returns>
    protected override DbCommand CreateDbCommand() => new IoTDBCommand { Connection = this };

    /// <summary>释放资源</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        if (disposing) Close();

        base.Dispose(disposing);
    }

    private void ParseConnectionString()
    {
        var builder = new ConnectionStringBuilder(ConnectionString);

        _dataSource = builder["Server"] ?? "http://localhost:18080";
        if (!_dataSource.StartsWithIgnoreCase("http://", "https://")) _dataSource = "http://" + _dataSource;

        User = builder["User"] ?? builder["Username"] ?? "root";
        Password = builder["Password"] ?? "root";
        RowLimit = builder["RowLimit"].ToInt();
    }

    /// <summary>认证头。IoTDB REST v2 使用 Basic 认证</summary>
    /// <returns></returns>
    internal String GetAuthorization()
    {
        var raw = $"{User}:{Password}";
        return "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>执行查询 SQL</summary>
    /// <param name="sql">SQL 文本</param>
    /// <returns>响应 JSON</returns>
    internal String Query(String sql)
    {
        var body = new Dictionary<String, Object?> { ["sql"] = sql };
        if (RowLimit > 0) body["row_limit"] = RowLimit;

        return PostJson("/rest/v2/query", body.ToJson()!);
    }

    /// <summary>执行非查询 SQL</summary>
    /// <param name="sql">SQL 文本</param>
    /// <returns>响应 JSON</returns>
    internal String NonQuery(String sql) => PostJson("/rest/v2/nonQuery", new Dictionary<String, Object?> { ["sql"] = sql }.ToJson()!);

    private String PostJson(String path, String json)
    {
        var httpClient = HttpClient ?? throw new InvalidOperationException(L.T("HttpClient is not initialized."));

        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path);
        request.Headers.Add("Authorization", GetAuthorization());
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
        var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode) throw new Exception(L.T("{0} request failed: {1}", "IoTDB", text));

        return text;
    }
    #endregion
}

/// <summary>IoTDB连接字符串构建器</summary>
public class IoTDBConnectionStringBuilder : DbConnectionStringBuilder
{
}
