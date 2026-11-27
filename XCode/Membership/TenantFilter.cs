namespace XCode.Membership;

/// <summary>租户过滤器。为实现了 <see cref="ITenantScope"/> 的实体提供全路径租户隔离</summary>
/// <remarks>
/// 覆盖单条/批量增删改与查询；缓存读取在结果阶段按租户再判断一次。
/// <para>系统维护或跨租户管理请使用 <see cref="EntityFilterScope.SuppressAll"/> 显式放开。</para>
/// </remarks>
public class TenantFilter : EntityFilter
{
    /// <summary>实例化租户过滤器</summary>
    public TenantFilter()
    {
        Order = -1000;
    }

    /// <summary>为指定实体类初始化过滤器</summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>实体是否实现 <see cref="ITenantScope"/></returns>
    protected override Boolean OnInit(Type entityType) => typeof(ITenantScope).IsAssignableFrom(entityType);

    /// <summary>操作执行前应用租户隔离</summary>
    /// <param name="context">操作上下文</param>
    public override void OnExecuting(EntityContext context)
    {
        var tenantId = TenantContext.CurrentId;
        if (tenantId <= 0) return;

        // 原始SQL无法安全追加租户条件，安全优先直接拒绝，调用方可通过 Suppress 显式放开
        if (context.Action == DataMethod.FindSQL && !context.Sql.IsNullOrEmpty())
        {
            context.Abort(new XCodeException(L.T("Raw SQL query is not allowed when tenant filter is enabled. Use expression query or suppress the filter.")));
            return;
        }

        // 创建实体对象时只补齐空租户，不做校验；数据加载阶段可能带入其它租户的原始行
        if (context.Action == DataMethod.Create)
        {
            if (context.Entity is ITenantScope tenant0 && tenant0.TenantId == 0) tenant0.TenantId = tenantId;
            return;
        }



        var field = context.Factory == null ? null : TenantSourceHelper.GetTenantField(context.Factory);
        if (field == null) return;

        // 批量实体
        if (context.Entities != null && context.Entities.Count > 0)
        {
            foreach (var item in context.Entities)
            {
                if (item is not ITenantScope tenant) continue;

                switch (context.Action)
                {
                    case DataMethod.Insert:
                        Fill(tenant, tenantId, context);
                        break;
                    case DataMethod.Upsert:
                    case DataMethod.Replace:
                        FillOrCheckUpsert(tenant, tenantId, context);
                        break;
                    case DataMethod.Update:
                        if (item is IEntity ie && !ie.HasDirty) break;
                        Check(tenant, tenantId, context);
                        break;
                    case DataMethod.Delete:
                        Check(tenant, tenantId, context);
                        break;
                    default:
                        FillOrCheckUpsert(tenant, tenantId, context);
                        break;
                }
            }

            return;
        }

        // 单条实体
        if (context.Entity is ITenantScope entity)
        {
            switch (context.Action)
            {
                case DataMethod.Insert:
                    Fill(entity, tenantId, context);
                    break;
                case DataMethod.Upsert:
                case DataMethod.Replace:
                    FillOrCheckUpsert(entity, tenantId, context);
                    break;
                case DataMethod.Update:
                    if (context.Entity is IEntity ie && !ie.HasDirty) break;
                    Check(entity, tenantId, context);
                    break;
                case DataMethod.Delete:
                    Check(entity, tenantId, context);
                    break;
                default:
                    FillOrCheckUpsert(entity, tenantId, context);
                    break;
            }

            return;
        }

        // 查询
        if (context.IsQuery)
        {
            context.AppendWhere(field.Equal(tenantId));
        }
        // 条件删除：没有实体，直接把租户条件追加到 where
        else if (context.Builder != null && context.Action is (DataMethod.Delete or DataMethod.Update))
        {
            context.AppendWhere(field.Equal(tenantId));
        }
    }

    /// <summary>结果阶段过滤。缓存读取不经过SQL条件，需要在内存里再判断一次租户归属</summary>
    /// <param name="context">操作上下文</param>
    public override void OnExecuted(EntityContext context)
    {
        var tenantId = TenantContext.CurrentId;
        if (tenantId <= 0 || context.Result == null) return;

        if (context.Result is ITenantScope entity)
        {
            if (entity.TenantId != tenantId) context.Result = null;
        }
        else if (context.Result is IEnumerable<IEntity> list)
        {
            var rs = new List<IEntity>();
            var changed = false;
            foreach (var item in list)
            {
                if (item is ITenantScope tenant && tenant.TenantId != tenantId)
                    changed = true;
                else
                    rs.Add(item);
            }

            // 没有需要剔除的数据时保持原集合，避免无谓复制
            if (changed) context.Result = rs;
        }
    }

    /// <summary>填充租户标识，已有其它租户标识时中止</summary>
    /// <param name="entity">实体</param>
    /// <param name="tenantId">当前租户</param>
    /// <param name="context">操作上下文</param>
    protected virtual void Fill(ITenantScope entity, Int32 tenantId, EntityContext context)
    {
        if (entity.TenantId == 0)
            entity.TenantId = tenantId;
        else if (entity.TenantId != tenantId)
            context.Abort(new XCodeException(L.T("Cannot create data for another tenant [{0}].", entity.TenantId)));
    }

    /// <summary>校验租户归属</summary>
    /// <param name="entity">实体</param>
    /// <param name="tenantId">当前租户</param>
    /// <param name="context">操作上下文</param>
    protected virtual void Check(ITenantScope entity, Int32 tenantId, EntityContext context)
    {
        if (entity.TenantId != tenantId)
            context.Abort(new XCodeException(L.T("Cannot modify data of another tenant [{0}].", entity.TenantId)));
    }

    /// <summary>Upsert/Replace 场景：有键时可能更新已有行，不能仅凭 0 值直接补租户</summary>
    private void FillOrCheckUpsert(ITenantScope entity, Int32 tenantId, EntityContext context)
    {
        if (entity is not IEntity ie)
        {
            context.Abort(new XCodeException(L.T("Tenant filter requires entity to implement IEntity when upserting.")));
            return;
        }

        // 已从数据库加载且没有脏字段时不会产生写入，跳过校验
        if (ie.IsFromDatabase && !ie.HasDirty) return;

        if (ie.IsNullKey)
        {
            Fill(entity, tenantId, context);
        }
        else if (ie.IsFromDatabase)
        {
            Check(entity, tenantId, context);
        }
        else
        {
            // 按键 Upsert 可能更新其它租户的同键记录，安全优先要求先加载实体或显式 Suppress
            context.Abort(new XCodeException(L.T("Keyed upsert is not allowed in tenant scope. Load the entity first or suppress the filter.")));
        }
    }
}
