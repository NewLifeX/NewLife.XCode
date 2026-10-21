using System;
using System.Globalization;
using NewLife;
using NewLife.Log;
using XCode;
using XCode.Cache;
using Xunit;

namespace XUnitTest.XCode;

[Collection("Database")]
public class PrefixedLogTests
{
    [Fact(DisplayName = "带前缀日志先翻译消息再拼接前缀")]
    public void TranslateBeforePrefix()
    {
        if (LocalizationTestHelper.IsInvariantGlobalization) return;

        var savedCulture = Localization.LogCulture;
        var savedLog = XTrace.Log;
        var savedDebug = CacheBase.Debug;
        try
        {
            Localization.LogCulture = CultureInfo.GetCultureInfo("zh-Hans");
            CacheBase.Debug = true;
            var log = new ProbeLog();
            XTrace.Log = log;

            var cache = new ProbeCache();
            cache.Write("Updating (#{0}), reason: {1}", 3, "manual");

            Assert.Equal("ProbeCache正在更新（第3次），原因：manual", log.Last);
        }
        finally
        {
            XTrace.Log = savedLog;
            CacheBase.Debug = savedDebug;
            Localization.LogCulture = savedCulture;
        }
    }

    [Fact(DisplayName = "运行时日志与提示词条在中文环境下翻译")]
    public void RuntimeKeysTranslate()
    {
        if (LocalizationTestHelper.IsInvariantGlobalization) return;

        var savedCulture = Localization.LogCulture;
        try
        {
            Localization.LogCulture = CultureInfo.GetCultureInfo("zh-Hans");

            Assert.Contains("总次数", Localization.TranslateForLog("Total {0,11:n0}", 5));
            Assert.Equal("连接创建失败！请检查驱动是否正常。", Localization.TranslateForLog("Failed to create a connection! Check that the driver is working properly."));
            Assert.Equal("未登录或令牌无效。", Localization.TranslateForLog("Not logged in or the token is invalid."));
            Assert.Equal("导入模型：X.xml", Localization.TranslateForLog("Importing model: {0}", "X.xml"));
            Assert.Equal("事务[t1]无法重新启动。", Localization.TranslateForLog("Transaction [{0}] cannot be restarted.", "t1"));
            Assert.Equal("InfluxDB 不支持事务。", Localization.TranslateForLog("InfluxDB does not support transactions."));
            Assert.Contains("SonnetDB", Localization.TranslateForLog("SonnetDB does not yet support persisting table and column comments. See SonnetDB issue #237; the capability must be implemented and released."));
        }
        finally
        {
            Localization.LogCulture = savedCulture;
        }
    }

    [Fact(DisplayName = "日志语言与界面语言相互独立")]
    public void LogCultureSeparateFromUiCulture()
    {
        if (LocalizationTestHelper.IsInvariantGlobalization) return;

        var savedCulture = Localization.Culture;
        var savedLogCulture = Localization.LogCulture;
        try
        {
            Localization.Culture = CultureInfo.GetCultureInfo("zh-Hans");
            Localization.LogCulture = CultureInfo.GetCultureInfo("en");

            var english = Localization.TranslateForLog("Total {0,11:n0}", 5);
            Assert.Contains("Total", english);
            Assert.DoesNotContain("总次数", english);

            Localization.LogCulture = CultureInfo.GetCultureInfo("zh-Hans");
            Assert.Contains("总次数", Localization.TranslateForLog("Total {0,11:n0}", 5));
        }
        finally
        {
            Localization.Culture = savedCulture;
            Localization.LogCulture = savedLogCulture;
        }
    }

    [Fact(DisplayName = "普通日志经内置日志器自动翻译")]
    public void PlainWriteLineTranslates()
    {
        if (LocalizationTestHelper.IsInvariantGlobalization) return;

        var savedCulture = Localization.LogCulture;
        var savedLog = XTrace.Log;
        try
        {
            Localization.LogCulture = CultureInfo.GetCultureInfo("zh-Hans");
            var log = new LoggerProbe();
            XTrace.Log = log;

            XTrace.WriteLine("Failed to create a connection! Check that the driver is working properly.");

            Assert.Equal("连接创建失败！请检查驱动是否正常。", log.Last);
        }
        finally
        {
            XTrace.Log = savedLog;
            Localization.LogCulture = savedCulture;
        }
    }

    [Fact(DisplayName = "未提供译文时带前缀日志回退英文")]
    public void FallbackToEnglish()
    {
        if (LocalizationTestHelper.IsInvariantGlobalization) return;

        var savedCulture = Localization.LogCulture;
        var savedLog = XTrace.Log;
        var savedDebug = CacheBase.Debug;
        try
        {
            Localization.LogCulture = CultureInfo.GetCultureInfo("sw-KE");
            CacheBase.Debug = true;
            var log = new ProbeLog();
            XTrace.Log = log;

            var cache = new ProbeCache();
            cache.Write("Updating (#{0}), reason: {1}", 3, "manual");

            Assert.Equal("ProbeCacheUpdating (#3), reason: manual", log.Last);
        }
        finally
        {
            XTrace.Log = savedLog;
            CacheBase.Debug = savedDebug;
            Localization.LogCulture = savedCulture;
        }
    }

    [Fact(DisplayName = "异常消息按界面语言翻译")]
    public void ExceptionUsesCulture()
    {
        if (LocalizationTestHelper.IsInvariantGlobalization) return;

        var savedCulture = Localization.Culture;
        try
        {
            Localization.Culture = CultureInfo.GetCultureInfo("zh-Hans");

            var ex = new XCodeException("No string primary key is defined for [{0}].", "User");

            Assert.Equal("[User]未定义字符串类型的主键。", ex.Message);
        }
        finally
        {
            Localization.Culture = savedCulture;
        }
    }

    [Fact(DisplayName = "31 种语言的词表均已注册")]
    public void AllCulturesAreRegistered()
    {
        if (LocalizationTestHelper.IsInvariantGlobalization) return;

        var savedCulture = Localization.LogCulture;
        var cultures = new[]
        {
            "zh-Hans", "zh-Hant", "es", "de", "ja", "fr", "pt", "ru", "ko", "ar", "bn", "cs", "da", "el", "fa", "fi",
            "he", "hi", "hu", "id", "it", "ms", "nb", "nl", "pl", "ro", "sv", "th", "tr", "uk", "vi",
        };
        var keys = new[]
        {
            "No relation key is set.",
            "Detected secondary cache version change {0} => {1}; expiring local cache",
            "Updating (#{0}), reason: {1}",
        };
        try
        {
            foreach (var culture in cultures)
            {
                Localization.LogCulture = CultureInfo.GetCultureInfo(culture);
                foreach (var key in keys)
                {
                    Assert.NotEqual(key, Localization.TranslateForLog(key));
                }
            }

            // 8 种主要语言已全量翻译，抽查会员、备份恢复、驱动与数据访问日志词条
            var majorCultures = new[] { "de", "es", "fr", "ja", "ko", "pt", "ru", "ar" };
            var majorKeys = new[]
            {
                "Incorrect username or password!",
                "Backup of [{0}/{1}] completed; total [{2:n0}] rows, elapsed {3:n0}ms, speed {4:n0}tps",
                "Slow SQL [{0:n0}ms] {1}",
                "The current database does not support batch operations!",
                "Data connection [{0}] is read-only; {1} is not allowed.",
                "Found a legacy-format Boolean field in Hana: {0} {1}",
            };
            foreach (var culture in majorCultures)
            {
                Localization.LogCulture = CultureInfo.GetCultureInfo(culture);
                foreach (var key in majorKeys)
                {
                    Assert.NotEqual(key, Localization.TranslateForLog(key));
                }
            }
        }
        finally
        {
            Localization.LogCulture = savedCulture;
        }
    }

    private sealed class ProbeCache : CacheBase
    {
        public ProbeCache() => LogPrefix = "ProbeCache";

        public void Write(String format, params Object?[] args) => WriteLog(format, args);
    }

    private sealed class ProbeLog : ILog
    {
        public String? Last { get; private set; }

        public Boolean Enable { get; set; } = true;

        public LogLevel Level { get; set; } = LogLevel.Info;

        public void Write(LogLevel level, String format, params Object?[] args) => Last = args.Length > 0 ? String.Format(format, args) : format;

        public void Debug(String format, params Object?[] args) => Write(LogLevel.Debug, format, args);

        public void Info(String format, params Object?[] args) => Write(LogLevel.Info, format, args);

        public void Warn(String format, params Object?[] args) => Write(LogLevel.Warn, format, args);

        public void Error(String format, params Object?[] args) => Write(LogLevel.Error, format, args);

        public void Fatal(String format, params Object?[] args) => Write(LogLevel.Fatal, format, args);
    }

    private sealed class LoggerProbe : Logger
    {
        public String? Last { get; private set; }

        protected override void OnWrite(LogLevel level, String format, params Object?[] args) => Last = Format(format, args);
    }
}
