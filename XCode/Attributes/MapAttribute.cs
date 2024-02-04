using NewLife;
using NewLife.Reflection;
using XCode.Configuration;

namespace XCode;

/// <summary>用于指定数据属性映射关系</summary>
[AttributeUsage(AttributeTargets.Property)]
public class MapAttribute : Attribute
{
    #region 属性
    /// <summary>数据列</summary>
    public String Name { get; set; }

    private MapProvider? _Provider;
    /// <summary>目标提供者</summary>
    public MapProvider? Provider { get { return _Provider ??= GetProvider(_Type, _Key); } set { _Provider = value; } }

    private readonly Type? _Type;
    private readonly String? _Key;
    #endregion

    #region 构造
    /// <summary>指定一个表内关联关系</summary>
    /// <param name="column"></param>
    public MapAttribute(String column)
    {
        Name = column;
    }

    /// <summary>指定一个关系</summary>
    /// <param name="name"></param>
    /// <param name="type"></param>
    /// <param name="key"></param>
    public MapAttribute(String name, Type type, String? key = null)
    {
        Name = name;
        _Type = type;
        _Key = key;
    }
    #endregion

    #region 方法
    private MapProvider? GetProvider(Type? type, String? key)
    {
        if (type == null) return null;

        // 区分实体类和提供者
        if (type.As<MapProvider>()) return Activator.CreateInstance(type) as MapProvider;

        if (key.IsNullOrEmpty())
        {
            key = ((type.AsFactory()?.Unique?.Name) ?? throw new ArgumentNullException(nameof(key)));
        }

        return new MapProvider { EntityType = type, Key = key };
    }
    #endregion
}

/// <summary>映射提供者</summary>
public class MapProvider
{
    #region 属性
    /// <summary>实体类型</summary>
    public Type? EntityType { get; set; }

    /// <summary>关联键</summary>
    public String? Key { get; set; }
    #endregion

    #region 方法
    /// <summary>获取数据源</summary>
    /// <returns></returns>
    public virtual IDictionary<Object, String> GetDataSource()
    {
        var fact = EntityType?.AsFactory() ?? throw new ArgumentNullException(nameof(EntityType));

        var key = Key;
        var mst = fact.Master?.Name;

        if (key.IsNullOrEmpty()) key = fact.Unique?.Name;
        if (key.IsNullOrEmpty()) throw new ArgumentNullException("没有设置关联键", nameof(Key));
        if (mst.IsNullOrEmpty()) throw new ArgumentNullException("没有设置主要字段");

        // 修正字段大小写，用户书写Map特性时，可能把字段名大小写写错
        if (fact.Table.FindByName(key) is FieldItem fi)
        {
            key = fi.Name;
        }

        // 数据较少时，从缓存读取
        var list = fact.Session.Count < 1000 ? fact.FindAllWithCache() : fact.FindAll("", null, null, 0, 100);

        //return list.Where(e => e[key] != null).ToDictionary(e => e[key], e => e[mst] + "");
        return list.Where(e => e[key] != null).ToDictionary(e => e[key]!, e => e.ToString());//用ToString()可以显示更多信息 2023-08-11
    }
    #endregion
}