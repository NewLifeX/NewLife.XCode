using System.Data;
using System.Data.Common;
using System.Net.Http;
using System.Text;
using XCode.DataAccessLayer;

namespace XCode.InfluxDB;

/// <summary>InfluxDB连接</summary>
public class InfluxDBConnection : DbConnection
{
    #region 属性
    private String _connectionString = String.Empty;
    private ConnectionState _state = ConnectionState.Closed;
    private HttpClient? _httpClient;
    private String _database = String.Empty;
    private String _dataSource = String.Empty;

    /// <summary>连接字符串</summary>
    public override String ConnectionString
    {
        get => _connectionString;
        set => _connectionString = value;
    }

    /// <summary>数据库名</summary>
    public override String Database => _database;

    /// <summary>数据源</summary>
    public override String DataSource => _dataSource;

    /// <summary>服务器版本</summary>
    public override String ServerVersion => "InfluxDB 2.x";

    /// <summary>连接状态</summary>
    public override ConnectionState State => _state;

    /// <summary>InfluxDB Token</summary>
    public String Token { get; private set; } = String.Empty;

    /// <summary>InfluxDB Organization</summary>
    public String Organization { get; private set; } = String.Empty;

    /// <summary>InfluxDB Bucket（相当于数据库）</summary>
    public String Bucket { get; private set; } = String.Empty;

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

        // 创建 HTTP 客户端
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_dataSource),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {Token}");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/csv");

        _state = ConnectionState.Open;
    }

    /// <summary>关闭连接</summary>
    public override void Close()
    {
        if (_state == ConnectionState.Closed) return;

        _httpClient?.Dispose();
        _httpClient = null;
        _state = ConnectionState.Closed;
    }

    /// <summary>开始事务</summary>
    /// <param name="isolationLevel">隔离级别</param>
    /// <returns></returns>
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        throw new NotSupportedException("InfluxDB does not support transactions.");
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
        _dataSource = builder["Server"] ?? "http://localhost:8086";
        Token = builder["Token"] ?? String.Empty;
        Organization = builder["Organization"] ?? builder["Org"] ?? String.Empty;
        Bucket = builder["Bucket"] ?? builder["Database"] ?? String.Empty;
        _database = Bucket;

        if (String.IsNullOrEmpty(Token))
            throw new ArgumentException("Token is required in connection string.");
        if (String.IsNullOrEmpty(Organization))
            throw new ArgumentException("Organization is required in connection string.");
        if (String.IsNullOrEmpty(Bucket))
            throw new ArgumentException("Bucket is required in connection string.");
    }
    #endregion
}

/// <summary>连接字符串构建器</summary>
public class InfluxDBConnectionStringBuilder : DbConnectionStringBuilder
{
}
