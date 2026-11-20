using System;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>DatabaseCapability枚举及各数据库实现的纯单元测试，无需数据库连接</summary>
public class DatabaseCapabilityTests
{
    [Fact]
    [System.ComponentModel.Description("MySql默认驱动具备Insert/InsertIgnore/Replace/Upsert能力，不含Update")]
    public void MySql_DefaultDriver_DatabaseCapability()
    {
        var db = DbFactory.Create(DatabaseType.MySql);
        var cap = db.Capabilities;

        Assert.True(cap.HasFlag(DatabaseCapability.BulkInsert));
        Assert.True(cap.HasFlag(DatabaseCapability.BulkInsertIgnore));
        Assert.True(cap.HasFlag(DatabaseCapability.BulkReplace));
        Assert.True(cap.HasFlag(DatabaseCapability.BulkUpsert));
        // NewLife.MySql未加载时IsNewLifeDriver为false，不含Update；集成测试中再验证
        // 只要包含四个基础能力即通过
    }

    [Fact]
    [System.ComponentModel.Description("MySql含Update标志当且仅当IsNewLifeDriver为true")]
    public void MySql_WithNewLifeDriver_DatabaseCapabilityIncludesUpdate()
    {
        var db = DbFactory.Create(DatabaseType.MySql);
        var mysqlDb = (MySql)db;
        var cap = db.Capabilities;

        // Update标志与IsNewLifeDriver保持一致
        Assert.Equal(mysqlDb.IsNewLifeDriver, cap.HasFlag(DatabaseCapability.BulkUpdate));
    }

    [Fact]
    [System.ComponentModel.Description("Oracle具备Insert/Update/Upsert能力，不含InsertIgnore/Replace")]
    public void Oracle_DatabaseCapability()
    {
        var db = DbFactory.Create(DatabaseType.Oracle);
        var cap = db.Capabilities;

        Assert.True(cap.HasFlag(DatabaseCapability.BulkInsert));
        Assert.True(cap.HasFlag(DatabaseCapability.BulkUpdate));
        Assert.True(cap.HasFlag(DatabaseCapability.BulkUpsert));
        Assert.False(cap.HasFlag(DatabaseCapability.BulkInsertIgnore));
        Assert.False(cap.HasFlag(DatabaseCapability.BulkReplace));
    }

    [Fact]
    [System.ComponentModel.Description("SQLite具备Insert/InsertIgnore/Replace/Upsert能力，不含Update")]
    public void SQLite_DatabaseCapability()
    {
        var db = DbFactory.Create(DatabaseType.SQLite);
        var cap = db.Capabilities;

        Assert.True(cap.HasFlag(DatabaseCapability.BulkInsert));
        Assert.True(cap.HasFlag(DatabaseCapability.BulkInsertIgnore));
        Assert.True(cap.HasFlag(DatabaseCapability.BulkReplace));
        Assert.True(cap.HasFlag(DatabaseCapability.BulkUpsert));
        Assert.False(cap.HasFlag(DatabaseCapability.BulkUpdate));
    }

    [Fact]
    [System.ComponentModel.Description("SqlServer仅具备Insert/Upsert能力，不含Update/InsertIgnore/Replace")]
    public void SqlServer_DatabaseCapability()
    {
        var db = DbFactory.Create(DatabaseType.SqlServer);
        var cap = db.Capabilities;

        Assert.True(cap.HasFlag(DatabaseCapability.BulkInsert));
        Assert.True(cap.HasFlag(DatabaseCapability.BulkUpsert));
        Assert.False(cap.HasFlag(DatabaseCapability.BulkUpdate));
        Assert.False(cap.HasFlag(DatabaseCapability.BulkInsertIgnore));
        Assert.False(cap.HasFlag(DatabaseCapability.BulkReplace));
    }

    [Fact]
    [System.ComponentModel.Description("PostgreSQL仅具备Insert/Upsert能力，不含Update/InsertIgnore/Replace")]
    public void PostgreSQL_DatabaseCapability()
    {
        var db = DbFactory.Create(DatabaseType.PostgreSQL);
        var cap = db.Capabilities;

        Assert.True(cap.HasFlag(DatabaseCapability.BulkInsert));
        Assert.True(cap.HasFlag(DatabaseCapability.BulkUpsert));
        Assert.False(cap.HasFlag(DatabaseCapability.BulkUpdate));
        Assert.False(cap.HasFlag(DatabaseCapability.BulkInsertIgnore));
        Assert.False(cap.HasFlag(DatabaseCapability.BulkReplace));
    }

    [Fact]
    [System.ComponentModel.Description("NovaDb具备Insert/InsertIgnore/Replace/Upsert能力，不含Update")]
    public void NovaDb_DatabaseCapability()
    {
        var db = DbFactory.Create(DatabaseType.NovaDb);
        var cap = db.Capabilities;

        Assert.True(cap.HasFlag(DatabaseCapability.BulkInsert));
        Assert.True(cap.HasFlag(DatabaseCapability.BulkInsertIgnore));
        Assert.True(cap.HasFlag(DatabaseCapability.BulkReplace));
        Assert.True(cap.HasFlag(DatabaseCapability.BulkUpsert));
        Assert.False(cap.HasFlag(DatabaseCapability.BulkUpdate));
    }

    [Fact]
    [System.ComponentModel.Description("InfluxDB具备Insert/Upsert能力，不含Update/InsertIgnore/Replace")]
    public void InfluxDB_DatabaseCapability()
    {
        var db = DbFactory.Create(DatabaseType.InfluxDB);
        var cap = db.Capabilities;

        Assert.True(cap.HasFlag(DatabaseCapability.BulkInsert));
        Assert.True(cap.HasFlag(DatabaseCapability.BulkUpsert));
        Assert.False(cap.HasFlag(DatabaseCapability.BulkUpdate));
        Assert.False(cap.HasFlag(DatabaseCapability.BulkInsertIgnore));
        Assert.False(cap.HasFlag(DatabaseCapability.BulkReplace));
    }

    [Fact]
    [System.ComponentModel.Description("DatabaseCapability枚举值满足Flags语义，组合标志可通过HasFlag判断")]
    public void DatabaseCapability_FlagsSemantics()
    {
        var combined = DatabaseCapability.BulkInsert | DatabaseCapability.BulkUpsert | DatabaseCapability.BulkUpdate;

        Assert.True(combined.HasFlag(DatabaseCapability.BulkInsert));
        Assert.True(combined.HasFlag(DatabaseCapability.BulkUpsert));
        Assert.True(combined.HasFlag(DatabaseCapability.BulkUpdate));
        Assert.False(combined.HasFlag(DatabaseCapability.BulkReplace));
        Assert.False(combined.HasFlag(DatabaseCapability.BulkInsertIgnore));
        Assert.Equal(DatabaseCapability.None, combined & DatabaseCapability.BulkReplace);
    }

    [Fact]
    [System.ComponentModel.Description("DAL.Capabilities代理Db.Capabilities属性，两者值相同")]
    public void DAL_Capabilities_DelegatesFromDb()
    {
        DAL.AddConnStr("BatchCap_SQLite_unit", "Data Source=:memory:", null, "SQLite");
        var dal = DAL.Create("BatchCap_SQLite_unit");

        Assert.Equal(dal.Db.Capabilities, dal.Capabilities);
    }

    [Fact]
    [System.ComponentModel.Description("DAL.SupportBatch已标记Obsolete，仍可正常访问不抛出异常")]
    public void DAL_SupportBatch_ObsoleteStillAccessible()
    {
        DAL.AddConnStr("BatchCap_SupportBatch_unit", "Data Source=:memory:", null, "SQLite");
        var dal = DAL.Create("BatchCap_SupportBatch_unit");

#pragma warning disable CS0618
        var _ = dal.SupportBatch;
#pragma warning restore CS0618
        // 只要不抛出即通过
    }

    [Fact]
    [System.ComponentModel.Description("MySql DatabaseCapability不等于单独的Insert，SupportBatch固定返回false")]
    public void MySql_SupportBatch_ReturnsFalse()
    {
        // SupportBatch定义为 Db.Capabilities == DatabaseCapability.BulkInsert
        // MySql至少含Insert|InsertIgnore|Replace|Upsert，因此始终为false
        var db = DbFactory.Create(DatabaseType.MySql);
        Assert.NotEqual(DatabaseCapability.BulkInsert, db.Capabilities);
    }

    [Theory]
    [InlineData(DatabaseType.DuckDB)]
    [InlineData(DatabaseType.ClickHouse)]
    [InlineData(DatabaseType.MongoDB)]
    [System.ComponentModel.Description("未实现批量方法的提供者不得声明 BulkInsert，避免能力标志说谎")]
    public void ProviderWithoutBatchImplementation_DoesNotClaimBulkInsert(DatabaseType dbType)
    {
        var db = DbFactory.Create(dbType);
        Assert.NotNull(db);
        Assert.False(db.Capabilities.HasFlag(DatabaseCapability.BulkInsert));
    }

    [Theory]
    [InlineData(DatabaseType.MongoDB)]
    [InlineData(DatabaseType.InfluxDB)]
    [System.ComponentModel.Description("非 SQL 提供者不得声明 SQL/事务能力")]
    public void NonSqlProvider_DoesNotClaimSqlOrTransactions(DatabaseType dbType)
    {
        var db = DbFactory.Create(dbType);
        Assert.NotNull(db);
        Assert.False(db.Capabilities.HasFlag(DatabaseCapability.Sql));
        Assert.False(db.Capabilities.HasFlag(DatabaseCapability.Transactions));
    }

    [Theory]
    [InlineData(DatabaseType.Presto)]
    [InlineData(DatabaseType.Trino)]
    [InlineData(DatabaseType.Spanner)]
    [InlineData(DatabaseType.MongoDB)]
    [InlineData(DatabaseType.InfluxDB)]
    [System.ComponentModel.Description("不支持自动迁移的提供者，最大迁移模式必须为 Off")]
    public void ProviderWithoutDdl_MaxMigrationIsOff(DatabaseType dbType)
    {
        var db = DbFactory.Create(dbType);
        Assert.NotNull(db);
        Assert.Equal(Migration.Off, db.MaxMigration);
        Assert.Equal(Migration.Off, db.EffectiveMigration);
    }

    [Theory]
    [InlineData(DatabaseType.MongoDB)]
    [InlineData(DatabaseType.InfluxDB)]
    [System.ComponentModel.Description("非 SQL 提供者调用实体插入能力门禁时抛出明确异常")]
    public void NonSqlProvider_EnsureSqlThrows(DatabaseType dbType)
    {
        var db = DbFactory.Create(dbType);
        Assert.NotNull(db);
        Assert.Throws<NotSupportedException>(() => db.EnsureSupported(DatabaseCapability.Sql | DatabaseCapability.Insert, "entity insert"));
    }
}
