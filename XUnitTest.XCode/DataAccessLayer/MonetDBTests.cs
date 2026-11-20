using System;
using System.Threading.Tasks;
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

    [Fact]
    public async Task IdentityNotSupportedAsyncTest()
    {
        var db = DbFactory.Create(DatabaseType.MonetDB);
        db.ConnectionString = "Host=localhost;Database=test";

        var session = (IAsyncDbSession)db.CreateSession();
        await Assert.ThrowsAsync<NotSupportedException>(() => session.InsertAndGetIdentityAsync("insert into t(a) values(1)"));
    }

    [Fact]
    public void FormatDateTimeTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.MonetDB)!;

        Assert.Equal("TIMESTAMP '2024-01-02 03:04:05'", db.FormatDateTime(new DateTime(2024, 1, 2, 3, 4, 5)));
    }

    [Fact]
    public void CreateTableSqlTest()
    {
        var db = DbFactory.Create(DatabaseType.MonetDB)!;

        var sql = db.CreateMetaData().GetSchemaSQL(DDLSchema.CreateTable, CreateTable())!;

        Assert.StartsWith("Create Table", sql);
        Assert.Contains("testuser", sql, StringComparison.OrdinalIgnoreCase);
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
        table.DbType = DatabaseType.MonetDB;

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
