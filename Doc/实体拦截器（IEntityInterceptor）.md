# 实体拦截器（IEntityInterceptor，兼容层）

> 自 v12.4 起，[实体操作过滤器（IEntityFilter）](实体过滤器（IEntityFilter）.md) 是唯一主管道。`IEntityInterceptor` 和 `EntityInterceptor` 保留编译兼容，已标记 `[Obsolete]`；新代码请实现 `IEntityFilter` 或继承 `EntityFilter`。

## 兼容范围

- 旧自定义 `IEntityInterceptor` 实现可以继续注册；内部由 `LegacyEntityFilter` 适配到过滤器管道。
- 旧 `EntityInterceptor` 派生类可以继续编译；旧钩子 `Create/Valid/Query/Filter` 会映射到过滤器前置/后置。
- `TimeInterceptor`、`UserInterceptor`、`IPInterceptor`、`TraceInterceptor` 是空壳类，实际逻辑在 `TimeFilter/UserFilter/IPFilter/TraceFilter`；空壳类已标记 `[Obsolete]`。
- `TenantInterceptor`、`DataScopeInterceptor` 同样是空壳类，实际逻辑在 `TenantFilter/DataScopeFilter`，也已标记 `[Obsolete]`。
- 过滤器集合以 `Meta.Filters` 为准，`Meta.Interceptors` 是它的过时别名。

## 迁移建议

- 新代码：实现 `IEntityFilter` 或继承 `EntityFilter`，用 `Meta.Filters.Add<T>()` 注册。
- 旧拦截器代码：可以继续编译但会提示过时；运行一次 `xcode` 即可把 `Meta.Interceptors.Add<XxxInterceptor>()` 迁移为 `Meta.Filters.Add<XxxFilter>()`。
- Module 时代写法（`IEntityModule`、`EntityModules`、`Meta.Modules`、`*Module`）已删除，由代码生成器迁移到最新结构。