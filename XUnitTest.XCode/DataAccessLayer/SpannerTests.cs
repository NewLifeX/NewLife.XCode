using System;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>Google Cloud Spanner 数据库测试。无真实服务时仅验证方言与注册</summary>
[Collection("Database")]
public class SpannerTests
{
    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.Spanner);
        Assert.NotNull(db);
        Assert.Equal(DatabaseType.Spanner, db.Type);

        // 主键必须作为表级子句声明，禁止自动迁移
        Assert.Equal(Migration.Off, db.Migration);
    }

    [Fact]
    public void SupportTest()
    {
        var db = DbFactory.Create(DatabaseType.Spanner);
        Assert.True(db.Support("Spanner"));
        Assert.True(db.Support("Google.Cloud.Spanner.Data"));
        Assert.False(db.Support("SqlServer"));
    }

    [Fact]
    public void FormatKeyWordTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.Spanner)!;

        Assert.Equal("`TableName`", db.FormatKeyWord("TableName"));
        Assert.Equal("`AlreadyQuoted`", db.FormatKeyWord("`AlreadyQuoted`"));
    }

    [Fact]
    public void FormatValueTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.Spanner)!;

        var table = DAL.CreateTable();
        var col = table.CreateColumn();
        col.DataType = typeof(Boolean);

        Assert.Equal("TRUE", db.FormatValue(col, true));
        Assert.Equal("FALSE", db.FormatValue(col, false));
    }

    [Fact]
    public void PageSplitTest()
    {
        var db = DbFactory.Create(DatabaseType.Spanner);

        Assert.Contains("limit 10", db.PageSplit("SELECT * FROM test", 0, 10, null));
        Assert.Contains("limit 10 offset 10", db.PageSplit("SELECT * FROM test", 10, 10, null));
    }

    [Fact]
    public void ConnectionStringTest()
    {
        DAL.AddConnStr("spanner_test", "Data Source=projects/p/instances/i/databases/d;UsePlainText=true", null, "Spanner");
        var dal = DAL.Create("spanner_test");
        Assert.Equal(DatabaseType.Spanner, dal.DbType);
        Assert.DoesNotContain("Database=", dal.Db.ConnectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IdentityNotSupportedTest()
    {
        var db = DbFactory.Create(DatabaseType.Spanner);
        db.ConnectionString = "Data Source=projects/p/instances/i/databases/d";

        var session = db.CreateSession();
        Assert.Throws<NotSupportedException>(() => session.InsertAndGetIdentity("insert into t(a) values(1)"));
    }

    [Fact]
    public void DdlNotSupportedTest()
    {
        var db = DbFactory.Create(DatabaseType.Spanner);
        var meta = db.CreateMetaData();

        var table = DAL.CreateTable();
        table.TableName = "test";

        Assert.Throws<NotSupportedException>(() => meta.CreateTable(table));
    }
}
