using System;
using System.Collections.Generic;
using System.IO;
using NewLife;
using NewLife.Data;
using NewLife.Serialization;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Security;
using XCode.DataAccessLayer;
using XCode.Services;
using Xunit;

namespace XUnitTest.XCode.Services;

/// <summary>DbController控制器测试</summary>
public class DbControllerTests : IDisposable
{
    private readonly String _dbFile;
    private readonly String _connName;
    private readonly DAL _dal;

    public DbControllerTests()
    {
        _connName = "test_ctrl_" + Rand.Next();
        _dbFile = Path.Combine(Path.GetTempPath(), _connName + ".db");
        DAL.AddConnStr(_connName, $"Data Source={_dbFile}", null, "SQLite");
        _dal = DAL.Create(_connName);
    }

    public void Dispose()
    {
        try { if (File.Exists(_dbFile)) File.Delete(_dbFile); } catch { }
    }

    private DbService CreateService()
    {
        var service = new DbService { Log = XTrace.Log };
        return service;
    }

    [Fact]
    public void Login_EmptyDb_ReturnsError()
    {
        var controller = new DbController { Service = CreateService() };

        var ex = Assert.Throws<ApiException>(() => controller.Login("", "mytoken"));

        Assert.Equal(ApiCode.BadRequest, ex.Code);
    }

    [Fact]
    public void Login_EmptyToken_ReturnsError()
    {
        var controller = new DbController { Service = CreateService() };

        var ex = Assert.Throws<ApiException>(() => controller.Login("testdb", ""));

        Assert.Equal(ApiCode.BadRequest, ex.Code);
    }

    [Fact]
    public void Login_Valid_ReturnsLoginInfo()
    {
        var controller = new DbController { Service = CreateService() };

        var result = controller.Login(_connName, "anytoken");

        Assert.IsType<LoginInfo>(result);
        var info = (LoginInfo)result;
        Assert.Equal(DatabaseType.SQLite, info.DbType);
    }

    [Fact]
    public void Query_NotLoggedIn_ReturnsError()
    {
        var controller = new DbController { Service = CreateService() };

        var ex = Assert.Throws<ApiException>(() => controller.Query("SELECT 1", null, null, _connName));

        Assert.Equal(ApiCode.Unauthorized, ex.Code);
    }

    [Fact]
    public void Query_AfterLogin_Works()
    {
        var service = CreateService();
        var controller = new DbController { Service = service };

        var token = "testtoken";
        service.Tokens[token] = new[] { _connName };

        // 创建测试表
        _dal.Execute("CREATE TABLE IF NOT EXISTS ctrl_test(id INTEGER PRIMARY KEY, name TEXT)");
        _dal.Execute("INSERT INTO ctrl_test(id,name) VALUES(1,'test')");

        // 查询
        var result = controller.Query("SELECT * FROM ctrl_test", null, token, _connName);

        Assert.NotNull(result);
        var packet = Assert.IsAssignableFrom<IPacket>(result);

        var dt = new DbTable();
        dt.Read(packet);
        Assert.Equal("test", dt.Rows[0][1]?.ToString());
    }

    [Fact]
    public void Execute_AfterLogin_Works()
    {
        var service = CreateService();
        var controller = new DbController { Service = service };

        var token = "testtoken";
        service.Tokens[token] = new[] { _connName };

        _dal.Execute("CREATE TABLE IF NOT EXISTS ctrl_exec(id INTEGER PRIMARY KEY, name TEXT)");

        var result = controller.Execute("INSERT INTO ctrl_exec(id,name) VALUES(1,'hello')", null, token, _connName);

        var json = result.ToJson();
        Assert.Contains("\"data\":1", json);
    }

    [Fact]
    public void InsertAndGetIdentity_AfterLogin_Works()
    {
        var service = CreateService();
        var controller = new DbController { Service = service };

        var token = "testtoken";
        service.Tokens[token] = new[] { _connName };

        _dal.Execute("CREATE TABLE IF NOT EXISTS ctrl_ident(id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT)");

        var result = controller.InsertAndGetIdentity("INSERT INTO ctrl_ident(name) VALUES('first')", null, token, _connName);

        var json = result.ToJson();
        Assert.Contains("\"data\":", json);
    }

    [Fact]
    public void QueryCount_AfterLogin_Works()
    {
        var service = CreateService();
        var controller = new DbController { Service = service };

        var token = "testtoken";
        service.Tokens[token] = new[] { _connName };

        _dal.Execute("CREATE TABLE IF NOT EXISTS ctrl_count(id INTEGER PRIMARY KEY, name TEXT)");
        _dal.Execute("INSERT INTO ctrl_count(id,name) VALUES(1,'a')");
        _dal.Execute("INSERT INTO ctrl_count(id,name) VALUES(2,'b')");

        var result = controller.QueryCount("ctrl_count", token, _connName);

        var json = result.ToJson();
        Assert.Contains("\"data\":", json);
    }

    [Fact]
    public void GetTables_AfterLogin_Works()
    {
        var service = CreateService();
        var controller = new DbController { Service = service };

        var token = "testtoken";
        service.Tokens[token] = new[] { _connName };

        _dal.Execute("CREATE TABLE IF NOT EXISTS ctrl_t1(id INTEGER PRIMARY KEY, name TEXT)");

        var result = controller.GetTables(token, _connName);

        Assert.NotNull(result);
        Assert.IsType<IDataTable[]>(result);
        Assert.True(result.Length >= 1);
    }

    [Fact]
    public void Query_TokenCannotAccessDb_Unauthorized()
    {
        var service = CreateService();
        var controller = new DbController { Service = service };

        service.Tokens["testtoken"] = new[] { "db1" };

        var ex = Assert.Throws<ApiException>(() => controller.Query("SELECT 1", null, "testtoken", _connName));

        Assert.Equal(ApiCode.Unauthorized, ex.Code);
    }

    [Fact]
    public void Query_ValidationCached_WorksWithinCachePeriod()
    {
        var service = CreateService();
        var controller = new DbController { Service = service };

        var token = "cachetoken";
        service.Tokens[token] = new[] { _connName };

        var first = controller.Query("SELECT 1", null, token, _connName);
        Assert.NotNull(first);

        service.Tokens.Clear();

        var second = controller.Query("SELECT 1", null, token, _connName);
        Assert.NotNull(second);
    }
}
