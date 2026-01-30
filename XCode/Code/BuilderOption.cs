using System.ComponentModel;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace XCode.Code;

/// <summary>生成器选项</summary>
public class BuilderOption
{
    #region 属性
    /// <summary>类名模板。其中{name}替换为Table.Name，如{name}Model/I{name}Dto等</summary>
    [Description("类名模板。其中{name}替换为Table.Name，如{name}Model/I{name}Dto等")]
    public String? ClassNameTemplate { get; set; }

    /// <summary>显示名模板。其中{displayName}替换为Table.DisplayName</summary>
    [Description("显示名模板。其中{displayName}替换为Table.DisplayName")]
    public String? DisplayNameTemplate { get; set; }

    /// <summary>基类。可能包含基类和接口，其中{name}替换为Table.Name</summary>
    [Description("基类。可能包含基类和接口，其中{name}替换为Table.Name")]
    public String? BaseClass { get; set; }

    /// <summary>命名空间</summary>
    [Description("命名空间")]
    public String? Namespace { get; set; }
    /// <summary>引用命名空间,逗号分隔,区分大小写</summary>
    [Description("引用命名空间")]
    public String? ExtendNameSpace { get; set; }
    /// <summary>输出目录</summary>
    [Description("输出目录")]
    public String? Output { get; set; } = @".\";

    /// <summary>是否使用中文文件名。默认false</summary>
    [Description("是否使用中文文件名。默认false")]
    public Boolean ChineseFileName { get; set; }

    ///// <summary>是否分部类</summary>
    //[Description("是否分部类")]
    //[XmlIgnore, IgnoreDataMember]
    //public Boolean Partial { get; set; }

    /// <summary>用于生成Copy函数的参数类型。例如{name}或I{name}</summary>
    [Description("用于生成Copy函数的参数类型。例如{name}或I{name}")]
    public String? ModelNameForCopy { get; set; }

    /// <summary>带有索引器。实现IModel接口</summary>
    [Description("带有索引器。实现IModel接口")]
    public Boolean HasIModel { get; set; }

    /// <summary>可为null上下文。生成String?等</summary>
    [Description("可为null上下文。生成String?等")]
    public Boolean Nullable { get; set; }

    /// <summary>引用命名空间。区分大小写</summary>
    [XmlIgnore, IgnoreDataMember]
    public ICollection<String> Usings { get; set; } = [];

    ///// <summary>纯净类。去除属性上的Description等特性</summary>
    //[XmlIgnore, IgnoreDataMember]
    //public Boolean Pure { get; set; }

    ///// <summary>纯净接口。不带其它特性</summary>
    //[XmlIgnore, IgnoreDataMember]
    //public Boolean Interface { get; set; }

    /// <summary>排除项。要排除的表或者字段，不区分大小写</summary>
    [XmlIgnore, IgnoreDataMember]
    public ICollection<String> Excludes { get; set; } = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

    /// <summary>扩展数据</summary>
    public IDictionary<String, String> Items { get; set; } = new Dictionary<String, String>();

    /// <summary>模型版本号。用于自动升级配置，不参与XML序列化</summary>
    [XmlIgnore, IgnoreDataMember]
    public Version? ModelVersion { get; set; }

    #endregion 属性

    #region 构造

    /// <summary>实例化</summary>
    public BuilderOption()
    {
        Namespace = GetType().Namespace;

        Usings.Add("System");
        Usings.Add("System.Collections.Generic");
        Usings.Add("System.ComponentModel");
        Usings.Add("System.Runtime.Serialization");
        Usings.Add("System.Web.Script.Serialization");
        Usings.Add("System.Xml.Serialization");
    }

    #endregion 构造

    #region 方法

    //public virtual BuilderOption Clone(BuilderOption option)
    //{

    //}

    /// <summary>克隆</summary>
    /// <returns></returns>
    public virtual BuilderOption Clone()
    {
        var option = (MemberwiseClone() as BuilderOption)!;

        option.Usings = new List<String>(Usings);
        option.Excludes = new HashSet<String>(Excludes, StringComparer.OrdinalIgnoreCase);

        return option;
    }

    #endregion 方法
}