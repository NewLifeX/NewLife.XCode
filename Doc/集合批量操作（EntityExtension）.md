# 集合批量操作（EntityExtension）

`EntityExtension` 是 XCode 对 `IEnumerable<IEntity>` 的扩展方法集合，提供集合级别的 **Insert/Update/Save/Delete** 以及底层的 **BatchInsert/BatchUpdate/BatchUpsert** 原语，是高吞吐写入的核心工具。

---

## 1. 集合操作方法总览

| 扩展方法 | 说明 | 内部调用 |
|----------|------|---------|
| `list.Insert()` | 集合插入 | 支持批量则走 `BatchInsert`，否则逐条 |
| `list.Update()` | 集合更新 | Oracle 批量，其他逐条 |
| `list.Save()` | 智能保存（新增/更新/Upsert 自动判断） | `BatchSave` + 逐条 |
| `list.SaveWithoutValid()` | 同 `Save` 但跳过 `Valid` 校验 | 同上，去掉验证 |
| `list.Delete()` | 集合删除 | 单主键批量 `IN`，复合主键逐条 |

---

## 2. 简单集合操作

### 2.1 Insert

```csharp
var list = new List<Order>();
for (var i = 0; i < 1000; i++)
    list.Add(new Order { No = $"ORD{i}", Amount = i * 100 });

list.Insert();    // 数据库支持批量时自动走 BatchInsert
```

- 写入前每条都会调用 `Valid(DataMethod.Insert)` 做业务校验；过滤器在整批开始前执行一次，上下文通过 `Entities` 提供整批数据
- 若存在 `ShardPolicy`，会自动按分片分组后分别批量插入

### 2.2 Update

```csharp
var list = Order.FindAll(Order._.Status == 0);
foreach (var item in list) item.Status = 1;
list.Update();
```

- Oracle 支持多行批量 UPDATE，其他数据库逐条执行
- 逐条执行时，可传 `useTransition: true` 包裹事务以提升 SQLite/单机场景性能

### 2.3 Save

```csharp
// 混合场景：有来自数据库的已有记录，也有新建记录
var list = BuildOrders();
list.Save();
// IsFromDatabase=true → Update；IsNullKey → Insert；其他 → Upsert
```

`Save` 内部调用 `Split()` 将列表拆分为：
- `IsFromDatabase=true` → 走 `Update` 路径
- 其余 → 走 `BatchSave` (Insert / Upsert 智能选择)

### 2.4 Delete

```csharp
var old = Order.FindAll(Order._.CreateTime < DateTime.Today.AddDays(-90));
old.Delete();
```

单主键时自动生成 `DELETE FROM Order WHERE Id IN (1,2,3,...)` 每批最多 1 000 条。

---

## 3. 底层批量原语

底层批量方法直接向数据库发送多行 VALUES 或 MERGE 语句，性能最高，但**不触发旧版实体拦截器**（`TimeInterceptor`、`UserInterceptor` 等不会自动填充字段），需要调用方自行确保数据完整性。新版 [实体操作过滤器（IEntityFilter）](实体过滤器（IEntityFilter）.md) 会在每批调用时触发一次，可通过 `Entities` 统一填充或校验。

### 3.1 BatchInsert

```csharp
// 批量插入（自动生成 INSERT INTO ... VALUES (...),(...),...）
list.BatchInsert();

// 只插入指定列
list.BatchInsert(new[] { Order._.No.Field, Order._.Amount.Field });

// 使用 BatchOption 精细控制
var opt = new BatchOption { BatchSize = 500 };
list.BatchInsert(opt);
```

> Oracle / MySQL 对批量写入有原子性保证：任意一条失败则整批回滚。

### 3.2 BatchInsertIgnore

```csharp
// 主键冲突时忽略，不报错也不更新（INSERT IGNORE / INSERT OR IGNORE）
list.BatchInsertIgnore();
```

适合幂等写入场景（如日志去重，消费侧重复投递时安全忽略）。

### 3.3 BatchUpdate

```csharp
// 批量更新所有字段
list.BatchUpdate();

// 只更新指定列（updateColumns），可选累加列（addColumns）
var opt = new BatchOption
{
    UpdateColumns = [nameof(Order.Status), nameof(Order.Remark)],
    AddColumns    = [nameof(Order.Quantity)],   // 生成 Quantity=Quantity+@v
};
list.BatchUpdate(opt);
```

### 3.4 BatchUpsert

```csharp
// Upsert：有则更新，无则插入（借助 ON DUPLICATE KEY UPDATE / MERGE INTO 等）
list.BatchUpsert();

var opt = new BatchOption
{
    UpdateColumns = [nameof(Order.Amount), nameof(Order.Status)],
};
list.BatchUpsert(opt);
```

---

## 4. BatchOption 参数说明

| 属性 | 类型 | 说明 |
|------|------|------|
| `BatchSize` | `Int32` | 单批行数，默认由 DAL 决定（通常 1 000）|
| `Columns` | `IDataColumn[]?` | 指定参与操作的列，null 表示所有列 |
| `UpdateColumns` | `ICollection<String>?` | Upsert/Update 更新列名列表 |
| `AddColumns` | `ICollection<String>?` | 累加列（`Col = Col + value`）|

---

## 5. ToDictionary / CreateParameter

### 5.1 转字典

```csharp
// 主键做 Key，整个实体做 Value（IDictionary<Int32, Order>）
var dic = orders.ToDictionary();

// 指定 Value 字段（IDictionary<Int32, String>）
var nameDic = orders.ToDictionary("Name");
```

适合在缓存层快速按主键查实体。

### 5.2 创建参数数组

```csharp
// 生成与实体字段对应的 IDataParameter[]（用于存储过程 / 参数化裸 SQL）
var session = Order.Meta.Session;
var dps = entity.CreateParameter(session);
```

---

## 6. 数据库支持情况

| 操作 | MySQL | SQLite | SqlServer | Oracle | PostgreSQL |
|------|-------|--------|-----------|--------|------------|
| BatchInsert | ✅ | ✅ | ✅ | ✅ | ✅ |
| BatchInsertIgnore | ✅ | ✅ | ❌ | ❌ | ✅ |
| BatchUpdate | ❌ | ❌ | ✅ | ✅ | ✅ |
| BatchUpsert | ✅ | ✅ | ✅ | ✅ | ✅ |

> 不支持时，XCode 会自动降级为逐条操作，不影响功能，仅影响性能。

---

## 7. 常见问题

- **批量插入后拦截器字段未填充**：旧版拦截器不触发，请在调用 `BatchInsert` 前手动调用 `list.Valid(true)` 或逐条调用 `Valid(DataMethod.Insert)`；使用新版 `IEntityFilter` 时可在 `OnExecuting` 中遍历 `context.Entities` 自动填充。
- **分表场景需要指定 session**：跨分片批量操作时，传入目标 `IEntitySession` 避免路由到默认表。
- **Save 大量混合数据很慢**：`IsFromDatabase` 判断依赖内存标记，若实体来源混乱可改用 `BatchUpsert` 并提供唯一索引让数据库判断冲突。
- **useTransition 何时为 true**：SQLite 单机场景下逐条操作前后加事务能显著提升速度；其他数据库批量操作自带原子性，不需要额外事务。
