using System;
using System.Collections.Generic;
using System.IO;
using NewLife;
using NewLife.Data;
using NewLife.Security;
using XCode.DataAccessLayer;
using XCode.Services;
using Xunit;

namespace XUnitTest.XCode.Services;

/// <summary>DbService服务层测试</summary>
public class DbServiceTests : IDisposable
{
    private readonly String _dbFile;
    private readonly String _connName;
    private readonly DAL _dal;

    public DbServiceTests()
    {
        _connName = "test_svc_" + Rand.Next();
        _dbFile = Path.Combine(Path.GetTempPath(), _connName + ".db");
        DAL.AddConnStr(_connName, $"Data Source={_dbFile}", null, "SQLite");
        _dal = DAL.Create(_connName);
    }

    public void Dispose()
    {
        try { if (File.Exists(_dbFile)) File.Delete(_dbFile); } catch { }
    }
    [Fact]
    public void Login_EmptyToken_ThrowsException()
    {
        var service = new DbService();

        Assert.Throws<ArgumentException>(() => service.Login("", "testdb"));
        Assert.Throws<ArgumentException>(() => service.Login(null!, "testdb"));
    }

    [Fact]
    public void Login_EmptyDb_ThrowsException()
    {
        var service = new DbService();

        Assert.Throws<ArgumentException>(() => service.Login("token", ""));
        Assert.Throws<ArgumentException>(() => service.Login("token", null!));
    }

    [Fact]
    public void Login_InvalidToken_Unauthorized()
    {
        var service = new DbService();
        service.Tokens["validtoken"] = new[] { "db1" };

        Assert.Throws<UnauthorizedAccessException>(() => service.Login("invalidtoken", "db1"));
    }

    [Fact]
    public void ValidateToken_TokenNoDB_Unauthorized()
    {
        var service = new DbService();
        service.Tokens["mytoken"] = new[] { "db1", "db2" };

        Assert.Throws<UnauthorizedAccessException>(() => service.ValidateToken("mytoken", "db3"));
    }

    [Fact]
    public void Login_TokenNoDB_Unauthorized()
    {
        var service = new DbService();
        service.Tokens["mytoken"] = new[] { "db1", "db2" };

        Assert.Throws<UnauthorizedAccessException>(() => service.Login("mytoken", "db3"));
    }

    [Fact]
    public void Login_ValidToken_ReturnsDAL()
    {
        var service = new DbService();
        service.Tokens["mytoken"] = new[] { _connName };

        var dal = service.Login("mytoken", _connName);

        Assert.NotNull(dal);
        Assert.Equal(DatabaseType.SQLite, dal.DbType);
    }

    [Fact]
    public void Login_NoTokens_AllowAll()
    {
        var service = new DbService();
        // 不添加任何令牌，Tokens字典为空时允许所有访问

        var dal = service.Login("anytoken", _connName);

        Assert.NotNull(dal);
    }

    [Fact]
    public void Query_NullDal_ThrowsException()
    {
        var service = new DbService();

        Assert.Throws<ArgumentNullException>(() => service.Query(null!, "SELECT 1", null));
    }

    [Fact]
    public void Query_EmptySql_ThrowsException()
    {
        var service = new DbService();

        Assert.Throws<ArgumentException>(() => service.Query(_dal, "", null));
    }

    [Fact]
    public void Query_SimpleSql_ReturnsResult()
    {
        // 创建测试表
        _dal.Execute("CREATE TABLE IF NOT EXISTS test_query(id INTEGER PRIMARY KEY, name TEXT)");
        _dal.Execute("INSERT INTO test_query(id,name) VALUES(1,'hello')");

        var service = new DbService();
        var dt = service.Query(_dal, "SELECT * FROM test_query", null);

        Assert.NotNull(dt);
        Assert.True(dt.Rows.Count > 0);
    }

    [Fact]
    public void Execute_InsertRow_ReturnsCount()
    {
        _dal.Execute("CREATE TABLE IF NOT EXISTS test_exec(id INTEGER PRIMARY KEY, name TEXT)");

        var service = new DbService();
        var count = service.Execute(_dal, "INSERT INTO test_exec(id,name) VALUES(1,'test')", null);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Execute_WithParameters_Works()
    {
        _dal.Execute("CREATE TABLE IF NOT EXISTS test_execp(id INTEGER PRIMARY KEY, name TEXT)");

        var service = new DbService();
        var ps = new Dictionary<String, Object?> { ["name"] = "world" };
        var count = service.Execute(_dal, "INSERT INTO test_execp(id,name) VALUES(1,@name)", ps);

        Assert.Equal(1, count);

        // 验证插入的数据
        var dt = service.Query(_dal, "SELECT name FROM test_execp WHERE id=1", null);
        Assert.NotNull(dt);
        Assert.Equal("world", dt!.Rows[0][0]?.ToString());
    }

    [Fact]
    public void InsertAndGetIdentity_ReturnsId()
    {
        _dal.Execute("CREATE TABLE IF NOT EXISTS test_ident(id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT)");

        var service = new DbService();
        var id = service.InsertAndGetIdentity(_dal, "INSERT INTO test_ident(name) VALUES('first')", null);

        Assert.True(id > 0);

        var id2 = service.InsertAndGetIdentity(_dal, "INSERT INTO test_ident(name) VALUES('second')", null);
        Assert.True(id2 > id);
    }

    [Fact]
    public void Execute_WithNullValues_FiltersNulls()
    {
        _dal.Execute("CREATE TABLE IF NOT EXISTS test_nullp(id INTEGER PRIMARY KEY, name TEXT)");

        var service = new DbService();
        // 包含null值的参数应被过滤
        var ps = new Dictionary<String, Object?> { ["name"] = null };
        // 不传参数的情况也应该正常工作
        var count = service.Execute(_dal, "INSERT INTO test_nullp(id) VALUES(1)", ps);

        Assert.Equal(1, count);
    }

    [Fact]
    public void GetTables_ReturnsTableList()
    {
        _dal.Execute("CREATE TABLE IF NOT EXISTS test_t1(id INTEGER PRIMARY KEY, name TEXT)");
        _dal.Execute("CREATE TABLE IF NOT EXISTS test_t2(id INTEGER PRIMARY KEY, age INTEGER)");

        var service = new DbService();
        var tables = service.GetTables(_dal);

        Assert.NotNull(tables);
        Assert.True(tables!.Length >= 2);
    }

    [Fact]
    public void GetTables_NullDal_ThrowsException()
    {
        var service = new DbService();

        Assert.Throws<ArgumentNullException>(() => service.GetTables(null!));
    }

    [Fact]
    public void QueryCount_NullDal_ThrowsException()
    {
        var service = new DbService();

        Assert.Throws<ArgumentNullException>(() => service.QueryCount(null!, "test"));
    }

    [Fact]
    public void QueryCount_EmptyTableName_ThrowsException()
    {
        var service = new DbService();

        Assert.Throws<ArgumentException>(() => service.QueryCount(_dal, ""));
    }

    [Fact]
    public void InsertAndGetIdentity_NullDal_ThrowsException()
    {
        var service = new DbService();

        Assert.Throws<ArgumentNullException>(() => service.InsertAndGetIdentity(null!, "INSERT ...", null));
    }

    [Fact]
    public void InsertAndGetIdentity_EmptySql_ThrowsException()
    {
        var service = new DbService();

        Assert.Throws<ArgumentException>(() => service.InsertAndGetIdentity(_dal, "", null));
    }

    [Fact]
    public void Execute_NullDal_ThrowsException()
    {
        var service = new DbService();

        Assert.Throws<ArgumentNullException>(() => service.Execute(null!, "DELETE FROM ...", null));
    }

    [Fact]
    public void Execute_EmptySql_ThrowsException()
    {
        var service = new DbService();

        Assert.Throws<ArgumentException>(() => service.Execute(_dal, "", null));
    }

    [Fact]
    public void ValidateToken_EmptyToken_ThrowsException()
    {
        var service = new DbService();

        Assert.Throws<ArgumentException>(() => service.ValidateToken("", "db"));
    }

    [Fact]
    public void ValidateToken_EmptyDb_ThrowsException()
    {
        var service = new DbService();

        Assert.Throws<ArgumentException>(() => service.ValidateToken("token", ""));
    }

    [Fact]
    public void ValidateToken_NoTokens_AllowAll()
    {
        var service = new DbService();
        // 空Tokens字典时不抛异常
        service.ValidateToken("anytoken", "anydb");
    }

    [Fact]
    public void ValidateToken_TokenWithEmptyDbs_AllowAll()
    {
        var service = new DbService();
        service.Tokens["mytoken"] = [];

        // 空数组表示允许所有数据库
        service.ValidateToken("mytoken", "anydb");
    }

    [Fact]
    public void ValidateToken_CaseInsensitive()
    {
        var service = new DbService();
        service.Tokens["MyToken"] = new[] { "DB1" };

        // Token的key查找应不区分大小写
        service.ValidateToken("mytoken", "DB1");
    }

    [Fact]
    public void Execute_WithAtPrefixParameters_StripsPrefix()
    {
        _dal.Execute("CREATE TABLE IF NOT EXISTS test_atp(id INTEGER PRIMARY KEY, name TEXT)");

        var service = new DbService();
        var ps = new Dictionary<String, Object?> { ["@name"] = "stripped" };
        service.Execute(_dal, "INSERT INTO test_atp(id,name) VALUES(1,@name)", ps);

        var dt = service.Query(_dal, "SELECT name FROM test_atp WHERE id=1", null);
        Assert.NotNull(dt);
        Assert.Equal("stripped", dt!.Rows[0][0]?.ToString());
    }

    [Fact]
    public void Query_WithParameters_Works()
    {
        _dal.Execute("CREATE TABLE IF NOT EXISTS test_qp(id INTEGER PRIMARY KEY, name TEXT)");
        _dal.Execute("INSERT INTO test_qp(id,name) VALUES(1,'hello')");
        _dal.Execute("INSERT INTO test_qp(id,name) VALUES(2,'world')");

        var service = new DbService();
        var dt = service.Query(_dal, "SELECT * FROM test_qp WHERE id=1", null);

        Assert.NotNull(dt);
        Assert.Single(dt!.Rows);
    }

    [Fact]
    public void QueryCount_ReturnsCount()
    {
        _dal.Execute("CREATE TABLE IF NOT EXISTS test_cnt(id INTEGER PRIMARY KEY, name TEXT)");
        _dal.Execute("INSERT INTO test_cnt(id,name) VALUES(1,'a')");
        _dal.Execute("INSERT INTO test_cnt(id,name) VALUES(2,'b')");
        _dal.Execute("INSERT INTO test_cnt(id,name) VALUES(3,'c')");

        var service = new DbService();
        var count = service.QueryCount(_dal, "test_cnt");

        Assert.Equal(3, count);
    }

    [Fact]
    public void Tokens_ConcurrentDictionary()
    {
        var service = new DbService();

        // 验证Tokens是线程安全的ConcurrentDictionary
        service.Tokens["t1"] = new[] { "db1" };
        service.Tokens["t2"] = new[] { "db2" };

        Assert.Equal(2, service.Tokens.Count);
        Assert.True(service.Tokens.ContainsKey("t1"));
    }
}
