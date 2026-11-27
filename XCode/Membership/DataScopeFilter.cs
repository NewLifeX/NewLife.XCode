namespace XCode.Membership;

/// <summary>数据权限过滤器。为实现了 <see cref="IDataScope"/>、<see cref="IUserScope"/> 或 <see cref="IDepartmentScope"/> 的实体提供全路径行级权限控制</summary>
/// <remarks>
/// 覆盖单条/批量增删改与查询；缓存读取在结果阶段按数据权限再判断一次。
/// <para>系统维护或跨权限管理请使用 <see cref="EntityFilterScope.Suppress{TFilter}"/> 显式放开。</para>
/// </remarks>
public class DataScopeFilter : EntityFilter
{
    /// <summary>实例化数据权限过滤器</summary>
    public DataScopeFilter()
    {
        Order = -900;
    }

    /// <summary>为指定实体类初始化过滤器</summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>实体是否实现数据权限接口</returns>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "实体类型来自 XCode 内部过滤器入口，AOT/裁剪下由应用侧保证实体类型接口被保留")]
    protected override Boolean OnInit(Type entityType)
    {
        var interfaces = entityType.GetInterfaces();
        return interfaces.Any(e => e == typeof(IDataScope) || e == typeof(IUserScope) || e == typeof(IDepartmentScope));
    }

    /// <summary>操作执行前应用数据权限</summary>
    /// <param name="context">操作上下文</param>
    public override void OnExecuting(EntityContext context)
    {
        var ctx = GetContext();
        if (ctx == null) return;

        // 创建实体对象时只补齐空用户/部门，不做校验；数据加载阶段可能带入他人的原始行
        if (context.Action == DataMethod.Create)
        {
            if (context.Entity is IUserScope user0 && user0.UserId <= 0) user0.UserId = ctx.UserId;
            if (context.Entity is IDepartmentScope dept0 && dept0.DepartmentId <= 0) dept0.DepartmentId = ctx.DepartmentId;
            return;
        }

        if (ctx.IsSystem) return;

        // 原始SQL无法安全追加数据权限条件，安全优先直接拒绝，调用方可通过 Suppress 显式放开
        if (context.Action == DataMethod.FindSQL && !context.Sql.IsNullOrEmpty())
        {
            context.Abort(new XCodeException(L.T("Raw SQL query is not allowed when data scope filter is enabled. Use expression query or suppress the filter.")));
            return;
        }

        // 批量实体
        if (context.Entities != null && context.Entities.Count > 0)
        {
            foreach (var item in context.Entities)
            {
                if (!Validate(item, context, ctx)) return;
            }

            return;
        }

        // 单条实体
        if (context.Entity != null)
        {
            Validate(context.Entity, context, ctx);
            return;
        }

        // 查询
        if (context.IsQuery)
        {
            if (context.Factory == null) return;

            context.AppendWhere(DataScopeHelper.GetFilter(context.Factory, ctx));
        }
        // 条件删除：没有实体，直接把数据权限条件追加到 where
        else if (context.Builder != null && context.Action is (DataMethod.Delete or DataMethod.Update))
        {
            if (context.Factory == null) return;

            context.AppendWhere(DataScopeHelper.GetFilter(context.Factory, ctx));
        }
    }

    /// <summary>结果阶段过滤。缓存读取不经过SQL条件，需要在内存里再判断一次数据权限</summary>
    /// <param name="context">操作上下文</param>
    public override void OnExecuted(EntityContext context)
    {
        var ctx = GetContext();
        if (ctx == null || ctx.IsSystem || context.Result == null) return;

        if (context.Result is IEntity entity)
        {
            if (!CanAccess(entity, ctx)) context.Result = null;
        }
        else if (context.Result is IEnumerable<IEntity> list)
        {
            var rs = new List<IEntity>();
            var changed = false;
            foreach (var item in list)
            {
                if (CanAccess(item, ctx))
                    rs.Add(item);
                else
                    changed = true;
            }

            // 没有需要剔除的数据时保持原集合，避免无谓复制
            if (changed) context.Result = rs;
        }
    }

    private static Boolean Validate(IEntity entity, EntityContext context, DataScopeContext ctx)
    {
        switch (context.Action)
        {
            case DataMethod.Insert:
                return ValidateInsert(entity, context, ctx);
            case DataMethod.Upsert:
            case DataMethod.Replace:
                return ValidateUpsert(entity, context, ctx);
            case DataMethod.Update:
                // 无脏字段时不会产生写入，跳过校验
                if (!entity.HasDirty) return true;
                return ValidateUpdateOrDelete(entity, context, ctx);
            case DataMethod.Delete:
                return ValidateUpdateOrDelete(entity, context, ctx);
            default:
                return true;
        }
    }

    /// <summary>Upsert/Replace 场景：有键时可能更新已有行，必须校验归属</summary>
    private static Boolean ValidateUpsert(IEntity entity, EntityContext context, DataScopeContext ctx)
    {
        if (ctx.DataScope == DataScopes.全部) return true;

        // 已从数据库加载且没有脏字段时不会产生写入，跳过校验
        if (entity.IsFromDatabase && !entity.HasDirty) return true;

        if (entity.IsNullKey) return ValidateInsert(entity, context, ctx);

        if (!entity.IsFromDatabase)
        {
            // 按键 Upsert 可能更新他人的同键记录，安全优先要求先加载实体或显式 Suppress
            context.Abort(new XCodeException(L.T("Keyed upsert is not allowed under data scope. Load the entity first or suppress the filter.")));
            return false;
        }

        return ValidateUpdateOrDelete(entity, context, ctx);
    }

    private static Boolean ValidateInsert(IEntity entity, EntityContext context, DataScopeContext ctx)
    {
        if (ctx.DataScope == DataScopes.全部) return true;

        // 校验用户归属
        if (entity is IUserScope user)
        {
            if (user.UserId <= 0)
                user.UserId = ctx.UserId;
            else if (!DataScopeHelper.CanAccess(user, ctx))
            {
                context.Abort(new XCodeException(L.T("No permission to create data for user [{0}].", user.UserId)));
                return false;
            }
        }

        // 校验部门归属
        if (entity is IDepartmentScope dept)
        {
            if (dept.DepartmentId <= 0)
                dept.DepartmentId = ctx.DepartmentId;
            else if (ctx.AccessibleDepartmentIds != null && !ctx.AccessibleDepartmentIds.Contains(dept.DepartmentId))
            {
                context.Abort(new XCodeException(L.T("No permission to create data under department [{0}].", dept.DepartmentId)));
                return false;
            }
        }

        return true;
    }

    private static Boolean ValidateUpdateOrDelete(IEntity entity, EntityContext context, DataScopeContext ctx)
    {
        if (ctx.DataScope == DataScopes.全部) return true;

        if (CanAccess(entity, ctx)) return true;

        context.Abort(new XCodeException(L.T("No permission to operate this data (data ownership mismatch).")));

        return false;
    }

    private static Boolean CanAccess(IEntity entity, DataScopeContext ctx)
    {
        if (entity is IDataScope dataScope) return DataScopeHelper.CanAccess(dataScope, ctx);
        if (entity is IUserScope userScope) return DataScopeHelper.CanAccess(userScope, ctx);
        if (entity is IDepartmentScope deptScope) return DataScopeHelper.CanAccess(deptScope, ctx);

        return true;
    }

    private static DataScopeContext? GetContext()
    {
        // 构建上下文期间跳过过滤：上下文尚未就绪，且计算过程可能查询实体，禁止递归创建
        if (DataScopeContext.IsBuilding) return null;

        var ctx = DataScopeContext.Current;
        if (ctx != null) return ctx;

        var user = ManageProvider.User;
        if (user == null) return null;

        return DataScopeContext.Create(user, ManageProvider.CurrentMenu);
    }
}
