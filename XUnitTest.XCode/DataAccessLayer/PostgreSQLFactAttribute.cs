using System;
using System.Collections.Generic;
using System.Net.Sockets;
using NewLife;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>PostgreSQL 真库用例特性。服务不可达时自动跳过，避免缺少数据库的环境（如本地开发机）出现红灯</summary>
/// <remarks>
/// xUnit 2.x 不支持运行时跳过，只能在用例发现阶段设置 Skip。
/// CI 通过环境变量 XCode_pgsql 注入连接串即视为服务可用，不做探测，防止探测误差掩盖真实失败。
/// </remarks>
public sealed class PostgreSQLFactAttribute : FactAttribute
{
    private static readonly Lazy<String> _skip = new(Probe, true);

    public PostgreSQLFactAttribute()
    {
        var skip = _skip.Value;
        if (!skip.IsNullOrEmpty()) Skip = skip;
    }

    private static String Probe()
    {
        // CI 显式给出连接串时，直接执行，由用例本身暴露真实问题
        if (!Environment.GetEnvironmentVariable("XCode_pgsql").IsNullOrEmpty()) return null;

        try
        {
            var connStr = PostgreSQLTests.ResolveConnectionString();
            if (connStr.IsNullOrEmpty()) return "未配置 PostgreSQL 连接串，跳过真库用例";

            var dic = connStr.SplitAsDictionary("=", ";", true);
            if (!TryGetEndPoint(dic, out var host, out var port))
                return "PostgreSQL 连接串缺少 Server，跳过真库用例";

            if (!CanConnect(host, port))
                return $"PostgreSQL 服务不可达（{host}:{port}），跳过真库用例；可通过环境变量 XCode_pgsql 指定连接串";

            return null;
        }
        catch (Exception ex)
        {
            return $"PostgreSQL 服务探测失败（{ex.Message}），跳过真库用例";
        }
    }

    private static Boolean TryGetEndPoint(IDictionary<String, String> dic, out String host, out Int32 port)
    {
        host = "";
        port = 5432;

        if (dic.TryGetValue("Server", out var value) && !value.IsNullOrEmpty()) host = value.Trim();
        if (host.IsNullOrEmpty() && dic.TryGetValue("Host", out value) && !value.IsNullOrEmpty()) host = value.Trim();
        if (host.IsNullOrEmpty() && dic.TryGetValue("Data Source", out value) && !value.IsNullOrEmpty()) host = value.Trim();

        if (dic.TryGetValue("Port", out var str) && !str.IsNullOrEmpty()) port = str.Trim().ToInt(5432);

        return !host.IsNullOrEmpty() && port is > 0 and <= 65535;
    }

    private static Boolean CanConnect(String host, Int32 port)
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
}
