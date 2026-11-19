using System;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>YashanDB(崖山) 数据库测试。无真实服务时仅验证方言与注册</summary>
[Collection("Database")]
public class YashanDBTests
{
    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.YashanDB);
        Assert.NotNull(db);
        Assert.Equal(DatabaseType.YashanDB, db.Type);
    }

    [Fact]
    public void SupportTest()
    {
        var db = DbFactory.Create(DatabaseType.YashanDB);
        Assert.True(db.Support("YashanDB"));
        Assert.True(db.Support("Yasdb"));
        Assert.False(db.Support("SqlServer"));
    }

    [Fact]
    public void FormatKeyWordTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.YashanDB)!;

        Assert.Equal("\"TableName\"", db.FormatKeyWord("TableName"));
    }

    [Fact]
    public void FormatDateTimeTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.YashanDB)!;

        Assert.Equal("TIMESTAMP '2024-01-02 03:04:05'", db.FormatDateTime(new DateTime(2024, 1, 2, 3, 4, 5)));
    }

    [Fact]
    public void PageSplitTest()
    {
        var db = DbFactory.Create(DatabaseType.YashanDB);

        var first = db.PageSplit("SELECT * FROM test", 0, 10, null);
        Assert.Contains("rownum<=10", first);

        var second = db.PageSplit("SELECT * FROM test", 10, 10, null);
        Assert.Contains("rowNumber>10", second);
        Assert.Contains("rowNumber<=20", second);
    }

    [Fact]
    public void StringConcatTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.YashanDB)!;

        Assert.Equal("a||b", db.StringConcat("a", "b"));
    }

    [Fact]
    public void ConnectionStringTest()
    {
        DAL.AddConnStr("yashandb_test", "Data Source=localhost:1688;User ID=u;Password=p", null, "YashanDB");
        var dal = DAL.Create("yashandb_test");
        Assert.Equal(DatabaseType.YashanDB, dal.DbType);
    }

    [Fact]
    public void IdentityNotSupportedTest()
    {
        var db = DbFactory.Create(DatabaseType.YashanDB);
        db.ConnectionString = "Data Source=localhost:1688;User ID=u";

        var session = db.CreateSession();
        Assert.Throws<NotSupportedException>(() => session.InsertAndGetIdentity("insert into t(a) values(1)"));
    }
}
