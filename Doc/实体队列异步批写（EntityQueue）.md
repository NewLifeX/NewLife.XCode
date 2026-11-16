# 实体队列异步批写（EntityQueue）

`EntityQueue` 是 XCode 的高吞吐写入缓冲层，将实体的增删改操作**批量化、异步化**，显著降低数据库 I/O 频率，同时通过动态调节刷新周期和背压机制保障系统稳定性。

---

## 1. 工作原理

```
业务线程 → Add(entity) → ConcurrentDictionary ─→ TimerX 定期触发
                        DelayEntities(延迟队列) ─┘        │
                                                          ↓
                                              OnProcess(batch) → DB批量写入
```

- **近实时队列**（`Entities`）：立即加入，下次 Timer 触发时全量刷出
- **延迟队列**（`DelayEntities`）：`Add(entity, msDelay)` 指定毫秒后才参与刷出
- Timer 周期默认 **1 000 ms**，根据每次处理量自动调节至 500–5 000 ms

---

## 2. 属性列表

| 属性 | 默认值 | 说明 |
|------|--------|------|
| `Method` | 0（None） | 强制写入方式：Insert/Update/Delete/Upsert/Replace |
| `InsertOnly` | false | 为 true 时仅走批量 Insert，跳过 Upsert |
| `Period` | 1 000 ms | Timer 初始/当前周期，动态调节 |
| `MaxEntity` | 1 000 000 | 最大队列深度，超出时写入线程阻塞最多 15 s |
| `Speed` | - | 只读，上一次刷出的速度（tps） |
| `ShowSQL` | null | 覆盖 DAL 会话的 ShowSQL 开关 |

---

## 3. 使用方法

### 3.1 通过实体 `SaveAsync` 隐式使用

```csharp
// 最常见用法：实体的 SaveAsync 内部使用 EntityQueue
await entity.SaveAsync(3000);  // 3 秒延迟保存
```

### 3.2 直接实例化（高级场景）

```csharp
// 与 EntitySession 绑定
var queue = new EntityQueue(Order.Meta.Session)
{
    Method = DataMethod.Insert,
    InsertOnly = true,
    MaxEntity = 500_000,
};

// 生产者线程
foreach (var order in incomingOrders)
{
    queue.Add(order, 0); // 立即进入近实时队列
}

// 程序退出时 Dispose 会等待最多 3 s 完成剩余刷出
queue.Dispose();
```

### 3.3 自定义处理逻辑（继承扩展）

```csharp
public class MyQueue : EntityQueue
{
    public MyQueue(IEntitySession session) : base(session) { }

    protected override void OnProcess(IList<IEntity> batch)
    {
        // 自定义写入逻辑，如写完后发消息通知
        base.OnProcess(batch);
        NotifyDownstream(batch);
    }
}
```

---

## 4. 批量处理规则

1. 每次刷出的 batch 上限为 **10 000** 条，超过时自动分批
2. `Method == 0`（默认）：`InsertOnly=true` 时走 `BatchInsert`，否则走 `SaveWithoutValid`（先 Update 后 Insert）
3. `Method > 0`：按指定方法执行，优先使用 DAL 批量接口（减少往返次数）

---

## 5. 背压与安全机制

| 场景 | 行为 |
|------|------|
| 队列深度 >= `MaxEntity` | 写入线程 `Thread.Sleep(100)` 间歇等待，最多 `OverflowWaitTime`（默认 15 s） |
| 仍无法入队 | 抛 `XCodeException`，该条未入队 |
| 恰好推到队列上限 | 该条已入队，始终返回 `true`，避免调用方重试写重 |
| Dispose 时队列非空 | 后台 Task 最多等待 3 s 完成剩余写入；超时写 Tracer 错误 Span |
| 批处理抛异常 | 调用 `OnError(batch, ex)`（可重写），不影响其余批次；失败批次不会自动重试，需要重试请在 `OnError` 中自行处理 |

---

## 6. 动态周期调节算法

```
保存量 > 1000 → 缩短 Period（需要更快刷出）
保存量 < 1000 → 延长 Period（减少空跑）
边界：[500 ms, 5000 ms]
```

---

## 7. 关联阅读

- `/xcode/idb_session`（实际执行批量写入的会话接口）
- `/xcode/entity_session`（`EntitySession` 持有队列实例）
- `/xcode/find`（查询侧）
