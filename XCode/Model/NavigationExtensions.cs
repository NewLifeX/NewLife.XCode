using System.Linq.Expressions;
using System.Reflection;
using NewLife.Reflection;
using XCode.Configuration;

namespace XCode.Model;

/// <summary>导航属性 Fluent API 扩展。提供 HasOne/HasMany 链式配置方法</summary>
/// <remarks>
/// 在实体静态构造或应用启动时调用，向 NavigationRegistry 注册关系。
/// <code>
/// // 在 User 实体静态构造中
/// Meta.Factory.HasOne&lt;User, Role&gt;(u => u.RoleId, r => r.Id);
/// Meta.Factory.HasMany&lt;User, Order&gt;(u => u.Id, o => o.UserId);
/// </code>
/// </remarks>
public static class NavigationExtensions
{
    /// <summary>配置一对一/多对一引用导航</summary>
    /// <typeparam name="TSource">源实体类型（声明导航属性的实体）</typeparam>
    /// <typeparam name="TTarget">目标实体类型（关联的实体）</typeparam>
    /// <param name="factory">源实体工厂（通过 Meta.Factory 获取）</param>
    /// <param name="foreignKeySelector">外键选择器，指向源实体中存储关联键的属性</param>
    /// <param name="primaryKeySelector">主键选择器，指向目标实体的主键属性。不指定时自动取目标实体的主键</param>
    /// <returns>注册的导航属性</returns>
    public static NavigationProperty HasOne<TSource, TTarget>(
        this IEntityFactory factory,
        Expression<Func<TSource, Object?>> foreignKeySelector,
        Expression<Func<TTarget, Object?>>? primaryKeySelector = null)
        where TSource : IEntity
        where TTarget : IEntity
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        if (foreignKeySelector == null) throw new ArgumentNullException(nameof(foreignKeySelector));

        var fkMember = GetMemberExpression(foreignKeySelector);
        var fkField = GetFieldItem(typeof(TSource), fkMember.Member.Name);

        var fkExpr = foreignKeySelector.ToString();

        FieldItem? pkField = null;
        String? pkExpr = null;
        if (primaryKeySelector != null)
        {
            var pkMember = GetMemberExpression(primaryKeySelector);
            pkField = GetFieldItem(typeof(TTarget), pkMember.Member.Name);
            pkExpr = primaryKeySelector.ToString();
        }
        else
        {
            // 自动取目标实体的主键
            var targetFactory = typeof(TTarget).AsFactory();
            pkField = targetFactory?.Unique;
        }

        // 自动推导导航名称：外键名去掉 Id/ID 后缀
        var navName = fkMember.Member.Name.TrimEnd("Id", "ID");
        if (navName.IsNullOrEmpty()) navName = typeof(TTarget).Name;

        var nav = new NavigationProperty
        {
            Name = navName,
            Type = NavigationType.HasOne,
            SourceType = typeof(TSource),
            TargetType = typeof(TTarget),
            ForeignKey = fkField,
            PrimaryKey = pkField,
            ForeignKeyExpression = fkExpr,
            PrimaryKeyExpression = pkExpr,
        };

        NavigationRegistry.Global.Register(nav);

        return nav;
    }

    /// <summary>配置一对多集合导航</summary>
    /// <typeparam name="TSource">源实体类型（声明导航属性的实体，"一"方）</typeparam>
    /// <typeparam name="TTarget">目标实体类型（子实体，"多"方）</typeparam>
    /// <param name="factory">源实体工厂</param>
    /// <param name="primaryKeySelector">源实体主键选择器</param>
    /// <param name="foreignKeySelector">目标实体外键选择器，指向子实体中关联回源实体的字段</param>
    /// <returns>注册的导航属性</returns>
    public static NavigationProperty HasMany<TSource, TTarget>(
        this IEntityFactory factory,
        Expression<Func<TSource, Object?>> primaryKeySelector,
        Expression<Func<TTarget, Object?>> foreignKeySelector)
        where TSource : IEntity
        where TTarget : IEntity
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        if (primaryKeySelector == null) throw new ArgumentNullException(nameof(primaryKeySelector));
        if (foreignKeySelector == null) throw new ArgumentNullException(nameof(foreignKeySelector));

        var pkMember = GetMemberExpression(primaryKeySelector);
        var pkField = GetFieldItem(typeof(TSource), pkMember.Member.Name);

        var fkMember = GetMemberExpression(foreignKeySelector);
        var fkField = GetFieldItem(typeof(TTarget), fkMember.Member.Name);

        // 自动推导导航名称：目标实体名复数形式
        var navName = typeof(TTarget).Name;
        // 简单复数化：加 s
        if (!navName.EndsWith("s") && !navName.EndsWith("S"))
            navName += "s";

        var nav = new NavigationProperty
        {
            Name = navName,
            Type = NavigationType.HasMany,
            SourceType = typeof(TSource),
            TargetType = typeof(TTarget),
            ForeignKey = fkField,
            PrimaryKey = pkField,
            ForeignKeyExpression = foreignKeySelector.ToString(),
            PrimaryKeyExpression = primaryKeySelector.ToString(),
        };

        NavigationRegistry.Global.Register(nav);

        return nav;
    }

    /// <summary>通过字符串字段名配置一对一/多对一引用导航（无泛型表达式版本）</summary>
    /// <param name="factory">源实体工厂</param>
    /// <param name="foreignKeyName">源实体外键字段名</param>
    /// <param name="targetType">目标实体类型</param>
    /// <param name="primaryKeyName">目标实体主键字段名，不指定时自动取主键</param>
    /// <param name="navigationName">导航名称，不指定时自动推导</param>
    /// <returns>注册的导航属性</returns>
    public static NavigationProperty HasOne(
        this IEntityFactory factory,
        String foreignKeyName,
        Type targetType,
        String? primaryKeyName = null,
        String? navigationName = null)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        if (foreignKeyName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(foreignKeyName));
        if (targetType == null) throw new ArgumentNullException(nameof(targetType));

        var fkField = factory.Table.FindByName(foreignKeyName);
        var targetFactory = targetType.AsFactory();
        var pkField = !primaryKeyName.IsNullOrEmpty()
            ? targetFactory?.Table.FindByName(primaryKeyName!)
            : targetFactory?.Unique;

        var navName = navigationName;
        if (navName.IsNullOrEmpty())
        {
            navName = foreignKeyName.TrimEnd("Id", "ID");
            if (navName.IsNullOrEmpty()) navName = targetType.Name;
        }

        var nav = new NavigationProperty
        {
            Name = navName!,
            Type = NavigationType.HasOne,
            SourceType = factory.EntityType,
            TargetType = targetType,
            ForeignKey = fkField,
            PrimaryKey = pkField,
        };

        NavigationRegistry.Global.Register(nav);

        return nav;
    }

    /// <summary>通过字符串字段名配置一对多集合导航（无泛型表达式版本）</summary>
    /// <param name="factory">源实体工厂</param>
    /// <param name="primaryKeyName">源实体主键字段名</param>
    /// <param name="targetType">目标实体类型</param>
    /// <param name="foreignKeyName">目标实体外键字段名</param>
    /// <param name="navigationName">导航名称，不指定时自动推导</param>
    /// <returns>注册的导航属性</returns>
    public static NavigationProperty HasMany(
        this IEntityFactory factory,
        String primaryKeyName,
        Type targetType,
        String foreignKeyName,
        String? navigationName = null)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        if (primaryKeyName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(primaryKeyName));
        if (targetType == null) throw new ArgumentNullException(nameof(targetType));
        if (foreignKeyName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(foreignKeyName));

        var pkField = factory.Table.FindByName(primaryKeyName);
        var targetFactory = targetType.AsFactory();
        var fkField = targetFactory?.Table.FindByName(foreignKeyName);

        var navName = navigationName;
        if (navName.IsNullOrEmpty())
        {
            navName = targetType.Name;
            if (!navName.EndsWith("s") && !navName.EndsWith("S"))
                navName += "s";
        }

        var nav = new NavigationProperty
        {
            Name = navName!,
            Type = NavigationType.HasMany,
            SourceType = factory.EntityType,
            TargetType = targetType,
            ForeignKey = fkField,
            PrimaryKey = pkField,
        };

        NavigationRegistry.Global.Register(nav);

        return nav;
    }

    /// <summary>获取 Lambda 表达式的成员表达式</summary>
    private static MemberExpression GetMemberExpression<TDelegate>(Expression<TDelegate> expression)
    {
        var body = expression.Body;
        // 处理 Convert 节点（如装箱）
        if (body.NodeType == ExpressionType.Convert)
            body = ((UnaryExpression)body).Operand;

        if (body is MemberExpression memberExpr)
            return memberExpr;

        throw new ArgumentException($"表达式必须是成员访问表达式，当前为 {body.NodeType}");
    }

    /// <summary>获取类型上指定名称的 FieldItem（数据字段）</summary>
    private static FieldItem GetFieldItem(Type entityType, String propertyName)
    {
        var factory = entityType.AsFactory();
        if (factory == null)
            throw new InvalidOperationException($"无法获取实体工厂: {entityType.FullName}");

        var field = factory.Table.FindByName(propertyName);
        if (field is null)
            throw new InvalidOperationException($"实体 {entityType.Name} 中未找到字段: {propertyName}");

        return field;
    }
}
