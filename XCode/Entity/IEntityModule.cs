using System.Collections;
using System.Collections.Concurrent;
using NewLife;
using NewLife.Reflection;
using XCode.Configuration;

namespace XCode;

/// <summary>实体处理模块</summary>
public interface IEntityModule
{
    /// <summary>为指定实体类初始化模块，返回是否支持</summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    Boolean Init(Type entityType);

    /// <summary>创建实体对象</summary>
    /// <param name="entity"></param>
    /// <param name="forEdit"></param>
    void Create(IEntity entity, Boolean forEdit);

    /// <summary>验证实体对象</summary>
    /// <param name="entity"></param>
    /// <param name="method"></param>
    /// <returns></returns>
    Boolean Valid(IEntity entity, DataMethod method);
}

/// <summary>实体模块集合</summary>
public class EntityModules : IEnumerable<IEntityModule>
{
    #region 全局静态
    /// <summary></summary>
    public static EntityModules Global { get; } = new EntityModules(null);
    #endregion

    #region 属性
    /// <summary>实体类型</summary>
    public Type? EntityType { get; set; }

    /// <summary>模块集合</summary>
    public IEntityModule[] Modules { get; set; } = new IEntityModule[0];
    #endregion

    #region 构造
    /// <summary>实例化实体模块集合</summary>
    /// <param name="entityType"></param>
    public EntityModules(Type? entityType) => EntityType = entityType;
    #endregion

    #region 方法
    /// <summary>添加实体模块</summary>
    /// <param name="module"></param>
    /// <returns></returns>
    public virtual void Add(IEntityModule module)
    {
        // 未指定实体类型表示全局模块，不需要初始化
        var type = EntityType;
        if (type != null)
        {
            // 提前设置字段，加速初始化过程，避免实体模块里面获取字段时，被当前实体类的静态构造函数阻塞
            var fs = type.AsFactory()?.Fields;
            if (fs != null) EntityModule.SetFields(type, fs);
        }

        // 异步添加实体模块，避免死锁。实体类一般在静态构造函数里面添加模块，如果这里同步初始化会非常危险
        //ThreadPool.UnsafeQueueUserWorkItem(s => AddAsync(s as IEntityModule), module);
        var task = Task.Run(() => AddAsync(module));
        task.Wait(100);
    }

    /// <summary>添加实体模块</summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public virtual void Add<T>() where T : IEntityModule, new() => Add(new T());

    private void AddAsync(IEntityModule module)
    {
        // 未指定实体类型表示全局模块，不需要初始化
        var type = EntityType;
        if (type != null && !module.Init(type)) return;

        lock (this)
        {
            var list = new List<IEntityModule>(Modules)
            {
                module
            };

            Modules = list.ToArray();
        }
    }

    /// <summary>创建实体时执行模块</summary>
    /// <param name="entity"></param>
    /// <param name="forEdit"></param>
    public void Create(IEntity entity, Boolean forEdit)
    {
        foreach (var item in Modules)
        {
            item.Create(entity, forEdit);
        }

        if (this != Global) Global.Create(entity, forEdit);
    }

    /// <summary>添删改实体时验证</summary>
    /// <param name="entity"></param>
    /// <param name="method"></param>
    /// <returns></returns>
    public Boolean Valid(IEntity entity, DataMethod method)
    {
        foreach (var item in Modules)
        {
            if (!item.Valid(entity, method)) return false;
        }

        if (this != Global) return Global.Valid(entity, method);

        return true;
    }
    #endregion

    #region IEnumerable<IEntityModule> 成员
    IEnumerator<IEntityModule> IEnumerable<IEntityModule>.GetEnumerator()
    {
        foreach (var item in Modules)
        {
            yield return item;
        }
    }
    #endregion

    #region IEnumerable 成员
    IEnumerator IEnumerable.GetEnumerator() => Modules.GetEnumerator();
    #endregion
}

/// <summary>实体模块基类</summary>
public abstract class EntityModule : IEntityModule
{
    #region IEntityModule 成员
    private readonly Dictionary<Type, Boolean> _Inited = new();
    /// <summary>为指定实体类初始化模块，返回是否支持</summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    public Boolean Init(Type entityType)
    {
        var dic = _Inited;
        if (dic.TryGetValue(entityType, out var b)) return b;
        lock (dic)
        {
            if (dic.TryGetValue(entityType, out b)) return b;

            return dic[entityType] = OnInit(entityType);
        }
    }

    /// <summary>为指定实体类初始化模块，返回是否支持</summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    protected virtual Boolean OnInit(Type entityType) => true;

    /// <summary>创建实体对象</summary>
    /// <param name="entity"></param>
    /// <param name="forEdit"></param>
    public void Create(IEntity entity, Boolean forEdit)
    {
        if (entity != null && Init(entity.GetType()))
            OnCreate(entity, forEdit);
    }

    /// <summary>创建实体对象</summary>
    /// <param name="entity"></param>
    /// <param name="forEdit"></param>
    protected virtual void OnCreate(IEntity entity, Boolean forEdit) { }

    /// <summary>验证实体对象</summary>
    /// <param name="entity"></param>
    /// <param name="method"></param>
    /// <returns></returns>
    public Boolean Valid(IEntity entity, DataMethod method)
    {
        if (entity == null || !Init(entity.GetType())) return true;

        return OnValid(entity, method);
    }

    /// <summary>验证实体对象</summary>
    /// <param name="entity"></param>
    /// <param name="method"></param>
    /// <returns></returns>
    protected virtual Boolean OnValid(IEntity entity, DataMethod method) => true;
    #endregion

    #region 辅助
    /// <summary>设置脏数据项。如果某个键存在并且数据没有脏，则设置</summary>
    /// <param name="fields"></param>
    /// <param name="entity"></param>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <returns>返回是否成功设置了数据</returns>
    protected virtual Boolean SetNoDirtyItem(ICollection<FieldItem> fields, IEntity entity, String name, Object value)
    {
        var fi = fields.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
        if (fi == null) { return false; }
        name = fi.Name;
        if (!entity.IsDirty(name)) return entity.SetItem(name, value);
        return false;
    }

    /// <summary>如果是默认值则覆盖，无视脏数据，此时很可能是新增</summary>
    /// <param name="fields"></param>
    /// <param name="entity"></param>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <returns>返回是否成功设置了数据</returns>
    protected virtual Boolean SetItem(ICollection<FieldItem> fields, IEntity entity, String name, Object value)
    {
        // 没有这个字段，就不想了
        var fi = fields.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
        if (fi == null) return false;
        name = fi.Name;
        // 如果是默认值则覆盖，无视脏数据，此时很可能是新增
        if (fi.Type.IsInt())
        {
            if (entity[name].ToLong() != 0) return false;
        }
        else if (fi.Type == typeof(String))
        {
            if (entity[name] is String str && !str.IsNullOrEmpty()) return false;
        }
        else if (fi.Type == typeof(DateTime))
        {
            if (entity[name] is DateTime dt && dt.Year > 2000) return false;
        }

        return entity.SetItem(name, value);
    }

    private static readonly ConcurrentDictionary<Type, FieldItem[]> _fields = new();
    /// <summary>获取实体类的字段名。带缓存</summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    protected static FieldItem[] GetFields(Type entityType) => _fields.GetOrAdd(entityType, t => t.AsFactory().Fields);

    /// <summary>提前设置字段，加速初始化过程</summary>
    /// <param name="entityType"></param>
    /// <param name="fields"></param>
    public static void SetFields(Type entityType, FieldItem[] fields) => _fields.TryAdd(entityType, fields);
    #endregion
}