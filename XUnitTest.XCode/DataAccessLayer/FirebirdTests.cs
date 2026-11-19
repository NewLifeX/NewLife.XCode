using System;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>Firebird 数据库测试。无真实服务时仅验证方言与注册</summary>
[Collection("Database")]
public class FirebirdTests
{
    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.Firebird);
        Assert.NotNull(db);
        Assert.Equal(DatabaseType.Firebird, db.Type);
    }

    [Fact]
    public void SupportTest()
    {
        var db = DbFactory.Create(DatabaseType.Firebird);
        Assert.True(db.Support("Firebird"));
        Assert.True(db.Support("FirebirdSql.Data.FirebirdClient"));
        Assert.False(db.Support("SqlServer"));
    }

    [Fact]
    public void FormatKeyWordTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.Firebird)!;

        Assert.Equal("\"TableName\"", db.FormatKeyWord("TableName"));
        Assert.Equal("\"AlreadyQuoted\"", db.FormatKeyWord("\"AlreadyQuoted\""));
    }

    [Fact]
    public void PageSplitTest()
    {
        var db = DbFactory.Create(DatabaseType.Firebird);

        Assert.Equal("SELECT * FROM test rows 1 to 10", db.PageSplit("SELECT * FROM test", 0, 10, null));
        Assert.Equal("SELECT * FROM test rows 11 to 20", db.PageSplit("SELECT * FROM test", 10, 10, null));
    }

    [Fact]
    public void StringConcatTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.Firebird)!;

        Assert.Equal("a||b", db.StringConcat("a", "b"));
        Assert.Equal("''||b", db.StringConcat(null, "b"));
    }

    [Fact]
    public void ConnectionStringTest()
    {
        DAL.AddConnStr("firebird_test", "Database=..\\Data\\firebird_test.fdb;User=SYSDBA;Password=masterkey", null, "Firebird");
        var dal = DAL.Create("firebird_test");
        Assert.Equal(DatabaseType.Firebird, dal.DbType);

        // Database 表示数据库文件，解析为绝对路径
        Assert.NotNull(dal.Db.DatabaseName);
        Assert.EndsWith("firebird_test.fdb", dal.Db.DatabaseName!);
    }
}
