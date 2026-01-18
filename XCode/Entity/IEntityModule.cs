using System.Collections;
using System.Collections.Concurrent;
using NewLife.Reflection;
using XCode.Configuration;

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
    /// <param name="entityType">实体类型</param>
    /// <returns>是否支持该实体类型</returns>
    Boolean Init(Type entityType);

    /// <summary>创建实体对象</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="forEdit">是否用于编辑</param>
    void Create(IEntity entity, Boolean forEdit);

    /// <summary>验证实体对象</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="method">数据操作方法</param>
    /// <returns>验证是否通过</returns>
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
/// <remarks>用于管理多个实体拦截器的执行，支持实体类级别和全局级别的拦截器</remarks>
/// <remarks>实例化实体拦截器集合</remarks>
/// <param name="entityType">实体类型，null表示全局拦截器</param>
public class EntityInterceptors(Type? entityType) : IEnumerable<IEntityInterceptor>
{
    #region 全局静态
    /// <summary>全局拦截器集合，对所有实体生效</summary>
    public static EntityInterceptors Global { get; } = new(null);
    #endregion

    #region 属性
    /// <summary>实体类型，null表示全局拦截器集合</summary>
    public Type? EntityType { get; set; } = entityType;

    /// <summary>拦截器集合</summary>
    public IEntityInterceptor[] Interceptors { get; set; } = [];

    private Boolean IsEmpty => Interceptors.Length == 0;
    #endregion

    #region 方法
    /// <summary>添加实体拦截器</summary>
    /// <param name="interceptor">要添加的拦截器</param>
    /// <remarks>
    /// 添加拦截器时会先初始化其字段信息以加速查询，然后在后台线程中异步注册，避免在静态构造函数中导致死锁
    /// </remarks>
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
    /// <typeparam name="T">拦截器类型，必须实现 IEntityInterceptor 接口</typeparam>
    /// <remarks>会自动创建拦截器实例并添加到集合</remarks>
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
    /// <param name="entity">实体对象</param>
    /// <param name="forEdit">是否用于编辑</param>
    /// <remarks>会先执行实体类级别的拦截器，再执行全局拦截器</remarks>
    public void Create(IEntity entity, Boolean forEdit)
    {
        if (IsEmpty && (this == Global || Global.IsEmpty)) return;

        foreach (var item in Interceptors)
        {
            item.Create(entity, forEdit);
        }

        if (this != Global) Global.Create(entity, forEdit);
    }

    /// <summary>添删改实体时验证</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="method">数据操作方法</param>
    /// <returns>验证是否通过</returns>
    /// <remarks>
    /// 按顺序执行所有拦截器的验证方法，任何一个返回false则立即停止并返回false
    /// 会先执行实体类级别的拦截器，再执行全局拦截器
    /// </remarks>
    public Boolean Valid(IEntity entity, DataMethod method)
    {
        if (IsEmpty && (this == Global || Global.IsEmpty)) return true;

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
    /// <returns>修改后的查询条件，若无条件则返回空的 WhereExpression</returns>
    /// <remarks>
    /// 按顺序执行所有拦截器修改查询条件，每个拦截器的输出作为下一个的输入
    /// 会先执行实体类级别的拦截器，再执行全局拦截器
    /// </remarks>
    public Expression? Query(IEntityFactory factory, Expression? where, QueryAction action)
    {
        if (IsEmpty && (this == Global || Global.IsEmpty)) return where;

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
    /// <remarks>
    /// 按顺序执行所有拦截器的过滤方法，如果列表被清空则停止继续过滤
    /// 会先执行实体类级别的拦截器，再执行全局拦截器
    /// 若没有任何过滤发生，返回原列表；否则返回新的列表
    /// </remarks>
    public IList<T> Filter<T>(IList<T> list) where T : IEntity
    {
        if (list == null || list.Count == 0) return list!;
        if (IsEmpty && (this == Global || Global.IsEmpty)) return list;

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
    /// <remarks>
    /// 按顺序执行所有拦截器的过滤方法，任何一个返回false则立即停止并返回false
    /// 会先执行实体类级别的拦截器，再执行全局拦截器
    /// 若实体为null则返回true
    /// </remarks>
    public Boolean Filter(IEntity? entity)
    {
        if (entity == null) return true;
        if (IsEmpty && (this == Global || Global.IsEmpty)) return true;

        foreach (var item in Interceptors)
        {
            if (!item.Filter(entity)) return false;
        }

        if (this != Global) return Global.Filter(entity);

        return true;
    }
    #endregion

    #region IEnumerable<IEntityInterceptor> 成员
    /// <summary>获取拦截器集合的枚举器</summary>
    /// <returns>拦截器枚举器</returns>
    IEnumerator<IEntityInterceptor> IEnumerable<IEntityInterceptor>.GetEnumerator()
    {
        foreach (var item in Interceptors)
        {
            yield return item;
        }
    }
    #endregion

    #region IEnumerable 成员
    /// <summary>获取拦截器集合的枚举器</summary>
    /// <returns>拦截器枚举器</returns>
    IEnumerator IEnumerable.GetEnumerator() => Interceptors.GetEnumerator();
    #endregion
}

/// <summary>实体模块集合。旧版名称，建议使用 EntityInterceptors</summary>
/// <remarks>实例化实体模块集合</remarks>
/// <param name="entityType">实体类型，null表示全局模块集合</param>
[Obsolete("请使用 EntityInterceptors")]
public class EntityModules(Type? entityType) : EntityInterceptors(entityType)
{
    /// <summary>全局模块集合，对所有实体生效</summary>
    public new static EntityModules Global { get; } = new EntityModules(null);
}

/// <summary>实体拦截器基类</summary>
/// <remarks>
/// 提供实体拦截器的默认实现，子类可重写虚拟方法来自定义拦截逻辑
/// 支持自动字段裁剪、脏数据判断、字段缓存等功能
/// </remarks>
public abstract class EntityInterceptor : IEntityInterceptor
{
    #region 属性
    /// <summary>自动清理超长字符串字段，默认为true</summary>
    /// <remarks>当设置字符串值时，如果超过字段长度限制则自动截断</remarks>
    public Boolean AutoTrim { get; set; } = true;
    #endregion

    #region IEntityInterceptor 成员
    private readonly Dictionary<Type, Boolean> _Inited = [];

    /// <summary>为指定实体类初始化拦截器，返回是否支持</summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>是否支持该实体类型</returns>
    /// <remarks>
    /// 会缓存初始化结果，同一实体类型的重复调用会直接返回缓存结果
    /// 线程安全，首次初始化后会缓存结果用于后续快速判断
    /// </remarks>
    public Boolean Init(Type entityType)
    {
        var dic = _Inited;
        if (dic.TryGetValue(entityType, out var b)) return b;
        lock (dic)
        {
            if (dic.TryGetValue(entityType, out b)) return b;

            b = OnInit(entityType);
            dic[entityType] = b;

            return b;
        }
    }

    /// <summary>为指定实体类初始化拦截器，返回是否支持</summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>是否支持该实体类型，默认返回true</returns>
    /// <remarks>子类可重写此方法实现自定义的初始化逻辑和条件判断</remarks>
    protected virtual Boolean OnInit(Type entityType) => true;

    /// <summary>创建实体对象</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="forEdit">是否用于编辑</param>
    /// <remarks>
    /// 首先验证实体类型是否被支持（通过Init方法），
    /// 如果支持则调用OnCreate虚拟方法进行自定义处理
    /// </remarks>
    public void Create(IEntity entity, Boolean forEdit)
    {
        if (entity != null && Init(entity.GetType()))
            OnCreate(entity, forEdit);
    }

    /// <summary>创建实体对象</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="forEdit">是否用于编辑</param>
    /// <remarks>子类可重写此方法在实体创建时执行自定义逻辑</remarks>
    protected virtual void OnCreate(IEntity entity, Boolean forEdit) { }

    /// <summary>验证实体对象</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="method">数据操作方法</param>
    /// <returns>验证是否通过</returns>
    /// <remarks>
    /// 首先验证实体是否为null和实体类型是否被支持，
    /// 如果验证失败返回true（即放行），否则调用OnValid虚拟方法进行自定义验证
    /// </remarks>
    public Boolean Valid(IEntity entity, DataMethod method)
    {
        if (entity == null || !Init(entity.GetType())) return true;

        return OnValid(entity, method);
    }

    /// <summary>验证实体对象</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="method">数据操作方法</param>
    /// <returns>验证是否通过，默认返回true</returns>
    /// <remarks>子类可重写此方法实现自定义的实体验证逻辑</remarks>
    protected virtual Boolean OnValid(IEntity entity, DataMethod method) => true;

    /// <summary>查询时修改查询条件</summary>
    /// <param name="factory">实体工厂</param>
    /// <param name="where">查询条件表达式</param>
    /// <param name="action">查询操作来源</param>
    /// <returns>修改后的查询条件</returns>
    /// <remarks>
    /// 进行快速判断以避免不必要的处理：
    /// 1. 工厂为null时返回空的WhereExpression
    /// 2. 不支持当前实体类型时直接返回原条件
    /// 然后调用OnQuery虚拟方法进行实际处理，结果为null时返回空的WhereExpression
    /// </remarks>
    public Expression Query(IEntityFactory factory, Expression? where, QueryAction action)
    {
        if (factory == null) return where ?? new WhereExpression();

        // 验证是否支持该实体类型（带缓存）
        if (!Init(factory.EntityType)) return where ?? new WhereExpression();

        return OnQuery(factory, where, action) ?? new WhereExpression();
    }

    /// <summary>查询时修改查询条件</summary>
    /// <param name="factory">实体工厂</param>
    /// <param name="where">查询条件表达式</param>
    /// <param name="action">查询操作来源</param>
    /// <returns>修改后的查询条件，默认返回原条件</returns>
    /// <remarks>子类可重写此方法在查询前修改查询条件</remarks>
    protected virtual Expression? OnQuery(IEntityFactory factory, Expression? where, QueryAction action) => where;

    /// <summary>过滤实体列表</summary>
    /// <param name="list">实体列表</param>
    /// <returns>过滤后的实体列表</returns>
    /// <remarks>
    /// 进行快速判断以避免不必要的处理：
    /// 1. 列表为null或为空时直接返回
    /// 2. 不支持当前实体类型时直接返回原列表
    /// 然后调用OnFilter虚拟方法进行实际过滤
    /// </remarks>
    public IList<IEntity> Filter(IList<IEntity> list)
    {
        if (list == null || list.Count == 0) return list!;

        var entityType = list[0].GetType();
        if (!Init(entityType)) return list;

        return OnFilter(list);
    }

    /// <summary>过滤实体列表</summary>
    /// <param name="list">实体列表</param>
    /// <returns>过滤后的实体列表，默认返回原列表</returns>
    /// <remarks>子类可重写此方法实现自定义的列表过滤逻辑</remarks>
    protected virtual IList<IEntity> OnFilter(IList<IEntity> list) => list;

    /// <summary>过滤单个实体</summary>
    /// <param name="entity">实体对象</param>
    /// <returns>是否允许访问该实体</returns>
    /// <remarks>
    /// 进行快速判断以避免不必要的处理：
    /// 1. 实体为null时返回true（允许访问）
    /// 2. 不支持当前实体类型时返回true（允许访问）
    /// 然后调用OnFilter虚拟方法进行实际判断
    /// </remarks>
    public Boolean Filter(IEntity? entity)
    {
        if (entity == null) return true;

        var entityType = entity.GetType();
        if (!Init(entityType)) return true;

        return OnFilter(entity);
    }

    /// <summary>过滤单个实体</summary>
    /// <param name="entity">实体对象</param>
    /// <returns>是否允许访问该实体，默认返回true</returns>
    /// <remarks>子类可重写此方法实现自定义的实体访问权限判断</remarks>
    protected virtual Boolean OnFilter(IEntity entity) => true;
    #endregion

    #region 辅助
    /// <summary>设置未被修改的实体数据项</summary>
    /// <param name="fields">字段集合</param>
    /// <param name="entity">实体对象</param>
    /// <param name="name">字段名</param>
    /// <param name="value">要设置的值</param>
    /// <returns>是否成功设置了数据</returns>
    /// <remarks>
    /// 仅当指定字段存在且实体该字段数据未被标记为脏数据时，才会设置值
    /// 用于在初始化阶段设置默认值，而不会在后续修改中覆盖用户更改
    /// </remarks>
    protected virtual Boolean SetNoDirtyItem(ICollection<FieldItem> fields, IEntity entity, String name, Object? value)
    {
        var fi = fields.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
        if (fi == null) return false;

        name = fi.Name;
        if (!entity.IsDirty(name)) return entity.SetItem(name, value);

        return false;
    }

    /// <summary>覆盖实体数据项，如果是默认值则无视脏数据标记</summary>
    /// <param name="fields">字段集合</param>
    /// <param name="entity">实体对象</param>
    /// <param name="name">字段名</param>
    /// <param name="value">要设置的值</param>
    /// <returns>是否成功设置了数据</returns>
    /// <remarks>
    /// 用于新增实体时设置默认值，会忽略脏数据标记
    /// 对于整数类型，仅当当前值为0时才会覆盖
    /// 对于字符串类型，仅当当前值为空时才会覆盖，并支持自动裁剪超长字符串
    /// 对于DateTime类型，仅当当前值年份小于2000时才会覆盖
    /// </remarks>
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

    /// <summary>获取实体类的字段信息，带缓存</summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>字段信息数组</returns>
    /// <remarks>
    /// 第一次调用时会通过工厂从实体类型获取字段信息并缓存，
    /// 后续调用会直接返回缓存的字段信息，提高性能
    /// </remarks>
    protected static FieldItem[] GetFields(Type entityType) => _fields.GetOrAdd(entityType, t => t.AsFactory().Fields);

    /// <summary>提前设置字段信息，加速初始化过程</summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="fields">字段信息数组</param>
    /// <remarks>
    /// 在实体类初始化时调用此方法预设字段信息，避免重复获取，
    /// 仅当字段信息尚未缓存时才会设置成功
    /// </remarks>
    public static void SetFields(Type entityType, FieldItem[] fields) => _fields.TryAdd(entityType, fields);
    #endregion
}

/// <summary>实体模块基类。旧版名称，建议使用 EntityInterceptor</summary>
[Obsolete("请使用 EntityInterceptor")]
public abstract class EntityModule : EntityInterceptor { }