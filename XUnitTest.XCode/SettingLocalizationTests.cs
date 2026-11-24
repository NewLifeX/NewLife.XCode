using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using NewLife;
using XCode;
using Xunit;

namespace XUnitTest.XCode;

/// <summary>XCode 设置多语言测试</summary>
/// <remarks>Language 表由模块初始化注册，Culture 为进程级状态，禁止并行。</remarks>
[Collection("Localization")]
public class SettingLocalizationTests : IDisposable
{
    private readonly CultureInfo? _old = Localization.Culture;

    /// <summary>恢复原始目标语言</summary>
    public void Dispose() => Localization.Culture = _old;

    private static void LoadAssemblies()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(XCodeSetting).Module.ModuleHandle);
    }

    [Fact(DisplayName = "XCode 设置描述支持中文与繁体翻译")]
    public void XCodeSettingDescriptions()
    {
        if (LocalizationTestHelper.IsInvariantGlobalization) return;

        LoadAssemblies();

        Localization.Culture = new CultureInfo("zh-Hans");
        Assert.Equal("SQL日志。把执行的SQL语句写入日志，默认开启；日志量过大时可在生产环境关闭", Localization.Translate("SQL logging. Writes executed SQL statements to the log; enabled by default. Turn it off in production when log volume is too high"));
        Assert.Equal("SQL日志脱敏。按脱敏模式掩码SQL日志与异常中的口令、密钥和令牌，默认开启", Localization.Translate("SQL log masking. Masks passwords, secrets and tokens in SQL logs and exceptions according to the masking mode; enabled by default"));
        Assert.Equal("XCode设置", Localization.Translate("XCode settings"));

        Localization.Culture = new CultureInfo("zh-Hant");
        Assert.Equal("XCode 設定", Localization.Translate("XCode settings"));
    }

    [Fact(DisplayName = "XCode 设置分类与描述支持全部语言")]
    public void XCodeSettingLanguages()
    {
        if (LocalizationTestHelper.IsInvariantGlobalization) return;

        LoadAssemblies();

        Localization.Culture = new CultureInfo("de");
        Assert.Equal("Zwischenspeicher", Localization.Translate("Cache"));
        Assert.Equal("Ablauf des Abfragecaches (s). Zwischenspeichert Abfrageergebnisse nach SQL; 0 deaktiviert die Zwischenspeicherung; Standard 0", Localization.Translate("Query cache expiration (s). Caches query results by SQL; 0 disables caching; default 0"));

        Localization.Culture = new CultureInfo("ja");
        Assert.Equal("キャッシュ", Localization.Translate("Cache"));
        Assert.Equal("XCode 設定", Localization.Translate("XCode settings"));

        Localization.Culture = new CultureInfo("ar");
        Assert.Equal("إعدادات XCode", Localization.Translate("XCode settings"));

        Localization.Culture = new CultureInfo("ru");
        Assert.Equal("Кэш", Localization.Translate("Cache"));
    }

    [Fact(DisplayName = "XCode 设置项均带分类标签")]
    public void XCodeSettingFieldsHaveCategory()
    {
        foreach (var property in typeof(XCodeSetting).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.DeclaringType != typeof(XCodeSetting)) continue;

            var category = property.GetCustomAttribute<CategoryAttribute>()?.Category;
            Assert.False(category.IsNullOrEmpty(), $"{property.Name} 缺少分类");
        }
    }

    [Fact(DisplayName = "XCode 设置项中文译文用全角句号分隔名称与说明")]
    public void XCodeSettingTitlesSeparated()
    {
        if (LocalizationTestHelper.IsInvariantGlobalization) return;

        LoadAssemblies();
        Localization.Culture = new CultureInfo("zh-Hans");

        foreach (var property in typeof(XCodeSetting).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.DeclaringType != typeof(XCodeSetting)) continue;

            var description = property.GetCustomAttribute<DescriptionAttribute>()?.Description;
            if (description.IsNullOrEmpty()) continue;

            var text = Localization.Translate(description);
            Assert.NotEqual(description, text);

            var index = text.IndexOf('。');
            Assert.True(index > 0, $"{property.Name} 的中文译文缺少全角句号分隔：{text}");
        }
    }

    [Fact(DisplayName = "XCode 设置词条在 31 种语言下均有译文")]
    public void AllCulturesHaveSettingsTranslations()
    {
        if (LocalizationTestHelper.IsInvariantGlobalization) return;

        LoadAssemblies();

        var cultures = new[]
        {
            "zh-Hans", "zh-Hant", "es", "de", "ja", "fr", "pt", "ru", "ko", "ar", "bn", "cs", "da", "el", "fa", "fi",
            "he", "hi", "hu", "id", "it", "ms", "nb", "nl", "pl", "ro", "sv", "th", "tr", "uk", "vi",
        };
        var keys = new[]
        {
            "SQL log masking. Masks passwords, secrets and tokens in SQL logs and exceptions according to the masking mode; enabled by default",
            "Query cache expiration (s). Caches query results by SQL; 0 disables caching; default 0",
            "XCode settings",
        };
        foreach (var culture in cultures)
        {
            Localization.Culture = CultureInfo.GetCultureInfo(culture);
            foreach (var key in keys)
            {
                Assert.NotEqual(key, Localization.Translate(key));
            }
        }
    }

    [Fact(DisplayName = "实体元数据保持项目母语，不参与词表翻译")]
    public void EntityMetadataIsNotLocalized()
    {
        var userType = typeof(global::XCode.Membership.User);
        var desc = userType.GetCustomAttribute<DescriptionAttribute>()?.Description;
        Assert.StartsWith("用户", desc);

        var display = userType.GetProperty("Name")!.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;
        Assert.Equal("名称", display);
    }
}

/// <summary>多语言测试集合。Localization.Culture 是进程级全局状态，禁止与其它测试并行</summary>
[CollectionDefinition("Localization", DisableParallelization = true)]
public class LocalizationCollection { }
