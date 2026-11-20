using System;
using System.IO;
using System.Linq;
using System.Reflection;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>数据库产品目录测试。目录必须覆盖全部内置提供者，并包含协议兼容数据库</summary>
public class DatabaseProductTests
{
    [Fact(DisplayName = "数据库目录覆盖全部已注册提供者")]
    public void CatalogCoversRegisteredProviders()
    {
        foreach (var type in Enum.GetValues<DatabaseType>())
        {
            if (type is DatabaseType.None or DatabaseType.Access or DatabaseType.SqlCe) continue;
            if (DbFactory.Create(type) == null) continue;

            var products = DatabaseProducts.GetByProvider(type);
            Assert.NotEmpty(products);
        }
    }

    [Fact(DisplayName = "实际数据库数量大于通信协议数量")]
    public void ProductCountGreaterThanProtocolCount()
    {
        Assert.Equal(33, DatabaseProducts.ProtocolCount);
        Assert.Equal(55, DatabaseProducts.Count);
        Assert.True(DatabaseProducts.Count > DatabaseProducts.ProtocolCount);
    }

    [Fact(DisplayName = "数据库目录与提供者协议别名保持一致")]
    public void CatalogMatchesProviderAliases()
    {
        AssertAliases<MySql>(DatabaseType.MySql);
        AssertAliases<PostgreSQL>(DatabaseType.PostgreSQL);
    }

    private static void AssertAliases<T>(DatabaseType provider)
    {
        var field = typeof(T).GetField("_ProtocolAliases", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var aliases = (String[])field.GetValue(null)!;
        var products = DatabaseProducts.GetByProvider(provider)
            .Where(e => e.IsProtocolCompatible)
            .Select(e => e.ProtocolAlias!)
            .ToArray();

        Assert.Equal(aliases.OrderBy(e => e), products.OrderBy(e => e));
    }

    [Fact(DisplayName = "README 数据库数量与目录一致")]
    public void ReadmeCountsMatchCatalog()
    {
        var root = FindRepositoryRoot();
        if (root == null) return;

        var text = File.ReadAllText(Path.Combine(root, "README.md"));
        Assert.Contains($"**{DatabaseProducts.ProtocolCount} communication-protocol providers**", text);
        Assert.Contains($"**{DatabaseProducts.Count} databases**", text);
    }

    private static String? FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "README.md")) &&
                Directory.Exists(Path.Combine(dir.FullName, "XCode")))
                return dir.FullName;

            dir = dir.Parent;
        }

        return null;
    }

    [Fact(DisplayName = "协议兼容数据库别名均能被对应提供者识别")]
    public void ProtocolAliasesAreRecognized()
    {
        foreach (var product in DatabaseProducts.All)
        {
            if (!product.IsProtocolCompatible) continue;

            var db = DbFactory.Create(product.Provider);
            Assert.NotNull(db);
            Assert.True(db.Support(product.ProtocolAlias!), $"{product.Name} 的别名 {product.ProtocolAlias} 未被识别");

            var type = DbFactory.GetProviderType("", product.ProtocolAlias);
            Assert.Equal(db.GetType(), type);
        }
    }

    [Theory(DisplayName = "按名称查找数据库产品")]
    [InlineData("TiDB", "TiDB")]
    [InlineData("tidb", "TiDB")]
    [InlineData("MySql", "MySQL")]
    [InlineData("SAP HANA", "SAP HANA")]
    public void FindByName(String name, String expected)
    {
        var product = DatabaseProducts.FindByName(name);

        Assert.NotNull(product);
        Assert.Equal(expected, product.Name);
    }
}
