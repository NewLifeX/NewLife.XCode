using System;
using System.Collections.Generic;
using System.IO;
using NewLife;
using NewLife.Log;
using NewLife.Security;
using NewLife.Serialization;
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

        var result = controller.Login("", "mytoken");

        Assert.NotNull(result);
        // 匿名对象检查
        var json = result.ToJson();
        Assert.Contains("401", json);
    }

    [Fact]
    public void Login_EmptyToken_ReturnsError()
    {
        var controller = new DbController { Service = CreateService() };

        var result = controller.Login("testdb", "");

        Assert.NotNull(result);
        var json = result.ToJson();
        Assert.Contains("401", json);
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

        var result = controller.Query("SELECT 1", null, null, null);

        Assert.NotNull(result);
        var json = result.ToJson();
        Assert.Contains("401", json);
    }

    [Fact]
    public void Query_AfterLogin_Works()
    {
        var service = CreateService();
        var controller = new DbController { Service = service };

        // 先登录
        var token = "testtoken";
        controller.Login(_connName, token);

        // 创建测试表
        _dal.Execute("CREATE TABLE IF NOT EXISTS ctrl_test(id INTEGER PRIMARY KEY, name TEXT)");
        _dal.Execute("INSERT INTO ctrl_test(id,name) VALUES(1,'test')");

        // 查询
        var result = controller.Query("SELECT * FROM ctrl_test", null, token, _connName);

        Assert.NotNull(result);
        Assert.IsType<String>(result);
        var json = (String)result!;
        Assert.Contains("test", json);
    }

    [Fact]
    public void Execute_AfterLogin_Works()
    {
        var service = CreateService();
        var controller = new DbController { Service = service };

        var token = "testtoken";
        controller.Login(_connName, token);

        _dal.Execute("CREATE TABLE IF NOT EXISTS ctrl_exec(id INTEGER PRIMARY KEY, name TEXT)");

        var result = controller.Execute("INSERT INTO ctrl_exec(id,name) VALUES(1,'hello')", null, token, _connName);

        Assert.NotNull(result);
        Assert.Equal(1L, Convert.ToInt64(result));
    }

    [Fact]
    public void InsertAndGetIdentity_AfterLogin_Works()
    {
        var service = CreateService();
        var controller = new DbController { Service = service };

        var token = "testtoken";
        controller.Login(_connName, token);

        _dal.Execute("CREATE TABLE IF NOT EXISTS ctrl_ident(id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT)");

        var result = controller.InsertAndGetIdentity("INSERT INTO ctrl_ident(name) VALUES('first')", null, token, _connName);

        Assert.NotNull(result);
        var id = Convert.ToInt64(result);
        Assert.True(id > 0);
    }

    [Fact]
    public void QueryCount_AfterLogin_Works()
    {
        var service = CreateService();
        var controller = new DbController { Service = service };

        var token = "testtoken";
        controller.Login(_connName, token);

        _dal.Execute("CREATE TABLE IF NOT EXISTS ctrl_count(id INTEGER PRIMARY KEY, name TEXT)");
        _dal.Execute("INSERT INTO ctrl_count(id,name) VALUES(1,'a')");
        _dal.Execute("INSERT INTO ctrl_count(id,name) VALUES(2,'b')");

        var result = controller.QueryCount("ctrl_count", token, _connName);

        Assert.NotNull(result);
        var count = Convert.ToInt64(result);
        Assert.True(count >= 2);
    }

    [Fact]
    public void GetTables_AfterLogin_Works()
    {
        var service = CreateService();
        var controller = new DbController { Service = service };

        var token = "testtoken";
        controller.Login(_connName, token);

        _dal.Execute("CREATE TABLE IF NOT EXISTS ctrl_t1(id INTEGER PRIMARY KEY, name TEXT)");

        var result = controller.GetTables(token, _connName);

        Assert.NotNull(result);
        Assert.IsType<String>(result);
    }
}
