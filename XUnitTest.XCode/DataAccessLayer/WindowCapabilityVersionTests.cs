using System;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>版本相关窗口能力判定测试</summary>
public class WindowCapabilityVersionTests
{
    [Theory]
    [InlineData(DatabaseType.KingBase)]
    [InlineData(DatabaseType.HighGo)]
    [InlineData(DatabaseType.VastBase)]
    [InlineData(DatabaseType.IRIS)]
    [System.ComponentModel.Description("PostgreSQL 系与 IRIS 静态声明窗口函数与窗口帧")]
    public void PostgreSqlLikeProviders_DeclareWindowCapabilities(DatabaseType dbType)
    {
        var db = DbFactory.Create(dbType);
        Assert.NotNull(db);

        Assert.True(db.Capabilities.HasFlag(DatabaseCapability.WindowFunctions), dbType.ToString());
        Assert.True(db.Capabilities.HasFlag(DatabaseCapability.WindowFrame), dbType.ToString());
    }

    [Fact]
    [System.ComponentModel.Description("版本未知时不声明窗口能力，避免提前建立连接")]
    public void UnknownVersion_DoesNotClaimWindowCapabilities()
    {
        var db = DbFactory.Create(DatabaseType.DB2);
        Assert.NotNull(db);

        Assert.False(db.Capabilities.HasFlag(DatabaseCapability.WindowFunctions));
        Assert.False(db.Capabilities.HasFlag(DatabaseCapability.WindowFrame));
    }

    [Theory]
    [InlineData("11.5.5.0", true)]
    [InlineData("10.5.0.0", true)]
    [InlineData("9.7.0.0", true)]
    [InlineData("8.2.0.0", false)]
    [System.ComponentModel.Description("DB2 按版本决定窗口能力")]
    public void DB2_VersionBasedCapability(String version, Boolean expected)
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.DB2)!;
        db._ServerVersion = version;

        Assert.Equal(expected, db.Capabilities.HasFlag(DatabaseCapability.WindowFunctions));
        Assert.Equal(expected, db.Capabilities.HasFlag(DatabaseCapability.WindowFrame));
    }

    [Theory]
    [InlineData("DBMS Version: 8.1.2.192", true)]
    [InlineData("7.6.0.0", false)]
    [System.ComponentModel.Description("达梦从产品版本串中解析主版本")]
    public void DaMeng_VersionBasedCapability(String version, Boolean expected)
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.DaMeng)!;
        db._ServerVersion = version;

        Assert.Equal(expected, db.Capabilities.HasFlag(DatabaseCapability.WindowFunctions));
    }

    [Theory]
    [InlineData("2.00.059.00", true)]
    [InlineData("1.00.122.00", false)]
    [System.ComponentModel.Description("SAP HANA 2.0 起声明窗口能力")]
    public void Hana_VersionBasedCapability(String version, Boolean expected)
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.Hana)!;
        db._ServerVersion = version;

        Assert.Equal(expected, db.Capabilities.HasFlag(DatabaseCapability.WindowFunctions));
    }

    [Fact]
    [System.ComponentModel.Description("版本相关能力不改变最大迁移模式")]
    public void VersionedCapabilities_DoNotAffectMigration()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.DB2)!;
        var before = db.MaxMigration;

        db._ServerVersion = "11.5.5.0";

        Assert.Equal(before, db.MaxMigration);
        Assert.True(db.Capabilities.HasFlag(DatabaseCapability.WindowFunctions));
    }
}
