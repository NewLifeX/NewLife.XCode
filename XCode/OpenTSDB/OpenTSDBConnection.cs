using System.Data;
using System.Data.Common;
using System.Net.Http;
using System.Text;
using XCode.DataAccessLayer;

namespace XCode.OpenTSDB;

#pragma warning disable CS8765 // 基类 setter 允许 null，自动属性无法分别标注 getter/setter 可空性

/// <summary>OpenTSDB连接</summary>
/// <remarks>
/// OpenTSDB 基于 HBase，提供 HTTP JSON 接口。连接字符串示例：
/// Server=http://localhost:4242;User=;Password=;Timeout=30
/// </remarks>
public class OpenTSDBConnection : DbConnection
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

    /// <summary>数据库名。OpenTSDB 没有数据库概念，固定返回空</summary>
    public override String Database => String.Empty;

    /// <summary>数据源</summary>
    public override String DataSource => _dataSource;

    /// <summary>服务器版本</summary>
    public override String ServerVersion => "OpenTSDB";

    /// <summary>连接状态</summary>
    public override ConnectionState State => _state;

    /// <summary>用户名</summary>
    public String User { get; private set; } = String.Empty;

    /// <summary>密码</summary>
    public String Password { get; private set; } = String.Empty;

    /// <summary>超时时间，单位秒</summary>
    public Int32 Timeout { get; private set; } = 30;

    /// <summary>HTTP客户端</summary>
    internal HttpClient? HttpClient => _httpClient;

    /// <summary>基础地址</summary>
    internal String BaseUrl => _dataSource.TrimEnd('/');
    #endregion

    #region 构造函数
    /// <summary>实例化</summary>
    public OpenTSDBConnection() { }

    /// <summary>实例化</summary>
    /// <param name="connectionString">连接字符串</param>
    public OpenTSDBConnection(String connectionString) => ConnectionString = connectionString;
    #endregion

    #region 方法
    /// <summary>打开连接</summary>
    public override void Open()
    {
        if (_state == ConnectionState.Open) return;

        ParseConnectionString();

        // 使用共享 HttpClient 以避免 socket 耗尽
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
    protected override DbCommand CreateDbCommand() => new OpenTSDBCommand { Connection = this };

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

        _dataSource = builder["Server"] ?? "http://localhost:4242";
        if (!_dataSource.StartsWithIgnoreCase("http://", "https://")) _dataSource = "http://" + _dataSource;

        User = builder["User"] ?? String.Empty;
        Password = builder["Password"] ?? String.Empty;

        var timeout = builder["Timeout"].ToInt();
        Timeout = timeout > 0 ? timeout : 30;
    }

    /// <summary>添加认证头。OpenTSDB 本身没有认证，保留 Basic 以兼容前置代理</summary>
    /// <param name="request">请求</param>
    internal void AddAuth(HttpRequestMessage request)
    {
        if (User.IsNullOrEmpty()) return;

        var raw = $"{User}:{Password}";
        request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));
    }

    /// <summary>发送 GET 请求并返回响应文本</summary>
    /// <param name="path">相对路径</param>
    /// <returns></returns>
    internal String Get(String path)
    {
        var httpClient = HttpClient ?? throw new InvalidOperationException(L.T("HttpClient is not initialized."));

        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path);
        AddAuth(request);

        var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
        var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode) throw new Exception(L.T("{0} request failed: {1}", "OpenTSDB", text));

        return text;
    }

    /// <summary>发送 POST 请求并返回响应文本</summary>
    /// <param name="path">相对路径</param>
    /// <param name="content">请求内容</param>
    /// <param name="mediaType">内容类型</param>
    /// <returns></returns>
    internal String Post(String path, String content, String mediaType)
    {
        var httpClient = HttpClient ?? throw new InvalidOperationException(L.T("HttpClient is not initialized."));

        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path);
        AddAuth(request);
        request.Content = new StringContent(content, Encoding.UTF8, mediaType);

        var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
        var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode) throw new Exception(L.T("{0} request failed: {1}", "OpenTSDB", text));

        return text;
    }
    #endregion
}

/// <summary>OpenTSDB连接字符串构建器</summary>
public class OpenTSDBConnectionStringBuilder : DbConnectionStringBuilder
{
}

#pragma warning restore CS8765
