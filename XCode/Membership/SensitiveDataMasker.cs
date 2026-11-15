using System;
using System.Text.RegularExpressions;
using NewLife;
using XCode.Configuration;

namespace XCode.Membership;

/// <summary>日志脱敏模式。按严格程度递增，数值越大越严格</summary>
/// <remarks>顺序为 <see cref="Off"/>、<see cref="PasswordOnly"/>、<see cref="Partial"/>、<see cref="Full"/>，可直接按数值比较严格程度</remarks>
public enum LogMaskModes
{
    /// <summary>关闭。凭据原样记录，仅用于本地调试</summary>
    Off = 0,

    /// <summary>仅口令。口令类字段全掩，密钥与令牌原样记录</summary>
    PasswordOnly = 1,

    /// <summary>部分。口令全掩，密钥与令牌保留头尾</summary>
    Partial = 2,

    /// <summary>完全。所有凭据整值掩码，默认</summary>
    Full = 3,
}

/// <summary>敏感数据掩码。审计日志写入前掩码密钥、口令与令牌，避免凭据进入数据库。</summary>
/// <remarks>
/// 日志备注可能保存请求报文（JSON）或查询串，其中包含口令、密钥与令牌。
/// 日志列表可被有查看权限的账号访问，也可能被导出，因此必须在写入前脱敏，而不是仅在页面隐藏。
/// 兼容两类写法：JSON 的 <c>"Secret":"..."</c> 与查询串/键值文本的 <c>secret=...</c>。
/// 只识别“凭据字段名 + 分隔符 + 值”的形态，普通叙述文本不受影响。
/// 脱敏强度由 <see cref="LogMaskModes"/> 控制，口令始终全掩，密钥与令牌可在部分模式下保留头尾。
/// </remarks>
public static class SensitiveDataMasker
{
    /// <summary>完全掩码值</summary>
    public const String Mask = "***";

    /// <summary>部分掩码时头尾保留的字符数</summary>
    public const Int32 KeepLength = 4;

    /// <summary>部分掩码的中间占位，固定长度，不泄漏原值长度</summary>
    public const String PartialMask = "****";

    /// <summary>部分掩码的最短长度。头尾之外至少还要掩盖四位，过短的值直接全掩</summary>
    public static Int32 PartialMinLength => KeepLength * 2 + PartialMask.Length;

    // 归一化（忽略大小写、下划线与连字符）后以这些词结尾的字段名视为凭据。
    // 不含 Key/Sign/Mobile/Mail/IP 等业务字段，避免把审计需要的可读信息一并掩掉。
    private static readonly String[] _suffixes = ["password", "pwd", "secret", "token", "apikey", "privatekey"];

    // pass 容易与 bypass/compass 等普通单词混淆，只接受完整字段名
    private static readonly String[] _names = ["pass"];

    // 口令类字段名。口令在任何模式下都不保留头尾
    private static readonly String[] _passwordSuffixes = ["password", "pwd"];

    // pass 必须是完整字段名；其余按后缀匹配，兼容 AppSecret/AccessToken/client_secret 等常见写法
    private const String _namePattern = "pass|(?:[A-Za-z0-9_\\-]*(?:password|pwd|secret|token|api[-_]?key|private[-_]?key))";

    private static readonly Regex _jsonField = new(
        "(?i)(?<prefix>\"(?<name>" + _namePattern + ")\"\\s*:\\s*\")(?<value>(?:[^\"\\\\]|\\\\.)*)(?<suffix>\")",
        RegexOptions.Compiled);

    // \b 在 access_token= 中不成立（下划线属于单词字符），改用负向后行断言
    private static readonly Regex _keyValue = new(
        "(?i)(?<![\\w\\-])(?<prefix>(?<name>" + _namePattern + ")\\s*=\\s*)(?<value>[^\\s,;&}\\]]+)",
        RegexOptions.Compiled);

    /// <summary>掩码备注中的密钥、口令与令牌，保留字段名与其余可读内容</summary>
    /// <param name="remark">原始备注，可能为 null 或空字符串</param>
    /// <param name="mode">脱敏模式，默认完全掩码</param>
    /// <returns>脱敏后的备注；入参为空或关闭脱敏时原样返回</returns>
    /// <example>
    /// <code>
    /// var remark = "{\"Code\":\"app\",\"Secret\":\"$md5$1793198435$93FCA6D1E7CF4C414E776F6B785EFCE6\"}";
    /// SensitiveDataMasker.Redact(remark);
    /// // {"Code":"app","Secret":"***"}
    /// SensitiveDataMasker.Redact(remark, LogMaskModes.Partial);
    /// // {"Code":"app","Secret":"$md5****FCE6"}
    /// </code>
    /// </example>
    public static String Redact(String remark, LogMaskModes mode = LogMaskModes.Full)
    {
        if (remark.IsNullOrEmpty() || mode == LogMaskModes.Off) return remark;

        // 先处理 JSON 报文，再处理查询串与键值文本
        var text = _jsonField.Replace(remark, m =>
        {
            var value = m.Groups["value"].Value;
            var masked = MaskValue(m.Groups["name"].Value, value, mode);

            // 头尾可能截断 \" 或 \uXXXX 等转义序列，含反斜杠时直接全掩
            if (masked != value && masked != Mask && value.Contains("\\")) masked = Mask;

            return m.Groups["prefix"].Value + masked + m.Groups["suffix"].Value;
        });
        return _keyValue.Replace(text, m => m.Groups["prefix"].Value + MaskValue(m.Groups["name"].Value, m.Groups["value"].Value, mode));
    }

    /// <summary>按字段名与模式掩码单个值</summary>
    /// <param name="name">字段名，如 Password、AppSecret、access_token</param>
    /// <param name="value">原始值</param>
    /// <param name="mode">脱敏模式</param>
    /// <returns>掩码后的值；关闭脱敏或原值为 null 时原样返回</returns>
    public static String? MaskValue(String name, String? value, LogMaskModes mode = LogMaskModes.Full)
    {
        if (value == null || mode == LogMaskModes.Off) return value;

        // 口令必须全掩，部分模式也不保留头尾，避免泄漏口令结构
        if (IsPasswordName(name)) return Mask;

        // 仅口令模式不处理密钥与令牌
        if (mode == LogMaskModes.PasswordOnly) return value;

        return mode == LogMaskModes.Full ? Mask : MaskPartial(value);
    }

    /// <summary>按实体字段与模式掩码单个值</summary>
    /// <remarks>显式标记为只写（<c>ItemType=password</c>）的字段同样按口令处理，任何模式都不保留头尾</remarks>
    /// <param name="field">字段元数据</param>
    /// <param name="value">原始值</param>
    /// <param name="mode">脱敏模式</param>
    /// <returns>掩码后的值；关闭脱敏或原值为 null 时原样返回</returns>
    public static Object? MaskField(FieldItem field, Object? value, LogMaskModes mode = LogMaskModes.Full)
    {
        if (field == null || value == null || mode == LogMaskModes.Off) return value;

        var itemType = field.Field?.ItemType ?? field.Column?.ItemType;
        if (itemType.EqualIgnoreCase("password") || IsPasswordName(field.Name)) return Mask;

        if (mode == LogMaskModes.PasswordOnly) return value;

        return mode == LogMaskModes.Partial && value is String str ? MaskPartial(str) : Mask;
    }

    // 过短的值保留头尾等于暴露大半，直接全掩
    private static String MaskPartial(String value) =>
        value.Length < PartialMinLength ? Mask : value.Substring(0, KeepLength) + PartialMask + value.Substring(value.Length - KeepLength);

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

    /// <summary>判断字段名是否属于口令。口令在任何模式下都不保留头尾</summary>
    /// <param name="name">字段名，如 Password、pwd</param>
    /// <returns>是否属于口令</returns>
    public static Boolean IsPasswordName(String name)
    {
        if (name.IsNullOrEmpty()) return false;

        // 归一化，兼容 old_password/new-pwd 等写法
        var key = name.Replace("_", null).Replace("-", null).ToLowerInvariant();
        if (key == "pass") return true;

        foreach (var item in _passwordSuffixes)
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
