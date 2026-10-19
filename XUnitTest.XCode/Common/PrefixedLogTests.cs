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

    [Fact(DisplayName = "未提供译文时带前缀日志回退英文")]
    public void FallbackToEnglish()
    {
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
}
