using System;
using System.Globalization;

namespace XUnitTest.XCode;

/// <summary>多语言测试辅助</summary>
internal static class LocalizationTestHelper
{
    /// <summary>是否处于不变全球化模式。该模式只支持不变区域性，无法切换具名语言，译文按英文兜底</summary>
    public static Boolean IsInvariantGlobalization { get; } = Detect();

    private static Boolean Detect()
    {
        try
        {
            return !String.Equals(CultureInfo.GetCultureInfo("zh-Hans").Name, "zh-Hans", StringComparison.OrdinalIgnoreCase);
        }
        catch (CultureNotFoundException)
        {
            return true;
        }
    }
}
