using System.ComponentModel;

namespace XCode.Membership;

/// <summary>参数数据类型</summary>
public enum ParameterKinds
{
    /// <summary>普通</summary>
    [Description("普通")]
    Normal = 0,

    /// <summary>布尔型</summary>
    [Description("布尔型")]
    Boolean = 3,

    /// <summary>整数</summary>
    [Description("整数")]
    Int = 9,

    /// <summary>浮点数</summary>
    [Description("浮点数")]
    Double = 14,

    /// <summary>时间日期</summary>
    [Description("时间日期")]
    DateTime = 16,

    /// <summary>字符串</summary>
    [Description("字符串")]
    String = 18,

    /// <summary>列表</summary>
    [Description("列表")]
    List = 21,

    /// <summary>哈希</summary>
    [Description("哈希")]
    Hash = 22,
}
