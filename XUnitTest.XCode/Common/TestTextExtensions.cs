using System;

namespace XUnitTest.XCode;

/// <summary>测试用文本扩展</summary>
internal static class TestTextExtensions
{
    /// <summary>统一换行符为 \n，屏蔽 Windows/Linux 生成内容的换行差异</summary>
    /// <param name="text">原始文本</param>
    /// <returns>统一换行后的文本</returns>
    public static String? NormalizeNewLine(this String? text) => text?.Replace("\r\n", "\n").Replace('\r', '\n');
}