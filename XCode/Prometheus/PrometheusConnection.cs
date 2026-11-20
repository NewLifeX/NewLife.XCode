using System.Data;
using System.Data.Common;
using System.Net.Http;
using System.Text;
using XCode.DataAccessLayer;

namespace XCode.Prometheus;

/// <summary>Prometheus 查询模式</summary>
public enum PrometheusQueryMode
{
    /// <summary>即时查询。调用 /api/v1/query</summary>
    Instant = 0,

    /// <summary>范围查询。调用 /api/v1/query_range</summary>
    Range,
}

/// <summary>Prometheus 连接。只读 PromQL 查询，兼容 VictoriaMetrics</summary>
/// <remarks>
/// 连接字符串示例：
/// Server=http://localhost:9090;Mode=Range;Start=-1h;End=now;Step=60s
/// </remarks>
public class PrometheusConnection : DbConnection
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

    /// <summary>数据库名。Prometheus 没有数据库概念</summary>
    public override String Database => String.Empty;

    /// <summary>数据源</summary>
    public override String DataSource => _dataSource;

    /// <summary>服务器版本</summary>
    public override String ServerVersion => "Prometheus";

    /// <summary>连接状态</summary>
    public override ConnectionState State => _state;

    /// <summary>用户名</summary>
    public String User { get; private set; } = String.Empty;

    /// <summary>密码</summary>
    public String Password { get; private set; } = String.Empty;

    /// <summary>Bearer 令牌</summary>
    public String BearerToken { get; private set; } = String.Empty;

    /// <summary>查询模式</summary>
    public PrometheusQueryMode Mode { get; private set; }

    /// <summary>范围查询起始时间</summary>
    public String Start { get; private set; } = "-5m";

    /// <summary>范围查询结束时间</summary>
    public String End { get; private set; } = "now";

    /// <summary>范围查询步长</summary>
    public String Step { get; private set; } = "15s";

    /// <summary>HTTP客户端</summary>
    internal HttpClient? HttpClient => _httpClient;

    /// <summary>基础地址</summary>
    internal String BaseUrl => _dataSource.TrimEnd('/');
    #endregion

    #region 构造函数
    /// <summary>实例化</summary>
    public PrometheusConnection() { }

    /// <summary>实例化</summary>
    /// <param name="connectionString">连接字符串</param>
    public PrometheusConnection(String connectionString) => ConnectionString = connectionString;
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
    protected override DbCommand CreateDbCommand() => new PrometheusCommand { Connection = this };

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

        _dataSource = builder["Server"] ?? "http://localhost:9090";
        if (!_dataSource.StartsWithIgnoreCase("http://", "https://")) _dataSource = "http://" + _dataSource;

        User = builder["User"] ?? builder["Username"] ?? String.Empty;
        Password = builder["Password"] ?? String.Empty;
        BearerToken = builder["BearerToken"] ?? builder["Token"] ?? String.Empty;

        if (!builder["Start"].IsNullOrEmpty()) Start = builder["Start"]!;
        if (!builder["End"].IsNullOrEmpty()) End = builder["End"]!;
        if (!builder["Step"].IsNullOrEmpty()) Step = builder["Step"]!;

        Mode = builder["Mode"].EqualIgnoreCase("Range") ? PrometheusQueryMode.Range : PrometheusQueryMode.Instant;
    }

    /// <summary>发送 GET 请求并返回响应文本</summary>
    /// <param name="path">相对路径与查询串</param>
    /// <returns></returns>
    internal String Get(String path)
    {
        var httpClient = HttpClient ?? throw new InvalidOperationException(L.T("HttpClient is not initialized."));

        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path);
        if (!BearerToken.IsNullOrEmpty())
        {
            request.Headers.Add("Authorization", $"Bearer {BearerToken}");
        }
        else if (!User.IsNullOrEmpty())
        {
            var raw = $"{User}:{Password}";
            request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));
        }

        var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
        var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode) throw new Exception(L.T("{0} request failed: {1}", "Prometheus", text));

        return text;
    }

    /// <summary>构建查询地址</summary>
    /// <param name="query">PromQL</param>
    /// <param name="range">是否范围查询</param>
    /// <returns></returns>
    internal String BuildQueryUrl(String query, Boolean range)
    {
        var q = Uri.EscapeDataString(query);
        if (!range) return $"/api/v1/query?query={q}";

        // Prometheus 的 start/end 只接受 RFC3339 或 Unix 时间戳，这里把 -1h/now 之类的相对写法换算成时间戳
        var now = DateTimeOffset.UtcNow;
        var start = ResolveTime(Start, now);
        var end = ResolveTime(End, now);

        return $"/api/v1/query_range?query={q}&start={Uri.EscapeDataString(start)}&end={Uri.EscapeDataString(end)}&step={Uri.EscapeDataString(Step)}";
    }

    /// <summary>解析时间参数。支持 now、-1h/-5m/-30s/-2d/-1w，其余原样返回</summary>
    /// <param name="value">时间文本</param>
    /// <param name="now">当前时间</param>
    /// <returns>Unix 秒或原样文本</returns>
    internal static String ResolveTime(String value, DateTimeOffset now)
    {
        if (value.IsNullOrEmpty() || value.EqualIgnoreCase("now")) return ToUnixSeconds(now).ToString();

        if (value[0] == '-' && value.Length > 2 && Int32.TryParse(value[1..^1], out var number) && number > 0)
        {
            var span = value[^1] switch
            {
                's' => TimeSpan.FromSeconds(number),
                'm' => TimeSpan.FromMinutes(number),
                'h' => TimeSpan.FromHours(number),
                'd' => TimeSpan.FromDays(number),
                'w' => TimeSpan.FromDays(number * 7),
                _ => TimeSpan.Zero,
            };
            if (span > TimeSpan.Zero) return ToUnixSeconds(now.Subtract(span)).ToString();
        }

        return value;
    }

    private static Int64 ToUnixSeconds(DateTimeOffset time) => (Int64)(time.UtcDateTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

    /// <summary>执行查询并返回 JSON</summary>
    /// <param name="query">PromQL</param>
    /// <param name="range">是否范围查询</param>
    /// <returns></returns>
    internal String Query(String query, Boolean range) => Get(BuildQueryUrl(query, range));
    #endregion
}

/// <summary>Prometheus连接字符串构建器</summary>
public class PrometheusConnectionStringBuilder : DbConnectionStringBuilder
{
}
