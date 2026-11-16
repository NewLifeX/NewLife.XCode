using System.IO;
using System.Security.Cryptography;
using NewLife;
using NewLife.Common;
using NewLife.Configuration;
using NewLife.Log;
using NewLife.Security;
using XCode.Configuration;
using Xunit;

namespace XUnitTest.XCode.Configuration;

public class DbConfigProviderTests
{
    readonly IConfigProvider _provider;

    public DbConfigProviderTests() => _provider = new DbConfigProvider { UserId = 1234 };

    [Fact(DisplayName = "本地缓存密钥直接取分类名，便于外部工具离线解密")]
    public void GetCacheKey_DerivesPerCategory()
    {
        var key1 = DbConfigProvider.GetCacheKey("Alpha");
        var key2 = DbConfigProvider.GetCacheKey("Beta");

        Assert.Equal("Alpha".GetBytes(), key1);
        Assert.NotEqual(key1, key2);
    }

    [Fact(DisplayName = "加密缓存按分类名密钥 AES-CBC 存储，外部工具可直接解密")]
    public void EncryptedCache_CompatibleWithCategoryKey()
    {
        var category = "TestCfg";
        var provider = new DbConfigProvider { UserId = 0, Category = category, CacheLevel = ConfigCacheLevel.Encrypted };

        // 按外部工具的方式加密：密钥取分类名，AES-CBC/PKCS7
        var json = "{\"Key\":\"Value\"}";
        var cipher = Aes.Create().Encrypt(json.GetBytes(), category.GetBytes()).ToBase64();

        var file = NewLife.Setting.Current.DataPath.CombinePath($"dbConfig_{category}.json").GetFullPath();
        File.WriteAllText(file.EnsureDirectory(true), cipher);
        try
        {
            provider.Init(null);

            Assert.Equal("Value", provider["Key"]);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void TestLoadAndSave()
    {
        var set = new ConfigModel
        {
            Debug = true,
            LogLevel = LogLevel.Fatal,
            LogPath = "xxx",
            NetworkLog = "255.255.255.255:514",
            TempPath = "yyy",

            Sys = new SysConfig
            {
                Name = "NewLife.Cube",
                DisplayName = "魔方平台",
                Company = "新生命开发团队",
            },
        };

        _provider.Save(set);

        var prv = _provider;
        Assert.NotNull(prv);
        Assert.Equal(set.Debug.ToString().ToLower(), prv["Debug"]);
        Assert.Equal(set.LogLevel + "", prv["LogLevel"]);
        Assert.Equal(set.LogPath, prv["LogPath"]);
        Assert.Equal(set.NetworkLog, prv["NetworkLog"]);
        Assert.Equal(set.LogFileFormat, prv["LogFileFormat"]);
        Assert.Equal(set.TempPath, prv["TempPath"]);
        Assert.Equal(set.PluginPath, prv["PluginPath"]);
        Assert.Equal(set.PluginServer, prv["PluginServer"]);

        Assert.Equal("全局调试。XTrace.Debug", prv.GetSection("Debug").Comment);
        Assert.Equal("系统配置", prv.GetSection("Sys").Comment);
        // 描述文字来自 NewLife.Core 的 DescriptionAttribute，随包版本可能调整语言，按实际特性值断言
        var nameProperty = typeof(SysConfig).GetProperty(nameof(SysConfig.Name))!;
        var nameDescription = (System.ComponentModel.DescriptionAttribute?)System.Attribute.GetCustomAttribute(nameProperty, typeof(System.ComponentModel.DescriptionAttribute));
        Assert.Equal(nameDescription?.Description, prv.GetSection("Sys:Name").Comment);

        var sys = set.Sys;
        Assert.Equal(sys.Name, prv["Sys:Name"]);
        Assert.Equal(sys.DisplayName, prv["Sys:DisplayName"]);
        Assert.Equal(sys.Company, prv["Sys:Company"]);

        var prv2 = new DbConfigProvider { UserId = (_provider as DbConfigProvider).UserId };
        var set2 = prv2.Load<ConfigModel>();

        Assert.NotNull(set2);
        Assert.Equal(set.Debug, set2.Debug);
        Assert.Equal(set.LogLevel, set2.LogLevel);
        Assert.Equal(set.LogPath, set2.LogPath);
        Assert.Equal(set.NetworkLog, set2.NetworkLog);
        Assert.Equal(set.LogFileFormat, set2.LogFileFormat);
        Assert.Equal(set.TempPath, set2.TempPath);
        Assert.Equal(set.PluginPath, set2.PluginPath);
        Assert.Equal(set.PluginServer, set2.PluginServer);

        Assert.Equal("全局调试。XTrace.Debug", prv2.GetSection("Debug").Comment);
        //Assert.Equal("系统配置", prv2.GetSection("Sys").Comment);
        Assert.Equal(nameDescription?.Description, prv2.GetSection("Sys:Name").Comment);

        var sys2 = set2.Sys;
        Assert.NotNull(sys2);
        Assert.Equal(sys.Name, sys2.Name);
        Assert.Equal(sys.DisplayName, sys2.DisplayName);
        Assert.Equal(sys.Company, sys2.Company);
    }
}