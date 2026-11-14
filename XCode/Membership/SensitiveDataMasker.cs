using System;
using System.Text.RegularExpressions;
using NewLife;
using XCode.Configuration;

namespace XCode.Membership;

/// <summary>敏感数据掩码。审计日志写入前掩码密钥、口令与令牌，避免凭据进入数据库。</summary>
/// <remarks>
/// 日志备注可能保存请求报文（JSON）或查询串，其中包含口令、密钥与令牌。
/// 日志列表可被有查看权限的账号访问，也可能被导出，因此必须在写入前脱敏，而不是仅在页面隐藏。
/// 兼容两类写法：JSON 的 <c>"Secret":"..."</c> 与查询串/键值文本的 <c>secret=...</c>。
/// 只识别“凭据字段名 + 分隔符 + 值”的形态，普通叙述文本不受影响。
/// </remarks>
public static class SensitiveDataMasker
{
    /// <summary>掩码值</summary>
    public const String Mask = "***";

    // 归一化（忽略大小写、下划线与连字符）后以这些词结尾的字段名视为凭据。
    // 不含 Key/Sign/Mobile/Mail/IP 等业务字段，避免把审计需要的可读信息一并掩掉。
    private static readonly String[] _suffixes = ["password", "pwd", "secret", "token", "apikey", "privatekey"];

    // pass 容易与 bypass/compass 等普通单词混淆，只接受完整字段名
    private static readonly String[] _names = ["pass"];

    // pass 必须是完整字段名；其余按后缀匹配，兼容 AppSecret/AccessToken/client_secret 等常见写法
    private const String _namePattern = "pass|(?:[A-Za-z0-9_\\-]*(?:password|pwd|secret|token|api[-_]?key|private[-_]?key))";

    private static readonly Regex _jsonField = new(
        "(?i)(\"(?:" + _namePattern + ")\"\\s*:\\s*\")(?:[^\"\\\\]|\\\\.)*(\")",
        RegexOptions.Compiled);

    // \b 在 access_token= 中不成立（下划线属于单词字符），改用负向后行断言
    private static readonly Regex _keyValue = new(
        "(?i)(?<![\\w\\-])((?:" + _namePattern + ")\\s*=\\s*)([^\\s,;&}\\]]+)",
        RegexOptions.Compiled);

    /// <summary>掩码备注中的密钥、口令与令牌，保留字段名与其余可读内容</summary>
    /// <param name="remark">原始备注，可能为 null 或空字符串</param>
    /// <returns>脱敏后的备注；入参为空时原样返回</returns>
    /// <example>
    /// <code>
    /// var remark = SensitiveDataMasker.Redact("{\"Code\":\"app\",\"Secret\":\"$md5$1$ABC\"}");
    /// // {"Code":"app","Secret":"***"}
    /// </code>
    /// </example>
    public static String Redact(String remark)
    {
        if (remark.IsNullOrEmpty()) return remark;

        // 先处理 JSON 报文，再处理查询串与键值文本
        var text = _jsonField.Replace(remark, "$1" + Mask + "$2");
        return _keyValue.Replace(text, "$1" + Mask);
    }

    /// <summary>判断字段名是否属于口令、密钥或令牌</summary>
    /// <param name="name">字段名，如 Password、AppSecret、access_token</param>
    /// <returns>是否属于凭据类字段</returns>
    public static Boolean IsSensitiveName(String name)
    {
        if (name.IsNullOrEmpty()) return false;

        // 归一化，兼容 AccessToken/access_token/access-token 等写法
        var key = name.Replace("_", null).Replace("-", null).ToLowerInvariant();
        foreach (var item in _names)
        {
            if (key == item) return true;
        }
        foreach (var item in _suffixes)
        {
            if (key.Length >= item.Length && key.EndsWith(item, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    /// <summary>判断实体字段是否需要在日志中脱敏</summary>
    /// <remarks>除凭据名称外，显式标记为只写字段（<c>ItemType=password</c>）的字段同样不得进入日志</remarks>
    /// <param name="field">字段元数据</param>
    /// <returns>是否需要脱敏</returns>
    public static Boolean IsSensitiveField(FieldItem field)
    {
        if (field == null) return false;

        var itemType = field.Field?.ItemType ?? field.Column?.ItemType;
        if (itemType.EqualIgnoreCase("password")) return true;

        return IsSensitiveName(field.Name);
    }
}
