# 实体操作过滤器（IEntityFilter）

`IEntityFilter` 是 XCode 面向下游（重点魔方 Cube）提供的实体层操作干涉机制，也是**唯一的主管道**。它在添、删、改、查的**前后**执行，统一覆盖单条、批量、异步查询，主要用于多租户隔离、行级数据权限、审计、软删除、数据脱敏等场景。

> 自 v12.4 起，旧的 `IEntityInterceptor` 已标记为过时，只作为兼容层存在。内置的时间/用户/IP/链路过滤器各自配一个同名空壳拦截器类（`TimeInterceptor` 等），旧实体静态构造函数继续注册也能走本管道；新代码一律实现或注册 `IEntityFilter`。

---

## 1. 接口定义

```csharp
public interface IEntityFilter
{
    Int32 Order { get; }                 // 越小越先执行
    Boolean Init(Type entityType);       // 是否支持该实体
    void OnExecuting(EntityContext ctx); // 操作前
    void OnExecuted(EntityContext ctx);  // 操作后
}
```

前置过滤器按 `Order` 正序执行，后置过滤器逆序执行，共享同一个上下文。

---

## 2. EntityContext

每次操作都会创建一个独立上下文，只在本次过滤器管道内传递，过滤器可以读取和修改：

| 字段 | 说明 |
|------|------|
| `Action` | 操作类型：Create/Insert/Update/Delete/Upsert/Replace/Find/FindAll/FindCount/FindSQL |
| `IsQuery` / `IsWrite` | 操作分类，按 `Action` 的取值范围判断 |
| `ForEdit` | 创建实体对象时是否用于编辑 |
| `Factory` / `Session` | 实体工厂与实体会话 |
| `Entity` | 单条操作的实体 |
| `Entities` | 批量写入的实体列表；非 null 即表示批量操作，批量不再使用独立 `Action` |
| `Builder` | 查询生成器，可直接改 `Where`/`OrderBy`/`Column` |
| `Sql` | 原始 SQL |
| `Result` / `AffectedRows` | 操作结果（写入行数/查询数据表/计数） |
| `Exception` / `ExceptionHandled` | 异常与是否已处理 |
| `Cancel` | 取消真实操作，后置过滤器仍会执行 |
| `Items` / `context["key"]` | 过滤器之间传递业务数据，实现 `IExtend` |

辅助方法：

```csharp
context.AppendWhere(expression);          // 追加 Builder.Where
context.AppendWhere("TenantId=1");        // 追加字符串条件，调用方负责转义
context.Abort(exception);                 // 失败关闭：取消操作并抛出异常
context.GetEntity<TEntity>();             // 取当前实体
```

### 2.1 上下文生命周期

- 每个拦截点直接 `new EntityContext()`，设置本次操作的数据后，通过 `Meta.Filters.Execute` 传给过滤器；不再有 `Current`、`Use` 或 `AsyncLocal` 当前上下文。
- 调用方只有一条路径：不论有没有过滤器都走 `Execute`。没有过滤器、批量内部重入或被 `EntityFilterScope` 全部抑制时，管道直接执行真实操作，因此不需要在业务代码里判断 `HasFiltersFor`。
- 外部不参与内部上下文，嵌套操作拥有自己的上下文，不会覆盖外层 `Action/Entity/Result`。
- 过滤器之间需要共享数据时使用 `Items` 或索引器；跨操作的用户、租户、数据权限信息继续使用 `TenantContext`、`DataScopeContext` 等专用上下文。

---

## 3. 注册与执行顺序

```csharp
static Order()
{
    Meta.Filters.Add<TimeFilter>();
    Meta.Filters.Add<TenantFilter>();                   // 纯过滤器也可用 Add<T>
    Meta.Filters.Add<DataScopeFilter>();          // 与 Add<T> 等价，参数是实例
}

// 应用启动时全局注册
EntityFilters.Global.Add<TimeFilter>();
EntityFilters.Global.Add<TenantFilter>();
EntityFilters.Global.Add<DataScopeFilter>();
```

> `Add(IEntityFilter)` 注册过滤器；`Add(IEntityInterceptor)` 是过时的兼容重载；泛型 `Add<T>()` 按实例类型自动分派（C# 不允许仅按约束重载泛型方法，所以只能有一个 `Add<T>()`）。
>
> 兼容约定：`Meta.Interceptors` 是 `Meta.Filters` 的过时别名，`TimeInterceptor` 等空壳类也已标记 `[Obsolete]`，旧注册仍可运行但会给出编译警告；运行代码生成器即可把它们迁移为 `Meta.Filters.Add<TimeFilter>()` / `Meta.Filters.Add<TenantFilter>()`。

执行顺序：

```
实体级 + 全局过滤器合并，按 Order 升序（同 Order 时实体级在前）
    → 真实操作（Valid → 雪花Id → 分库分表 → 持久化 → 缓存更新）
    → 后置过滤器逆序执行
```

内置优先级（在各过滤器构造函数中写死）：

| 过滤器 | Order | 用途 |
|--------|------:|------|
| `TenantFilter` | -1000 | 多租户 |
| `DataScopeFilter` | -900 | 数据权限 |
| 其它过滤器 | 0 | 审计、通知等 |

需要更早或更晚执行时，直接设置过滤器的 `Order` 属性；不再提供 `EntityFilterOrder` 常量类。

---

## 4. 覆盖矩阵

| 操作 | 是否覆盖 | 说明 |
|------|:--------:|------|
| 实体对象创建 (`CreateInstance`) | ✅ | 执行 `DataMethod.Create`，租户/数据权限过滤器补齐空字段 |
| `entity.Insert/Update/Delete/Save` | ✅ | 包含 Valid、雪花Id、分库分表、缓存更新全过程 |
| `InsertAsync/UpdateAsync/DeleteAsync` | ✅ | 异步同样执行同步过滤器 |
| `list.Insert/Update/Delete/Save/Upsert` | ✅ | 每批触发一次，上下文提供 `Entities` |
| `BatchInsert/BatchInsertIgnore/BatchReplace/BatchUpdate/BatchUpsert` | ✅ | 直接调用批量原语也会触发 |
| 单条 `Upsert` | ✅ | |
| `Entity.Update(setClause, whereClause)` / `Delete(whereClause)` / `Delete(Expression)` | ✅ | where 会交给过滤器追加租户/数据权限条件 |
| `Entity.Update(setNames, setValues, whereNames, whereValues)` / `Delete(names, values)` | ⚠️ | 直接执行，不经过过滤器，调用方自行保证条件安全 |
| `Entity.Insert(names, values)` | ⚠️ | 直接执行，不经过过滤器 |
| `Find/FindAll/FindCount` | ✅ | 在 EntitySession 查询层拦截，可改 Builder/SQL 与结果数据表 |
| `FindAllAsync/FindCountAsync` | ✅ | |
| `FindAll(String sql)` | ✅ | 过滤器可改 SQL；租户/数据权限过滤器默认拒绝原始 SQL |
| `FindAllWithCache/FindByKeyWithCache/FindBySlaveWithCache` | ✅ | 缓存加载不做过滤，读取时执行后置阶段，按租户/数据权限裁剪结果 |
| `Meta.Cache.Entities` / `Meta.Cache.Find/FindAll` | ❌ | 直接读取缓存内部数据，不经后置过滤；请改用 `FindAllWithCache` |
| `IEntityPersistence`、`session.Execute`、DAL 直连 | ❌ | 低于实体层，不经过过滤器 |

### 4.1 零开销

无过滤器时不构造上下文，走原有路径。

### 4.2 批量粒度

批量操作**每批触发一次**过滤器，`Action` 与单条操作相同（如 `Insert`），通过 `Entities` 非 null 区分批量。需要逐条处理时由过滤器自行遍历。

### 4.3 与旧 API 的关系

`DataMethod` 已从“添删改方法”扩展为统一的实体操作类型，新增 `Create` 与 `Find*` 值；原有 `Insert/Update/Delete/Upsert/Replace` 数值不变，旧 `EntityBase.Valid(DataMethod)` 重写代码无需修改。

查询操作从 21 开始，与写操作值隔开；旧 `IEntityInterceptor.Query` 也直接使用 `DataMethod`。

### 4.4 拦截场景与上下文成员

| 场景 | 需要的上下文成员 |
|------|------------------|
| 创建实体对象 | `Action`、`Factory`、`Session`、`Entity`、`ForEdit` |
| 单条 Insert/Update/Delete/Upsert/Replace | `Action`、`Factory`、`Session`、`Entity`、`Result`、`AffectedRows` |
| 批量 Insert/Update/Delete/Upsert/Replace | `Action`、`Factory`、`Session`、`Entities`、`Result`、`AffectedRows` |
| `Find/FindAll/FindCount` | `Action`、`Factory`、`Session`、`Builder`、`Result` |
| 原始 SQL `FindSQL` | `Action`、`Factory`、`Session`、`Sql`、`Result` |
| 所有场景的管道控制 | `Cancel`、`Exception`、`ExceptionHandled`、`Items` |

只保留添删改查主路径需要的数据。分页参数、嵌套深度和 `Handled` 已移除；分页需要过滤时直接改 `Builder`，不需要额外成员。

---

## 5. 多租户

框架提供 `TenantFilter`，实体实现 `ITenantScope` 即自动生效：

```csharp
public partial class Order : Entity<Order>, ITenantScope
{
    public Int32 TenantId { get; set; }
}

static Order()
{
    Meta.Filters.Add<TenantFilter>();
}
```

行为：

- Insert/批量 Insert：租户标识为 0 时自动填充当前租户；已有其它租户标识时拒绝。
- Update/Delete 单条：校验实体租户归属，不一致拒绝。
- 查询/计数：自动追加租户条件。
- 静态条件写：`Update(setClause, whereClause)`、`Delete(whereClause)`、`Delete(Expression)` 会自动追加租户条件；`Insert` 和按属性名/值的重载不经过过滤器。
- 缓存查询：缓存本体保存整表原始数据，加载时抑制过滤器；`FindAllWithCache/FindByKeyWithCache/FindBySlaveWithCache` 读取时执行后置阶段，按租户/数据权限裁剪。
- 原始 SQL：默认拒绝，避免绕过租户隔离。

`TenantContext.CurrentId == 0` 表示未进入具体租户（管理后台）；如需更严格，可在业务侧要求必须进入租户。

---

## 6. 数据权限

框架提供 `DataScopeFilter`，实体实现 `IDataScope`/`IUserScope`/`IDepartmentScope` 即自动生效：

```csharp
static Order()
{
    Meta.Filters.Add<DataScopeFilter>();
}
```

- 查询：追加 `DataScopeHelper.GetFilter` 条件。
- 单条/批量写入：校验用户/部门归属；新增时自动填充当前用户/部门。
- 静态条件写：`Update(setClause, whereClause)`、`Delete(whereClause)`、`Delete(Expression)` 会自动追加数据权限条件；`Insert` 和按属性名/值的重载不经过过滤器。
- 缓存查询：读取时执行后置阶段，按租户/数据权限裁剪。
- 原始 SQL：默认拒绝。
- 复用 `DataScopeContext.IsBuilding` 防止构建上下文时递归。

---

## 7. 显式绕过

系统维护、跨租户管理、数据修复等场景必须显式声明：

```csharp
using (EntityFilterScope.Suppress<TenantFilter>())
{
    // 此范围内租户过滤器不执行
}

using (EntityFilterScope.SuppressAll())
{
    // 此范围内所有实体过滤器不执行
}

// 作用域覆盖整个方法时，可以写得更短
using var suppress = EntityFilterScope.SuppressAll();
```

`SuppressAll` 用于两类明确场景：实体缓存加载必须读取整表，系统维护必须允许跨租户处理；如果缓存加载也走租户过滤，缓存会被裁成当前租户的数据，其它租户读取就会出错。业务代码不要用它绕过权限，除非确实在做系统维护。

批量操作在外层执行过一次过滤器后，内部逐条写入由管道内部的重入标记跳过，不借用本作用域。

`EntityFilterScope` 只表达“显式绕过”，不参与管道内部流程控制；批量重入与递归保护由内部 `EntityPipeline` 负责，公共作用域里不再有“是否正在执行过滤器”“是否批量内层”这类状态。

支持嵌套，支持按基类/接口抑制；net45 下使用 ThreadLocal，异步流不传递。

> `Suppress` 抑制的是过滤器管道中的全部过滤器。`TenantInterceptor`/`DataScopeInterceptor` 是 `TenantFilter`/`DataScopeFilter` 的空壳子类，因此 `Suppress<TenantFilter>()` 对两种注册方式都生效。

> 静态条件写：`Update(setClause, whereClause)`、`Delete(whereClause)`、`Delete(Expression)` 会把 where 交给过滤器追加权限条件；`Insert(names, values)` 和按属性名/值的 `Update/Delete` 重载直接执行，不经过过滤器。

---

## 8. 失败关闭

- 安全过滤器校验失败调用 `context.Abort(exception)`，真实操作不执行，后置过滤器完成后抛出。
- 租户/数据权限过滤器默认拒绝“未加载实体直接按键 Upsert/Replace”，避免用同键覆盖其它租户或他人的数据；需要时先加载实体，或显式 `Suppress`。
- 静态条件写：`Update(setClause, whereClause)`、`Delete(whereClause)`、`Delete(Expression)` 会经过过滤器追加条件；按属性名/值和 `Insert` 重载直接执行。
- 过滤器自身异常会写入 `context.Exception`；`ExceptionHandled=true` 时吞掉异常并返回 `AffectedRows`。

---

## 9. 缓存策略

- 实体缓存（`EntityCache`）是表级共享缓存，加载时使用 `EntityFilterScope.SuppressAll()` 保存整表原始数据。
- `FindAllWithCache/FindByKeyWithCache/FindBySlaveWithCache` 读取时会执行过滤器的后置阶段（租户/数据权限再判断一次）；直接访问 `Meta.Cache.Entities` 等缓存内部成员仍不经过过滤器，业务代码请使用前面三个方法。
- 多租户或行级数据权限实体不要使用这些缓存 API；需要权限隔离时使用数据库查询（`FindAll/FindByKey`）。
- 过滤器不需要声明 `ApplyToCache`：缓存加载自动抑制过滤器，读取时统一走 `OnExecuted`，因此任何过滤器都能在 `OnExecuted` 里处理缓存结果。

---

## 10. 与旧接口的关系

`IEntityInterceptor` 不再是一条独立管道，已标记 `[Obsolete]`。两者关系如下：

| 角色 | 说明 |
|------|------|
| `IEntityFilter` | 唯一主管道，负责 `OnExecuting/OnExecuted`、优先级、短路和异常；`EntityFilterScope` 只负责显式绕过 |
| `EntityInterceptor` | 旧基类，同时实现 `IEntityFilter`；把 `Create/Valid/Query/Filter` 映射到过滤器的前置/后置阶段 |
| `IEntityInterceptor` | 兼容接口，仅用于编译旧代码；直接实现它的类由 `LegacyEntityFilter` 包装后进入同一管道 |
| `TenantInterceptor` / `DataScopeInterceptor` | 继承新过滤器的空壳类，已标记 `[Obsolete]`，实体静态构造函数暂时无需修改 |

因此，下游不需要同时维护两套拦截逻辑；旧实现只要注册到 `Meta.Filters`，最终都会进入 `IEntityFilter` 管道。

## 11. 兼容与迁移

### 11.1 旧注册方式仍可运行（会给出过时警告）

```csharp
// 兼容期继续可用，内部进入过滤器管道，但会出现 CS0618 警告
#pragma warning disable CS0618
Meta.Interceptors.Add<TimeInterceptor>();
Meta.Interceptors.Add(new UserInterceptor { AllowEmpty = false });
EntityInterceptors.Global.Add<TenantInterceptor>();
#pragma warning restore CS0618
```

- 推荐写法：`Meta.Filters.Add<TimeFilter>()`；旧写法 `Meta.Interceptors.Add<TimeInterceptor>()` 等价，但集合与空壳类都已标记 `[Obsolete]`，运行一次 `xcode` 即可迁移。
- `Add` / `Add<T>` 不再标记过时；`Add<T>` 同时支持 `IEntityFilter` 和旧 `IEntityInterceptor`。
- `Add(IEntityInterceptor)` 是过时重载，内部用 `LegacyEntityFilter` 包一层；新代码只实现 `IEntityFilter`。
- `TenantInterceptor`/`DataScopeInterceptor` 是 `TenantFilter`/`DataScopeFilter` 的空壳子类，行为与新过滤器一致。
- 直接实现 `IEntityInterceptor` 的自定义类会由 `LegacyEntityFilter` 包装；`Create/Valid/Query/Filter` 分别映射到创建、写入校验、查询条件、结果过滤。
- 集合类型为 `EntityFilters`，过滤器数组属性为 `Items`；`EntityInterceptors`、`Filters`、`Interceptors` 都保留为过时别名。

### 11.2 行为变化

- 违规统一抛 `XCodeException`，不再返回 `false` 或静默放行。
- 静态条件写（`Update(setClause, whereClause)`、`Delete(whereClause)`、`Delete(Expression)`）会把 where 交给过滤器追加租户/数据权限条件；`Insert` 和按属性名/值的重载直接执行。
- 新增 `DataMethod.Create`：`CreateInstance` 时执行过滤器，租户/数据权限过滤器会补齐空的租户、用户、部门字段。
- `EntityContext` 改为每次操作独立创建，移除 `Current/Use` 环境上下文，以及 `Depth/Handled/StartRowIndex/MaximumRows`；`Items` 实现 `IExtend`，过滤器间可用索引器共享数据。
- 批量操作不再定义 `BatchInsert` 等独立 `Action`，统一使用 `Insert/Update/Delete/Upsert/Replace`，通过 `Entities` 非 null 区分；`Save` 归入 `Upsert`。
- `Update`（含批量）对没有脏字段的实体跳过校验，与旧拦截器一致。
- 查询结果仍执行后置过滤；`Find` 单条结果和旧 `Filter` 钩子语义保持一致。

### 11.3 迁移建议

1. 新代码实现 `EntityFilter`，只重写需要的 `OnExecuting/OnExecuted`。
2. 实体静态构造函数统一写 `Meta.Filters.Add<T>()` 或 `Meta.Filters.Add(new XxxFilter())`。
3. 旧自定义拦截器可以继续运行；确认行为后再逐个改为 `IEntityFilter`，不必一次性重写。
4. 删除或跨租户维护必须使用 `EntityFilterScope.SuppressAll()`，不要依赖隐式放行。

---

## 12. 注意事项

- **原始 SQL**：租户/数据权限过滤器默认拒绝，需改写为表达式查询或显式 `Suppress`。
- **表级计数**：`Session.Count/LongCount` 是表级缓存值，不代表过滤后的数量；过滤场景请使用 `FindCount()`。
- **分片路由**：查询过滤器在 EntitySession 层执行，无法影响查询前的分片选择；租户作为分片键时建议在业务查询条件中显式带上租户。
- **延迟队列**：`SaveAsync` 入队时只执行 `Valid`，过滤器在落库批次执行。
- **递归**：过滤器内部再操作实体会再次进入管道，建议用 `Suppress<TFilter>()` 或 `Items` 标记；深度超过 32 抛异常。
- **直接缓存属性**：`Meta.Cache.Entities` 不经过过滤器；需要租户/数据权限时请用 `FindAllWithCache`。
- **版本**：该能力为 12.4 版本新增（发版时更新 ChangeLog/ReleaseNotes）。

---

## 13. 关联阅读

- [实体拦截器（IEntityInterceptor）](实体拦截器（IEntityInterceptor）.md)
- [插件化架构](插件化架构.md)
- [内置实体拦截器（Time/User/IP/Trace）](内置实体拦截器（Time_User_IP_Trace）.md)
- [数据权限](数据权限.md)
- [实体缓存EntityCache](实体缓存EntityCache.md)
- [集合批量操作（EntityExtension）](集合批量操作（EntityExtension）.md)
- [架构设计](架构设计.md)
