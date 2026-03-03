using System;
using System.Collections.Generic;
using NewLife;
using XCode.Services;
using Xunit;

namespace XUnitTest.XCode.Services;

/// <summary>DbClient客户端测试</summary>
public class DbClientTests
{
    [Fact]
    public void Ctor_Default()
    {
        using var client = new DbClient();

        Assert.Null(client.Server);
        Assert.Null(client.Db);
        Assert.Null(client.Token);
        Assert.Null(client.Client);
        Assert.False(client.Logined);
    }

    [Fact]
    public void Ctor_WithParams()
    {
        using var client = new DbClient("http://127.0.0.1:3305", "Membership", "mytoken");

        Assert.Equal("http://127.0.0.1:3305", client.Server);
        Assert.Equal("Membership", client.Db);
        Assert.Equal("mytoken", client.Token);
        Assert.Null(client.Client);
        Assert.False(client.Logined);
    }

    [Fact]
    public void Open_CreatesClient()
    {
        using var client = new DbClient("http://127.0.0.1:3305", "Membership", "mytoken");

        client.Open();

        Assert.NotNull(client.Client);
        Assert.Equal("mytoken", client.Client!.Token);
    }

    [Fact]
    public void Open_NoServer_ThrowsException()
    {
        using var client = new DbClient();

        Assert.Throws<InvalidOperationException>(() => client.Open());
    }

    [Fact]
    public void Open_MultipleCalls_SameClient()
    {
        using var client = new DbClient("http://127.0.0.1:3305", "Membership");

        client.Open();
        var c1 = client.Client;

        client.Open();
        var c2 = client.Client;

        Assert.Same(c1, c2);
    }

    [Fact]
    public void Dispose_ClearsClient()
    {
        var client = new DbClient("http://127.0.0.1:3305", "Membership");
        client.Open();
        Assert.NotNull(client.Client);

        client.Dispose();
        Assert.Null(client.Client);
    }

    [Fact]
    public void Open_NoToken_ClientTokenEmpty()
    {
        using var client = new DbClient("http://127.0.0.1:3305", "Membership");

        client.Open();

        Assert.NotNull(client.Client);
        Assert.True(client.Client!.Token.IsNullOrEmpty());
    }
}
