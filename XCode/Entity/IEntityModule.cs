using System.Collections;
using System.Collections.Concurrent;
using NewLife;
using NewLife.Reflection;
using XCode.Configuration;
using XCode.Model;

namespace XCode;

/// <summary>查询操作来源</summary>
public enum QueryAction
{
    /// <summary>未知</summary>
    Unknown = 0,

    /// <summary>FindAll查询</summary>
    FindAll = 1,

    /// <summary>FindCount查询</summary>
    FindCount = 2,

    /// <summary>FindSQL查询</summary>
    FindSQL = 3,
}

/// <summary>实体拦截器。在实体的创建、验证、查询、过滤等操作中进行拦截处理</summary>
/// <remarks>
/// 拦截器可以在实体的生命周期中进行横切处理：
/// <para>- Init: 初始化，判断是否支持指定实体类</para>
/// <para>- Create: 创建实体对象时</para>
/// <para>- Valid: 添删改验证时</para>
/// <para>- Query: 查询时修改条件</para>
/// <para>- Filter: 过滤查询结果</para>
/// </remarks>
public interface IEntityInterceptor
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

    /// <summary>查询时修改查询条件</summary>
    /// <param name="factory">实体工厂</param>
    /// <param name="where">查询条件表达式</param>
    /// <param name="action">查询操作来源</param>
    /// <returns>修改后的查询条件</returns>
    Expression Query(IEntityFactory factory, Expression? where, QueryAction action);

    /// <summary>过滤实体列表</summary>
    /// <param name="list">实体列表</param>
    /// <returns>过滤后的实体列表</returns>
    IList<IEntity> Filter(IList<IEntity> list);

    /// <summary>过滤单个实体</summary>
    /// <param name="entity">实体对象</param>
    /// <returns>是否允许访问该实体</returns>
    Boolean Filter(IEntity? entity);
}

/// <summary>实体处理模块。旧版名称，建议使用 IEntityInterceptor</summary>
[Obsolete("请使用 IEntityInterceptor")]
public interface IEntityModule : IEntityInterceptor { }

/// <summary>实体拦截器集合</summary>
public class EntityInterceptors : IEnumerable<IEntityInterceptor>
{
    #region 全局静态
    /// <summary>全局拦截器集合</summary>
    public static EntityInterceptors Global { get; } = new EntityInterceptors(null);
    #endregion


    #region 属性
    /// <summary>实体类型</summary>
    public Type? EntityType { get; set; }

    /// <summary>拦截器集合</summary>
    public IEntityInterceptor[] Interceptors { get; set; } = [];
    #endregion

    #region 构造
    /// <summary>实例化实体拦截器集合</summary>
    /// <param name="entityType"></param>
    public EntityInterceptors(Type? entityType) => EntityType = entityType;
    #endregion

    #region 方法
    /// <summary>添加实体拦截器</summary>
    /// <param name="interceptor"></param>
    /// <returns></returns>
    public virtual void Add(IEntityInterceptor interceptor)
    {
        // 未指定实体类型表示全局拦截器，不需要初始化
        var type = EntityType;
        if (type != null)
        {
            // 提前设置字段，加速初始化过程，避免实体拦截器里面获取字段时，被当前实体类的静态构造函数阻塞
            var fs = type.AsFactory()?.Fields;
            if (fs != null) EntityInterceptor.SetFields(type, fs);
        }

        // 异步添加实体拦截器，避免死锁。实体类一般在静态构造函数里面添加拦截器，如果这里同步初始化会非常危险
        //ThreadPool.UnsafeQueueUserWorkItem(s => AddAsync(s as IEntityInterceptor), interceptor);
        var task = Task.Run(() => AddAsync(interceptor));
        task.Wait(100);
    }

    /// <summary>添加实体拦截器</summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public virtual void Add<T>() where T : IEntityInterceptor, new() => Add(new T());

    private void AddAsync(IEntityInterceptor interceptor)
    {
        // 未指定实体类型表示全局拦截器，不需要初始化
        var type = EntityType;
        if (type != null && !interceptor.Init(type)) return;

        lock (this)
        {
            var list = new List<IEntityInterceptor>(Interceptors)
            {
                interceptor
            };

            Interceptors = list.ToArray();
        }
    }


    /// <summary>创建实体时执行拦截器</summary>
    /// <param name="entity"></param>
    /// <param name="forEdit"></param>
    public void Create(IEntity entity, Boolean forEdit)
    {
        foreach (var item in Interceptors)
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
        foreach (var item in Interceptors)
        {
            if (!item.Valid(entity, method)) return false;
        }

        if (this != Global) return Global.Valid(entity, method);

        return true;
    }

    /// <summary>查询时修改查询条件</summary>
    /// <param name="factory">实体工厂</param>
    /// <param name="where">查询条件表达式</param>
    /// <param name="action">查询操作来源</param>
    /// <returns>修改后的查询条件</returns>
    public Expression? Query(IEntityFactory factory, Expression? where, QueryAction action)
    {
        foreach (var item in Interceptors)
        {
            where = item.Query(factory, where, action);
        }

        if (this != Global) where = Global.Query(factory, where, action);

        return where;
    }

    /// <summary>过滤实体列表</summary>
    /// <param name="list">实体列表</param>
    /// <returns>过滤后的实体列表</returns>
    public IList<T> Filter<T>(IList<T> list) where T : IEntity
    {
        if (list == null || list.Count == 0) return list;

        // 转换为 IEntity 列表进行过滤
        var entityList = list.Cast<IEntity>().ToList();
        IList<IEntity> result = entityList;

        foreach (var item in Interceptors)
        {
            result = item.Filter(result);
            if (result.Count == 0) break;
        }

        if (this != Global && result.Count > 0)
            result = Global.Filter(result);

        // 如果没有过滤，直接返回原列表
        if (result.Count == entityList.Count) return list;

        return result.Cast<T>().ToList();
    }

    /// <summary>过滤单个实体</summary>
    /// <param name="entity">实体对象</param>
    /// <returns>是否允许访问该实体</returns>
    public Boolean Filter(IEntity? entity)
    {
        if (entity == null) return true;


        foreach (var item in Interceptors)
        {
            if (!item.Filter(entity)) return false;
        }

        if (this != Global) return Global.Filter(entity);

        return true;
    }
    #endregion

    #region IEnumerable<IEntityInterceptor> 成员
    IEnumerator<IEntityInterceptor> IEnumerable<IEntityInterceptor>.GetEnumerator()
    {
        foreach (var item in Interceptors)
        {
            yield return item;
        }
    }
    #endregion

    #region IEnumerable 成员
    IEnumerator IEnumerable.GetEnumerator() => Interceptors.GetEnumerator();
    #endregion
}

/// <summary>实体模块集合。旧版名称，建议使用 EntityInterceptors</summary>
[Obsolete("请使用 EntityInterceptors")]
public class EntityModules : EntityInterceptors
{
    /// <summary>全局模块集合</summary>
    public new static EntityModules Global { get; } = new EntityModules(null);

    /// <summary>实例化实体模块集合</summary>
    /// <param name="entityType"></param>
    public EntityModules(Type? entityType) : base(entityType) { }
}

/// <summary>实体拦截器基类</summary>
public abstract class EntityInterceptor : IEntityInterceptor
{
    #region 属性
    /// <summary>自动清理超长字段。默认true</summary>
    public Boolean AutoTrim { get; set; } = true;

    /// <summary>所属拦截器集合的实体类型。非全局拦截器时有效，可用于快速判断</summary>
    protected Type? OwnerEntityType { get; private set; }
    #endregion

    #region IEntityInterceptor 成员
    private readonly Dictionary<Type, Boolean> _Inited = [];

    /// <summary>为指定实体类初始化拦截器，返回是否支持</summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    public Boolean Init(Type entityType)
    {
        var dic = _Inited;
        if (dic.TryGetValue(entityType, out var b)) return b;
        lock (dic)
        {
            if (dic.TryGetValue(entityType, out b)) return b;

            b = OnInit(entityType);
            dic[entityType] = b;

            // 首次初始化成功时，记录实体类型，用于后续快速判断
            if (b && OwnerEntityType == null) OwnerEntityType = entityType;

            return b;
        }
    }

    /// <summary>为指定实体类初始化拦截器，返回是否支持</summary>
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

    /// <summary>查询时修改查询条件</summary>
    /// <param name="factory">实体工厂</param>
    /// <param name="where">查询条件表达式</param>
    /// <param name="action">查询操作来源</param>
    /// <returns>修改后的查询条件</returns>
    public Expression Query(IEntityFactory factory, Expression? where, QueryAction action)
    {
        if (factory == null) return where ?? new WhereExpression();

        // 快速判断：如果已知实体类型且不匹配，直接返回
        var ownerType = OwnerEntityType;
        if (ownerType != null && ownerType != factory.EntityType) return where ?? new WhereExpression();

        // 全局拦截器需要验证 Init
        if (ownerType == null && !Init(factory.EntityType)) return where ?? new WhereExpression();

        return OnQuery(factory, where, action) ?? new WhereExpression();
    }

    /// <summary>查询时修改查询条件</summary>
    /// <param name="factory">实体工厂</param>
    /// <param name="where">查询条件表达式</param>
    /// <param name="action">查询操作来源</param>
    /// <returns>修改后的查询条件</returns>
    protected virtual Expression? OnQuery(IEntityFactory factory, Expression? where, QueryAction action) => where;

    /// <summary>过滤实体列表</summary>
    /// <param name="list">实体列表</param>
    /// <returns>过滤后的实体列表</returns>
    public IList<IEntity> Filter(IList<IEntity> list)
    {
        if (list == null || list.Count == 0) return list;

        // 快速判断：如果已知实体类型且不匹配，直接返回
        var entityType = list[0].GetType();
        var ownerType = OwnerEntityType;
        if (ownerType != null && ownerType != entityType) return list;

        // 全局拦截器需要验证 Init
        if (ownerType == null && !Init(entityType)) return list;

        return OnFilter(list);
    }

    /// <summary>过滤实体列表</summary>
    /// <param name="list">实体列表</param>
    /// <returns>过滤后的实体列表</returns>
    protected virtual IList<IEntity> OnFilter(IList<IEntity> list) => list;

    /// <summary>过滤单个实体</summary>
    /// <param name="entity">实体对象</param>
    /// <returns>是否允许访问该实体</returns>
    public Boolean Filter(IEntity? entity)
    {
        if (entity == null) return true;

        // 快速判断：如果已知实体类型且不匹配，直接返回
        var entityType = entity.GetType();
        var ownerType = OwnerEntityType;
        if (ownerType != null && ownerType != entityType) return true;

        // 全局拦截器需要验证 Init
        if (ownerType == null && !Init(entityType)) return true;

        return OnFilter(entity);
    }

    /// <summary>过滤单个实体</summary>
    /// <param name="entity">实体对象</param>
    /// <returns>是否允许访问该实体</returns>
    protected virtual Boolean OnFilter(IEntity entity) => true;
    #endregion

    #region 辅助
    /// <summary>设置脏数据项。如果某个键存在并且数据没有脏，则设置</summary>
    /// <param name="fields"></param>
    /// <param name="entity"></param>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <returns>返回是否成功设置了数据</returns>
    protected virtual Boolean SetNoDirtyItem(ICollection<FieldItem> fields, IEntity entity, String name, Object? value)
    {
        var fi = fields.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
        if (fi == null) return false;

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
        var fi = fields.FirstOrDefault(e => name.EqualIgnoreCase(e.Name, e.ColumnName));
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

            // 自动清理超长字段
            if (AutoTrim && value is String str2)
            {
                if (fi.Length > 0 && str2.Length > fi.Length) value = str2.Substring(0, fi.Length);
            }
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

/// <summary>实体模块基类。旧版名称，建议使用 EntityInterceptor</summary>
[Obsolete("请使用 EntityInterceptor")]
public abstract class EntityModule : EntityInterceptor { }