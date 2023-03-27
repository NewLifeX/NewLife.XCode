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

}

/// <summary>多租户助手</summary>
public static class TenantSourceHelper
{
    //public static Expression AppendTenant(this WhereExpression whereExpression, ITenantSource tenantSource)
    //{

    //}
}