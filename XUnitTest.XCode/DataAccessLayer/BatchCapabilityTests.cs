using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>BatchCapability枚举及各数据库实现的纯单元测试，无需数据库连接</summary>
public class BatchCapabilityTests
{
    [Fact]
    [System.ComponentModel.Description("MySql默认驱动具备Insert/InsertIgnore/Replace/Upsert能力，不含Update")]
    public void MySql_DefaultDriver_BatchCapability()
    {
        var db = DbFactory.Create(DatabaseType.MySql);
        var cap = db.BatchCapability;

        Assert.True(cap.HasFlag(BatchCapability.Insert));
        Assert.True(cap.HasFlag(BatchCapability.InsertIgnore));
        Assert.True(cap.HasFlag(BatchCapability.Replace));
        Assert.True(cap.HasFlag(BatchCapability.Upsert));
        // NewLife.MySql未加载时IsNewLifeDriver为false，不含Update；集成测试中再验证
        // 只要包含四个基础能力即通过
    }

    [Fact]
    [System.ComponentModel.Description("MySql含Update标志当且仅当IsNewLifeDriver为true")]
    public void MySql_WithNewLifeDriver_BatchCapabilityIncludesUpdate()
    {
        var db = DbFactory.Create(DatabaseType.MySql);
        var mysqlDb = (MySql)db;
        var cap = db.BatchCapability;

        // Update标志与IsNewLifeDriver保持一致
        Assert.Equal(mysqlDb.IsNewLifeDriver, cap.HasFlag(BatchCapability.Update));
    }

    [Fact]
    [System.ComponentModel.Description("Oracle具备Insert/Update/Upsert能力，不含InsertIgnore/Replace")]
    public void Oracle_BatchCapability()
    {
        var db = DbFactory.Create(DatabaseType.Oracle);
        var cap = db.BatchCapability;

        Assert.True(cap.HasFlag(BatchCapability.Insert));
        Assert.True(cap.HasFlag(BatchCapability.Update));
        Assert.True(cap.HasFlag(BatchCapability.Upsert));
        Assert.False(cap.HasFlag(BatchCapability.InsertIgnore));
        Assert.False(cap.HasFlag(BatchCapability.Replace));
    }

    [Fact]
    [System.ComponentModel.Description("SQLite具备Insert/InsertIgnore/Replace/Upsert能力，不含Update")]
    public void SQLite_BatchCapability()
    {
        var db = DbFactory.Create(DatabaseType.SQLite);
        var cap = db.BatchCapability;

        Assert.True(cap.HasFlag(BatchCapability.Insert));
        Assert.True(cap.HasFlag(BatchCapability.InsertIgnore));
        Assert.True(cap.HasFlag(BatchCapability.Replace));
        Assert.True(cap.HasFlag(BatchCapability.Upsert));
        Assert.False(cap.HasFlag(BatchCapability.Update));
    }

    [Fact]
    [System.ComponentModel.Description("SqlServer仅具备Insert/Upsert能力，不含Update/InsertIgnore/Replace")]
    public void SqlServer_BatchCapability()
    {
        var db = DbFactory.Create(DatabaseType.SqlServer);
        var cap = db.BatchCapability;

        Assert.True(cap.HasFlag(BatchCapability.Insert));
        Assert.True(cap.HasFlag(BatchCapability.Upsert));
        Assert.False(cap.HasFlag(BatchCapability.Update));
        Assert.False(cap.HasFlag(BatchCapability.InsertIgnore));
        Assert.False(cap.HasFlag(BatchCapability.Replace));
    }

    [Fact]
    [System.ComponentModel.Description("PostgreSQL仅具备Insert/Upsert能力，不含Update/InsertIgnore/Replace")]
    public void PostgreSQL_BatchCapability()
    {
        var db = DbFactory.Create(DatabaseType.PostgreSQL);
        var cap = db.BatchCapability;

        Assert.True(cap.HasFlag(BatchCapability.Insert));
        Assert.True(cap.HasFlag(BatchCapability.Upsert));
        Assert.False(cap.HasFlag(BatchCapability.Update));
        Assert.False(cap.HasFlag(BatchCapability.InsertIgnore));
        Assert.False(cap.HasFlag(BatchCapability.Replace));
    }

    [Fact]
    [System.ComponentModel.Description("NovaDb具备Insert/InsertIgnore/Replace/Upsert能力，不含Update")]
    public void NovaDb_BatchCapability()
    {
        var db = DbFactory.Create(DatabaseType.NovaDb);
        var cap = db.BatchCapability;

        Assert.True(cap.HasFlag(BatchCapability.Insert));
        Assert.True(cap.HasFlag(BatchCapability.InsertIgnore));
        Assert.True(cap.HasFlag(BatchCapability.Replace));
        Assert.True(cap.HasFlag(BatchCapability.Upsert));
        Assert.False(cap.HasFlag(BatchCapability.Update));
    }

    [Fact]
    [System.ComponentModel.Description("InfluxDB具备Insert/Upsert能力，不含Update/InsertIgnore/Replace")]
    public void InfluxDB_BatchCapability()
    {
        var db = DbFactory.Create(DatabaseType.InfluxDB);
        var cap = db.BatchCapability;

        Assert.True(cap.HasFlag(BatchCapability.Insert));
        Assert.True(cap.HasFlag(BatchCapability.Upsert));
        Assert.False(cap.HasFlag(BatchCapability.Update));
        Assert.False(cap.HasFlag(BatchCapability.InsertIgnore));
        Assert.False(cap.HasFlag(BatchCapability.Replace));
    }

    [Fact]
    [System.ComponentModel.Description("BatchCapability枚举值满足Flags语义，组合标志可通过HasFlag判断")]
    public void BatchCapability_FlagsSemantics()
    {
        var combined = BatchCapability.Insert | BatchCapability.Upsert | BatchCapability.Update;

        Assert.True(combined.HasFlag(BatchCapability.Insert));
        Assert.True(combined.HasFlag(BatchCapability.Upsert));
        Assert.True(combined.HasFlag(BatchCapability.Update));
        Assert.False(combined.HasFlag(BatchCapability.Replace));
        Assert.False(combined.HasFlag(BatchCapability.InsertIgnore));
        Assert.Equal(BatchCapability.None, combined & BatchCapability.Replace);
    }

    [Fact]
    [System.ComponentModel.Description("DAL.BatchCapabilities代理Db.BatchCapability属性，两者值相同")]
    public void DAL_BatchCapabilities_DelegatesFromDb()
    {
        DAL.AddConnStr("BatchCap_SQLite_unit", "Data Source=:memory:", null, "SQLite");
        var dal = DAL.Create("BatchCap_SQLite_unit");

        Assert.Equal(dal.Db.BatchCapability, dal.BatchCapabilities);
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
    [System.ComponentModel.Description("MySql BatchCapability不等于单独的Insert，SupportBatch固定返回false")]
    public void MySql_SupportBatch_ReturnsFalse()
    {
        // SupportBatch定义为 Db.BatchCapability == BatchCapability.Insert
        // MySql至少含Insert|InsertIgnore|Replace|Upsert，因此始终为false
        var db = DbFactory.Create(DatabaseType.MySql);
        Assert.NotEqual(BatchCapability.Insert, db.BatchCapability);
    }
}
