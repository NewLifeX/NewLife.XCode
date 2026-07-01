using System;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>数据库类型注册测试。验证所有 DatabaseType 枚举值均已在 DbFactory 中注册</summary>
[Collection("Database")]
public class DatabaseTypeTests
{
    [Fact]
    public void AllTypes_CanCreate()
    {
        var types = new[]
        {
            DatabaseType.SQLite,
            DatabaseType.SqlServer,
            DatabaseType.MySql,
            DatabaseType.Oracle,
            DatabaseType.PostgreSQL,
            DatabaseType.DaMeng,
            DatabaseType.DB2,
            DatabaseType.TDengine,
            DatabaseType.Hana,
            DatabaseType.KingBase,
            DatabaseType.HighGo,
            DatabaseType.IRIS,
            DatabaseType.VastBase,
            DatabaseType.InfluxDB,
            DatabaseType.NovaDb,
            DatabaseType.ClickHouse,
            DatabaseType.DuckDB,
            DatabaseType.MongoDB,
            DatabaseType.Network,
        };

        foreach (var dbType in types)
        {
            var db = DbFactory.Create(dbType);
            Assert.NotNull(db);
            Assert.Equal(dbType, db.Type);
            XTrace.WriteLine("[{0}] OK: {1}", dbType, db.GetType().Name);
        }
    }

    [Fact]
    public void ClickHouse_TypeCheck()
    {
        var db = DbFactory.Create(DatabaseType.ClickHouse);
        Assert.Equal(DatabaseType.ClickHouse, db.Type);
        Assert.Equal("system", (db as RemoteDb)?.SystemDatabaseName);
    }

    [Fact]
    public void DuckDB_TypeCheck()
    {
        var db = DbFactory.Create(DatabaseType.DuckDB);
        Assert.Equal(DatabaseType.DuckDB, db.Type);
    }

    [Fact]
    public void MongoDB_TypeCheck()
    {
        var db = DbFactory.Create(DatabaseType.MongoDB);
        Assert.Equal(DatabaseType.MongoDB, db.Type);
        Assert.Equal("admin", (db as RemoteDb)?.SystemDatabaseName);
    }

    [Fact]
    public void Network_TypeCheck()
    {
        var db = DbFactory.Create(DatabaseType.Network);
        Assert.Equal(DatabaseType.Network, db.Type);
    }

    [Fact]
    public void Support_SelfIdentification()
    {
        // 验证每种类型都能识别自己的名称
        var clickHouse = DbFactory.Create(DatabaseType.ClickHouse);
        Assert.True(clickHouse.Support("ClickHouse"));

        var duckDB = DbFactory.Create(DatabaseType.DuckDB);
        Assert.True(duckDB.Support("DuckDB"));

        var mongoDB = DbFactory.Create(DatabaseType.MongoDB);
        Assert.True(mongoDB.Support("MongoDB"));
    }
}
