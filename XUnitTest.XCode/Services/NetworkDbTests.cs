using System;
using NewLife.Security;
using XCode.DataAccessLayer;
using Xunit;

using LoginInfo = XCode.Services.LoginInfo;
using DbRequest = XCode.Services.DbRequest;

namespace XUnitTest.XCode.Services;

/// <summary>NetworkDb网络虚拟数据库测试</summary>
public class NetworkDbTests
{
    [Fact]
    public void DatabaseType_HasNetwork()
    {
        Assert.Equal(100, (Int32)DatabaseType.Network);
    }

    [Fact]
    public void DbFactory_CreateNetwork()
    {
        var db = DbFactory.Create(DatabaseType.Network);

        Assert.NotNull(db);
        Assert.Equal(DatabaseType.Network, db.Type);
    }

    [Fact]
    public void Support_ProviderName()
    {
        var db = DbFactory.Create(DatabaseType.Network);

        Assert.True(db.Support("Network"));
        Assert.True(db.Support("network"));
        Assert.True(db.Support("net"));
    }

    [Fact]
    public void ConnectionString_Parse()
    {
        var connStr = "Server=http://127.0.0.1:3305;Database=Membership;Password=token123;Provider=Network";

        // 通过DAL.AddConnStr注册网络数据库连接
        var connName = "test_network_parse_" + Rand.Next();
        DAL.AddConnStr(connName, connStr, null, "Network");

        var dal = DAL.Create(connName);

        Assert.NotNull(dal);
        Assert.Equal(DatabaseType.Network, dal.DbType);
    }

    [Fact]
    public void DAL_Register_Network()
    {
        var connName = "test_network_reg_" + Rand.Next();
        DAL.AddConnStr(connName, "Server=http://localhost:3305;Database=testdb;Password=pwd;Provider=Network", null, "Network");

        var dal = DAL.Create(connName);

        Assert.NotNull(dal);
        Assert.Equal(DatabaseType.Network, dal.DbType);
        Assert.NotNull(dal.Db);
    }

    [Fact]
    public void LoginInfo_Properties()
    {
        var info = new LoginInfo
        {
            DbType = DatabaseType.SQLite,
            Version = "3.39.0"
        };

        Assert.Equal(DatabaseType.SQLite, info.DbType);
        Assert.Equal("3.39.0", info.Version);
    }

    [Fact]
    public void DbRequest_Properties()
    {
        var req = new DbRequest
        {
            Sql = "SELECT 1",
            Parameters = new System.Collections.Generic.Dictionary<String, Object?> { ["p1"] = 123 }
        };

        Assert.Equal("SELECT 1", req.Sql);
        Assert.NotNull(req.Parameters);
        Assert.Equal(123, req.Parameters!["p1"]);
    }

    [Fact]
    public void PageSplit_Default()
    {
        var db = DbFactory.Create(DatabaseType.Network);

        // 无服务端数据库实例时，使用默认LIMIT分页
        var sql = db.PageSplit("SELECT * FROM test", 0, 10, null);

        Assert.Contains("limit", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatParameterName_Default()
    {
        var db = DbFactory.Create(DatabaseType.Network);

        var name = db.FormatParameterName("id");

        Assert.Equal("@id", name);
    }
}
