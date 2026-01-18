using NewLife.Threading;

namespace XCode;

/// <summary>时间拦截器。自动填充创建时间和更新时间</summary>
public class TimeInterceptor : EntityInterceptor
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

    /// <summary>创建实体对象</summary>
    /// <param name="entity"></param>
    /// <param name="forEdit"></param>
    protected override void OnCreate(IEntity entity, Boolean forEdit)
    {
        if (forEdit) OnValid(entity, DataMethod.Insert);
    }

    /// <summary>验证数据，自动加上创建和更新的信息</summary>
    /// <param name="entity"></param>
    /// <param name="method"></param>
    protected override Boolean OnValid(IEntity entity, DataMethod method)
    {
        if (method == DataMethod.Delete) return true;
        if (method == DataMethod.Update && !entity.HasDirty) return true;

        var fs = GetFields(entity.GetType());

        switch (method)
        {
            case DataMethod.Insert:
                SetItem(fs, entity, __.CreateTime, TimerX.Now);
                SetItem(fs, entity, __.UpdateTime, TimerX.Now);
                break;
            case DataMethod.Update:
                // 不管新建还是更新，都改变更新时间
                SetNoDirtyItem(fs, entity, __.UpdateTime, TimerX.Now);
                break;
            case DataMethod.Delete:
                break;
            default:
                break;
        }

        return true;
    }
}

/// <summary>时间模型。旧版名称，建议使用 TimeInterceptor</summary>
[Obsolete("请使用 TimeInterceptor")]
public class TimeModule : TimeInterceptor { }
