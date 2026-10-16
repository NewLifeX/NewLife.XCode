# 实体拦截器（IEntityInterceptor）

实体拦截器是 XCode 中对实体生命周期进行横切处理的核心机制，替代已废弃的 `IEntityModule`。通过拦截器可以在不修改实体业务代码的情况下，统一处理时间戳、用户信息、链路追踪、数据权限过滤等横切关注点。

## 1. 接口定义

```csharp
public interface IEntityInterceptor
{
    // 初始化：判断是否支持指定实体类型，返回 false 则跳过该实体
    Boolean Init(Type entityType);

    // 创建实体对象时（new 或 forEdit 模式）
    void Create(IEntity entity, Boolean forEdit);

    // 调用 Insert/Update/Delete 前的验证
    Boolean Valid(IEntity entity, DataMethod method);

    // 查询时修改 WHERE 条件（可追加过滤）
    Expression Query(IEntityFactory factory, Expression? where, QueryAction action);

    // 过滤实体列表（FindAll 返回后）
    IList<IEntity> Filter(IList<IEntity> list);

    // 过滤单个实体（Find 返回后）
    Boolean Filter(IEntity? entity);
}
```

> `IEntityModule` 是旧版别名，已标注 `[Obsolete]`，新代码一律使用 `IEntityInterceptor`。

## 2. 基类 EntityInterceptor

XCode 提供了 `EntityInterceptor` 抽象基类，所有方法都有默认空实现，只需重写关心的方法：

```csharp
public abstract class EntityInterceptor : IEntityInterceptor
{
    // 子类重写：决定是否匹配
    protected virtual Boolean OnInit(Type entityType);

    // 子类重写：创建时
    protected virtual void OnCreate(IEntity entity, Boolean forEdit);

    // 子类重写：验证时
    protected virtual Boolean OnValid(IEntity entity, DataMethod method);

    // ...

    // 辅助方法：设置字段值（自动处理脏标记）
    protected static void SetItem(FieldItem[] fs, IEntity entity, String name, Object value);

    // 辅助方法：设置字段值但不覆盖已脏字段
    protected static void SetNoDirtyItem(FieldItem[] fs, IEntity entity, String name, Object value);
}
```

## 3. 内置拦截器

### 3.1 TimeInterceptor / TimeModule

自动填充 `CreateTime`（Insert）和 `UpdateTime`（Insert+Update）。

```csharp
static MyEntity()
{
    Meta.Interceptors.Add<TimeInterceptor>();
}
```

> 旧写法 `Meta.Modules.Add<TimeModule>()` 仍可编译：`Modules` 已标注 `[Obsolete]`，`TimeModule` 等为兼容子类；新代码一律使用 `Meta.Interceptors`。

**触发条件**：实体有 `DateTime` 类型字段且名称匹配 `CreateTime` / `UpdateTime`。

### 3.2 UserInterceptor / UserModule

自动从 `ManageProvider.Provider.Current` 填充创建人/更新人。

```csharp
Meta.Interceptors.Add<UserInterceptor>();
```

**填充字段**：

| 字段名 | 类型 | 时机 |
|--------|------|------|
| `CreateUserID` | `Int32`/`Int64` | Insert |
| `CreateUser` | `String` | Insert |
| `UpdateUserID` | `Int32`/`Int64` | Insert + Update |
| `UpdateUser` | `String` | Insert + Update |

**无当前用户时**：Insert 会使用 `Environment.UserName`（或机器名）兜底；Update 可通过 `AllowEmpty=true` 清零。

### 3.3 IPInterceptor / IPModule

自动填充客户端 IP。

```csharp
Meta.Interceptors.Add<IPInterceptor>();
```

**填充字段**：

| 字段名 | 时机 |
|--------|------|
| `CreateIP` | Insert |
| `UpdateIP` | Insert + Update |

### 3.4 TraceInterceptor / TraceModule

填充链路追踪 ID。

```csharp
Meta.Interceptors.Add<TraceInterceptor>();
```

填充 `TraceId` 字段（`String`），值来自当前 `DefaultTracer` 的活跃 Span。

## 4. 注册方式

### 4.1 实体类级别（推荐）

在实体的静态构造函数中注册，只对该实体生效：

```csharp
public partial class Order : Entity<Order>
{
    static Order()
    {
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add<UserInterceptor>();
    }
}
```

### 4.2 全局级别

对所有实体生效：

```csharp
EntityInterceptors.Global.Add(new TimeInterceptor());
EntityInterceptors.Global.Add(new UserInterceptor());
```

> 全局拦截器在 `Init` 时仍会被调用 `OnInit` 判断是否支持，不匹配字段的实体等于跳过。

## 5. 编写自定义拦截器

以"软删除"为例：

```csharp
/// <summary>软删除拦截器。删除时改状态而非物理删除，查询时自动过滤已删除行</summary>
public class SoftDeleteInterceptor : EntityInterceptor
{
    protected override Boolean OnInit(Type entityType)
    {
        var fs = GetFields(entityType);
        return fs.Any(f => f.Name == "Deleted" && f.Type == typeof(Boolean));
    }

    protected override Boolean OnValid(IEntity entity, DataMethod method)
    {
        if (method == DataMethod.Delete)
        {
            entity["Deleted"] = true;
            entity["DeleteTime"] = DateTime.Now;
            entity.Update();
            return false; // 阻止真正的物理删除
        }
        return true;
    }

    public override Expression Query(IEntityFactory factory, Expression? where, QueryAction action)
    {
        var deletedField = factory.Table.FindByName("Deleted");
        if (deletedField == null) return where!;

        var notDeleted = new FieldExpression(deletedField) == false;
        return where == null ? notDeleted : where & notDeleted;
    }
}
```

注册：

```csharp
static Order()
{
    Meta.Interceptors.Add<SoftDeleteInterceptor>();
}
```

## 6. 拦截器的执行顺序

```
实体类拦截器（按 Add 顺序）
    → 全局拦截器（EntityInterceptors.Global，按 Add 顺序）
```

各操作触发点：

| 操作 | 触发的拦截器方法 |
|------|----------------|
| `new T()` / `Create(forEdit: true)` | `Create` |
| `Insert()` | `Valid(Insert)` |
| `Update()` | `Valid(Update)` |
| `Delete()` | `Valid(Delete)` |
| `FindAll(...)` | `Query` → `Filter(list)` |
| `Find(...)` | `Query` → `Filter(entity)` |

## 7. 与旧版 Module 对照

| 旧版 | 新版 |
|------|------|
| `IEntityModule` | `IEntityInterceptor` |
| `EntityModule` | `EntityInterceptor` |
| `TimeModule` | `TimeInterceptor`（`TimeModule` 仍可用，内部继承新类） |
| `UserModule` | `UserInterceptor` |
| `IPModule` | `IPInterceptor` |
| `TraceModule` | `TraceInterceptor` |

> 旧版 `Add<TimeModule>()` 写法仍能编译运行，但会触发编译警告 `CS0612`，建议统一迁移到新名称。

## 8. 常见问题

- **拦截器不生效**：检查是否在静态构造函数中注册；检查 `OnInit` 是否返回 `true`（字段名要完全匹配）。
- **时间未自动填充**：确认字段类型是 `DateTime`，字段名精确匹配 `CreateTime`/`UpdateTime`（大小写敏感）。
- **多线程写入时 CreatedBy 错乱**：`UserInterceptor` 从 `AsyncLocal` 上下文取用户，确保在请求上下文内正确初始化 `ManageProvider.Provider.Current`。
- **Query 拦截器影响 Count 不对**：`Query` 会在 `FindCount` 时同样触发，确认追加的条件在统计场景仍正确。
