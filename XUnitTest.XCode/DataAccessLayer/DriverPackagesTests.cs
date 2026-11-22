using System;
using System.ComponentModel;
using System.Linq;
using NewLife;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>驱动 NuGet 包索引测试。驱动缺失时的异常提示必须给出正确的包名</summary>
public class DriverPackagesTests
{
    [Theory]
    [InlineData(DatabaseType.SqlServer, "System.Data.SqlClient")]
    [InlineData(DatabaseType.MySql, "NewLife.MySql")]
    [InlineData(DatabaseType.PostgreSQL, "Npgsql")]
    [InlineData(DatabaseType.Snowflake, "Snowflake.Data")]
    [InlineData(DatabaseType.Spanner, "Google.Cloud.Spanner.Data")]
    [InlineData(DatabaseType.YashanDB, "Yashandb.Data.YashandbClient")]
    [InlineData(DatabaseType.MonetDB, "MonetDb.Mapi")]
    [InlineData(DatabaseType.TDengine, "NewLife.TDengine")]
    [DisplayName("按数据库类型取推荐 NuGet 包")]
    public void GetByType(DatabaseType type, String expected) => Assert.Equal(expected, DriverPackages.Get(type));

    [Theory]
    [InlineData("Snowflake.Data.dll", "Snowflake.Data")]
    [InlineData("Google.Cloud.Spanner.Data.dll", "Google.Cloud.Spanner.Data")]
    [InlineData("NewLife.Presto.dll", "NewLife.Presto")]
    [InlineData("NewLife.TDengine.dll", "NewLife.TDengine")]
    [InlineData(@"C:\plugins\Npgsql.dll", "Npgsql")]
    [InlineData("Unknown.Data.dll", null)]
    [DisplayName("按驱动程序集文件名取推荐 NuGet 包")]
    public void GetByAssembly(String file, String expected) => Assert.Equal(expected, DriverPackages.Get(file));

    [Theory]
    [InlineData(DatabaseType.Presto, "NewLife.Presto")]
    [InlineData(DatabaseType.TDengine, "NewLife.TDengine")]
    [DisplayName("独立驱动包优先于内置回退")]
    public void FactoryFromPackage(DatabaseType type, String assembly)
    {
        var db = DbFactory.Create(type);
        Assert.NotNull(db);

        // 只创建工厂，不连接服务；包缺失时会回退旧驱动或抛异常，此断言即可暴露
        db.ConnectionString = type == DatabaseType.Presto ? "Host=localhost" : "Server=localhost";

        var factory = db.Factory;
        Assert.NotNull(factory);
        Assert.Equal(assembly, factory!.GetType().Assembly.GetName().Name);
    }

    [Fact(DisplayName = "已注册数据库均能给出驱动包提示")]
    public void AllRegisteredDatabasesCovered()
    {
        // 这些提供者自带工厂，不依赖外部 NuGet 驱动
        var selfContained = new[] { DatabaseType.Network, DatabaseType.InfluxDB, DatabaseType.OpenTSDB, DatabaseType.IoTDB, DatabaseType.Prometheus };
        foreach (var type in Enum.GetValues<DatabaseType>())
        {
            if (type is DatabaseType.None || selfContained.Contains(type)) continue;
            if (DbFactory.Create(type) == null) continue;

            Assert.False(DriverPackages.Get(type).IsNullOrEmpty(), $"{type} 缺少 NuGet 包提示");
        }
    }
}
