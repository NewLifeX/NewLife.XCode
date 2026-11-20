using System;
using NewLife;
using NewLife.Log;
using NewLife.UnitTest;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>MongoDB 数据库测试。MongoDB 是文档型 NoSQL 数据库，默认端口 27017</summary>
[TestCaseOrderer("NewLife.UnitTest.PriorityOrderer", "NewLife.UnitTest")]
public class MongoDBTests
{
    private const String DefaultConnStr = "mongodb://127.0.0.1:27017/test";

    private static String _ConnStr = DefaultConnStr;

    /// <summary>解析连接串。环境变量 XCode_mongodb 优先，其次 Config/mongodb.config，均无则写入并使用本机默认值</summary>
    /// <remarks>MongoDB 连接串不是 key=value 形式，本地探测无法解析服务地址时按跳过处理，CI 通过环境变量 XCode_mongodb 注入</remarks>
    public MongoDBTests() => _ConnStr = LiveDatabase.Resolve("mongodb", "mongodb", DefaultConnStr);

    [TestOrder(0)]
    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.MongoDB);
        Assert.NotNull(db);
        Assert.Equal(DatabaseType.MongoDB, db.Type);

        XTrace.WriteLine("MongoDB Provider Type: {0}", db.GetType().FullName);
    }

    [TestOrder(10)]
    [LiveDatabaseFact("mongodb", "mongodb", 27017)]
    public void ConnectTest()
    {
        var db = DbFactory.Create(DatabaseType.MongoDB);

        // MongoDB 使用 mongodb:// 连接字符串
        db.ConnectionString = _ConnStr;

        var conn = db.OpenConnection();
        Assert.NotNull(conn);
        XTrace.WriteLine("MongoDB connected: {0}", conn.ServerVersion);
    }

    [TestOrder(20)]
    [LiveDatabaseFact("mongodb", "mongodb", 27017)]
    public void DALTest()
    {
        DAL.AddConnStr("sysMongoDB", _ConnStr, null, "MongoDB");
        var dal = DAL.Create("sysMongoDB");
        Assert.NotNull(dal);
        Assert.Equal("sysMongoDB", dal.ConnName);
        Assert.Equal(DatabaseType.MongoDB, dal.DbType);

        var db = dal.Db;
        var connstr = db.ConnectionString;
        XTrace.WriteLine("ConnectionString: {0}", connstr);
    }

    [Fact]
    public void SupportTest()
    {
        var db = DbFactory.Create(DatabaseType.MongoDB);
        Assert.True(db.Support("MongoDB"));
        Assert.True(db.Support("mongo"));
        Assert.False(db.Support("SqlServer"));
    }

    [Fact]
    public void FormatKeyWordTest()
    {
        var db = DbFactory.Create(DatabaseType.MongoDB) as DbBase;
        Assert.NotNull(db);

        var result = db.FormatKeyWord("CollectionName");
        Assert.Equal("\"CollectionName\"", result);

        var result2 = db.FormatKeyWord("\"AlreadyQuoted\"");
        Assert.Equal("\"AlreadyQuoted\"", result2);
    }

    [Fact]
    public void FormatValueTest()
    {
        var db = DbFactory.Create(DatabaseType.MongoDB) as DbBase;
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
    public void ConnectionStringTest()
    {
        var db = DbFactory.Create(DatabaseType.MongoDB);
        db.ConnectionString = "mongodb://localhost:27017/test";

        var connStr = db.ConnectionString;
        Assert.Contains("mongodb://", connStr);
        Assert.Contains("test", connStr);
    }

    [Fact]
    public void SystemDatabaseTest()
    {
        var db = DbFactory.Create(DatabaseType.MongoDB) as DbBase;
        Assert.NotNull(db);

        // MongoDB 通过 override SystemDatabaseName 返回 "admin"
        var sysDb = (db as RemoteDb)?.SystemDatabaseName;
        Assert.NotNull(sysDb);
        Assert.Equal("admin", sysDb);
    }

    [Fact]
    public void StringConcatTest()
    {
        var db = DbFactory.Create(DatabaseType.MongoDB) as DbBase;
        Assert.NotNull(db);

        var result = db.StringConcat("a", "b");
        Assert.Equal("concat(a,b)", result);
    }
}
