using System.Collections.Concurrent;
using XCode.Configuration;
using XCode.Membership;

namespace XCode;

/// <summary>IP地址过滤器。自动填充创建IP和更新IP</summary>
public class IPFilter : EntityFilter
{
    #region 静态引用
    /// <summary>字段名</summary>
    public class __
    {
        /// <summary>创建IP</summary>
        public static String CreateIP = nameof(CreateIP);

        /// <summary>更新IP</summary>
        public static String UpdateIP = nameof(UpdateIP);
    }
    #endregion

    #region 属性
    /// <summary>允许空内容。在没有当前IP信息时，是否允许填充空内容，若允许可能是清空上一次更新IP。默认false</summary>
    public Boolean AllowEmpty { get; set; }
    #endregion

    /// <summary>初始化。检查是否匹配</summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    protected override Boolean OnInit(Type entityType)
    {
        var fs = GetFields(entityType);
        foreach (var fi in fs)
        {
            if (fi.Type == typeof(String) && fi.Name.EqualIgnoreCase(__.CreateIP, __.UpdateIP))
                return true;
        }

        var fs2 = GetIPFieldNames(entityType);
        return fs2 != null && fs2.Length > 0;
    }

    /// <summary>操作执行前填充IP字段</summary>
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

        var ip = ManageProvider.UserHost;

        // 新增时如果没有IP信息，尝试获取当前IP。更新时不适用，避免原来的更新IP被覆盖为本机IP
        if (ip.IsNullOrEmpty() && method == DataMethod.Insert)
            ip = NetHelper.MyIP()?.ToString();

        if (!ip.IsNullOrEmpty())
        {
            // 如果不是IPv6，去掉后面端口
            var p = ip.IndexOf("://");
            if (p >= 0) ip = ip.Substring(p + 3);

            switch (method)
            {
                case DataMethod.Insert:
                    var fs2 = GetIPFieldNames(entity.GetType());
                    if (fs2 != null)
                    {
                        foreach (var fi in fs2)
                        {
                            SetItem(fs2, entity, fi.Name, ip);
                        }
                    }
                    break;
                case DataMethod.Update:
                    // 不管新建还是更新，都改变更新
                    SetNoDirtyItem(fs, entity, __.UpdateIP, ip);
                    break;
            }
        }
        else if (AllowEmpty)
        {
            // 不管新建还是更新，都改变更新
            SetNoDirtyItem(fs, entity, __.UpdateIP, ip);
        }
    }

    private static readonly ConcurrentDictionary<Type, FieldItem[]> _ipFieldNames = new();
    /// <summary>获取实体类的字段名。带缓存</summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    protected static FieldItem[] GetIPFieldNames(Type entityType) => _ipFieldNames.GetOrAdd(entityType, t => GetFields(t).Where(e => e.Name.EqualIgnoreCase(__.CreateIP, __.UpdateIP)).ToArray());
}

/// <summary>IP地址拦截器。兼容旧类名，实际实现见 <see cref="IPFilter"/></summary>
[Obsolete("请使用 IPFilter；运行 xcode 可自动迁移")]
public class IPInterceptor : IPFilter { }