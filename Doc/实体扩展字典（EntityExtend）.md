# 实体扩展字典（EntityExtend）

每个实体对象都持有一个 `Extends` 属性，类型为 `EntityExtend`。它是一个带**过期自动刷新**机制的轻量字典，专门用来挂载"从其他表关联查来"的计算属性，避免每次访问都真正查库。

---

## 1. 为什么需要 EntityExtend

XCode 不鼓励多表 JOIN，而是提倡*扩展属性*模式：

```
User.RoleName
  → 首次访问：查一次 Role 表 → 存入 Extends
  → 后续访问：直接从 Extends 返回缓存值（默认 10 秒）
  → 10 秒到期：下次访问时异步刷新
```

与在 `getter` 里每次调用 `Role.FindByKey(RoleId)` 相比：
- 无重复查库
- 不需要自己维护字段-级 null 私有成员
- 过期后自动刷新，数据不永久僵化

---

## 2. 用法

### 2.1 在 *.Biz.cs 中定义扩展属性

```csharp
public partial class User
{
    // 从 Role 表取角色名，10 秒过期后自动重新查
    public String? RoleName => Extends.Get(nameof(RoleName),
        k => Role.FindByKey(RoleId)?.Name);

    // 复杂对象也可以缓存
    public Role? RoleObj => Extends.Get(nameof(RoleObj),
        k => Role.FindByKey(RoleId));
}
```

`Extends.Get<T>(key, func)` 语义：
- 键不存在或已过期 → 调用 `func` 取值，写入缓存并返回
- 键存在且未过期 → 直接返回缓存值
- `func=null` 且键不存在 → 返回 `default(T)`

### 2.2 仅读（不刷新）

```csharp
// 不传 func，只读当前缓存值，不存在时返回 null
var cached = Extends.Get<String>(nameof(RoleName));
```

### 2.3 手动写入

```csharp
// 可被序列化时跳过，由代码直接写
entity.Extends.Set(nameof(Score), computedScore);
```

> `Set` 方法将值写入缓存，过期时间与 `Expire` 一致。

### 2.4 清空缓存

```csharp
entity.Extends.Clear();    // 清空该实体的全部扩展属性缓存
```

---

## 3. 过期时间配置

全局默认过期时间来自 `XCodeSetting.Current.ExtendExpire`（单位：秒，默认 `10`），可在 `XCode.json` / `appsettings.json` 中覆盖：

```json
{
  "XCode": {
    "ExtendExpire": 30
  }
}
```

也可以在实体的静态构造函数中针对该实体调整：

```csharp
static User()
{
    // 该实体的扩展属性缓存 60 秒
    // （在首次 new User() 后，Extends 对象使用当时的 ExtendExpire 值）
    XCodeSetting.Current.ExtendExpire = 60;
}
```

---

## 4. 序列化行为

`EntityExtend` 上的属性已标记以下特性，所有主流序列化器均会**跳过** `Extends` 字段：

```csharp
[XmlIgnore, ScriptIgnore, IgnoreDataMember]
public Int32 Expire { get; set; }
```

内容字典本身为私有字段，也不参与序列化。因此：
- JSON/XML 序列化输出不含扩展属性（符合预期）
- 如需输出 `RoleName`，在实体上定义可序列化的属性，其内部调用 `Extends.Get()`

---

## 5. 线程安全

`EntityExtend` 内部使用 `lock(dic)` 双重检查锁定（读不加锁，写加锁）保证线程安全，适合**多线程共享同一实体对象**的场景（如缓存池）。

---

## 6. 与单对象缓存对比

| 机制 | 颗粒度 | 过期 | 适用场景 |
|------|--------|------|---------|
| `EntityExtend.Get()` | 实体实例级 | 短期（秒级） | 关联表属性、计算字段 |
| `SingleCache` | 全局实体集合 | 可配置 | 高频根据主键/唯一键查询 |
| `EntityCache` | 整张表 | 可配置 | 记录数较少、读多写少 |

---

## 7. 注意事项

- 如果同一实体可能被多个地方修改，`Clear()` 应在实体重写 `Update()` / `Delete()` 时调用，防止脏读。
- `func` 内部不要再访问同一实体的 `Extends`，否则可能构成循环依赖。
- 值为 `null` / `default` 时，`EntityExtend` **不会缓存**（避免因未初始化完毕导致永久返回空）。如需缓存 null，可用包装类型。
