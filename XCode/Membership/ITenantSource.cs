using XCode.Configuration;

namespace XCode.Membership;

/// <summary>租户数据权限接口。实体类实现此接口后可按租户过滤数据</summary>
/// <remarks>
/// 适用场景：多租户系统，数据需要按租户隔离。
/// <para>在实体对象插入时，补充或校验租户标识；</para>
/// <para>在实体对象更新或删除时，校验租户标识；</para>
/// <para>在实体查询时，补充租户查询条件；</para>
/// </remarks>
public interface ITenantScope
{
    /// <summary>租户标识</summary>
    Int32 TenantId { get; set; }
}

/// <summary>租户数据源接口（已过期，请使用 ITenantScope）</summary>
[Obsolete("请使用 ITenantScope")]
public interface ITenantSource : ITenantScope { }

/// <summary>租户上下文</summary>
/// <remarks>
/// 在实体对象插入时，补充或校验租户标识；
/// 在实体对象更新或删除时，校验租户标识；
/// 在实体查询时，补充租户查询条件；
/// </remarks>
public class TenantContext
{
    #region 属性
    /// <summary>租户标识。0表示进入管理后台，没有进入任意租户</summary>
    public Int32 TenantId { get; set; }

    private ITenant? _tenant;
    /// <summary>租户对象</summary>
    public ITenant? Tenant { get => _tenant ??= Membership.Tenant.FindById(TenantId) ?? null; set => _tenant = value; }
    #endregion

#if NET45
    private static readonly ThreadLocal<TenantContext> _Current = new();
#else
    private static readonly AsyncLocal<TenantContext> _Current = new();
#endif
    /// <summary>当前租户上下文</summary>
    public static TenantContext Current { get => _Current.Value; set => _Current.Value = value; }

    /// <summary>当前租户标识。无效时返回0</summary>
    public static Int32 CurrentId => Current?.TenantId ?? 0;
}

/// <summary>多租户助手</summary>
public static class TenantSourceHelper
{
    /// <summary>应用租户过滤（如果实体实现了 ITenantScope 接口）</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="where">现有条件</param>
    /// <returns>合并后的条件</returns>
    /// <remarks>
    /// 在 Search 方法中使用示例：
    /// <code>
    /// public static IList&lt;Order&gt; Search(Int32 status, PageParameter page)
    /// {
    ///     var exp = new WhereExpression();
    ///     if (status &gt;= 0) exp &amp;= _.Status == status;
    ///     
    ///     // 应用租户过滤
    ///     exp = exp.ApplyTenant&lt;Order&gt;();
    ///     
    ///     return FindAll(exp, page);
    /// }
    /// </code>
    /// </remarks>
    public static WhereExpression ApplyTenant<TEntity>(this WhereExpression where) where TEntity : Entity<TEntity>, new()
    {
        // 检查实体是否实现租户接口
        if (!typeof(ITenantScope).IsAssignableFrom(typeof(TEntity))) return where;

        return ApplyTenant(where, Entity<TEntity>.Meta.Factory);
    }

    /// <summary>应用租户过滤（通过实体工厂）</summary>
    /// <param name="where">现有条件</param>
    /// <param name="factory">实体工厂</param>
    /// <returns>合并后的条件</returns>
    public static WhereExpression ApplyTenant(this WhereExpression where, IEntityFactory factory)
    {
        if (factory == null) return where;

        // 检查实体是否实现租户接口
        if (!typeof(ITenantScope).IsAssignableFrom(factory.EntityType)) return where;

        var tenantId = TenantContext.CurrentId;
        if (tenantId <= 0) return where;

        // 获取租户字段，优先使用自定义字段
        var tenantField = GetTenantField(factory);
        if (tenantField == null) return where;

        where &= tenantField.Equal(tenantId);

        return where;
    }

    /// <summary>获取租户字段。优先使用 IDataScopeFieldProvider 自定义字段，否则使用默认的 TenantId</summary>
    /// <param name="factory">实体工厂</param>
    /// <returns>租户字段</returns>
    public static FieldItem? GetTenantField(IEntityFactory factory)
    {
        if (factory == null) return null;

        // 优先使用自定义字段提供者
        if (factory.Default is IDataScopeFieldProvider provider)
        {
            var field = provider.GetTenantField();
            if (field != null) return field;
        }

        // 使用默认字段名
        return factory.Table.FindByName(nameof(ITenantScope.TenantId));
    }
}

/// <summary>租户过滤器。添加修改时自动设置租户标识</summary>
public class TenantModule : EntityModule
{
    /// <summary>初始化。检查是否匹配</summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>是否匹配</returns>
    protected override Boolean OnInit(Type entityType) => typeof(ITenantScope).IsAssignableFrom(entityType);

    /// <summary>创建实体对象</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="forEdit">是否用于编辑</param>
    protected override void OnCreate(IEntity entity, Boolean forEdit)
    {
        var ctx = TenantContext.Current;
        if (ctx != null && entity is ITenantScope tenant)
        {
            tenant.TenantId = ctx.TenantId;
        }
    }

    /// <summary>验证数据，自动加上创建和更新的信息</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="method">数据操作方法</param>
    protected override Boolean OnValid(IEntity entity, DataMethod method)
    {
        if (entity is not ITenantScope tenant) return true;

        var ctx = TenantContext.Current;
        if (ctx == null || ctx.TenantId == 0) return true;

        try
        {
            // 只能操作本租户数据
            switch (method)
            {
                case DataMethod.Insert:
                    // 新增：强制设置租户
                    if (tenant.TenantId == 0)
                        tenant.TenantId = ctx.TenantId;
                    else if (tenant.TenantId != ctx.TenantId)
                        throw new InvalidOperationException($"不能为其他租户[{tenant.TenantId}]创建数据");
                    break;

                case DataMethod.Update:
                    // 更新：校验租户归属
                    if (!entity.HasDirty) return true;
                    if (tenant.TenantId != ctx.TenantId)
                        throw new InvalidOperationException($"不能修改其他租户[{tenant.TenantId}]的数据");
                    break;

                case DataMethod.Delete:
                    // 删除：校验租户归属
                    if (tenant.TenantId != ctx.TenantId)
                        throw new InvalidOperationException($"不能删除其他租户[{tenant.TenantId}]的数据");
                    break;
            }
        }
        catch (InvalidOperationException ex)
        {
            // 失败时记录审计日志
            LogProvider.Provider.WriteLog(entity.GetType(), method + "", false, ex.Message);
        }

        return true;
    }
}
