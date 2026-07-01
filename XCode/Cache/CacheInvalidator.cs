using NewLife;
using NewLife.Caching;
using NewLife.Log;
using XCode.DataAccessLayer;

namespace XCode.Cache;

/// <summary>二级缓存失效协调器。通过分布式 ICache 实现跨进程缓存失效通知</summary>
/// <remarks>
/// 基于分布式缓存的版本号机制：每次清除缓存时递增版本号，其它进程检测到版本变化后主动过期本地缓存。
/// 配置示例：
/// <code>
/// XCodeSetting.Current.CacheProvider = new FullRedis("server=127.0.0.1:6379;db=0");
/// </code>
/// </remarks>
public static class CacheInvalidator
{
    /// <summary>分布式缓存提供者</summary>
    public static ICache? Provider => XCodeSetting.Current.CacheProvider;

    private const String KeyPrefix = "XCode:Cache:Ver";

    /// <summary>通知所有进程清除指定实体类型的缓存。递增分布式版本号</summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="reason">清除原因</param>
    public static void Invalidate(Type entityType, String? reason = null)
    {
        var cache = Provider;
        if (cache == null) return;

        try
        {
            var key = $"{KeyPrefix}:{entityType.FullName}";
            var ver = cache.Get<Int64>(key);
            cache.Set(key, ver + 1);

            if (DAL.Debug)
                DAL.WriteLog("CacheInvalidator: 递增 [{0}] 缓存版本号 {1} => {2}，原因={3}", entityType.Name, ver, ver + 1, reason);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }

    /// <summary>获取当前分布式缓存版本号</summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>版本号，没有分布式缓存时返回0</returns>
    public static Int64 GetVersion(Type entityType)
    {
        var cache = Provider;
        if (cache == null) return 0;

        try
        {
            var key = $"{KeyPrefix}:{entityType.FullName}";
            return cache.Get<Int64>(key);
        }
        catch
        {
            return 0;
        }
    }
}
