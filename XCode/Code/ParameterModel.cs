namespace XCode.Code;

/// <summary>参数模型</summary>
public class ParameterModel
{
    /// <summary>名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>参数名</summary>
    public String ParameterName { get; set; } = null!;

    /// <summary>类型名。简称</summary>
    public String TypeName { get; set; } = null!;

    /// <summary>类型全名。含命名空间</summary>
    public String TypeFullName { get; set; } = null!;

    /// <summary>显示名</summary>
    public String? DisplayName { get; set; }

    /// <summary>描述</summary>
    public String? Description { get; set; }

    /// <summary>是否扩展参数</summary>
    public Boolean Extend { get; set; }
}
