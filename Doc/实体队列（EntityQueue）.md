# 实体队列（EntityQueue）

`EntityQueue` 是 XCode 内置的**异步批量写入队列**，用于解决高并发场景下"每次修改都立即落库"所带来的性能瓶颈。它将多次写操作合并成一批，以定时器单线程周期性持久化，同时支持背压控制防止内存溢出。

---

## 1. 工作原理

```
业务线程 ─── entity.SaveAsync() ──► Entities (ConcurrentDictionary)
                                        │
                                   TimerX 每 ~1s
                                        │
                                        ▼
                                   批量 Insert/Update/Upsert
                                        │
                                        ▼
                                   数据库（持久化）
```

- **近实时队列**（`Entities`）：立即入队，下一个定时器触发时写入
- **延迟队列**（`DelayEntities`）：指定毫秒后才到期，到期才从队列消费

周期自适应：定时器目标是**每次持久化约 1 000 条**，如果吞吐量更高，周期自动缩短；吞吐量低时周期拉长，节省资源。

---

## 2. 使用方式

### 2.1 直接调用（低频场景）

```csharp
// 立即入队（下个周期写库）
entity.SaveAsync();

// 延迟 2 秒后写库（适合频繁更新同一对象）
entity.SaveAsync(2000);
```

`SaveAsync` 内部调用 `Meta.Session.Queue.Add(entity, msDelay)`。

### 2.2 InsertOnly 模式（日志/埋点等）

如果表是**只增不改**的流水表（`INSERT` 操作），可在 `Model.xml` 中标记：

```xml
<Table Name="AccessLog" InsertOnly="True">
```

或在代码中：

```csharp
Meta.Session.Queue.InsertOnly = true;
```

`InsertOnly=true` 时队列只做批量 `INSERT`，不做 `Update/Upsert`，性能更高，内存占用更少（无需按主键去重）。

### 2.3 指定写入方式

```csharp
// 强制 Upsert（先查存在则更新，否则插入）
Meta.Session.Queue.Method = DataMethod.Upsert;
```

---

## 3. 关键参数

| 属性 | 默认值 | 说明 |
|------|--------|------|
| `Period` | 1 000 ms | 定时器初始周期，运行时自动调节 |
| `MaxEntity` | 1 000 000 | 队列上限，超过时**阻塞生产线程**（最多等 15s） |
| `InsertOnly` | false | 仅插入模式 |
| `Method` | `Insert/Update` 自动 | 写入方式 |
| `ShowSQL` | null（继承全局） | 是否输出 SQL 日志 |

---

## 4. 背压控制

当队列中等待的实体总数超过 `MaxEntity` 时：
1. `Add()` 不立即返回，而是进入**最多 15 秒的自旋等待**；
2. 等待期间若队列消费到低于 `MaxEntity`，立即恢复入队并返回 `true`；
3. 等 15 秒仍未消费完则返回 `false`（丢弃该条写入请求，并通过链路追踪记录 `db:MaxQueueOverflow` 错误）。

> 在 SkyWalking/NewLife Tracer 中若看到 `db:MaxQueueOverflow`，说明写入速度持续超过数据库处理能力，需要扩容写库或优化 SQL。

---

## 5. 应用退出时的数据安全

`EntityQueue` 实现了 `DisposeBase`，在 `Dispose` 时：
1. 停止定时器；
2. 启动一个最多 **3 秒**的 Task 把队列中剩余数据写入数据库；
3. 若 3 秒内未能写完，通过追踪记录 `db:EntityQueueNotEmptyOnDispose` 告警。

配合 .NET `IHostedService` 的 `StopAsync` 机制，建议注册关闭钩子以确保应用退出时数据不丢失。

---

## 6. 分表支持

`EntityQueue` 绑定到 `IEntitySession`（连接名 + 表名），当实体开启了 `ShardPolicy` 时，`SaveAsync()` 会根据实体数据计算出对应的 Shard 并路由到相应的队列。

---

## 7. 适用场景与注意事项

| 适用 | 不适用 |
|------|--------|
| 访问日志、操作流水 | 需要立即读回写入结果的场景 |
| 实时计数器（访问量 / 点赞数） | 强事务一致性要求的金融写操作 |
| 传感器/IoT 高频数据写入 | 写入后立刻需要读回或依赖自增主键的操作 |

- `SaveAsync` 写入的数据在队列消费前**不能通过数据库查询读到**；
- 不要在同一实体上混用 `Save()`（同步）和 `SaveAsync()`（异步），可能导致顺序错误；
- 统计类实体配合 `AdditionalFields` 累加机制（如以累加列统计访问量）本身对并发安全，可配合队列使用。
