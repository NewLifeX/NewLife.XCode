using System.Data;
using System.Data.Common;
using System.Net.Http;

namespace XCode.InfluxDB;

#pragma warning disable CS8765 // 基类 setter 允许 null，自动属性无法分别标注 getter/setter 可空性

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
            throw new InvalidOperationException(L.T("Connection must be open."));

        // InfluxDB 写入操作使用 Line Protocol
        var httpClient = conn.HttpClient;
        if (httpClient == null)
            throw new InvalidOperationException(L.T("HttpClient is not initialized."));

        var url = conn.GetWriteUrl();

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        InfluxDBConnection.AddAuth(request, conn.Token, conn.User, conn.Password);
        request.Content = new StringContent(CommandText, System.Text.Encoding.UTF8, "text/plain");

        var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            var error = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            throw new Exception(L.T("InfluxDB write failed: {0}", error));
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
            throw new InvalidOperationException(L.T("Connection must be open."));

        var httpClient = conn.HttpClient;
        if (httpClient == null)
            throw new InvalidOperationException(L.T("HttpClient is not initialized."));

        conn.EnsureVersion();

        var query = CommandText;
        var language = conn.QueryLanguage;
        if (language == InfluxQueryLanguage.Auto) language = InfluxDBConnection.GetDefaultQueryLanguage(conn.Version);

        if (language == InfluxQueryLanguage.Flux)
        {
            if (conn.Version is 1 or 3) throw new NotSupportedException(L.T("{0} requires InfluxDB {1}.", "Flux queries", "2.x"));

            // 确保查询中包含 bucket 信息；以 import 开头的 Flux 脚本自带数据源，不能拼接到管道前面
            if (!query.Contains("from(bucket:") && !query.TrimStart().StartsWithIgnoreCase("import "))
            {
                query = $"from(bucket:\"{conn.Bucket}\") |> {query}";
            }

            var url = conn.GetQueryUrl(InfluxQueryLanguage.Flux, query);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            InfluxDBConnection.AddAuth(request, conn.Token, conn.User, conn.Password);
            request.Headers.Add("Accept", "application/csv");
            request.Content = new StringContent(query, System.Text.Encoding.UTF8, "application/vnd.flux");

            var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                var error = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                throw new Exception(L.T("InfluxDB query failed: {0}", error));
            }

            var csv = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return new InfluxDBDataReader(csv);
        }

        var table = conn.Query(query, language);
        return table.CreateDataReader();
    }
    #endregion
}

#pragma warning restore CS8765
