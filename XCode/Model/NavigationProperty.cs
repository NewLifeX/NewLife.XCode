using System.Reflection;
using XCode.Configuration;

namespace XCode.Model;

/// <summary>导航关系类型</summary>
public enum NavigationType
{
    /// <summary>一对一/多对一引用导航</summary>
    HasOne,

    /// <summary>一对多集合导航</summary>
    HasMany,
}

/// <summary>导航属性元数据。描述实体间关联关系</summary>
/// <remarks>
/// 由 Fluent API（HasOne/HasMany）注册到 NavigationRegistry，供导航属性加载器和 LINQ Include 查询使用。
/// 与 Model.xml Map 机制互补：Map 生成单向懒加载引用属性，NavigationProperty 支持双向关系 + 集合导航 + SQL JOIN。
/// </remarks>
public class NavigationProperty
{
    /// <summary>导航名称（属性名）</summary>
    public String Name { get; init; } = null!;

    /// <summary>导航关系类型</summary>
    public NavigationType Type { get; init; }

    /// <summary>源实体类型（声明导航属性的实体）</summary>
    public Type SourceType { get; init; } = null!;

    /// <summary>目标实体类型（关联的实体）</summary>
    public Type TargetType { get; init; } = null!;

    /// <summary>源实体的外键字段（存储关联键的字段）</summary>
    /// <remarks>HasOne 时为源实体FK，HasMany 时为目标实体FK</remarks>
    public FieldItem? ForeignKey { get; init; }

    /// <summary>目标实体的主键字段</summary>
    public FieldItem? PrimaryKey { get; init; }

    /// <summary>源实体外键属性选择器（Lambda 表达式字符串形式，调试用）</summary>
    public String? ForeignKeyExpression { get; init; }

    /// <summary>目标实体主键属性选择器（Lambda 表达式字符串形式，调试用）</summary>
    public String? PrimaryKeyExpression { get; init; }

    /// <summary>导航属性声明所在的实体类型（通常与 SourceType 相同）</summary>
    public Type DeclaringType => SourceType;

    /// <summary>调试显示</summary>
    /// <returns></returns>
    public override String ToString() => $"{SourceType.Name}.{Name} -> {TargetType.Name} ({Type}, FK={ForeignKey?.Name})";
}
