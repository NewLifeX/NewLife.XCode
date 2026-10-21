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
        Assert.Equal("输出SQL。是否输出SQL语句，默认启用", Localization.Translate("Output SQL. Whether to output SQL statements, enabled by default"));
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
        Assert.Equal("Daten-Cache. Zwischenspeicherung nach SQL, Standard 0 Sekunden", Localization.Translate("Data cache. Caches by SQL, default 0 seconds"));

        Localization.Culture = new CultureInfo("ja");
        Assert.Equal("キャッシュ", Localization.Translate("Cache"));
        Assert.Equal("XCode 設定", Localization.Translate("XCode settings"));

        Localization.Culture = new CultureInfo("ar");
        Assert.Equal("إعدادات XCode", Localization.Translate("XCode settings"));

        Localization.Culture = new CultureInfo("ru");
        Assert.Equal("Кэш", Localization.Translate("Cache"));
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
            "Output SQL. Whether to output SQL statements, enabled by default",
            "Data cache. Caches by SQL, default 0 seconds",
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
