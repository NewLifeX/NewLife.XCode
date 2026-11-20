using System;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>Snowflake 数据库测试。无真实服务时仅验证方言与注册</summary>
[Collection("Database")]
public class SnowflakeTests
{
    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.Snowflake);
        Assert.NotNull(db);
        Assert.Equal(DatabaseType.Snowflake, db.Type);
    }

    [Fact]
    public void SupportTest()
    {
        var db = DbFactory.Create(DatabaseType.Snowflake);
        Assert.True(db.Support("Snowflake"));
        Assert.True(db.Support("Snowflake.Data"));
        Assert.False(db.Support("SqlServer"));
    }

    [Fact]
    public void FormatKeyWordTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.Snowflake)!;

        Assert.Equal("\"TableName\"", db.FormatKeyWord("TableName"));
        Assert.Equal("\"AlreadyQuoted\"", db.FormatKeyWord("\"AlreadyQuoted\""));
    }

    [Fact]
    public void FormatValueTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.Snowflake)!;

        var table = DAL.CreateTable();
        var col = table.CreateColumn();
        col.DataType = typeof(Boolean);

        Assert.Equal("TRUE", db.FormatValue(col, true));
        Assert.Equal("FALSE", db.FormatValue(col, false));
    }

    [Fact]
    public void FormatDateTimeTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.Snowflake)!;

        var text = db.FormatDateTime(new DateTime(2024, 1, 2, 3, 4, 5));
        Assert.Equal("TO_TIMESTAMP_NTZ('2024-01-02 03:04:05')", text);
    }

    [Fact]
    public void PageSplitTest()
    {
        var db = DbFactory.Create(DatabaseType.Snowflake);

        Assert.Contains("limit 10", db.PageSplit("SELECT * FROM test", 0, 10, null));
        Assert.Contains("limit 10 offset 10", db.PageSplit("SELECT * FROM test", 10, 10, null));
    }

    [Fact]
    public void StringConcatTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.Snowflake)!;

        Assert.Equal("a||b", db.StringConcat("a", "b"));
    }

    [Fact]
    public void ConnectionStringTest()
    {
        DAL.AddConnStr("snowflake_test", "account=test;user=u;Database=TESTDB;Owner=PUBLIC;warehouse=WH", null, "Snowflake");
        var dal = DAL.Create("snowflake_test");
        Assert.Equal(DatabaseType.Snowflake, dal.DbType);

        var connStr = dal.Db.ConnectionString;
        Assert.True(connStr.Contains("db=TESTDB", StringComparison.OrdinalIgnoreCase), connStr);
        Assert.True(connStr.Contains("schema=PUBLIC", StringComparison.OrdinalIgnoreCase), connStr);
        Assert.False(connStr.Contains("Database=", StringComparison.OrdinalIgnoreCase), connStr);
    }

    [Fact]
    public void IdentityNotSupportedTest()
    {
        var db = DbFactory.Create(DatabaseType.Snowflake);
        db.ConnectionString = "account=test;user=u";

        var session = db.CreateSession();
        Assert.Throws<NotSupportedException>(() => session.InsertAndGetIdentity("insert into t(a) values(1)"));
    }

    [Fact]
    public void CreateTableSqlTest()
    {
        var db = DbFactory.Create(DatabaseType.Snowflake)!;

        var sql = db.CreateMetaData().GetSchemaSQL(DDLSchema.CreateTable, CreateTable())!;

        Assert.StartsWith("Create Table", sql);
        Assert.Contains("TESTUSER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INTEGER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VARCHAR(50)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Primary Key", sql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>构造模型表：主键 + 50 长度字符串字段</summary>
    private static IDataTable CreateTable()
    {
        var table = DAL.CreateTable();
        table.Name = "TestUser";
        table.TableName = "TestUser";
        table.DbType = DatabaseType.Snowflake;

        var id = table.CreateColumn();
        id.Name = "Id";
        id.ColumnName = "Id";
        id.DataType = typeof(Int32);
        id.PrimaryKey = true;
        table.Columns.Add(id);

        var name = table.CreateColumn();
        name.Name = "Name";
        name.ColumnName = "Name";
        name.DataType = typeof(String);
        name.Length = 50;
        table.Columns.Add(name);

        return table;
    }
}
