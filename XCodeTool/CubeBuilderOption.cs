using System;
using System.ComponentModel;
using XCode.Code;

namespace XCode;

public class CubeBuilderOption : EntityBuilderOption
{
    /// <summary>魔方区域显示名</summary>
    [Description("魔方区域显示名")]
    public String DisplayName { get; set; }

    /// <summary>魔方控制器输出目录</summary>
    [Description("魔方控制器输出目录")]
    public String CubeOutput { get; set; }
}
