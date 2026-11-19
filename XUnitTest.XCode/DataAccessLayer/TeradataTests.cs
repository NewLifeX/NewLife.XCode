using System;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>Teradata 数据库测试。无真实服务时仅验证方言与注册</summary>
[Collection("Database")]
public class TeradataTests
{
    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.Teradata);
        Assert.NotNull(db);
        Assert.Equal(DatabaseType.Teradata, db.Type);
    }

    [Fact]
    public void SupportTest()
    {
        var db = DbFactory.Create(DatabaseType.Teradata);
        Assert.True(db.Support("Teradata"));
        Assert.True(db.Support("Teradata.Client.Provider"));
        Assert.False(db.Support("SqlServer"));
    }

    [Fact]
    public void FormatKeyWordTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.Teradata)!;

        Assert.Equal("\"TableName\"", db.FormatKeyWord("TableName"));
    }

    [Fact]
    public void PageSplitTest()
    {
        var db = DbFactory.Create(DatabaseType.Teradata);

        var first = db.PageSplit("SELECT * FROM test", 0, 10, null);
        Assert.Contains("Top 10", first);

        var second = db.PageSplit("SELECT * FROM test ORDER BY Id", 10, 10, "Id");
        Assert.Contains("ROW_NUMBER()", second);
        Assert.Contains("BETWEEN 11 AND 20", second);
        Assert.Contains("FROM (SELECT * FROM test) T0", second);
    }

    [Fact]
    public void StringConcatTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.Teradata)!;

        Assert.Equal("a||b", db.StringConcat("a", "b"));
    }

    [Fact]
    public void ConnectionStringTest()
    {
        DAL.AddConnStr("teradata_test", "Data Source=localhost;User ID=u;Password=p;Database=test", null, "Teradata");
        var dal = DAL.Create("teradata_test");
        Assert.Equal(DatabaseType.Teradata, dal.DbType);
        Assert.Equal("test", dal.Db.DatabaseName);
    }

    [Fact]
    public void IdentityNotSupportedTest()
    {
        var db = DbFactory.Create(DatabaseType.Teradata);
        db.ConnectionString = "Data Source=localhost;Database=test";

        var session = db.CreateSession();
        Assert.Throws<NotSupportedException>(() => session.InsertAndGetIdentity("insert into t(a) values(1)"));
    }
}
