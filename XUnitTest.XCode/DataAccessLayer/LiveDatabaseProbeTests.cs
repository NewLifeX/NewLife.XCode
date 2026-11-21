using System;
using System.Collections.Generic;
using System.ComponentModel;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>真库探测的连接串解析</summary>
public class LiveDatabaseProbeTests
{
    [Fact]
    [DisplayName("真库探测_Server为URL_正确解析主机与端口")]
    public void ParseUrlServerTest()
    {
        var dic = new Dictionary<String, String> { ["Server"] = "http://localhost:9090" };

        Assert.True(LiveDatabase.TryGetEndPoint(dic, 0, out var host, out var port));
        Assert.Equal("localhost", host);
        Assert.Equal(9090, port);
    }

    [Fact]
    [DisplayName("真库探测_Server为主机端口_正确解析")]
    public void ParseHostPortServerTest()
    {
        var dic = new Dictionary<String, String> { ["Server"] = "127.0.0.1:5433" };

        Assert.True(LiveDatabase.TryGetEndPoint(dic, 0, out var host, out var port));
        Assert.Equal("127.0.0.1", host);
        Assert.Equal(5433, port);
    }
}
