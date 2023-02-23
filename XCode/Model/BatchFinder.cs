using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

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

    private readonly List<TKey> _Keys = new();
    /// <summary>主键集合</summary>
    public IList<TKey> Keys => _Keys;

    /// <summary>缓存数据</summary>
    public IDictionary<TKey, TEntity> Cache { get; } = new ConcurrentDictionary<TKey, TEntity>();

    /// <summary>批大小</summary>
    public Int32 BatchSize { get; set; } = 500;

    private Int32 _index = 0;
    #endregion

    #region 构造
    /// <summary>实例化批量查找器</summary>
    public BatchFinder() { }

    /// <summary>实例化批量查找器</summary>
    /// <param name="factory"></param>
    public BatchFinder(IEntityFactory factory) => Factory = factory;

    /// <summary>实例化批量查找器</summary>
    /// <returns></returns>
    public static BatchFinder<TKey, TEntity> Create() => new(typeof(TEntity).AsFactory());
    #endregion

    #region 方法
    /// <summary>
    /// 添加Keys
    /// </summary>
    /// <param name="keys"></param>
    public void Add(params TKey[] keys) => _Keys.AddRange(keys);

    /// <summary>根据Key查找对象</summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public TEntity FindByKey(TKey key)
    {
        if (Cache.TryGetValue(key, out var entity)) return entity;

        if (!_Keys.Contains(key)) throw new ArgumentOutOfRangeException(nameof(key));

        var uk = Factory.Table.FindByName(Factory.Unique);

        // 向前查询
        while (_index < _Keys.Count)
        {
            var ks = _Keys.Skip(_index).Take(BatchSize).ToList();
            var list = Factory.FindAll(uk.In(ks), null, null, 0, 0);
            foreach (var item in list)
            {
                Cache[(TKey)item[uk.Name]] = (TEntity)item;
            }

            // 找找有没有，如果没有则继续查
            if (Cache.TryGetValue(key, out entity)) return entity;

            _index += ks.Count;
        }

        return null;
    }
    #endregion
}
