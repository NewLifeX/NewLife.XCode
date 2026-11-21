using System.Data;
using System.Data.Common;
using System.Net.Http;
using System.Text;
using NewLife.Serialization;
using XCode.DataAccessLayer;

namespace XCode.InfluxDB;

/// <summary>InfluxDB查询语言</summary>
public enum InfluxQueryLanguage
{
    /// <summary>自动。按服务端版本选择，1.x 使用 InfluxQL，2.x 使用 Flux，3.x 使用 SQL</summary>
    Auto = 0,

    /// <summary>Flux。仅 InfluxDB 2.x 支持</summary>
    Flux,

    /// <summary>InfluxQL。InfluxDB 1.x/2.x/3.x 均支持</summary>
    InfluxQl,

    /// <summary>SQL。仅 InfluxDB 3.x 支持</summary>
    Sql,
}

/// <summary>InfluxDB连接</summary>
public class InfluxDBConnection : DbConnection
{
    #region 属性
    private String _connectionString = String.Empty;
    private ConnectionState _state = ConnectionState.Closed;
    private static HttpClient? _sharedHttpClient;
    private HttpClient? _httpClient;
    private String _database = String.Empty;
    private String _dataSource = String.Empty;

    /// <summary>连接字符串</summary>
    public override String ConnectionString
    {
        get => _connectionString;
        set => _connectionString = value;
    }

    /// <summary>数据库名。InfluxDB 1.x 的 database，或 2.x/3.x 的 bucket</summary>
    public override String Database => _database;

    /// <summary>数据源</summary>
    public override String DataSource => _dataSource;

    /// <summary>服务器版本</summary>
    public override String ServerVersion => Version > 0 ? $"InfluxDB {Version}.x" : "InfluxDB";
    /// <summary>连接状态</summary>
    public override ConnectionState State => _state;

    /// <summary>InfluxDB Token。2.x/3.x 使用</summary>
    public String Token { get; private set; } = String.Empty;

    /// <summary>InfluxDB Organization。2.x 使用</summary>
    public String Organization { get; private set; } = String.Empty;

    /// <summary>InfluxDB Bucket（相当于数据库）</summary>
    public String Bucket { get; private set; } = String.Empty;

    /// <summary>用户名。1.x 使用</summary>
    public String User { get; private set; } = String.Empty;

    /// <summary>密码。1.x 使用</summary>
    public String Password { get; private set; } = String.Empty;

    /// <summary>服务端主版本号。1/2/3，0 表示尚未探测</summary>
    public Int32 Version { get; private set; }

    /// <summary>查询语言</summary>
    public InfluxQueryLanguage QueryLanguage { get; private set; }

    /// <summary>HTTP客户端</summary>
    internal HttpClient? HttpClient => _httpClient;
    #endregion

    #region 构造函数
    /// <summary>实例化</summary>
    public InfluxDBConnection() { }

    /// <summary>实例化</summary>
    /// <param name="connectionString">连接字符串</param>
    public InfluxDBConnection(String connectionString)
    {
        ConnectionString = connectionString;
    }
    #endregion

    #region 方法
    /// <summary>打开连接</summary>
    public override void Open()
    {
        if (_state == ConnectionState.Open) return;

        // 解析连接字符串
        ParseConnectionString();

        // 显式指定版本时立即确定默认查询语言，避免调用方每次判断
        ApplyDefaultLanguage();

        // 使用共享 HttpClient 以避免 socket 耗尽
        // 为每个连接创建独立的 headers
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
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        throw new NotSupportedException(L.T("InfluxDB does not support transactions."));
    }

    /// <summary>改变数据库</summary>
    /// <param name="databaseName">数据库名</param>
    public override void ChangeDatabase(String databaseName)
    {
        Bucket = databaseName;
        _database = databaseName;
    }

    /// <summary>创建命令</summary>
    /// <returns></returns>
    protected override DbCommand CreateDbCommand()
    {
        return new InfluxDBCommand { Connection = this };
    }

    /// <summary>释放资源</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        if (disposing)
        {
            Close();
        }
        base.Dispose(disposing);
    }

    private void ParseConnectionString()
    {
        var builder = new ConnectionStringBuilder(ConnectionString);

        // Server=http://localhost:8086;Token=mytoken;Organization=myorg;Bucket=mybucket;Database=mybucket
        // 1.x: Server=http://localhost:8086;Database=telegraf;User=root;Password=root;Version=1
        // 3.x: Server=http://localhost:8181;Database=sensors;Token=xxx;Version=3
        _dataSource = builder["Server"] ?? "http://localhost:8086";
        Token = builder["Token"] ?? String.Empty;
        Organization = builder["Organization"] ?? builder["Org"] ?? String.Empty;
        Bucket = builder["Bucket"] ?? builder["Database"] ?? String.Empty;
        _database = Bucket;
        User = builder["User"] ?? builder["Username"] ?? String.Empty;
        Password = builder["Password"] ?? String.Empty;

        Version = ParseVersion(builder["Version"]);
        QueryLanguage = ParseQueryLanguage(builder["QueryLanguage"]);

        Validate();
    }

    /// <summary>解析版本号。Auto 返回 0，由首次请求探测</summary>
    /// <param name="value">版本文本</param>
    /// <returns>主版本号</returns>
    internal static Int32 ParseVersion(String? value)
    {
        if (value.IsNullOrEmpty() || value.EqualIgnoreCase("Auto")) return 0;

        var text = value.Trim();
        if (text.StartsWith("1")) return 1;
        if (text.StartsWith("2")) return 2;
        if (text.StartsWith("3")) return 3;

        return 0;
    }

    /// <summary>解析查询语言</summary>
    /// <param name="value">语言文本</param>
    /// <returns>查询语言</returns>
    internal static InfluxQueryLanguage ParseQueryLanguage(String? value)
    {
        if (value.IsNullOrEmpty() || value.EqualIgnoreCase("Auto")) return InfluxQueryLanguage.Auto;

        if (value.EqualIgnoreCase("Flux")) return InfluxQueryLanguage.Flux;
        if (value.EqualIgnoreCase("InfluxQL", "InfluxQl", "QL")) return InfluxQueryLanguage.InfluxQl;
        if (value.EqualIgnoreCase("SQL")) return InfluxQueryLanguage.Sql;

        return InfluxQueryLanguage.Auto;
    }

    private void Validate()
    {
        // 版本未知时不强制校验，连接串可能只写了 Server，等首次请求再探测
        if (Version == 0) return;

        if (Version == 1)
        {
            if (Bucket.IsNullOrEmpty()) throw new ArgumentException(L.T("Database is required in connection string."));
        }
        else if (Version == 2)
        {
            if (Token.IsNullOrEmpty()) throw new ArgumentException(L.T("Token is required in connection string."));
            if (Organization.IsNullOrEmpty()) throw new ArgumentException(L.T("Organization is required in connection string."));
            if (Bucket.IsNullOrEmpty()) throw new ArgumentException(L.T("Bucket is required in connection string."));
        }
        else if (Version == 3)
        {
            if (Bucket.IsNullOrEmpty()) throw new ArgumentException(L.T("Database is required in connection string."));
        }
    }

    /// <summary>默认查询语言。1.x 使用 InfluxQL，2.x 使用 Flux，3.x 使用 SQL</summary>
    /// <param name="version">主版本号</param>
    /// <returns>查询语言</returns>
    internal static InfluxQueryLanguage GetDefaultQueryLanguage(Int32 version) => version switch
    {
        1 => InfluxQueryLanguage.InfluxQl,
        3 => InfluxQueryLanguage.Sql,
        _ => InfluxQueryLanguage.Flux,
    };

    /// <summary>确保版本已探测</summary>
    /// <returns>主版本号，无法探测时返回 0</returns>
    internal Int32 EnsureVersion()
    {
        if (Version > 0)
        {
            ApplyDefaultLanguage();
            return Version;
        }

        var server = DataSource;
        VersionCache? cache = null;
        lock (_versionLock) _versionCache.TryGetValue(server, out cache);

        if (cache != null && cache.Expire > DateTime.Now)
        {
            Version = cache.Version;
        }
        else if (_httpClient != null)
        {
            Version = DetectVersion(server, Token, User, Password, _httpClient);

            lock (_versionLock) _versionCache[server] = new VersionCache { Version = Version, Expire = DateTime.Now.AddMinutes(Version > 0 ? 10 : 1) };
        }

        ApplyDefaultLanguage();

        return Version;
    }

    private void ApplyDefaultLanguage()
    {
        if (QueryLanguage == InfluxQueryLanguage.Auto) QueryLanguage = GetDefaultQueryLanguage(Version);
    }

    /// <summary>执行 InfluxQL 或 SQL 查询，返回结果表</summary>
    /// <param name="query">查询文本</param>
    /// <param name="language">查询语言</param>
    /// <returns>结果表</returns>
    internal DataTable Query(String query, InfluxQueryLanguage language)
    {
        var httpClient = HttpClient ?? throw new InvalidOperationException(L.T("HttpClient is not initialized."));

        var url = GetQueryUrl(language, query);

        // InfluxDB 3 的查询端点只接受 GET（POST 需要 JSON 请求体）；Flux 走 POST 且查询文本在请求体中
        using var request = new HttpRequestMessage(language == InfluxQueryLanguage.Flux ? HttpMethod.Post : HttpMethod.Get, url);
        AddAuth(request, Token, User, Password);
        request.Headers.Add("Accept", "application/json");

        var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
        var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode) throw new Exception(L.T("InfluxDB query failed: {0}", text));

        return InfluxDBResultParser.Parse(text, language);
    }

    /// <summary>执行 InfluxQL 查询。任意版本可用</summary>
    /// <param name="query">查询文本</param>
    /// <returns>结果表</returns>
    internal DataTable QueryInfluxQl(String query) => Query(query, InfluxQueryLanguage.InfluxQl);

    private sealed class VersionCache
    {
        public Int32 Version { get; set; }

        public DateTime Expire { get; set; }
    }

    private static readonly Dictionary<String, VersionCache> _versionCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Object _versionLock = new();

    /// <summary>探测服务端主版本号。通过 /ping 响应头，失败时回退 /health</summary>
    /// <param name="server">服务地址</param>
    /// <param name="token">令牌</param>
    /// <param name="user">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="http">HTTP客户端</param>
    /// <returns>主版本号，无法探测时返回 0</returns>
    internal static Int32 DetectVersion(String server, String token, String user, String password, HttpClient http)
    {
        var baseUrl = server.TrimEnd('/');

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/ping");
            AddAuth(request, token, user, password);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var response = http.SendAsync(request, cts.Token).GetAwaiter().GetResult();

            if (response.Headers.TryGetValues("X-Influxdb-Version", out var values))
            {
                var ver = ParseMajor(values.FirstOrDefault());
                if (ver > 0) return ver;
            }
        }
        catch
        {
            // 探测失败不影响主流程，后续请求会给出真实错误
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/health");
            AddAuth(request, token, user, password);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var response = http.SendAsync(request, cts.Token).GetAwaiter().GetResult();
            var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (JsonHelper.Parse(text) is IDictionary<String, Object?> dic && dic.TryGetValue("version", out var value))
                return ParseMajor(value as String);
        }
        catch
        {
            // 同上
        }

        return 0;
    }

    private static Int32 ParseMajor(String? version)
    {
        if (version.IsNullOrEmpty()) return 0;

        var text = version.Trim().TrimStart('v', 'V');
        var index = text.IndexOf('.');
        if (index > 0) text = text[..index];

        return text.ToInt();
    }

    /// <summary>添加认证头。2.x/3.x 使用 Token，1.x 使用 Basic</summary>
    /// <param name="request">请求</param>
    /// <param name="token">令牌</param>
    /// <param name="user">用户名</param>
    /// <param name="password">密码</param>
    internal static void AddAuth(HttpRequestMessage request, String token, String user, String password)
    {
        if (!token.IsNullOrEmpty())
        {
            request.Headers.Add("Authorization", $"Token {token}");
        }
        else if (!user.IsNullOrEmpty())
        {
            var raw = $"{user}:{password}";
            request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));
        }
    }

    /// <summary>基础地址</summary>
    /// <returns></returns>
    internal String GetBaseUrl() => DataSource.TrimEnd('/');

    /// <summary>写入地址。按版本选择 v1/v2/v3 端点</summary>
    /// <returns></returns>
    internal String GetWriteUrl()
    {
        EnsureVersion();

        var baseUrl = GetBaseUrl();
        return Version switch
        {
            1 => $"{baseUrl}/write?db={Uri.EscapeDataString(Bucket)}&precision=ns",
            3 => $"{baseUrl}/api/v3/write_lp?db={Uri.EscapeDataString(Bucket)}&precision=nanosecond&accept_partial=false",
            _ => $"{baseUrl}/api/v2/write?org={Uri.EscapeDataString(Organization)}&bucket={Uri.EscapeDataString(Bucket)}&precision=ns",
        };
    }

    /// <summary>查询地址。Flux 返回 /api/v2/query，InfluxQL 按版本选择，SQL 仅 3.x</summary>
    /// <param name="language">查询语言</param>
    /// <param name="query">查询文本</param>
    /// <returns></returns>
    internal String GetQueryUrl(InfluxQueryLanguage language, String query)
    {
        EnsureVersion();

        var baseUrl = GetBaseUrl();
        var db = Uri.EscapeDataString(Bucket);
        var q = Uri.EscapeDataString(query);

        if (language == InfluxQueryLanguage.Auto) language = QueryLanguage;
        if (language == InfluxQueryLanguage.Auto) language = GetDefaultQueryLanguage(Version);

        return language switch
        {
            InfluxQueryLanguage.Flux => $"{baseUrl}/api/v2/query?org={Uri.EscapeDataString(Organization)}",
            InfluxQueryLanguage.Sql => Version == 3
                ? $"{baseUrl}/api/v3/query_sql?db={db}&q={q}&format=jsonl"
                : throw new NotSupportedException(L.T("{0} requires InfluxDB {1}.", "SQL queries", "3.x")),
            _ => Version == 3
                ? $"{baseUrl}/api/v3/query_influxql?db={db}&q={q}&format=json"
                : $"{baseUrl}/query?db={db}&q={q}",
        };
    }
    #endregion
}

/// <summary>连接字符串构建器</summary>
public class InfluxDBConnectionStringBuilder : DbConnectionStringBuilder
{
}
