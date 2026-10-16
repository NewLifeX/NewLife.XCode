# 实体拦截器框架（IEntityInterceptor）

`IEntityInterceptor`（旧名 `IEntityModule`）是 XCode 的横切关注点机制，允许在实体的**创建、验证、查询、过滤**等生命周期节点插入自定义逻辑，无需修改实体类本身。

---

## 1. 接口定义

```csharp
public interface IEntityInterceptor
{
    // 初始化：判断是否支持指定实体类型，返回 false 则跳过注册
    Boolean Init(Type entityType);

    // 创建实体对象后触发（new 或工厂 Create）
    void Create(IEntity entity, Boolean forEdit);

    // 添删改前验证，返回 false 则中止操作
    Boolean Valid(IEntity entity, DataMethod method);

    // 查询前修改 WHERE 条件（管道：每个拦截器的输出作为下一个的输入）
    Expression Query(IEntityFactory factory, Expression? where, QueryAction action);

    // 过滤查询结果列表
    IList<IEntity> Filter(IList<IEntity> list);

    // 过滤单条实体（返回 false 表示不允许访问）
    Boolean Filter(IEntity? entity);
}
```

---

## 2. 注册方式

### 2.1 实体级拦截器（仅对当前实体类生效）

在实体类的静态构造函数中注册：

```csharp
static Order()
{
    Meta.Interceptors.Add<DataScopeInterceptor>();    // 数据权限（框架内置）
    Meta.Interceptors.Add<CreatorInterceptor>();      // 自定义拦截器（见 §5.1）
}
```

### 2.2 全局拦截器（对所有实体类生效）

```csharp
// 在应用启动时注册
EntityInterceptors.Global.Add<AuditLogInterceptor>();
```

### 2.3 使用 Add 泛型重载（自动 new）

```csharp
Meta.Interceptors.Add<MyInterceptor>();
// 等价于
Meta.Interceptors.Add(new MyInterceptor());
```

---

## 3. 执行顺序

1. 实体类级别的拦截器（按注册顺序）
2. 全局拦截器（`EntityInterceptors.Global`，按注册顺序）

`Valid` 方法：任意拦截器返回 `false` → 立即短路，后续拦截器不执行  
`Query` 方法：管道式传递，每个拦截器依次改写 `where`

---

## 4. 内置拦截器

| 拦截器 | 功能 |
|--------|------|
| `DataScopeInterceptor` | 自动填充 UserId/DepartmentId；权限校验 |
| `TenantInterceptor` | 查询自动附加租户过滤，写入校验/补充租户标识 |

> 表中仅列框架内置拦截器；软删除等业务逻辑拦截器需自行实现，可参考《实体拦截器》中的 `SoftDeleteInterceptor` 示例。

---

## 5. 自定义拦截器示例

### 5.1 自动填充创建者信息

```csharp
public class CreatorInterceptor : EntityInterceptor
{
    public override void Create(IEntity entity, Boolean forEdit)
    {
        if (!forEdit) return;
        if (entity is IEntity e)
        {
            e["CreateTime"] = DateTime.Now;
            e["Creator"] = Thread.CurrentPrincipal?.Identity?.Name;
        }
    }
}
```

### 5.2 查询时追加租户过滤

```csharp
public class TenantInterceptor : EntityInterceptor
{
    public override Expression Query(IEntityFactory factory, Expression? where, QueryAction action)
    {
        var tenant = TenantContext.Current;
        if (tenant != null)
        {
            var fi = factory.Fields.FirstOrDefault(f => f.Name == "TenantId");
            if (fi != null)
                where = (where & fi == tenant.Id)!;
        }
        return where!;
    }
}
```

---

## 6. `EntityInterceptor` 基类

大多数场景不需要实现所有方法，可继承基类 `EntityInterceptor`（空实现），只重写需要的方法：

```csharp
public abstract class EntityInterceptor : IEntityInterceptor
{
    public virtual Boolean Init(Type entityType) => true;
    public virtual void Create(IEntity entity, Boolean forEdit) { }
    public virtual Boolean Valid(IEntity entity, DataMethod method) => true;
    public virtual Expression Query(IEntityFactory factory, Expression? where, QueryAction action) => where!;
    public virtual IList<IEntity> Filter(IList<IEntity> list) => list;
    public virtual Boolean Filter(IEntity? entity) => true;
}
```

---

## 7. 注意事项

- `Add` 方法在后台线程异步完成初始化，避免在静态构造中死锁；调用后最多等待 100 ms
- `Init` 返回 `false` 时拦截器不会被添加到集合（相当于声明"我不支持此实体"）
- `Valid` 的 `false` 会中止数据库操作；建议同时写审计日志
- 全局拦截器对所有实体生效，需在 `Init` 中检查接口或属性再决定是否激活

---

## 8. 关联阅读

- `/xcode/data_scope`（数据权限：DataScopeInterceptor 实战）
- `/xcode/entity_factory_interface`（Meta.Interceptors 所属对象）
- `/xcode/find`（Query/Filter 的触发路径）
