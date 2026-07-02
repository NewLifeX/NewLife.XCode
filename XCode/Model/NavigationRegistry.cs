using System.Collections.Concurrent;
using XCode.Configuration;

namespace XCode.Model;

/// <summary>全局导航属性注册表。存储所有 Fluent API 注册的实体间关系</summary>
/// <remarks>
/// 线程安全，以 (源实体类型, 导航名称) 为键。
/// 典型使用：
/// <code>
/// // 在实体静态构造中注册
/// NavigationRegistry.Global.Register(new NavigationProperty { ... });
/// 
/// // 查询某实体的所有导航属性
/// var navigations = NavigationRegistry.Global.GetNavigations(typeof(User));
/// </code>
/// </remarks>
public class NavigationRegistry
{
    /// <summary>全局默认实例</summary>
    public static NavigationRegistry Global { get; } = new();

    private readonly ConcurrentDictionary<(Type SourceType, String Name), NavigationProperty> _navigations = new();
    private readonly ConcurrentDictionary<Type, List<NavigationProperty>> _bySource = new();

    /// <summary>注册一个导航属性</summary>
    /// <param name="navigation"></param>
    /// <returns>注册成功返回 true，已存在同名导航返回 false</returns>
    public Boolean Register(NavigationProperty navigation)
    {
        if (navigation == null) throw new ArgumentNullException(nameof(navigation));

        var key = (navigation.SourceType, navigation.Name);
        if (!_navigations.TryAdd(key, navigation)) return false;

        _bySource.AddOrUpdate(navigation.SourceType,
            _ => [navigation],
            (_, list) =>
            {
                lock (list)
                {
                    list.Add(navigation);
                }
                return list;
            });

        return true;
    }

    /// <summary>获取指定实体的所有已注册导航属性</summary>
    /// <param name="sourceType">源实体类型</param>
    /// <returns>导航属性列表，未注册时返回空数组</returns>
    public IReadOnlyList<NavigationProperty> GetNavigations(Type sourceType)
    {
        if (sourceType == null) return [];

        if (_bySource.TryGetValue(sourceType, out var list))
        {
            lock (list)
            {
                return list.ToArray();
            }
        }

        return [];
    }

    /// <summary>按名称查找导航属性</summary>
    /// <param name="sourceType">源实体类型</param>
    /// <param name="name">导航名称</param>
    /// <returns>导航属性，未找到返回 null</returns>
    public NavigationProperty? Find(Type sourceType, String name)
    {
        if (sourceType == null || name.IsNullOrEmpty()) return null;

        _navigations.TryGetValue((sourceType, name), out var nav);
        return nav;
    }

    /// <summary>清除所有注册</summary>
    public void Clear()
    {
        _navigations.Clear();
        _bySource.Clear();
    }

    /// <summary>已注册的实体类型数</summary>
    public Int32 SourceTypeCount => _bySource.Count;

    /// <summary>已注册的导航属性总数</summary>
    public Int32 NavigationCount => _navigations.Count;
}
