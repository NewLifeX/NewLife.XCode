#pragma warning disable CS0618 // 兼容层内部需要适配旧接口
namespace XCode;

/// <summary>实体拦截器。生命周期钩子，已由 <see cref="IEntityFilter"/> 接管操作管道</summary>
/// <remarks>
/// 保留兼容期。新代码请实现 <see cref="IEntityFilter"/>；旧实现会通过 EntityInterceptor 适配到过滤器管道。
/// </remarks>
[Obsolete("请使用 IEntityFilter")]
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
    /// <param name="action">操作类型</param>
    /// <returns>修改后的查询条件</returns>
    Expression Query(IEntityFactory factory, Expression? where, DataMethod action);

    /// <summary>过滤实体列表</summary>
    /// <param name="list">实体列表</param>
    /// <returns>过滤后的实体列表</returns>
    IList<IEntity> Filter(IList<IEntity> list);

    /// <summary>过滤单个实体</summary>
    /// <param name="entity">实体对象</param>
    /// <returns>是否允许访问该实体</returns>
    Boolean Filter(IEntity? entity);
}


/// <summary>实体拦截器基类。兼容旧代码，同时作为 <see cref="IEntityFilter"/> 适配器</summary>
/// <remarks>
/// 旧钩子 <c>OnCreate/OnValid/OnQuery/OnFilter</c> 会被映射到过滤器管道的 <c>OnExecuting/OnExecuted</c>。
/// 新代码建议直接继承 <see cref="EntityFilter"/>。
/// </remarks>
public abstract class EntityInterceptor : EntityFilter, IEntityInterceptor
{
    #region IEntityFilter 适配
    /// <summary>操作执行前</summary>
    /// <param name="context">操作上下文</param>
    public override void OnExecuting(EntityContext context) => OnFilterExecuting(context);

    /// <summary>操作执行后</summary>
    /// <param name="context">操作上下文</param>
    public override void OnExecuted(EntityContext context) => OnFilterExecuted(context);

    private void OnFilterExecuting(EntityContext context)
    {
        if (context == null) return;

        switch (context.Action)
        {
            case DataMethod.Create:
                if (context.Entity != null) OnCreate(context.Entity, context.ForEdit);
                return;

            case DataMethod.Find:
            case DataMethod.FindAll:
            case DataMethod.FindCount:
            case DataMethod.FindSQL:
                ApplyQuery(context);
                return;
        }

        if (context.Entities is { Count: > 0 })
        {
            foreach (var item in context.Entities)
            {
                if (!OnValid(item, context.Action))
                {
                    context.Cancel = true;
                    context.AffectedRows = 0;
                    return;
                }
            }
        }
        else if (context.Entity != null)
        {
            if (!OnValid(context.Entity, context.Action))
            {
                context.Cancel = true;
                context.AffectedRows = 0;
            }
        }
    }

    private void ApplyQuery(EntityContext context)
    {
        if (context.Factory == null || context.Builder == null) return;

        var db = context.Session?.Dal.Db;
        var where = context.Builder.Where.IsNullOrEmpty() ? null : new Expression(context.Builder.Where);
        var exp = OnQuery(context.Factory, where, context.Action);
        if (exp != null) context.Builder.Where = exp.GetString(db, null);
    }

    private void OnFilterExecuted(EntityContext context)
    {
        if (context?.Result == null) return;

        if (context.Result is IEntity entity)
        {
            if (!OnFilter(entity)) context.Result = null;
        }
        else if (context.Result is IEnumerable<IEntity> list)
        {
            context.Result = OnFilter(new List<IEntity>(list));
        }
    }

    #endregion

    #region IEntityInterceptor 成员
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
    public Expression Query(IEntityFactory factory, Expression? where, DataMethod action)
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
    protected virtual Expression? OnQuery(IEntityFactory factory, Expression? where, DataMethod action) => where;

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

}

/// <summary>旧接口适配器。把直接实现 IEntityInterceptor 的类包装成 IEntityFilter</summary>
internal sealed class LegacyEntityFilter(IEntityInterceptor interceptor) : IEntityFilter
{
    private readonly IEntityInterceptor _interceptor = interceptor;

    public Int32 Order { get; set; }

    public Boolean Init(Type entityType) => _interceptor.Init(entityType);

    public void OnExecuting(EntityContext context)
    {
        if (context == null) return;

        switch (context.Action)
        {
            case DataMethod.Create:
                if (context.Entity != null) _interceptor.Create(context.Entity, context.ForEdit);
                return;

            case DataMethod.Find:
            case DataMethod.FindAll:
            case DataMethod.FindCount:
            case DataMethod.FindSQL:
                if (context.Factory == null || context.Builder == null) return;

                var db = context.Session?.Dal.Db;
                var where = context.Builder.Where.IsNullOrEmpty() ? null : new Expression(context.Builder.Where);
                var exp = _interceptor.Query(context.Factory, where, context.Action);
                if (exp != null) context.Builder.Where = exp.GetString(db, null);
                return;
        }

        if (context.Entities is { Count: > 0 })
        {
            foreach (var item in context.Entities)
            {
                if (!_interceptor.Valid(item, context.Action))
                {
                    context.Cancel = true;
                    context.AffectedRows = 0;
                    return;
                }
            }
        }
        else if (context.Entity != null)
        {
            if (!_interceptor.Valid(context.Entity, context.Action))
            {
                context.Cancel = true;
                context.AffectedRows = 0;
            }
        }
    }

    public void OnExecuted(EntityContext context)
    {
        if (context?.Result == null) return;

        if (context.Result is IEntity entity)
        {
            if (!_interceptor.Filter(entity)) context.Result = null;
        }
        else if (context.Result is IEnumerable<IEntity> list)
        {
            context.Result = _interceptor.Filter(new List<IEntity>(list));
        }
    }
}

#pragma warning restore CS0618
