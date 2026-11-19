using System;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>MonetDB 数据库测试。无真实服务时仅验证方言与注册</summary>
[Collection("Database")]
public class MonetDBTests
{
    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.MonetDB);
        Assert.NotNull(db);
        Assert.Equal(DatabaseType.MonetDB, db.Type);
    }

    [Fact]
    public void SupportTest()
    {
        var db = DbFactory.Create(DatabaseType.MonetDB);
        Assert.True(db.Support("MonetDB"));
        Assert.True(db.Support("MonetDb.Mapi"));
        Assert.False(db.Support("SqlServer"));
    }

    [Fact]
    public void FormatKeyWordTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.MonetDB)!;

        Assert.Equal("\"TableName\"", db.FormatKeyWord("TableName"));
    }

    [Fact]
    public void FormatValueTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.MonetDB)!;

        var table = DAL.CreateTable();
        var col = table.CreateColumn();
        col.DataType = typeof(Boolean);

        Assert.Equal("TRUE", db.FormatValue(col, true));
        Assert.Equal("FALSE", db.FormatValue(col, false));
    }

    [Fact]
    public void PageSplitTest()
    {
        var db = DbFactory.Create(DatabaseType.MonetDB);

        Assert.Contains("limit 10", db.PageSplit("SELECT * FROM test", 0, 10, null));
        Assert.Contains("limit 10 offset 10", db.PageSplit("SELECT * FROM test", 10, 10, null));
    }

    [Fact]
    public void ConnectionStringTest()
    {
        DAL.AddConnStr("monetdb_test", "Host=localhost;Database=test;User=monetdb;Password=monetdb", null, "MonetDB");
        var dal = DAL.Create("monetdb_test");
        Assert.Equal(DatabaseType.MonetDB, dal.DbType);

        var connStr = dal.Db.ConnectionString;
        Assert.True(connStr.Contains("Port=50000", StringComparison.OrdinalIgnoreCase), connStr);
    }

    [Fact]
    public void IdentityNotSupportedTest()
    {
        var db = DbFactory.Create(DatabaseType.MonetDB);
        db.ConnectionString = "Host=localhost;Database=test";

        var session = db.CreateSession();
        Assert.Throws<NotSupportedException>(() => session.InsertAndGetIdentity("insert into t(a) values(1)"));
    }
}
