using XCode.Configuration;
using System.Diagnostics.CodeAnalysis;

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
    private static readonly ThreadLocal<TenantContext?> _Current = new();
#else
    private static readonly AsyncLocal<TenantContext?> _Current = new();
#endif
    /// <summary>当前租户上下文</summary>
    public static TenantContext? Current { get => _Current.Value; set => _Current.Value = value; }

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
    public static WhereExpression ApplyTenant<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TEntity>(this WhereExpression where) where TEntity : Entity<TEntity>, new()
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

/// <summary>租户拦截器。兼容旧类名，实际实现见 <see cref="TenantFilter"/></summary>
[Obsolete("请使用 TenantFilter；运行 xcode 可自动迁移")]
public class TenantInterceptor : TenantFilter { }
