using System.Data;
using System.Data.Common;
using System.Net.Http;

namespace XCode.InfluxDB;

/// <summary>InfluxDB命令</summary>
public class InfluxDBCommand : DbCommand
{
    #region 属性
    /// <summary>命令文本</summary>
    public override String CommandText { get; set; } = String.Empty;

    /// <summary>命令超时时间</summary>
    public override Int32 CommandTimeout { get; set; } = 30;

    /// <summary>命令类型</summary>
    public override CommandType CommandType { get; set; } = CommandType.Text;

    /// <summary>是否已设计时更新</summary>
    public override Boolean DesignTimeVisible { get; set; }

    /// <summary>更新行为来源</summary>
    public override UpdateRowSource UpdatedRowSource { get; set; }

    /// <summary>连接</summary>
    protected override DbConnection? DbConnection { get; set; }

    /// <summary>参数集合</summary>
    protected override DbParameterCollection DbParameterCollection { get; } = new InfluxDBParameterCollection();

    /// <summary>事务</summary>
    protected override DbTransaction? DbTransaction { get; set; }

    /// <summary>连接（强类型）</summary>
    public new InfluxDBConnection? Connection
    {
        get => DbConnection as InfluxDBConnection;
        set => DbConnection = value;
    }
    #endregion

    #region 方法
    /// <summary>取消命令</summary>
    public override void Cancel() { }

    /// <summary>执行非查询</summary>
    /// <returns></returns>
    public override Int32 ExecuteNonQuery()
    {
        var conn = Connection;
        if (conn == null || conn.State != ConnectionState.Open)
            throw new InvalidOperationException("Connection must be open.");

        // InfluxDB 写入操作使用 Line Protocol
        var httpClient = conn.HttpClient;
        if (httpClient == null)
            throw new InvalidOperationException("HttpClient is not initialized.");

        var url = $"/api/v2/write?org={conn.Organization}&bucket={conn.Bucket}&precision=ns";
        var content = new StringContent(CommandText, System.Text.Encoding.UTF8, "text/plain");

        var response = httpClient.PostAsync(url, content).Result;
        if (!response.IsSuccessStatusCode)
        {
            var error = response.Content.ReadAsStringAsync().Result;
            throw new Exception($"InfluxDB write failed: {error}");
        }

        return 1; // 假设写入成功
    }

    /// <summary>执行查询</summary>
    /// <returns></returns>
    public override Object? ExecuteScalar()
    {
        using var reader = ExecuteReader();
        if (reader.Read())
            return reader.GetValue(0);
        return null;
    }

    /// <summary>准备命令</summary>
    public override void Prepare() { }

    /// <summary>创建参数</summary>
    /// <returns></returns>
    protected override DbParameter CreateDbParameter() => new InfluxDBParameter();

    /// <summary>执行读取器</summary>
    /// <param name="behavior">行为</param>
    /// <returns></returns>
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        var conn = Connection;
        if (conn == null || conn.State != ConnectionState.Open)
            throw new InvalidOperationException("Connection must be open.");

        var httpClient = conn.HttpClient;
        if (httpClient == null)
            throw new InvalidOperationException("HttpClient is not initialized.");

        // InfluxDB 查询使用 Flux 语言
        var url = $"/api/v2/query?org={conn.Organization}";
        var fluxQuery = CommandText;

        // 确保查询中包含 bucket 信息
        if (!fluxQuery.Contains("from(bucket:"))
        {
            fluxQuery = $"from(bucket:\"{conn.Bucket}\") |> {fluxQuery}";
        }

        var content = new StringContent(fluxQuery, System.Text.Encoding.UTF8, "application/vnd.flux");

        var response = httpClient.PostAsync(url, content).Result;
        if (!response.IsSuccessStatusCode)
        {
            var error = response.Content.ReadAsStringAsync().Result;
            throw new Exception($"InfluxDB query failed: {error}");
        }

        var csv = response.Content.ReadAsStringAsync().Result;
        return new InfluxDBDataReader(csv);
    }
    #endregion
}
