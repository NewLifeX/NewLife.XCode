using System.Collections.Concurrent;
using NewLife;

namespace XCode.Model;

/// <summary>实体对象批量查找器</summary>
/// <remarks>
/// 送入一批主键，然后逐个查询使用
/// </remarks>
public class BatchFinder<TKey, TEntity> where TEntity : Entity<TEntity>, new()
{
    #region 属性
    /// <summary>实体工厂</summary>
    public IEntityFactory Factory { get; set; }

    private readonly List<TKey> _Keys = [];
    /// <summary>主键集合</summary>
    public IList<TKey> Keys => _Keys;

    /// <summary>批量查询数据的回调方法。支持外部自定义，内部默认使用In主键的操作</summary>
    public Func<IList<TKey>, IList<TEntity>>? Callback { get; set; }

    /// <summary>缓存数据</summary>
    public IDictionary<TKey, TEntity> Cache { get; } = new ConcurrentDictionary<TKey, TEntity>();

    /// <summary>批大小。默认500</summary>
    public Int32 BatchSize { get; set; } = 500;

    private Int32 _index = 0;
    #endregion

    #region 构造
    /// <summary>实例化批量查找器</summary>
    public BatchFinder() => Factory = typeof(TEntity).AsFactory();

    /// <summary>实例化批量查找器，并添加keys</summary>
    /// <param name="keys"></param>
    public BatchFinder(IEnumerable<TKey> keys) : this() => Add(keys);
    #endregion

    #region 方法
    /// <summary>添加Keys。可能多次调用，需要去重</summary>
    /// <param name="keys"></param>
    public void Add(IEnumerable<TKey> keys)
    {
        foreach (var item in keys)
        {
            if (item is Int32 n && n == 0) continue;
            if (item is Int64 g && g == 0) continue;
            if (item is String str && str.IsNullOrEmpty()) continue;

            if (!_Keys.Contains(item)) _Keys.Add(item);
        }
    }

    /// <summary>根据Key查找对象</summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public TEntity? FindByKey(TKey key)
    {
        // 先查缓存
        if (Cache.TryGetValue(key, out var entity)) return entity;

        if (key is Int32 n && n == 0) return null;
        if (key is Int64 g && g == 0) return null;
        if (key is String str && str.IsNullOrEmpty()) return null;
        if (!_Keys.Contains(key)) throw new ArgumentOutOfRangeException(nameof(key), key, "error");

        var uk = Factory.Table.FindByName(Factory.Unique) ?? throw new ArgumentNullException(nameof(Factory.Unique), "没有唯一主键");

        // 向前查询
        while (_index < _Keys.Count)
        {
            var ks = _Keys.Skip(_index).Take(BatchSize).ToList();
            var list = Callback != null ?
                Callback(ks) :
                Factory.FindAll(uk.In(ks), null, null, 0, 0).Cast<TEntity>().ToList();
            foreach (var item in list)
            {
                if (item[uk.Name] is TKey key2)
                    Cache[key2] = item;
            }

            // 找找有没有，如果没有则继续查
            if (Cache.TryGetValue(key, out entity)) return entity;

            _index += ks.Count;
        }

        return null;
    }

    /// <summary>索引访问器</summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public TEntity? this[TKey key] => FindByKey(key);
    #endregion
}