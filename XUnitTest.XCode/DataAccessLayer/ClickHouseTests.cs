using System;
using System.IO;
using NewLife;
using NewLife.Log;
using NewLife.UnitTest;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>ClickHouse 数据库测试。ClickHouse 是列式分析型数据库，默认 HTTP 端口 8123</summary>
[TestCaseOrderer("NewLife.UnitTest.PriorityOrderer", "NewLife.UnitTest")]
public class ClickHouseTests
{
    private static String _ConnStr = "Host=localhost;Port=8123;Database=default;Username=default;Password=";

    public ClickHouseTests()
    {
        var f = "Config\\clickhouse.config".GetFullPath();
        if (File.Exists(f))
            _ConnStr = File.ReadAllText(f);
        else
            File.WriteAllText(f.EnsureDirectory(true), _ConnStr);
    }

    [TestOrder(0)]
    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.ClickHouse);
        Assert.NotNull(db);
        Assert.Equal(DatabaseType.ClickHouse, db.Type);

        var factory = db.Factory;
        // ClickHouse 驱动可能未安装，Factory 可能为 null
        // Assert.NotNull(factory);

        XTrace.WriteLine("ClickHouse Provider Type: {0}", db.GetType().FullName);
    }

    [TestOrder(10)]
    [Fact(Skip = "需要 ClickHouse 服务")]
    public void ConnectTest()
    {
        var db = DbFactory.Create(DatabaseType.ClickHouse);
        var factory = db.Factory;
        Assert.NotNull(factory);

        var conn = factory.CreateConnection();
        conn.ConnectionString = _ConnStr;
        conn.Open();

        Assert.NotEmpty(conn.ServerVersion);
        XTrace.WriteLine("ServerVersion={0}", conn.ServerVersion);
        conn.Close();
    }

    [TestOrder(20)]
    [Fact(Skip = "需要 ClickHouse 服务")]
    public void DALTest()
    {
        DAL.AddConnStr("sysClickHouse", _ConnStr, null, "ClickHouse");
        var dal = DAL.Create("sysClickHouse");
        Assert.NotNull(dal);
        Assert.Equal("sysClickHouse", dal.ConnName);
        Assert.Equal(DatabaseType.ClickHouse, dal.DbType);

        var db = dal.Db;
        var connstr = db.ConnectionString;

        var ver = db.ServerVersion;
        Assert.NotEmpty(ver);
    }

    [TestOrder(30)]
    [Fact(Skip = "需要 ClickHouse 服务")]
    public void MetaTest()
    {
        DAL.AddConnStr("sysClickHouse", _ConnStr, null, "ClickHouse");
        var dal = DAL.Create("sysClickHouse");

        var tables = dal.Tables;
        Assert.NotNull(tables);
        XTrace.WriteLine("Tables count: {0}", tables.Count);
    }

    [Fact]
    public void SupportTest()
    {
        var db = DbFactory.Create(DatabaseType.ClickHouse);
        Assert.True(db.Support("ClickHouse"));
        Assert.True(db.Support("clickhouse.client"));
        Assert.False(db.Support("SqlServer"));
    }

    [Fact]
    public void FormatKeyWordTest()
    {
        var db = DbFactory.Create(DatabaseType.ClickHouse) as DbBase;
        Assert.NotNull(db);

        var result = db.FormatKeyWord("TableName");
        Assert.Equal("`TableName`", result);

        var result2 = db.FormatKeyWord("`AlreadyQuoted`");
        Assert.Equal("`AlreadyQuoted`", result2);
    }

    [Fact]
    public void FormatValueTest()
    {
        var db = DbFactory.Create(DatabaseType.ClickHouse) as DbBase;
        Assert.NotNull(db);

        // ClickHouse 用 0/1 表示 Boolean
        var table = DAL.CreateTable();
        var col = table.CreateColumn();
        col.DataType = typeof(Boolean);

        var trueVal = db.FormatValue(col, true);
        Assert.Equal("1", trueVal);

        var falseVal = db.FormatValue(col, false);
        Assert.Equal("0", falseVal);
    }

    [Fact]
    public void PageSplitTest()
    {
        var db = DbFactory.Create(DatabaseType.ClickHouse);

        // 第一页
        var sql = db.PageSplit("SELECT * FROM test", 0, 10, null);
        Assert.Contains("limit 10", sql);

        // 第二页
        var sql2 = db.PageSplit("SELECT * FROM test", 10, 10, null);
        Assert.Contains("limit 10 offset 10", sql2);
    }

    [Fact]
    public void StringConcatTest()
    {
        var db = DbFactory.Create(DatabaseType.ClickHouse) as DbBase;
        Assert.NotNull(db);

        var result = db.StringConcat("a", "b");
        Assert.Equal("concat(a,b)", result);
    }
}
