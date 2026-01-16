namespace XCode.Membership;

/// <summary>租户数据源接口，指示该类带有租户标识TenantId</summary>
public interface ITenantSource
{
    /// <summary>租户标识</summary>
    Int32 TenantId { get; set; }
}

/// <summary>租户上下文</summary>
/// <remarks>
/// 在实体对象插入时，补充或校验租户标识；
/// 在实体对象更新或删除时，校验租户标识；
/// 在实体查询时，补充租户查询条件；
/// </remarks>
public class TenantContext
{
    #region 属性
    /// <summary>租户标识</summary>
    public Int32 TenantId { get; set; }

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
    //public static Expression AppendTenant(this WhereExpression whereExpression, ITenantSource tenantSource)
    //{

    //}
}

/// <summary>租户过滤器。添加修改时自动设置租户标识</summary>
public class TenantModule : EntityModule
{
    /// <summary>初始化。检查是否匹配</summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    protected override Boolean OnInit(Type entityType) => entityType.GetInterfaces().Any(e => e == typeof(ITenantSource));

    /// <summary>创建实体对象</summary>
    /// <param name="entity"></param>
    /// <param name="forEdit"></param>
    protected override void OnCreate(IEntity entity, Boolean forEdit)
    {
        var ctx = TenantContext.Current;
        if (ctx != null && entity is ITenantSource tenant)
        {
            tenant.TenantId = ctx.TenantId;
        }
    }

    /// <summary>验证数据，自动加上创建和更新的信息</summary>
    /// <param name="entity"></param>
    /// <param name="method"></param>
    protected override Boolean OnValid(IEntity entity, DataMethod method)
    {
        if (entity is not ITenantSource tenant) return true;

        var ctx = TenantContext.Current;
        if (ctx == null) return true;

        // 只能操作本租户数据
        if (tenant.TenantId != ctx.TenantId)
        {
            // 插入时，如果没有指定租户标识，则补上当前租户标识
            if (method != DataMethod.Insert || tenant.TenantId != 0 || entity.IsDirty("TenantId"))
                return false;

            tenant.TenantId = ctx.TenantId;
        }
        //if (method == DataMethod.Delete) return tenant.TenantId == ctx.TenantId;

        //if (method == DataMethod.Update && !entity.HasDirty) return true;

        //if (tenant.TenantId == 0 && !entity.IsDirty("TenantId"))
        //    tenant.TenantId = ctx.TenantId;
        ////else if (tenant.TenantId != ctx.TenantId)
        ////    return false;

        return true;
    }
}
