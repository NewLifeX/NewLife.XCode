using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using NewLife;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>真库用例特性。未注入环境变量且服务探测不可达时自动跳过</summary>
/// <remarks>
/// CI 通过环境变量 XCode_&lt;key&gt; 注入连接串即视为服务可用，不做探测，防止探测误差掩盖真实失败。
/// 本地缺少数据库时读取 Config/&lt;configName&gt;.config，按 Server/Port 探测，不通则跳过。
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class LiveDatabaseFactAttribute : FactAttribute
{
    private static readonly Dictionary<String, Lazy<String?>> _probes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>数据库键。对应环境变量 XCode_&lt;key&gt;</summary>
    public String Key { get; }

    /// <summary>配置文件名称。对应 Config/&lt;configName&gt;.config</summary>
    public String ConfigName { get; }

    /// <summary>服务默认端口，用于本地探测</summary>
    public Int32 DefaultPort { get; }

    /// <summary>HTTP 探测路径。非空时按 HTTP 响应确认服务身份，避免端口被其它服务占用时误判</summary>
    public String? HttpPath { get; }

    /// <summary>HTTP 探测要求响应正文包含的关键字</summary>
    public String? HttpKeyword { get; }

    /// <summary>实例化</summary>
    /// <param name="key">数据库键，如 clickhouse</param>
    /// <param name="configName">配置文件名称，默认与 key 相同</param>
    /// <param name="defaultPort">服务默认端口，0 表示无法探测</param>
    /// <param name="httpPath">HTTP 探测路径，如 Presto/Trino 的 /v1/info</param>
    /// <param name="httpKeyword">HTTP 探测要求响应正文包含的关键字，可为空</param>
    public LiveDatabaseFactAttribute(String key, String? configName = null, Int32 defaultPort = 0, String? httpPath = null, String? httpKeyword = null)
    {
        Key = key;
        ConfigName = configName.IsNullOrEmpty() ? key : configName;
        DefaultPort = defaultPort;
        HttpPath = httpPath;
        HttpKeyword = httpKeyword;

        var skip = GetProbe(key, ConfigName, defaultPort, httpPath, httpKeyword).Value;
        if (!skip.IsNullOrEmpty()) Skip = skip;
    }

    private static Lazy<String?> GetProbe(String key, String configName, Int32 defaultPort, String? httpPath, String? httpKeyword)
    {
        // 缓存键包含探测参数：同一数据库键可能按不同端口或 HTTP 路径探测
        var cacheKey = $"{key}|{configName}|{defaultPort}|{httpPath}|{httpKeyword}";
        lock (_probes)
        {
            if (!_probes.TryGetValue(cacheKey, out var lazy))
            {
                lazy = new Lazy<String?>(() => Probe(key, configName, defaultPort, httpPath, httpKeyword), true);
                _probes[cacheKey] = lazy;
            }

            return lazy;
        }
    }

    private static String? Probe(String key, String configName, Int32 defaultPort, String? httpPath, String? httpKeyword)
    {
        // CI 显式给出连接串时，直接执行，由用例本身暴露真实问题
        if (!Environment.GetEnvironmentVariable($"XCode_{key}").IsNullOrEmpty()) return null;

        try
        {
            var connStr = LiveDatabase.ReadConnStr(configName);
            if (connStr.IsNullOrEmpty()) return $"未配置 {key} 连接串，跳过真库用例；可通过环境变量 XCode_{key} 或 Config/{configName}.config 指定";

            // URI 形式（如 mongodb://host:port/db）优先按 Uri 解析
            if (!LiveDatabase.TryGetUriEndPoint(connStr, defaultPort, out var host, out var port))
            {
                var dic = connStr.SplitAsDictionary("=", ";", true);
                if (!LiveDatabase.TryGetEndPoint(dic, defaultPort, out host, out port))
                    return $"{key} 连接串缺少 Server，跳过真库用例";
            }

            var online = httpPath.IsNullOrEmpty()
                ? LiveDatabase.CanConnect(host, port)
                : LiveDatabase.CanConnectHttp(host, port, httpPath!, httpKeyword);
            if (!online)
                return $"{key} 服务不可达（{host}:{port}），跳过真库用例；可通过环境变量 XCode_{key} 指定连接串";

            return null;
        }
        catch (Exception ex)
        {
            return $"{key} 服务探测失败（{ex.Message}），跳过真库用例";
        }
    }
}

/// <summary>真库连接串解析与探测</summary>
/// <remarks>
/// 解析顺序：环境变量 XCode_&lt;key&gt; → Config/&lt;configName&gt;.config → 默认值。
/// 配置文件缺失时写出默认值，便于本地开发直接修改。
/// </remarks>
public static class LiveDatabase
{
    /// <summary>解析连接串</summary>
    /// <param name="key">数据库键，对应环境变量 XCode_&lt;key&gt;</param>
    /// <param name="configName">配置文件名称，位于 Config/&lt;configName&gt;.config</param>
    /// <param name="defaultConnStr">默认连接串</param>
    /// <returns></returns>
    public static String Resolve(String key, String configName, String defaultConnStr)
    {
        // 优先使用环境变量（CI环境）
        var envConnStr = Environment.GetEnvironmentVariable($"XCode_{key}");
        if (!envConnStr.IsNullOrEmpty()) return envConnStr;

        var file = Path.Combine("Config", $"{configName}.config").GetFullPath();
        if (File.Exists(file))
        {
            var text = File.ReadAllText(file);
            if (!text.IsNullOrWhiteSpace()) return text.Trim();
        }
        else
        {
            File.WriteAllText(file.EnsureDirectory(true), defaultConnStr);
        }

        return defaultConnStr;
    }

    /// <summary>读取配置文件中的连接串，不存在时返回空</summary>
    /// <param name="configName">配置文件名称</param>
    /// <returns></returns>
    public static String? ReadConnStr(String configName)
    {
        var file = Path.Combine("Config", $"{configName}.config").GetFullPath();
        if (!File.Exists(file)) return null;

        var text = File.ReadAllText(file);

        return text.IsNullOrWhiteSpace() ? null : text.Trim();
    }

    /// <summary>从连接串字典解析服务地址与端口</summary>
    /// <param name="dic">连接串键值对</param>
    /// <param name="defaultPort">默认端口</param>
    /// <param name="host">服务地址</param>
    /// <param name="port">端口</param>
    /// <returns></returns>
    public static Boolean TryGetEndPoint(IDictionary<String, String> dic, Int32 defaultPort, out String host, out Int32 port)
    {
        host = "";
        port = defaultPort;

        if (dic.TryGetValue("Server", out var value) && !value.IsNullOrEmpty()) host = value;
        if (host.IsNullOrEmpty() && dic.TryGetValue("Host", out value) && !value.IsNullOrEmpty()) host = value;
        if (host.IsNullOrEmpty() && dic.TryGetValue("Data Source", out value) && !value.IsNullOrEmpty()) host = value;
        if (host.IsNullOrEmpty() && dic.TryGetValue("DataSource", out value) && !value.IsNullOrEmpty()) host = value;

        // Server 可能带端口，如 127.0.0.1:5433，也可能是完整 URL，如 http://localhost:9090
        if (!host.IsNullOrEmpty())
        {
            if (host.Contains("://") && Uri.TryCreate(host, UriKind.Absolute, out var uri) && !uri.Host.IsNullOrEmpty())
            {
                host = uri.Host;
                if (!uri.IsDefaultPort) port = uri.Port;
            }
            else
            {
                var p = host.IndexOf(':');
                if (p > 0 && Int32.TryParse(host[(p + 1)..], out var n))
                {
                    host = host[..p];
                    port = n;
                }
            }
        }

        if (dic.TryGetValue("Port", out var str) && !str.IsNullOrEmpty()) port = str.Trim().ToInt(defaultPort);

        return !host.IsNullOrEmpty() && port is > 0 and <= 65535;
    }

    /// <summary>从 URI 形式的连接串解析服务地址与端口，如 mongodb://127.0.0.1:27017/test</summary>
    /// <param name="connStr">连接串</param>
    /// <param name="defaultPort">默认端口</param>
    /// <param name="host">服务地址</param>
    /// <param name="port">端口</param>
    /// <returns></returns>
    public static Boolean TryGetUriEndPoint(String connStr, Int32 defaultPort, out String host, out Int32 port)
    {
        host = "";
        port = defaultPort;
        if (connStr.IsNullOrEmpty() || !connStr.Contains("://")) return false;
        if (!Uri.TryCreate(connStr, UriKind.Absolute, out var uri) || uri.Host.IsNullOrEmpty()) return false;

        host = uri.Host;
        if (!uri.IsDefaultPort) port = uri.Port;

        return port is > 0 and <= 65535;
    }

    /// <summary>探测服务端口是否可达</summary>
    /// <param name="host">服务地址</param>
    /// <param name="port">端口</param>
    /// <returns></returns>
    public static Boolean CanConnect(String host, Int32 port)
    {
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync(host, port).Wait(2000) && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>探测 HTTP 服务是否为目标数据库。端口可能被其它服务占用，需要按响应确认身份</summary>
    /// <param name="host">服务地址</param>
    /// <param name="port">服务端口</param>
    /// <param name="path">探测路径，如 Presto/Trino 的 /v1/info</param>
    /// <param name="keyword">响应正文必须包含的关键字，为空表示只要求响应成功</param>
    /// <returns></returns>
    public static Boolean CanConnectHttp(String host, Int32 port, String path, String? keyword)
    {
        if (!CanConnect(host, port)) return false;

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var response = client.GetAsync(new UriBuilder("http", host, port, path).Uri).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode) return false;
            if (keyword.IsNullOrEmpty()) return true;

            var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return text.Contains(keyword!, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
