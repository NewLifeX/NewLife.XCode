using System.ComponentModel;
using XCode.DataAccessLayer;

namespace XCode.Code;

/// <summary>实体类代码生成选项</summary>
public class EntityBuilderOption : BuilderOption
{
    /// <summary>数据库连接名</summary>
    [Description("数据库连接名")]
    public String? ConnName { get; set; }

    /// <summary>模型类模版。设置后生成模型类，用于接口数据传输，例如{name}Model</summary>
    [Description("模型类模版。设置后生成模型类，用于接口数据传输，例如{name}Model")]
    public String? ModelClass { get; set; }

    /// <summary>模型类输出目录。默认当前目录的Models子目录</summary>
    [Description("模型类输出目录。默认当前目录的Models子目录")]
    public String ModelsOutput { get; set; } = @".\Models\";

    /// <summary>模型接口模版。设置后生成模型接口，用于约束模型类和实体类，例如I{name}</summary>
    [Description("模型接口模版。设置后生成模型接口，用于约束模型类和实体类，例如I{name}")]
    public String? ModelInterface { get; set; }

    /// <summary>模型接口输出目录。默认当前目录的Interfaces子目录</summary>
    [Description("模型接口输出目录。默认当前目录的Interfaces子目录")]
    public String InterfacesOutput { get; set; } = @".\Interfaces\";

    /// <summary>用户实体转为模型类的模型类。例如{name}或{name}DTO</summary>
    [Description("用户实体转为模型类的模型类。例如{name}或{name}DTO")]
    public String? ModelNameForToModel { get; set; }

    /// <summary>命名格式。Default/Upper/Lower/Underline</summary>
    [Description("命名格式。Default/Upper/Lower/Underline")]
    public NameFormats NameFormat { get; set; }

    ///// <summary>生成器版本</summary>
    //[Description("生成器版本")]
    //public String Version { get; set; }

    ///// <summary>帮助文档</summary>
    //[Description("帮助文档")]
    //public String Document { get; set; }

    /// <summary>克隆</summary>
    /// <returns></returns>
    public override BuilderOption Clone()
    {
        var option = (base.Clone() as EntityBuilderOption)!;

        option.ConnName = ConnName;
        option.ModelClass = ModelClass;
        option.ModelsOutput = ModelsOutput;
        option.ModelInterface = ModelInterface;
        option.InterfacesOutput = InterfacesOutput;
        option.ModelNameForToModel = ModelNameForToModel;
        option.NameFormat = NameFormat;

        return option;
    }
}
