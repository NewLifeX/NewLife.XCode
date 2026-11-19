using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>协议别名测试。MySQL/PostgreSQL 协议兼容的数据库直接复用对应提供者</summary>
[Collection("Database")]
public class ProtocolAliasTests
{
    [Theory]
    [InlineData("MariaDB")]
    [InlineData("TiDB")]
    [InlineData("StarRocks")]
    [InlineData("Doris")]
    [InlineData("OceanBase")]
    [InlineData("SingleStore")]
    [InlineData("PolarDB")]
    [InlineData("TDSQL")]
    [InlineData("GoldenDB")]
    [InlineData("GreatDB")]
    [InlineData("Databend")]
    public void MySqlAliasTest(string provider)
    {
        var db = DbFactory.Create(DatabaseType.MySql);
        Assert.True(db.Support(provider));

        Assert.Equal(typeof(MySql), DbFactory.GetProviderType("", provider));
    }

    [Theory]
    [InlineData("openGauss")]
    [InlineData("GaussDB")]
    [InlineData("Greenplum")]
    [InlineData("Redshift")]
    [InlineData("CockroachDB")]
    [InlineData("YugabyteDB")]
    [InlineData("TimescaleDB")]
    [InlineData("QuestDB")]
    public void PostgreSqlAliasTest(string provider)
    {
        var db = DbFactory.Create(DatabaseType.PostgreSQL);
        Assert.True(db.Support(provider));

        Assert.Equal(typeof(PostgreSQL), DbFactory.GetProviderType("", provider));
    }

    [Theory]
    [InlineData("HighGo")]
    [InlineData("KingBase")]
    [InlineData("VastBase")]
    public void DedicatedProviderNotHijackedTest(string provider)
    {
        var type = DbFactory.GetProviderType("", provider);
        Assert.NotNull(type);

        // 有专属提供者的库不能被 PostgreSQL 别名抢先匹配
        Assert.NotEqual(typeof(PostgreSQL), type);
    }
}
