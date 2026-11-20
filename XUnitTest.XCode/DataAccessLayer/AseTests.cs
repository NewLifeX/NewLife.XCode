using System;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>SAP ASE(Sybase) 数据库测试。无真实服务时仅验证方言与注册</summary>
[Collection("Database")]
public class AseTests
{
    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.Ase);
        Assert.NotNull(db);
        Assert.Equal(DatabaseType.Ase, db.Type);
    }

    [Fact]
    public void SupportTest()
    {
        var db = DbFactory.Create(DatabaseType.Ase);
        Assert.True(db.Support("Ase"));
        Assert.True(db.Support("AdoNetCore.AseClient"));
        Assert.True(db.Support("Sybase"));
        Assert.False(db.Support("SqlServer"));
    }

    [Fact]
    public void FormatKeyWordTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.Ase)!;

        Assert.Equal("\"TableName\"", db.FormatKeyWord("TableName"));
    }

    [Fact]
    public void PageSplitTest()
    {
        var db = DbFactory.Create(DatabaseType.Ase);

        var first = db.PageSplit("SELECT * FROM test", 0, 10, null);
        Assert.Contains("Top 10", first);
        Assert.StartsWith("SELECT Top 10", first);

        var second = db.PageSplit("SELECT * FROM test", 10, 10, null);
        Assert.Contains("Top 10 Start At 11", second);
    }

    [Fact]
    public void PageSplitDistinctTest()
    {
        var db = DbFactory.Create(DatabaseType.Ase);

        var sql = db.PageSplit("SELECT DISTINCT Name FROM test", 0, 5, null);
        Assert.Contains("SELECT DISTINCT Top 5", sql);
    }

    [Fact]
    public void StringConcatTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.Ase)!;

        Assert.Equal("a+b", db.StringConcat("a", "b"));
    }

    [Fact]
    public void ConnectionStringTest()
    {
        DAL.AddConnStr("ase_test", "Data Source=localhost;Port=5000;Database=test;User ID=sa;Password=p", null, "Ase");
        var dal = DAL.Create("ase_test");
        Assert.Equal(DatabaseType.Ase, dal.DbType);
        Assert.Equal("test", dal.Db.DatabaseName);
    }

    [Fact]
    public void CreateTableSqlTest()
    {
        var db = DbFactory.Create(DatabaseType.Ase)!;

        var sql = db.CreateMetaData().GetSchemaSQL(DDLSchema.CreateTable, CreateTable())!;

        Assert.StartsWith("Create Table", sql);
        Assert.Contains("TestUser", sql);
        Assert.Contains("INTEGER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VARCHAR(50)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IDENTITY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Primary Key", sql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>构造模型表：自增主键 + 50 长度字符串字段</summary>
    private static IDataTable CreateTable()
    {
        var table = DAL.CreateTable();
        table.Name = "TestUser";
        table.TableName = "TestUser";
        table.DbType = DatabaseType.Ase;

        var id = table.CreateColumn();
        id.Name = "Id";
        id.ColumnName = "Id";
        id.DataType = typeof(Int32);
        id.Identity = true;
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
