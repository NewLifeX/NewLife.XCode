using XCode.Configuration;

namespace XCode.Membership;

/// <summary>数据权限实体接口（仅用户标识）。实体类实现此接口后可按用户过滤数据</summary>
/// <remarks>
/// 适用场景：个人数据，如个人笔记、收藏等，仅需按用户过滤。
/// <para>默认使用 UserId 字段过滤，如果实际字段名不同，可重写 GetUserField 方法</para>
/// </remarks>
public interface IUserScope
{
    /// <summary>用户。数据权限过滤时使用</summary>
    Int32 UserId { get; set; }
}

/// <summary>数据权限实体接口（仅部门标识）。实体类实现此接口后可按部门过滤数据</summary>
/// <remarks>
/// 适用场景：部门数据，如部门公告、部门任务等，仅需按部门过滤。
/// <para>默认使用 DepartmentId 字段过滤，如果实际字段名不同，可重写 GetDepartmentField 方法</para>
/// </remarks>
public interface IDepartmentScope
{
    /// <summary>部门编号。数据权限过滤时使用</summary>
    Int32 DepartmentId { get; set; }
}

/// <summary>数据权限实体接口（完整）。实体类实现此接口后可按用户或部门过滤数据</summary>
/// <remarks>
/// 适用场景：需要同时支持按用户和部门过滤的数据，如订单、工单等
/// <para>数据权限控制优先级：仅本人 > 本部门 > 本部门及下级 > 自定义 > 全部</para>
/// <para>当角色 DataScope 为"仅本人"时，使用 UserId 过滤</para>
/// <para>当角色 DataScope 为"本部门/本部门及下级/自定义"时，使用 DepartmentId 过滤</para>
/// </remarks>
public interface IDataScope : IUserScope, IDepartmentScope { }

/// <summary>可自定义数据权限字段的接口</summary>
/// <remarks>
/// 当实体的用户字段名不是 UserId、部门字段名不是 DepartmentId、租户字段名不是 TenantId 时，
/// 实体类可以额外实现此接口来指定实际的字段
/// </remarks>
public interface IDataScopeFieldProvider
{
    /// <summary>获取用户字段。返回 null 表示使用默认的 UserId</summary>
    FieldItem? GetUserField();

    /// <summary>获取部门字段。返回 null 表示使用默认的 DepartmentId</summary>
    FieldItem? GetDepartmentField();

    /// <summary>获取租户字段。返回 null 表示使用默认的 TenantId</summary>
    FieldItem? GetTenantField();
}

/// <summary>字段级权限接口。标识实体包含需要字段级权限控制的敏感字段</summary>
/// <remarks>
/// 适用场景：某些字段仅本人或特定角色可见，如工资、身份证号等
/// <para>实现方式：</para>
/// <para>1. 实体类实现此接口，返回敏感字段列表</para>
/// <para>2. 查询后调用 MaskSensitiveFields 方法遮蔽敏感数据</para>
/// <para>3. 配合前端显示控制，隐藏敏感字段列</para>
/// </remarks>
public interface IFieldScope
{
    /// <summary>获取敏感字段列表</summary>
    /// <returns>字段名数组</returns>
    String[] GetSensitiveFields();

    /// <summary>判断指定用户是否可以查看敏感字段</summary>
    /// <param name="userId">用户编号</param>
    /// <returns>是否可见</returns>
    Boolean CanViewSensitiveFields(Int32 userId);
}

/// <summary>字段级权限辅助类</summary>
public static class FieldScopeHelper
{
    /// <summary>遮蔽敏感字段</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="context">数据权限上下文，为 null 时使用 DataScopeContext.Current</param>
    /// <param name="maskValue">遮蔽值，默认为"***"</param>
    /// <returns>处理后的实体</returns>
    public static IEntity MaskSensitiveFields(this IEntity entity, DataScopeContext? context = null, String maskValue = "***")
    {
        if (entity is not IFieldScope fieldScope) return entity;

        context ??= DataScopeContext.Current;

        // 检查是否有权限查看敏感字段
        var canView = false;
        if (context != null)
        {
            // 优先使用上下文中的权限
            canView = context.ViewSensitive;

            // 其次检查是否是本人数据
            if (!canView && entity is IUserScope userScope)
                canView = userScope.UserId == context.UserId;
        }
        else
        {
            // 没有上下文时，使用实体自身的判断逻辑
            var userId = ManageProvider.Provider?.Current?.ID ?? 0;
            canView = fieldScope.CanViewSensitiveFields(userId);
        }

        if (canView) return entity;

        var sensitiveFields = fieldScope.GetSensitiveFields();
        if (sensitiveFields == null || sensitiveFields.Length == 0) return entity;

        // 获取实体工厂
        if (entity is not IEntity ent) return entity;

        // 替换字段为遮蔽值
        foreach (var fieldName in sensitiveFields)
        {
            var value = ent[fieldName];
            if (value != null && value is String)
            {
                //ent.SetItem(fieldName, maskValue);
                ent[fieldName] = maskValue;
            }
        }

        return entity;
    }

    /// <summary>批量遮蔽敏感字段</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="list">实体列表</param>
    /// <param name="context">数据权限上下文</param>
    /// <param name="maskValue">遮蔽值</param>
    /// <returns>处理后的列表</returns>
    public static IList<TEntity> MaskSensitiveFields<TEntity>(IList<TEntity> list, DataScopeContext? context = null, String maskValue = "***") where TEntity : class, IEntity
    {
        if (list == null || list.Count == 0) return list!;

        foreach (var entity in list)
        {
            MaskSensitiveFields(entity, context, maskValue);
        }


        return list;
    }
}
