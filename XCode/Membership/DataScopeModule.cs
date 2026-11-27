namespace XCode.Membership;

/// <summary>数据权限拦截器。兼容旧类名，实际实现见 <see cref="DataScopeFilter"/></summary>
[Obsolete("请使用 DataScopeFilter；运行 xcode 可自动迁移")]
public class DataScopeInterceptor : DataScopeFilter { }