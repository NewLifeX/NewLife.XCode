using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NewLife;
using XCode;
using Xunit;

namespace XUnitTest.XCode;

/// <summary>多语言词表全量覆盖测试</summary>
/// <remarks>逐个校验 31 种语言下每个基准键都有非英文占位译文，并保持占位符一致</remarks>
[Collection("Localization")]
public class LocalesCoverageTests
{
    private static readonly String[] _cultures =
    [
        "zh-Hans", "zh-Hant", "es", "de", "ja", "fr", "pt", "ru", "ko", "ar", "bn", "cs", "da", "el", "fa", "fi",
        "he", "hi", "hu", "id", "it", "ms", "nb", "nl", "pl", "ro", "sv", "th", "tr", "uk", "vi",
    ];

    // Cache 在法语、意大利语、荷兰语、北欧语言与印尼语/马来语中本身就是原生写法
    private static readonly String[] _allowSame = ["{0} format {1}", "Cache"];

    private static readonly Regex _entryPattern = new("^\\s*\"((?:[^\"\\\\]|\\\\.)*)\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"\\s*$");
    private static readonly Regex _escapePattern = new("\\\\(u[0-9a-fA-F]{4}|.)");
    private static readonly Regex _placeholderPattern = new("\\{(\\d+)(?:,-?\\d+)?(?::[^}]*)?\\}");

    [Fact(DisplayName = "31 种语言词表覆盖全部基准键且无未翻译占位")]
    public void AllKeysTranslated()
    {
        // 测试宿主不会自动执行模块初始化器，手动注册 XCode 词表
        RuntimeHelpers.RunModuleConstructor(typeof(XCodeSetting).Module.ModuleHandle);

        var dir = FindLocaleDirectory();
        var baseline = ReadLocale(Path.Combine(dir, "zh-Hans.yml"));
        Assert.NotEmpty(baseline);

        // 元数据键（实体元数据、程序集元数据）允许部分语言回退英文源文
        var metadata = ReadMetadataKeys(dir);

        // 不变全球化模式下源生成器跳过具名区域性注册，译文按英文兜底
        if (LocalizationTestHelper.IsInvariantGlobalization)
        {
            foreach (var item in baseline)
            {
                Assert.Equal(item.Key, Localization.Translate(item.Key));
            }

            return;
        }

        var savedCulture = Localization.Culture;
        try
        {
            foreach (var culture in _cultures)
            {
                Localization.Culture = CultureInfo.GetCultureInfo(culture);

                foreach (var item in baseline)
                {
                    var value = Localization.Translate(item.Key);

                    if (!_allowSame.Contains(item.Key) && !metadata.Contains(item.Key))
                        Assert.False(String.Equals(value, item.Key, StringComparison.Ordinal), $"[{culture}] 未翻译：{item.Key}");

                    var expected = GetPlaceholders(item.Key);
                    var actual = GetPlaceholders(value);
                    Assert.True(expected.SetEquals(actual), $"[{culture}] 占位符不一致：{item.Key} => {value}");
                }
            }
        }
        finally
        {
            Localization.Culture = savedCulture;
        }
    }

    [Fact(DisplayName = "元数据键覆盖全部 31 种语言且无英文占位")]
    public void MetadataKeysCovered()
    {
        var dir = FindLocaleDirectory();
        var metaPath = Path.Combine(dir, "metadata-keys.json");
        Assert.True(File.Exists(metaPath), "缺少实体元数据键清单");

        var keys = ReadMetadataKeys(dir);
        Assert.NotEmpty(keys);

        foreach (var lang in _cultures.Where(e => e != "zh-Hans"))
        {
            var entries = ReadLocale(Path.Combine(dir, lang + ".yml"));
            foreach (var key in keys)
            {
                Assert.True(entries.ContainsKey(key), $"[{lang}] 缺少元数据键：{key}");

                // 同形词（简繁/中日同形、法/意/荷/北欧语原生写法）允许与源文一致
                if (_allowSameMetadata.Contains(key)) continue;

                Assert.False(String.Equals(entries[key], key, StringComparison.Ordinal), $"[{lang}] 元数据键未翻译：{key}");
            }
        }

        // en 承载中文源文的英文译文；英文源文的键（如程序集标题）无需收录
        var en = ReadLocale(Path.Combine(dir, "en.yml"));
        foreach (var key in keys.Where(e => e.Any(IsCjk)))
        {
            Assert.True(en.ContainsKey(key), $"[en] 缺少元数据键：{key}");
        }
    }

    // 与 Tools/Scripts/check-locales.ps1 的 -AllowSameValue 保持一致
    private static readonly String[] _allowSameMetadata =
    [
        "必要", "操作", "成功", "更新者", "管理者", "角色", "角色。主要角色", "描述", "排序", "拼音", "全名", "生日", "修改", "整数", "年", "月", "日", "季度",
    ];

    /// <summary>是否为中日韩统一表意文字</summary>
    /// <param name="ch">字符</param>
    /// <returns>属于 CJK 时返回 true</returns>
    private static Boolean IsCjk(Char ch) => ch >= '\u4e00' && ch <= '\u9fff';

    /// <summary>读取元数据键清单</summary>
    /// <param name="dir">词表目录</param>
    /// <returns>元数据键集合；清单缺失时返回空集合</returns>
    private static HashSet<String> ReadMetadataKeys(String dir)
    {
        var path = Path.Combine(dir, "metadata-keys.json");
        if (!File.Exists(path)) return [];

        // 与 NewLife.Core 一致：{ comment, updated, keys } 对象格式
        using var document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        if (!document.RootElement.TryGetProperty("keys", out var array)) return [];

        var keys = new HashSet<String>(StringComparer.Ordinal);
        foreach (var item in array.EnumerateArray())
        {
            var key = item.GetString();
            if (!String.IsNullOrEmpty(key)) keys.Add(key);
        }

        return keys;
    }

    /// <summary>从测试输出目录向上查找 XCode/Locales 词表目录</summary>
    /// <returns>词表目录绝对路径</returns>
    private static String FindLocaleDirectory()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && !String.IsNullOrEmpty(dir); i++)
        {
            var path = Path.Combine(dir, "XCode", "Locales");
            if (File.Exists(Path.Combine(path, "zh-Hans.yml"))) return path;

            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        throw new DirectoryNotFoundException("未找到 XCode/Locales 词表目录，请在仓库内运行测试");
    }

    /// <summary>读取词表文件</summary>
    /// <param name="path">词表文件路径</param>
    /// <returns>英文源文到译文的映射</returns>
    private static Dictionary<String, String> ReadLocale(String path)
    {
        var entries = new Dictionary<String, String>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
        {
            var match = _entryPattern.Match(line);
            if (match.Success) entries[Unescape(match.Groups[1].Value)] = Unescape(match.Groups[2].Value);
        }

        return entries;
    }

    /// <summary>还原词表中的转义字符</summary>
    /// <param name="text">原文</param>
    /// <returns>还原后的文本</returns>
    private static String Unescape(String text) => _escapePattern.Replace(text, match =>
    {
        var value = match.Groups[1].Value;
        if (value.Length == 5) return ((Char)Convert.ToInt32(value.Substring(1), 16)).ToString();

        return value switch
        {
            "n" => "\n",
            "r" => "\r",
            "t" => "\t",
            "\\" => "\\",
            "\"" => "\"",
            _ => match.Value,
        };
    });

    /// <summary>提取文本中的占位符索引集合</summary>
    /// <param name="text">文本</param>
    /// <returns>占位符索引集合</returns>
    private static HashSet<Int32> GetPlaceholders(String text)
    {
        var set = new HashSet<Int32>();
        foreach (Match match in _placeholderPattern.Matches(text))
        {
            set.Add(Convert.ToInt32(match.Groups[1].Value));
        }

        return set;
    }
}
