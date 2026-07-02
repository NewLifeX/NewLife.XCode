using XCode.Model;

namespace XCode;

/// <summary>导航属性批量预加载扩展。将 N+1 查询优化为 2 次查询</summary>
/// <remarks>
/// 在已有实体列表上批量加载导航属性，避免逐个延迟加载导致的 N+1 问题。
/// 
/// **与 Include 的区别**：
/// - Include：在查询时声明需要哪个导航属性，框架自动批量加载或 JOIN
/// - LoadNavigation：在已有实体列表上事后批量加载导航属性
/// 
/// <code>
/// // 查询用户列表后，批量加载角色和订单
/// var users = User.FindAll();
/// users.LoadNavigation("Role");
/// users.LoadNavigation("Orders");
/// 
/// // 现在可以直接访问导航属性而不触发逐个查询
/// foreach (var user in users)
/// {
///     var roleName = user.Role?.Name;     // 已缓存
///     var orderCount = user.Orders.Count; // 已缓存
/// }
/// </code>
/// </remarks>
public static class NavigationLoader
{
    /// <summary>批量加载指定导航属性。将 N 个延迟查询合并为 1 次批量查询</summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entities">实体列表</param>
    /// <param name="navigationName">导航属性名</param>
    /// <returns>原列表，支持链式调用</returns>
    public static IList<T> LoadNavigation<T>(this IList<T> entities, String navigationName) where T : IEntity
    {
        if (entities is null || entities.Count == 0) return entities ?? [];
        if (navigationName.IsNullOrEmpty()) return entities;

        var firstType = entities[0].GetType();
        var nav = NavigationRegistry.Global.Find(firstType, navigationName);
        if (nav is null) return entities;

        switch (nav.Type)
        {
            case NavigationType.HasOne:
                LoadHasOne(entities, nav);
                break;
            case NavigationType.HasMany:
                LoadHasMany(entities, nav);
                break;
        }

        return entities;
    }

    /// <summary>批量加载实体上所有已注册的导航属性</summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entities">实体列表</param>
    /// <returns>原列表，支持链式调用</returns>
    public static IList<T> LoadAllNavigations<T>(this IList<T> entities) where T : IEntity
    {
        if (entities is null || entities.Count == 0) return entities ?? [];

        var firstType = entities[0].GetType();
        var navs = NavigationRegistry.Global.GetNavigations(firstType);

        foreach (var nav in navs)
        {
            LoadNavigation(entities, nav.Name);
        }

        return entities;
    }

    /// <summary>批量加载 HasOne 导航</summary>
    private static void LoadHasOne<T>(IList<T> entities, NavigationProperty nav) where T : IEntity
    {
        var targetFactory = nav.TargetType.AsFactory();
        if (targetFactory is null || nav.ForeignKey is null || nav.PrimaryKey is null) return;

        // 收集所有非空外键值
        var fkValues = new HashSet<Object>();
        foreach (var entity in entities)
        {
            var fkValue = entity[nav.ForeignKey.Name];
            if (fkValue is not null && !Equals(fkValue, 0) && !(fkValue is String s && s.IsNullOrEmpty()))
                fkValues.Add(fkValue);
        }

        if (fkValues.Count == 0) return;

        // 批量查询
        var pkCol = nav.PrimaryKey.ColumnName;
        var inClause = fkValues.Select(v => v is String ? $"'{v}'" : v.ToString()).Join(",");
        var relatedList = targetFactory.FindAll($"{pkCol} in({inClause})", null, null, 0, 0);
        if (relatedList is null || relatedList.Count == 0) return;

        // 构建 PK→实体 字典
        var dict = new Dictionary<Object, IEntity>();
        foreach (var rel in relatedList)
        {
            var key = rel[nav.PrimaryKey.Name];
            if (key is not null)
                dict[key] = rel;
        }

        // 填充导航属性到 Extends 缓存
        foreach (var entity in entities)
        {
            if (entity is not EntityBase eb) continue;

            var fkValue = entity[nav.ForeignKey.Name];
            if (fkValue is null) continue;

            if (dict.TryGetValue(fkValue, out var related))
            {
                var extends = ((IEntity)eb).Extends;
                extends?.Get<Object>(nav.Name, _ => related);
            }
        }
    }

    /// <summary>批量加载 HasMany 导航</summary>
    private static void LoadHasMany<T>(IList<T> entities, NavigationProperty nav) where T : IEntity
    {
        var targetFactory = nav.TargetType.AsFactory();
        if (targetFactory is null || nav.ForeignKey is null || nav.PrimaryKey is null) return;

        // 收集所有主键值
        var pkValues = new HashSet<Object>();
        foreach (var entity in entities)
        {
            var pkValue = entity[nav.PrimaryKey.Name];
            if (pkValue is not null && !Equals(pkValue, 0))
                pkValues.Add(pkValue);
        }

        if (pkValues.Count == 0) return;

        // 批量查询子实体
        var fkCol = nav.ForeignKey.ColumnName;
        var inClause = pkValues.Select(v => v is String ? $"'{v}'" : v.ToString()).Join(",");
        var children = targetFactory.FindAll($"{fkCol} in({inClause})", null, null, 0, 0);
        if (children is null || children.Count == 0) return;

        // 按外键分组
        var grouped = new Dictionary<Object, List<IEntity>>();
        foreach (var child in children)
        {
            var fkVal = child[nav.ForeignKey.Name];
            if (fkVal is null) continue;

            if (!grouped.TryGetValue(fkVal, out var group))
                grouped[fkVal] = group = [];

            group.Add(child);
        }

        // 填充到 Extends 缓存
        foreach (var entity in entities)
        {
            if (entity is not EntityBase eb) continue;

            var pkValue = entity[nav.PrimaryKey.Name];
            if (pkValue is null) continue;

            if (grouped.TryGetValue(pkValue, out var childList))
            {
                var extends = ((IEntity)eb).Extends;
                extends?.Get<Object>(nav.Name, _ => childList);
            }
        }
    }
}
