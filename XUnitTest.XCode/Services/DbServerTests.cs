using System;
using NewLife.Log;
using XCode.Services;
using Xunit;

namespace XUnitTest.XCode.Services;

/// <summary>DbServer服务器测试</summary>
public class DbServerTests
{
    [Fact]
    public void Ctor_Default()
    {
        var server = new DbServer();

        Assert.Equal("DbServer", server.Name);
        Assert.Equal(3305, server.Port);
        Assert.NotNull(server.Service);
    }

    [Fact]
    public void Ctor_WithService()
    {
        var service = new DbService { Log = XTrace.Log };
        var server = new DbServer(service);

        Assert.Equal("DbServer", server.Name);
        Assert.Equal(3305, server.Port);
        Assert.Same(service, server.Service);
    }

    [Fact]
    public void Ctor_NullService_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(() => new DbServer(null!));
    }

    [Fact]
    public void Log_SetPropagates()
    {
        var server = new DbServer();
        var log = XTrace.Log;

        server.Log = log;

        Assert.Same(log, server.Service.Log);
    }

    [Fact]
    public void Service_TokensConfig()
    {
        var server = new DbServer();
        server.Service.Tokens["token1"] = new[] { "db1", "db2" };
        server.Service.Tokens["token2"] = new[] { "db3" };

        Assert.Equal(2, server.Service.Tokens.Count);
        Assert.Contains("db1", server.Service.Tokens["token1"]);
    }
}
