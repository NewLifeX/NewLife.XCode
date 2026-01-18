namespace XCode.Membership;

/// <summary>数据权限拦截器。在增删改查时自动应用数据权限</summary>
/// <remarks>
/// 用法：在实体类的静态构造函数中注册：
/// <code>
/// static MyEntity()
/// {
///     Meta.Interceptors.Add&lt;DataScopeInterceptor&gt;();
/// }
/// </code>
/// <para>实体类需实现 IDataScope、IUserScope 或 IDepartmentScope 接口</para>
/// <para>运行时需设置 DataScopeContext.Current 或确保 ManageProvider.User 有效</para>
/// <para>查询时自动附加数据权限过滤条件，无需手动调用 ApplyDataScope</para>
/// </remarks>
public class DataScopeInterceptor : EntityInterceptor
{
    /// <summary>初始化。检查实体类是否实现了数据权限相关接口</summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>是否匹配</returns>
    protected override Boolean OnInit(Type entityType)
    {
        var interfaces = entityType.GetInterfaces();
        return interfaces.Any(e => e == typeof(IDataScope) || e == typeof(IUserScope) || e == typeof(IDepartmentScope));
    }

    /// <summary>创建实体对象时自动设置部门</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="forEdit">是否用于编辑</param>
    protected override void OnCreate(IEntity entity, Boolean forEdit)
    {
        var ctx = GetContext();
        if (ctx == null) return;

        // 自动设置用户
        if (entity is IUserScope userScope && userScope.UserId <= 0)
        {
            userScope.UserId = ctx.UserId;
        }

        // 自动设置部门
        if (entity is IDepartmentScope deptScope && deptScope.DepartmentId <= 0)
        {
            deptScope.DepartmentId = ctx.DepartmentId;
        }
    }

    /// <summary>验证数据权限</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="method">操作方法</param>
    /// <returns>是否通过验证</returns>
    protected override Boolean OnValid(IEntity entity, DataMethod method)
    {
        var ctx = GetContext();
        // 没有上下文或者是系统角色，不校验
        if (ctx == null || ctx.IsSystem) return true;

        try
        {
            switch (method)
            {
                case DataMethod.Insert:
                    return ValidInsert(entity, ctx);

                case DataMethod.Update:
                    if (!entity.HasDirty) return true;
                    return ValidUpdateOrDelete(entity, ctx, "修改");

                case DataMethod.Delete:
                    return ValidUpdateOrDelete(entity, ctx, "删除");
            }
        }
        catch (InvalidOperationException ex)
        {
            // 失败时记录审计日志
            LogProvider.Provider.WriteLog(entity.GetType(), method + "", false, ex.Message);
        }

        return true;
    }

    /// <summary>验证插入权限</summary>
    private Boolean ValidInsert(IEntity entity, DataScopeContext ctx)
    {
        // 全部权限可插入任意数据
        if (ctx.DataScope == DataScopes.全部) return true;

        // 校验用户归属
        if (entity is IUserScope userScope)
        {
            // 新增时如果没有设置用户，自动设置为当前用户
            if (userScope.UserId <= 0)
            {
                userScope.UserId = ctx.UserId;
            }
            else
            {
                // 检查是否有权为该用户创建数据
                if (!DataScopeHelper.CanAccess(userScope, ctx))
                {
                    throw new InvalidOperationException($"无权为用户[{userScope.UserId}]创建数据");
                }
            }
        }

        // 校验部门归属
        if (entity is IDepartmentScope deptScope)
        {
            // 新增时如果没有设置部门，自动设置为当前用户部门
            if (deptScope.DepartmentId <= 0)
            {
                deptScope.DepartmentId = ctx.DepartmentId;
            }
            else
            {
                // 检查是否有权在该部门下创建数据
                var deptIds = ctx.AccessibleDepartmentIds;
                if (deptIds != null && !deptIds.Contains(deptScope.DepartmentId))
                {
                    throw new InvalidOperationException($"无权在部门[{deptScope.DepartmentId}]下创建数据");
                }
            }
        }

        return true;
    }

    /// <summary>验证更新或删除权限</summary>
    private Boolean ValidUpdateOrDelete(IEntity entity, DataScopeContext ctx, String action)
    {
        // 全部权限可操作任意数据
        if (ctx.DataScope == DataScopes.全部) return true;

        // 完整数据权限实体
        if (entity is IDataScope dataScope)
        {
            if (!DataScopeHelper.CanAccess(dataScope, ctx))
            {
                throw new InvalidOperationException($"无权{action}此数据（数据归属不匹配）");
            }
            return true;
        }

        // 仅用户标识实体
        if (entity is IUserScope userScope)
        {
            if (!DataScopeHelper.CanAccess(userScope, ctx))
            {
                throw new InvalidOperationException($"无权{action}此数据（非本人数据）");
            }
            return true;
        }

        // 仅部门标识实体
        if (entity is IDepartmentScope deptScope)
        {
            if (!DataScopeHelper.CanAccess(deptScope, ctx))
            {
                throw new InvalidOperationException($"无权{action}此数据（部门权限不足）");
            }
            return true;
        }

        return true;
    }

    /// <summary>查询时自动附加数据权限过滤条件</summary>
    /// <param name="factory">实体工厂</param>
    /// <param name="where">查询条件表达式</param>
    /// <param name="action">查询操作来源</param>
    /// <returns>修改后的查询条件</returns>
    protected override Expression? OnQuery(IEntityFactory factory, Expression? where, QueryAction action)
    {
        var ctx = GetContext();
        if (ctx == null || ctx.IsSystem) return where;

        // 获取数据权限过滤表达式
        var filter = DataScopeHelper.GetFilterForType(factory, ctx);
        if (filter == null) return where;

        // 合并条件
        if (where == null || where.IsEmpty)
            return filter;

        return new WhereExpression(where, Operator.And, filter);
    }

    /// <summary>过滤实体列表</summary>
    /// <param name="list">实体列表</param>
    /// <returns>过滤后的实体列表</returns>
    protected override IList<IEntity> OnFilter(IList<IEntity> list)
    {
        var ctx = GetContext();
        if (ctx == null || ctx.IsSystem) return list;
        if (list.Count == 0) return list;

        // 根据实体类型选择过滤方式
        var result = new List<IEntity>();
        foreach (var entity in list)
        {
            if (CanAccess(entity, ctx)) result.Add(entity);
        }

        return result;
    }

    /// <summary>过滤单个实体</summary>
    /// <param name="entity">实体对象</param>
    /// <returns>是否允许访问该实体</returns>
    protected override Boolean OnFilter(IEntity entity)
    {
        var ctx = GetContext();
        if (ctx == null || ctx.IsSystem) return true;

        return CanAccess(entity, ctx);
    }

    /// <summary>判断是否有权访问实体</summary>
    private Boolean CanAccess(IEntity entity, DataScopeContext ctx)
    {
        if (entity is IDataScope dataScope)
            return DataScopeHelper.CanAccess(dataScope, ctx);

        if (entity is IUserScope userScope)
            return DataScopeHelper.CanAccess(userScope, ctx);

        if (entity is IDepartmentScope deptScope)
            return DataScopeHelper.CanAccess(deptScope, ctx);

        return true;
    }

    /// <summary>获取数据权限上下文</summary>
    private DataScopeContext? GetContext()
    {
        // 优先使用已设置的上下文
        var ctx = DataScopeContext.Current;
        if (ctx != null) return ctx;

        // 尝试从当前用户创建
        var user = ManageProvider.User;
        if (user == null) return null;

        return DataScopeContext.Create(user);
    }
}