using NewLife.Threading;

namespace XCode;

/// <summary>时间过滤器。自动填充创建时间和更新时间</summary>
public class TimeFilter : EntityFilter
{
    #region 静态引用
    /// <summary>字段名</summary>
    public class __
    {
        /// <summary>创建时间</summary>
        public static String CreateTime = nameof(CreateTime);

        /// <summary>更新时间</summary>
        public static String UpdateTime = nameof(UpdateTime);
    }
    #endregion

    /// <summary>初始化。检查是否匹配</summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    protected override Boolean OnInit(Type entityType)
    {
        var fs = GetFields(entityType);
        foreach (var fi in fs)
        {
            if (fi.Type == typeof(DateTime) && fi.Name.EqualIgnoreCase(__.CreateTime, __.UpdateTime))
                return true;
        }

        return false;
    }

    /// <summary>操作执行前填充时间字段</summary>
    /// <param name="context">操作上下文</param>
    public override void OnExecuting(EntityContext context)
    {
        if (context.Action == DataMethod.Create)
        {
            if (context.ForEdit && context.Entity != null) Fill(context.Entity, DataMethod.Insert);
            return;
        }

        if (context.Action is not (DataMethod.Insert or DataMethod.Update)) return;

        foreach (var entity in GetEntities(context))
        {
            if (context.Action == DataMethod.Update && !entity.HasDirty) continue;

            Fill(entity, context.Action);
        }
    }

    private void Fill(IEntity entity, DataMethod method)
    {
        var fs = GetFields(entity.GetType());

        switch (method)
        {
            case DataMethod.Insert:
                SetItem(fs, entity, __.CreateTime, TimerX.Now);
                SetItem(fs, entity, __.UpdateTime, TimerX.Now);
                break;
            case DataMethod.Update:
                SetNoDirtyItem(fs, entity, __.UpdateTime, TimerX.Now);
                break;
        }
    }
}

/// <summary>时间拦截器。兼容旧类名，实际实现见 <see cref="TimeFilter"/></summary>
[Obsolete("请使用 TimeFilter；运行 xcode 可自动迁移")]
public class TimeInterceptor : TimeFilter { }