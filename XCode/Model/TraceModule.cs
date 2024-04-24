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

    #region 属性
    /// <summary>允许合并。字段内允许合并保存多个TraceId，串在一个调用链上显示。默认false</summary>
    public Boolean AllowMerge { get; set; }
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
    /// <param name="method"></param>
    protected override Boolean OnValid(IEntity entity, DataMethod method)
    {
        if (method == DataMethod.Delete) return true;
        if (method == DataMethod.Update && !entity.HasDirty) return true;

        var traceId = DefaultSpan.Current?.TraceId;
        if (!traceId.IsNullOrEmpty() && traceId.Length < 50)
        {
            var fs = GetFields(entity.GetType());

            if (AllowMerge)
            {
                // 多编码合并
                var old = entity[__.TraceId] as String;
                var ss = old?.Split(',').ToList();
                if (ss != null && ss.Count > 0 && !ss.Contains(traceId))
                {
                    ss.Add(traceId);

                    // 最大长度
                    var fi = fs.FirstOrDefault(e => e.Name.EqualIgnoreCase(__.TraceId));
                    var len = fi.Length > 0 ? fi.Length : 50;

                    // 倒序取最后若干项
                    var rs = ss.Join(",");
                    while (rs.Length > len)
                    {
                        ss.RemoveAt(0);
                        rs = ss.Join(",");
                    }

                    if (!rs.IsNullOrEmpty()) traceId = rs;
                }
            }

            SetNoDirtyItem(fs, entity, __.TraceId, traceId);
        }

        return true;
    }
}