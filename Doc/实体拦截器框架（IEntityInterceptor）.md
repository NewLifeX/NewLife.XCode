# 实体拦截器框架（兼容层）

`EntityInterceptor` 是旧拦截器的抽象基类，保留用于编译兼容，已标记 `[Obsolete]`。它同时把旧钩子适配到 `IEntityFilter` 管道：

- `OnCreate` -> `OnExecuting` 的 `Create`
- `OnValid` -> `OnExecuting` 的写入
- `OnQuery` -> `OnExecuting` 的查询条件
- `OnFilter` -> `OnExecuted` 的结果过滤

新代码请直接继承 `EntityFilter` 或实现 `IEntityFilter`，不再使用本基类。Module 时代类型（`IEntityModule`、`EntityModules`、`Meta.Modules`）已删除，由代码生成器迁移。