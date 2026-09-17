using System.Text.RegularExpressions;

namespace XCode.Membership;

/// <summary>空富文本帮助。识别富文本编辑器遗留的无内容 HTML（如 &lt;p&gt;&lt;br&gt;&lt;/p&gt;），实体保存时清理，避免污染数据库并经 SSO 同步扩散</summary>
/// <remarks>
/// 备注类字段（ItemType=html）由富文本编辑器维护，空内容会序列化为 &lt;p&gt;&lt;br&gt;&lt;/p&gt; 一类无信息 HTML。
/// 这类值并非空字符串，容易在系统间同步时被当作有效数据传递。
/// </remarks>
public static class EmptyHtmlHelper
{
    private static readonly String[] _EmbedTags = ["<img", "<iframe", "<video", "<audio", "<object", "<embed", "<svg"];

    private static readonly Regex _TagRegex = new("<[^>]*>", RegexOptions.Compiled);

    /// <summary>判断 HTML 是否语义为空。仅含空段落、换行、空白等无内容标签时视为空；含图片、音视频等嵌入内容的不算空</summary>
    /// <param name="html">HTML 字符串</param>
    /// <returns>是否语义为空</returns>
    public static Boolean IsEmptyHtml(String html)
    {
        if (html.IsNullOrEmpty()) return false;

        // 含图片、音视频、附件等嵌入内容的不算空
        foreach (var tag in _EmbedTags)
        {
            if (html.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0) return false;
        }

        // 去掉全部标签与占位空白后无可见文本，则视为空
        var text = _TagRegex.Replace(html, "").Replace("&nbsp;", "").Replace("&#160;", "");
        return text.IsNullOrWhiteSpace();
    }
}
