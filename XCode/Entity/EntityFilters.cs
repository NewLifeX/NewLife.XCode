using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using NewLife.Reflection;
using XCode.Configuration;

#pragma warning disable CS0618 // 兼容层内部需要适配旧接口
namespace XCode;

/// <summary>实体过滤器集合。管理实体级与全局过滤器</summary>
/// <remarks>内部只保存并执行 <see cref="IEntityFilter"/>；旧拦截器注册时会适配到过滤器管道</remarks>
/// <param name="entityType">实体类型，null表示全局过滤器集合</param>
public class EntityFilters(Type? entityType)
{
    #region 全局静态
    /// <summary>全局过滤器集合，对所有实体生效</summary>
    public static EntityFilters Global { get; } = new(null);
    #endregion

    #region 属性
    /// <summary>实体类型，null表示全局过滤器集合</summary>
    public Type? EntityType { get; set; } = entityType;

    private IEntityFilter[] _Items = [];
    /// <summary>过滤器集合。在添删改查前后执行，支持优先级、短路与异常处理</summary>
    public IEntityFilter[] Items
    {
        get => _Items;
        set
        {
            _Items = value ?? [];
            Interlocked.Increment(ref _version);
        }
    }

    /// <summary>旧版名称。等价于 <see cref="Items"/>，保留用于兼容旧代码</summary>
    [Obsolete("请使用 Items")]
    public IEntityFilter[] Filters
    {
        get => Items;
        set => Items = value;
    }

    /// <summary>旧版名称。等价于 <see cref="Items"/>，保留用于兼容旧代码</summary>
    [Obsolete("请使用 Items")]
    public IEntityFilter[] Interceptors
    {
        get => Items;
        set => Items = value;
    }

    private Int32 _version;

    private sealed class FilterCacheEntry
    {
        public Int32 Version;
        public IEntityFilter[] Filters = [];
    }

    private readonly ConcurrentDictionary<Type, FilterCacheEntry> _filterCache = new();

    /// <summary>是否存在过滤器（含全局过滤器）</summary>
    public Boolean HasFilters => Items.Length > 0 || (this != Global && Global.Items.Length > 0);
    #endregion

    #region 方法
    /// <summary>添加实体操作过滤器</summary>
    /// <param name="filter">要添加的过滤器</param>
    /// <remarks>
    /// 在后台线程初始化并加入集合，避免在实体类静态构造函数中死锁；Init 返回 false 时忽略该过滤器
    /// </remarks>
    public virtual void Add(IEntityFilter filter)
    {
        if (filter == null) throw new ArgumentNullException(nameof(filter));

        // 提前设置字段，避免后台初始化时取不到实体元数据
        var type = EntityType;
        if (type != null)
        {
            var fs = type.AsFactory()?.Fields;
            if (fs != null) EntityFilter.SetFields(type, fs);
        }

        Task.Run(() => AddCore(filter)).Wait(100);
    }

    /// <summary>添加旧版拦截器。已过时，请实现 <see cref="IEntityFilter"/></summary>
    /// <param name="interceptor">旧拦截器实例</param>
    [Obsolete("请使用 Add(IEntityFilter)")]
    public virtual void Add(IEntityInterceptor interceptor)
    {
        if (interceptor == null) throw new ArgumentNullException(nameof(interceptor));

        Add(new LegacyEntityFilter(interceptor));
    }

    /// <summary>添加实体过滤器或兼容拦截器</summary>
    /// <typeparam name="T">实现 <see cref="IEntityFilter"/> 或 <see cref="IEntityInterceptor"/> 的类型</typeparam>
    public virtual void Add<T>() where T : class, new()
    {
        var instance = new T();
        switch (instance)
        {
            case IEntityFilter filter:
                Add(filter);
                break;
            case IEntityInterceptor interceptor:
                Add(new LegacyEntityFilter(interceptor));
                break;
            default:
                throw new XCodeException(L.T("Type [{0}] must implement [{1}] or [{2}].", typeof(T).FullName, nameof(IEntityFilter), nameof(IEntityInterceptor)));
        }
    }

    private void AddCore(IEntityFilter filter)
    {
        var type = EntityType;
        if (type != null && !filter.Init(type)) return;

        lock (this)
        {
            if (Items.Contains(filter)) return;

            Items = [.. Items, filter];
        }
    }

    /// <summary>获取指定实体类型生效的过滤器。实体级与全局过滤器合并后按 Order 升序，同 Order 时实体级在前</summary>
    /// <param name="entityType">实体类型</param>
    /// <returns></returns>
    public IEntityFilter[] Get(Type entityType)
    {
        if (entityType == null) return [];
        if (Items.Length == 0 && (this == Global || Global.Items.Length == 0)) return [];

        // 缓存键由实体级与全局版本号合成：任一集合变化都会让缓存失效
        // 版本号都是小整数，先乘质数 397 拉开数值再异或，减少不同组合得到同一个键的机会
        var version = unchecked((_version * 397) ^ (this == Global ? 0 : Global._version));
        if (_filterCache.TryGetValue(entityType, out var entry) && entry.Version == version) return entry.Filters;

        var list = new List<IEntityFilter>();
        foreach (var item in Items)
        {
            if (item.Init(entityType)) list.Add(item);
        }
        if (this != Global)
        {
            foreach (var item in Global.Items)
            {
                if (item.Init(entityType)) list.Add(item);
            }
        }

        var filters = list.OrderBy(e => e.Order).ToArray();
        _filterCache[entityType] = new FilterCacheEntry { Version = version, Filters = filters };

        return filters;
    }

    /// <summary>判断指定实体类型是否存在生效的过滤器</summary>
    /// <param name="entityType">实体类型</param>
    /// <returns></returns>
    public Boolean HasFiltersFor(Type entityType) => Get(entityType).Length > 0;

    /// <summary>执行实体操作管道。按优先级执行前置过滤器，再执行操作，最后逆序执行后置过滤器</summary>
    /// <param name="context">操作上下文</param>
    /// <param name="action">真实操作</param>
    /// <returns></returns>
    public EntityContext Execute(EntityContext context, Action<EntityContext> action)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (action == null) throw new ArgumentNullException(nameof(action));

        // 批量操作内部的单条操作不再重复进入管道；没有生效过滤器时直接执行
        var filters = GetActive(context);
        if (filters.Length == 0)
        {
            action(context);
            return context;
        }

        using var depth = EntityPipeline.Enter();

        var executed = new List<IEntityFilter>(filters.Length);
        try
        {
            foreach (var item in filters)
            {
                // 部分被抑制时跳过，后置阶段也不会执行
                if (EntityFilterScope.IsSuppressed(item.GetType())) continue;

                executed.Add(item);
                item.OnExecuting(context);

                if (context.Cancel) break;
            }

            if (!context.Cancel)
            {
                try
                {
                    action(context);
                }
                catch (Exception ex)
                {
                    context.Exception = ex;
                }
            }

            if (context.IsWrite && context.Result == null) context.Result = context.AffectedRows;
        }
        catch (Exception ex)
        {
            context.Exception ??= ex;
        }
        finally
        {
            for (var i = executed.Count - 1; i >= 0; i--)
            {
                try
                {
                    executed[i].OnExecuted(context);
                }
                catch (Exception ex)
                {
                    context.Exception ??= ex;
                }
            }
        }

        if (context.Exception != null && !context.ExceptionHandled) ExceptionDispatchInfo.Capture(context.Exception).Throw();

        return context;
    }

    /// <summary>异步执行实体操作管道</summary>
    /// <param name="context">操作上下文</param>
    /// <param name="action">真实操作</param>
    /// <returns></returns>
    public async Task<EntityContext> ExecuteAsync(EntityContext context, Func<EntityContext, Task> action)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (action == null) throw new ArgumentNullException(nameof(action));

        // 批量操作内部的单条操作不再重复进入管道；没有生效过滤器时直接执行
        var filters = GetActive(context);
        if (filters.Length == 0)
        {
            await action(context).ConfigureAwait(false);
            return context;
        }

        using var depth = EntityPipeline.Enter();

        var executed = new List<IEntityFilter>(filters.Length);
        try
        {
            foreach (var item in filters)
            {
                // 部分被抑制时跳过，后置阶段也不会执行
                if (EntityFilterScope.IsSuppressed(item.GetType())) continue;

                executed.Add(item);
                item.OnExecuting(context);

                if (context.Cancel) break;
            }

            if (!context.Cancel)
            {
                try
                {
                    await action(context).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    context.Exception = ex;
                }
            }

            if (context.IsWrite && context.Result == null) context.Result = context.AffectedRows;
        }
        catch (Exception ex)
        {
            context.Exception ??= ex;
        }
        finally
        {
            for (var i = executed.Count - 1; i >= 0; i--)
            {
                try
                {
                    executed[i].OnExecuted(context);
                }
                catch (Exception ex)
                {
                    context.Exception ??= ex;
                }
            }
        }

        if (context.Exception != null && !context.ExceptionHandled) ExceptionDispatchInfo.Capture(context.Exception).Throw();

        return context;
    }

    /// <summary>获取本次操作要执行的过滤器。批量内部重入或全部被抑制时返回空数组</summary>
    /// <param name="context">操作上下文</param>
    /// <returns></returns>
    private IEntityFilter[] GetActive(EntityContext context)
    {
        if (EntityPipeline.InNestedAction) return [];

        var entityType = context.Factory?.EntityType ?? EntityType;
        if (entityType == null) return [];

        var filters = Get(entityType);
        if (filters.Length == 0 || !EntityFilterScope.HasSuppressed) return filters;

        // 存在抑制作用域时逐项判断，避免每次操作都分配临时列表
        foreach (var item in filters)
        {
            if (!EntityFilterScope.IsSuppressed(item.GetType())) return filters;
        }

        return [];
    }

    /// <summary>过滤单个查询结果。仅执行后置结果过滤，不改写查询条件</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="entity">查询结果实体</param>
    /// <returns>过滤后的实体，不可访问时返回 null</returns>
    /// <remarks>缓存读取使用本方法按租户/数据权限再判断一次，缓存本身始终保存整表原始数据</remarks>
    public TEntity? FilterEntity<TEntity>(TEntity? entity) where TEntity : class, IEntity
    {
        if (entity == null) return null;
        if (!HasFiltersFor(entity.GetType())) return entity;

        var factory = entity.GetType().AsFactory();
        var context = new EntityContext();
        context.Prepare(DataMethod.Find, factory, factory?.Session);
        context.Entity = entity;
        context.Result = entity;
        context.AffectedRows = 1;
        Execute(context, static _ => { });

        return context.Result as TEntity;
    }

    /// <summary>过滤查询结果列表。仅执行后置结果过滤，不改写查询条件</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="list">查询结果列表</param>
    /// <returns>过滤后的列表，没有过滤器时原样返回</returns>
    /// <remarks>缓存读取使用本方法按租户/数据权限再判断一次，缓存本身始终保存整表原始数据</remarks>
    public IList<TEntity> FilterList<TEntity>(IList<TEntity> list) where TEntity : class, IEntity
    {
        if (list == null || list.Count == 0) return list ?? [];
        if (!HasFiltersFor(typeof(TEntity))) return list;

        var factory = typeof(TEntity).AsFactory();
        var context = new EntityContext();
        context.Prepare(DataMethod.FindAll, factory, factory?.Session);
        context.Result = list;
        context.AffectedRows = list.Count;
        Execute(context, static _ => { });

        // 过滤器没有改动结果时直接返回原列表，避免无谓复制
        if (ReferenceEquals(context.Result, list)) return list;
        if (context.Result is not IEnumerable<IEntity> filtered) return [];

        return filtered.Cast<TEntity>().ToList();
    }

    /// <summary>创建实体时执行过滤器。已过时，请使用 CreateFilter</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="forEdit">是否用于编辑</param>
    [Obsolete("请使用 CreateFilter")]
    public void Create(IEntity entity, Boolean forEdit) => CreateFilter(entity, forEdit);

    /// <summary>创建实体对象时执行过滤器</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="forEdit">是否用于编辑</param>
    /// <remarks>由 <see cref="Entity{TEntity}.CreateInstance(Boolean)"/> 调用；旧 Create 钩子在适配器内部执行，用于租户/用户字段预填充</remarks>
    public void CreateFilter(IEntity entity, Boolean forEdit)
    {
        if (entity == null) return;
        if (!HasFiltersFor(entity.GetType())) return;

        var factory = entity.GetType().AsFactory();
        var context = new EntityContext();
        context.Prepare(DataMethod.Create, factory, factory?.Session);
        context.Entity = entity;
        context.ForEdit = forEdit;

        Execute(context, static _ => { });
    }

    /// <summary>显式验证实体。通过过滤器管道执行</summary>
    /// <remarks>增删改操作会由管道自动执行过滤器，通常不需要再调用本方法；它用于业务代码单独触发一次过滤器验证。</remarks>
    /// <param name="entity">实体对象</param>
    /// <param name="method">数据操作方法</param>
    /// <returns>验证是否通过</returns>
    public Boolean Valid(IEntity entity, DataMethod method)
    {
        if (entity == null) return true;

        var factory = entity.GetType().AsFactory();
        if (factory == null || !HasFiltersFor(factory.EntityType)) return true;

        var context = new EntityContext();
        context.Prepare(method, factory, factory.Session);
        context.Entity = entity;

        try
        {
            Execute(context, static _ => { });
            return !context.Cancel && (context.Exception == null || context.ExceptionHandled);
        }
        catch (Exception)
        {
            return false;
        }
    }

    #endregion
}

/// <summary>旧版过滤器集合。等价于 <see cref="EntityFilters"/>，保留用于兼容旧代码</summary>
/// <param name="entityType">实体类型，null表示全局过滤器集合</param>
[Obsolete("请使用 EntityFilters")]
public class EntityInterceptors(Type? entityType) : EntityFilters(entityType) { }

/// <summary>过滤器管道内部作用域。控制递归深度与批量内层重入，仅管道内部使用</summary>
/// <remarks>公开的 <see cref="EntityFilterScope"/> 只负责显式绕过，执行流程状态留在管道内部，避免扩大公共 API。</remarks>
internal static class EntityPipeline
{
    private const Int32 MaxDepth = 32;

#if NET45
    private static readonly ThreadLocal<Int32> _depth = new();
    private static readonly ThreadLocal<Boolean> _nested = new();
#else
    private static readonly System.Threading.AsyncLocal<Int32> _depth = new();
    private static readonly System.Threading.AsyncLocal<Boolean> _nested = new();
#endif

    /// <summary>是否处于批量操作的内部执行阶段。为 true 时避免内层单条操作重复进入过滤器</summary>
    public static Boolean InNestedAction => _nested.Value;

    /// <summary>是否处于过滤器管道内部。管道内已执行过前置过滤器，内层逻辑不应重复触发</summary>
    public static Boolean InPipeline => _depth.Value > 0;

    public static IDisposable Enter()
    {
        var old = _depth.Value;
        if (old >= MaxDepth) throw new XCodeException(L.T("Entity filter recursion depth exceeds {0}.", MaxDepth));

        _depth.Value = old + 1;

        return new RestoreScope(() => _depth.Value = old);
    }

    public static IDisposable NestedAction()
    {
        var old = _nested.Value;
        _nested.Value = true;

        return new RestoreScope(() => _nested.Value = old);
    }

    private sealed class RestoreScope(Action restore) : IDisposable
    {
        private Action? _restore = restore;

        public void Dispose() => Interlocked.Exchange(ref _restore, null)?.Invoke();
    }
}

#pragma warning restore CS0618
