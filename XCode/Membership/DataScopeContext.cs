using NewLife.Caching;
using NewLife.Data;
using NewLife.Log;
using XCode.Configuration;

namespace XCode.Membership;

/// <summary>数据权限上下文</summary>
/// <remarks>
/// 存储当前用户的数据权限范围，在查询时可用于构建过滤条件。
/// <para>使用方式：在请求开始时设置 DataScopeContext.Current，后续查询自动应用数据权限过滤</para>
/// <para>配合 DataScopeModule 实现实体增删改的权限校验</para>
/// </remarks>
public class DataScopeContext
{
    #region 属性
    /// <summary>用户编号</summary>
    public Int32 UserId { get; set; }

    /// <summary>用户所属部门编号</summary>
    public Int32 DepartmentId { get; set; }

    /// <summary>数据范围。取自角色的 DataScope 字段，多角色时取最大权限</summary>
    public DataScopes DataScope { get; set; }

    /// <summary>可访问的部门编号列表。根据数据范围计算得出，null 表示不限制</summary>
    public Int32[]? AccessibleDepartmentIds { get; set; }

    /// <summary>是否不受数据权限约束。等价于 DataScope == DataScopes.全部</summary>
    public Boolean IsSystem => DataScope == DataScopes.全部;

    /// <summary>是否可以查看敏感字段。取自角色的 ViewSensitive 字段</summary>
    public Boolean ViewSensitive { get; set; }

    /// <summary>当前菜单编号。用于菜单级数据权限</summary>
    public Int32 MenuId { get; set; }
    #endregion

    #region 静态属性
#if NET45
    private static readonly ThreadLocal<DataScopeContext?> _Current = new();
#else
    private static readonly AsyncLocal<DataScopeContext?> _Current = new();
#endif
    /// <summary>当前数据权限上下文</summary>
    public static DataScopeContext? Current { get => _Current.Value; set => _Current.Value = value; }

    /// <summary>部门缓存。缓存用户可访问的部门列表，key=userId，过期时间5分钟</summary>
    private static readonly ICache _cache = MemoryCache.Instance;
    #endregion

    #region 构造
    /// <summary>从用户创建数据权限上下文</summary>
    /// <param name="user">用户对象</param>
    /// <param name="menuId">当前菜单编号，用于菜单级数据权限</param>
    /// <returns></returns>
    public static DataScopeContext? Create(IUser? user, Int32 menuId = 0)
    {
        if (user == null) return null;

        var ctx = new DataScopeContext
        {
            UserId = user.ID,
            DepartmentId = user.DepartmentID,
            MenuId = menuId,
        };

        // 获取所有角色
        var roles = user.Roles;
        if (roles == null || roles.Length == 0)
        {
            var role = user.Role;
            if (role != null) roles = [role];
        }

        if (roles != null && roles.Length > 0)
        {
            // 系统角色不受数据权限约束
            if (roles.Any(e => e.IsSystem))
            {
                ctx.DataScope = DataScopes.全部;
                ctx.ViewSensitive = true;
                return ctx;
            }

            // 多角色取最大权限（数值越小权限越大，全部=0 > 本部门及下级=1 > 本部门=2 > 仅本人=3）
            ctx.DataScope = roles.Min(e => e.DataScope);

            // 敏感字段权限，任一角色有权即可
            ctx.ViewSensitive = roles.Any(e => e.ViewSensitive);

            // 检查菜单级数据权限覆盖
            if (menuId > 0)
            {
                var menu = ManageProvider.Menu?.FindByID(menuId) ?? Menu.FindByID(menuId);
                if (menu != null && menu.DataScope > 0)
                {
                    // 菜单级数据权限优先，覆盖角色默认值
                    ctx.DataScope = (DataScopes)menu.DataScope;
                }
            }

            // 从缓存获取或计算可访问部门列表
            ctx.AccessibleDepartmentIds = GetCachedDepartmentIds(user.ID, user.DepartmentID, roles, ctx.DataScope);
        }
        else
        {
            // 没有角色时，默认仅本人
            ctx.DataScope = DataScopes.仅本人;
        }

        return ctx;
    }

    /// <summary>从缓存获取可访问部门列表</summary>
    private static Int32[]? GetCachedDepartmentIds(Int32 userId, Int32 deptId, IRole[] roles, DataScopes scope)
    {
        // 全部权限不需要缓存
        if (scope == DataScopes.全部) return null;

        var key = $"DataScope:{userId}:{(Int32)scope}";
        return _cache.GetOrAdd(key, k => DataScopeHelper.GetAccessibleDepartmentIds(deptId, roles, scope));
    }

    /// <summary>清除用户的数据权限缓存</summary>
    /// <param name="userId">用户编号，0表示清除所有</param>
    public static void ClearCache(Int32 userId = 0)
    {
        if (userId > 0)
        {
            // 清除该用户所有范围的缓存
            for (var i = 0; i <= 4; i++)
            {
                _cache.Remove($"DataScope:{userId}:{i}");
            }
        }
        else
            _cache.Clear();
    }

    /// <summary>设置当前菜单，用于菜单级数据权限</summary>
    /// <param name="menuId">菜单编号</param>
    public void SetMenu(Int32 menuId)
    {
        if (menuId <= 0 || menuId == MenuId) return;

        MenuId = menuId;

        // 检查菜单级数据权限覆盖
        var menu = ManageProvider.Menu?.FindByID(menuId) ?? Menu.FindByID(menuId);
        if (menu != null && menu.DataScope >= 0)
        {
            DataScope = (DataScopes)menu.DataScope;

            // 重新计算可访问部门
            var user = ManageProvider.User;
            if (user != null)
            {
                var roles = user.Roles ?? (user.Role != null ? [user.Role] : null);
                if (roles != null)
                    AccessibleDepartmentIds = DataScopeHelper.GetAccessibleDepartmentIds(DepartmentId, roles, DataScope);
            }
        }
    }
    #endregion
}

/// <summary>数据权限辅助类</summary>
/// <remarks>
/// 提供数据权限相关的计算和过滤方法。
/// <para>主要功能：</para>
/// <para>1. 根据角色计算可访问的部门ID列表</para>
/// <para>2. 构建数据权限过滤表达式</para>
/// <para>3. 处理多角色数据权限合并</para>
/// <para>4. 支持通过 PageParameter 传递数据权限过滤</para>
/// </remarks>
public static class DataScopeHelper
{
    #region PageParameter 常量
    /// <summary>PageParameter.State 中存储数据权限过滤表达式的键名</summary>
    public const String DataScopeFilterKey = "DataScopeFilter";

    /// <summary>PageParameter.State 中存储是否启用数据权限的键名</summary>
    public const String DataScopeEnabledKey = "DataScopeEnabled";
    #endregion

    #region 核心方法
    /// <summary>获取可访问的部门编号列表</summary>
    /// <param name="userDeptId">用户所属部门编号</param>
    /// <param name="roles">用户的所有角色</param>
    /// <param name="scope">指定的数据范围，为 null 时从角色中取最大权限</param>
    /// <returns>可访问的部门编号列表，null 表示不限制（全部可访问）</returns>
    public static Int32[]? GetAccessibleDepartmentIds(Int32 userDeptId, IRole[] roles, DataScopes? scope = null)
    {
        if (roles == null || roles.Length == 0) return [];

        // 系统角色不受限制
        if (roles.Any(e => e.IsSystem)) return null;

        // 如果指定了数据范围，使用指定的；否则从角色中计算
        var effectiveScope = scope ?? roles.Min(e => e.DataScope);

        // 全部权限不限制
        if (effectiveScope == DataScopes.全部) return null;

        var allDeptIds = new HashSet<Int32>();

        switch (effectiveScope)
        {
            case DataScopes.本部门及下级:
                var deptAndChildren = GetDepartmentAndChildren(userDeptId);
                foreach (var id in deptAndChildren)
                {
                    allDeptIds.Add(id);
                }
                break;

            case DataScopes.本部门:
                if (userDeptId > 0) allDeptIds.Add(userDeptId);
                break;

            case DataScopes.仅本人:
                // 仅本人不添加部门，由调用方使用 UserId 过滤
                break;

            case DataScopes.自定义:
                // 自定义时合并所有角色的自定义部门
                foreach (var role in roles)
                {
                    if (role.DataScope == DataScopes.自定义)
                    {
                        var customIds = ParseDepartmentIds(role.DataDepartmentIds);
                        foreach (var id in customIds)
                        {
                            allDeptIds.Add(id);
                        }
                    }
                }
                break;
        }

        return allDeptIds.ToArray();
    }

    /// <summary>获取部门及其所有下级部门的编号</summary>
    /// <param name="departmentId">部门编号</param>
    /// <returns>部门编号列表（包含自身）</returns>
    public static Int32[] GetDepartmentAndChildren(Int32 departmentId)
    {
        if (departmentId <= 0) return [];

        var dept = Department.FindByID(departmentId);
        if (dept == null) return [departmentId];

        var ids = new List<Int32> { departmentId };
        CollectChildDepartmentIds(dept, ids);

        return ids.ToArray();
    }

    /// <summary>递归收集下级部门编号</summary>
    private static void CollectChildDepartmentIds(Department dept, List<Int32> ids)
    {
        var childs = dept.Childs;
        if (childs == null) return;

        foreach (var child in childs)
        {
            if (!ids.Contains(child.ID))
            {
                ids.Add(child.ID);
                CollectChildDepartmentIds(child, ids);
            }
        }
    }

    /// <summary>解析部门编号字符串</summary>
    /// <param name="departmentIds">逗号分隔的部门编号字符串</param>
    /// <returns>部门编号数组</returns>
    public static Int32[] ParseDepartmentIds(String? departmentIds)
    {
        if (departmentIds.IsNullOrWhiteSpace()) return [];

        return departmentIds.SplitAsInt();
    }
    #endregion

    #region 过滤表达式
    /// <summary>获取数据权限过滤表达式（完整数据权限实体）</summary>
    /// <typeparam name="TEntity">实体类型，需实现 IDataScope 接口</typeparam>
    /// <param name="context">数据权限上下文，为 null 时尝试使用 DataScopeContext.Current</param>
    /// <returns>过滤表达式，null 表示不需要过滤</returns>
    public static Expression? GetFilter<TEntity>(DataScopeContext? context = null) where TEntity : Entity<TEntity>, IDataScope, new()
    {
        context ??= DataScopeContext.Current;
        if (context == null || context.IsSystem) return null;

        var factory = Entity<TEntity>.Meta.Factory;
        var userField = GetUserField(factory);
        var deptField = GetDepartmentField(factory);

        return BuildFilter(context, userField, deptField);
    }

    /// <summary>获取数据权限过滤表达式（仅用户标识实体）</summary>
    /// <typeparam name="TEntity">实体类型，需实现 IUserScope 接口</typeparam>
    /// <param name="context">数据权限上下文</param>
    /// <returns>过滤表达式</returns>
    public static Expression? GetUserScopeFilter<TEntity>(DataScopeContext? context = null) where TEntity : Entity<TEntity>, IUserScope, new()
    {
        context ??= DataScopeContext.Current;
        if (context == null || context.IsSystem) return null;

        // 如果数据范围是全部，不限制
        if (context.DataScope == DataScopes.全部) return null;

        // 仅用户标识的实体，只能按用户过滤
        var factory = Entity<TEntity>.Meta.Factory;
        var userField = GetUserField(factory);
        if (userField is null) return null;

        // 按创建用户过滤
        return userField.Equal(context.UserId);
    }

    /// <summary>获取数据权限过滤表达式（仅部门标识实体）</summary>
    /// <typeparam name="TEntity">实体类型，需实现 IDepartmentScope 接口</typeparam>
    /// <param name="context">数据权限上下文</param>
    /// <returns>过滤表达式</returns>
    public static Expression? GetDepartmentScopeFilter<TEntity>(DataScopeContext? context = null) where TEntity : Entity<TEntity>, IDepartmentScope, new()
    {
        context ??= DataScopeContext.Current;
        if (context == null || context.IsSystem) return null;

        // 如果数据范围是全部，不限制
        if (context.DataScope == DataScopes.全部) return null;

        var factory = Entity<TEntity>.Meta.Factory;
        var deptField = GetDepartmentField(factory);
        if (deptField is null) return null;

        return BuildDepartmentFilter(context, deptField);
    }

    /// <summary>构建过滤表达式</summary>
    private static Expression? BuildFilter(DataScopeContext context, FieldItem? userField, FieldItem? deptField)
    {
        switch (context.DataScope)
        {
            case DataScopes.全部:
                return null;

            case DataScopes.仅本人:
                // 仅本人：按用户过滤
                if (userField is not null)
                    return userField.Equal(context.UserId);
                // 没有用户字段时，退化为按部门过滤
                if (deptField is not null)
                    return deptField.Equal(context.DepartmentId);
                return null;

            case DataScopes.本部门:
            case DataScopes.本部门及下级:
            case DataScopes.自定义:
                return BuildDepartmentFilter(context, deptField);

            default:
                return null;
        }
    }

    /// <summary>构建部门过滤表达式</summary>
    private static Expression? BuildDepartmentFilter(DataScopeContext context, FieldItem? deptField)
    {
        if (deptField is null) return null;

        var deptIds = context.AccessibleDepartmentIds;
        if (deptIds == null) return null; // null 表示不限制

        if (deptIds.Length == 0) return deptField.Equal(-1); // 空数组表示无权限，返回恒假条件
        if (deptIds.Length == 1) return deptField.Equal(deptIds[0]);

        return deptField.In(deptIds);
    }
    #endregion

    #region 权限校验
    /// <summary>校验是否有权操作指定数据（完整数据权限实体）</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="context">数据权限上下文</param>
    /// <returns>是否有权操作</returns>
    public static Boolean CanAccess(IDataScope entity, DataScopeContext? context = null)
    {
        context ??= DataScopeContext.Current;
        if (context == null || context.IsSystem) return true;

        switch (context.DataScope)
        {
            case DataScopes.全部:
                return true;

            case DataScopes.仅本人:
                return entity.UserId == context.UserId;

            case DataScopes.本部门:
            case DataScopes.本部门及下级:
            case DataScopes.自定义:
                var deptIds = context.AccessibleDepartmentIds;
                if (deptIds == null) return true;
                return deptIds.Contains(entity.DepartmentId);

            default:
                return true;
        }
    }

    /// <summary>校验是否有权操作指定数据（仅用户标识实体）</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="context">数据权限上下文</param>
    /// <returns>是否有权操作</returns>
    public static Boolean CanAccess(IUserScope entity, DataScopeContext? context = null)
    {
        context ??= DataScopeContext.Current;
        if (context == null || context.IsSystem) return true;

        // 全部权限可访问所有数据
        if (context.DataScope == DataScopes.全部) return true;

        // 仅用户标识的实体，按用户校验
        return entity.UserId == context.UserId;
    }

    /// <summary>校验是否有权操作指定数据（仅部门标识实体）</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="context">数据权限上下文</param>
    /// <returns>是否有权操作</returns>
    public static Boolean CanAccess(IDepartmentScope entity, DataScopeContext? context = null)
    {
        context ??= DataScopeContext.Current;
        if (context == null || context.IsSystem) return true;

        // 全部权限可访问所有数据
        if (context.DataScope == DataScopes.全部) return true;

        var deptIds = context.AccessibleDepartmentIds;
        if (deptIds == null) return true;

        return deptIds.Contains(entity.DepartmentId);
    }
    #endregion

    #region WhereExpression 扩展
    /// <summary>应用数据权限过滤（根据实体类型自动构建过滤条件）</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="where">现有条件</param>
    /// <param name="context">数据权限上下文，为 null 时使用 DataScopeContext.Current</param>
    /// <returns>合并后的条件</returns>
    /// <remarks>
    /// 在 Search 方法中使用示例：
    /// <code>
    /// public static IList&lt;Order&gt; Search(Int32 status, PageParameter page)
    /// {
    ///     var exp = new WhereExpression();
    ///     if (status &gt;= 0) exp &amp;= _.Status == status;
    ///     
    ///     // 应用数据权限过滤
    ///     exp = exp.ApplyDataScope&lt;Order&gt;();
    ///     
    ///     return FindAll(exp, page);
    /// }
    /// </code>
    /// </remarks>
    public static WhereExpression ApplyDataScope<TEntity>(this WhereExpression where, DataScopeContext? context = null) where TEntity : Entity<TEntity>, new() => ApplyDataScope(where, Entity<TEntity>.Meta.Factory, context);

    /// <summary>应用数据权限过滤（通过实体工厂）</summary>
    /// <param name="where">现有条件</param>
    /// <param name="factory">实体工厂</param>
    /// <param name="context">数据权限上下文，为 null 时使用 DataScopeContext.Current</param>
    /// <returns>合并后的条件</returns>
    public static WhereExpression ApplyDataScope(this WhereExpression where, IEntityFactory factory, DataScopeContext? context = null)
    {
        var filter = GetFilterForType(factory, context);
        if (filter != null)
            where &= filter;

        return where;
    }

    /// <summary>同时应用租户和数据权限过滤</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="where">现有条件</param>
    /// <param name="context">数据权限上下文</param>
    /// <returns>合并后的条件</returns>
    /// <remarks>
    /// 在 Search 方法中使用示例：
    /// <code>
    /// public static IList&lt;Order&gt; Search(Int32 status, PageParameter page)
    /// {
    ///     var exp = new WhereExpression();
    ///     if (status &gt;= 0) exp &amp;= _.Status == status;
    ///     
    ///     // 同时应用租户和数据权限过滤
    ///     exp = exp.ApplyScope&lt;Order&gt;();
    ///     
    ///     return FindAll(exp, page);
    /// }
    /// </code>
    /// </remarks>
    public static WhereExpression ApplyScope<TEntity>(this WhereExpression where, DataScopeContext? context = null) where TEntity : Entity<TEntity>, new() => ApplyScope(where, Entity<TEntity>.Meta.Factory, context);

    /// <summary>同时应用租户和数据权限过滤（通过实体工厂）</summary>
    /// <param name="where">现有条件</param>
    /// <param name="factory">实体工厂</param>
    /// <param name="context">数据权限上下文</param>
    /// <returns>合并后的条件</returns>
    public static WhereExpression ApplyScope(this WhereExpression where, IEntityFactory factory, DataScopeContext? context = null)
    {
        // 先应用租户过滤
        where = where.ApplyTenant(factory);

        // 再应用数据权限过滤
        where = where.ApplyDataScope(factory, context);

        return where;
    }
    #endregion

    #region 辅助
    /// <summary>根据实体工厂获取数据权限过滤表达式</summary>
    /// <param name="factory">实体工厂</param>
    /// <param name="context">数据权限上下文</param>
    /// <returns>过滤表达式</returns>
    public static Expression? GetFilterForType(IEntityFactory factory, DataScopeContext? context = null)
    {
        if (factory == null) return null;

        context ??= DataScopeContext.Current;
        if (context == null || context.IsSystem) return null;
        if (context.DataScope == DataScopes.全部) return null;

        var type = factory.EntityType;

        // 根据实体实现的接口选择过滤方式
        if (typeof(IDataScope).IsAssignableFrom(type))
        {
            var userField = GetUserField(factory);
            var deptField = GetDepartmentField(factory);
            return BuildFilter(context, userField, deptField);
        }

        if (typeof(IUserScope).IsAssignableFrom(type))
        {
            var userField = GetUserField(factory);
            if (userField is not null)
                return userField.Equal(context.UserId);
        }

        if (typeof(IDepartmentScope).IsAssignableFrom(type))
        {
            var deptField = GetDepartmentField(factory);
            if (deptField is not null)
                return BuildDepartmentFilter(context, deptField);
        }

        return null;
    }

    /// <summary>获取用户字段</summary>
    /// <param name="factory">实体工厂</param>
    /// <returns>用户字段</returns>
    private static FieldItem? GetUserField(IEntityFactory factory)
    {
        // 尝试从实体实例获取自定义字段名
        var entity = factory.Create();
        if (entity is IDataScopeFieldProvider provider)
        {
            var field = provider.GetUserField();
            if (field != null) return field;
        }

        // 使用默认字段名
        return factory.Table.FindByName(nameof(IUserScope.UserId));
    }

    /// <summary>获取部门字段</summary>
    /// <param name="factory">实体工厂</param>
    /// <returns>部门字段</returns>
    private static FieldItem? GetDepartmentField(IEntityFactory factory)
    {
        // 尝试从实体实例获取自定义字段名
        var entity = factory.Create();
        if (entity is IDataScopeFieldProvider provider)
        {
            var field = provider.GetDepartmentField();
            if (field != null) return field;
        }

        // 使用默认字段名
        return factory.Table.FindByName(nameof(IDepartmentScope.DepartmentId));
    }
    #endregion

    #region 审计日志
    /// <summary>记录数据权限过滤日志</summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="context">数据权限上下文</param>
    /// <param name="filter">过滤表达式</param>
    /// <param name="action">操作类型</param>
    public static void WriteLog(Type entityType, DataScopeContext context, WhereExpression? filter, String action = "查询")
    {
        if (context == null) return;

        // 仅在以下情况记录审计日志：
        // 1. 启用了详细日志（调试模式）
        // 2. 数据权限实际生效（非全部权限）
        if (!XTrace.Debug) return;
        if (context.IsSystem || context.DataScope == DataScopes.全部) return;

        var msg = $"[数据权限]{action} {entityType.Name}，用户={context.UserId}，范围={context.DataScope}";
        if (filter != null)
            msg += $"，过滤={filter}";

        XTrace.WriteLine(msg);
    }
    #endregion
}
