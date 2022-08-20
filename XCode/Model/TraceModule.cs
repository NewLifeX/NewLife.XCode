using System;
using NewLife;
using NewLife.Log;

namespace XCode;

/// <summary>链路追踪过滤器。自动给TraceId赋值</summary>
public class TraceModule : EntityModule
{
    #region 静态引用
    /// <summary>字段名</summary>
    public class __
    {
        /// <summary>链路追踪。用于APM性能追踪定位，还原该事件的调用链</summary>
        public static String TraceId = nameof(TraceId);
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
            if (fi.Type == typeof(String))
            {
                if (fi.Name.EqualIgnoreCase(__.TraceId)) return true;
            }
        }

        return false;
    }

    /// <summary>验证数据，自动加上创建和更新的信息</summary>
    /// <param name="entity"></param>
    /// <param name="isNew"></param>
    protected override Boolean OnValid(IEntity entity, Boolean isNew)
    {
        if (!isNew && !entity.HasDirty) return true;

        var traceId = DefaultSpan.Current?.TraceId;
        if (!traceId.IsNullOrEmpty())
        {
            var fs = GetFields(entity.GetType());

            // 不管新建还是更新，都改变更新
            SetNoDirtyItem(fs, entity, __.TraceId, traceId);
        }

        return true;
    }
}