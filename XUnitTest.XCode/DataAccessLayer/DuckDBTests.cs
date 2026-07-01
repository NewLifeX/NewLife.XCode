using System;
using System.IO;
using System.Linq;
using NewLife;
using NewLife.Log;
using NewLife.UnitTest;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>DuckDB 数据库测试。DuckDB 是嵌入式 OLAP 数据库，类似 SQLite 的文件架构</summary>
[TestCaseOrderer("NewLife.UnitTest.PriorityOrderer", "NewLife.UnitTest")]
public class DuckDBTests
{
    private static String _ConnStr = "Data Source=:memory:";

    public DuckDBTests()
    {
        var f = "Config\\duckdb.config".GetFullPath();
        if (File.Exists(f))
            _ConnStr = File.ReadAllText(f);
        else
            File.WriteAllText(f.EnsureDirectory(true), _ConnStr);
    }

    [TestOrder(0)]
    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.DuckDB);
        Assert.NotNull(db);
        Assert.Equal(DatabaseType.DuckDB, db.Type);

        XTrace.WriteLine("DuckDB Provider Type: {0}", db.GetType().FullName);
    }

    [TestOrder(10)]
    [Fact(Skip = "需要 DuckDB.NET.Data 驱动")]
    public void ConnectTest()
    {
        var db = DbFactory.Create(DatabaseType.DuckDB);
        var factory = db.Factory;
        Assert.NotNull(factory);

        var conn = factory.CreateConnection();
        conn.ConnectionString = "Data Source=:memory:";
        conn.Open();

        XTrace.WriteLine("DuckDB connected, version={0}", conn.ServerVersion);
        conn.Close();
    }

    [TestOrder(20)]
    [Fact(Skip = "需要 DuckDB.NET.Data 驱动")]
    public void DALTest()
    {
        DAL.AddConnStr("sysDuckDB", _ConnStr, null, "DuckDB");
        var dal = DAL.Create("sysDuckDB");
        Assert.NotNull(dal);
        Assert.Equal("sysDuckDB", dal.ConnName);
        Assert.Equal(DatabaseType.DuckDB, dal.DbType);

        var db = dal.Db;
        var connstr = db.ConnectionString;
        XTrace.WriteLine("ConnectionString: {0}", connstr);
    }

    [TestOrder(30)]
    [Fact(Skip = "需要 DuckDB.NET.Data 驱动")]
    public void MetaTest()
    {
        DAL.AddConnStr("sysDuckDB", _ConnStr, null, "DuckDB");
        var dal = DAL.Create("sysDuckDB");

        dal.Execute("CREATE TABLE test_table (id INTEGER PRIMARY KEY, name VARCHAR)");
        dal.Execute("INSERT INTO test_table VALUES (1, 'hello')");

        var tables = dal.Tables;
        Assert.NotNull(tables);
        Assert.True(tables.Count > 0);

        var table = dal.Tables.FirstOrDefault(t => t.TableName.EqualIgnoreCase("test_table"));
        Assert.NotNull(table);

        dal.Execute("DROP TABLE test_table");
    }

    [Fact]
    public void SupportTest()
    {
        var db = DbFactory.Create(DatabaseType.DuckDB);
        Assert.True(db.Support("DuckDB"));
        Assert.True(db.Support("duckdb.net.data"));
        Assert.False(db.Support("SqlServer"));
    }

    [Fact]
    public void FormatKeyWordTest()
    {
        var db = DbFactory.Create(DatabaseType.DuckDB) as DbBase;
        Assert.NotNull(db);

        var result = db.FormatKeyWord("TableName");
        Assert.Equal("\"TableName\"", result);

        var result2 = db.FormatKeyWord("\"AlreadyQuoted\"");
        Assert.Equal("\"AlreadyQuoted\"", result2);
    }

    [Fact]
    public void FormatValueTest()
    {
        var db = DbFactory.Create(DatabaseType.DuckDB) as DbBase;
        Assert.NotNull(db);

        var table = DAL.CreateTable();
        var col = table.CreateColumn();
        col.DataType = typeof(Boolean);

        var trueVal = db.FormatValue(col, true);
        Assert.Equal("true", trueVal);

        var falseVal = db.FormatValue(col, false);
        Assert.Equal("false", falseVal);
    }

    [Fact]
    public void FormatDateTimeTest()
    {
        var db = DbFactory.Create(DatabaseType.DuckDB) as DbBase;
        Assert.NotNull(db);

        var table = DAL.CreateTable();
        var col = table.CreateColumn();
        col.DataType = typeof(DateTime);

        var dt = new DateTime(2026, 7, 1, 12, 0, 0);
        var result = db.FormatDateTime(col, dt);
        Assert.Contains("TIMESTAMP", result);
        Assert.Contains("2026-07-01 12:00:00", result);
    }

    [Fact]
    public void PageSplitTest()
    {
        var db = DbFactory.Create(DatabaseType.DuckDB);

        var sql = db.PageSplit("SELECT * FROM test", 0, 10, null);
        Assert.Contains("limit 10", sql);

        var sql2 = db.PageSplit("SELECT * FROM test", 10, 10, null);
        Assert.Contains("offset 10 limit 10", sql2);
    }

    [Fact]
    public void StringConcatTest()
    {
        var db = DbFactory.Create(DatabaseType.DuckDB) as DbBase;
        Assert.NotNull(db);

        var result = db.StringConcat("a", "b");
        Assert.Equal("a||b", result);
    }
}
