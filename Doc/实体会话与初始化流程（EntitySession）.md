# 实体会话与初始化流程（EntitySession）

`EntitySession<TEntity>` 是 XCode 的实体运行时中枢：
- 管理连接与表映射（`ConnName + TableName` 唯一）
- 管理建表/初始化（`CheckModel + InitData`）
- 管理缓存（`EntityCache` / `SingleCache` / `Count`）
- 管理事务与数据变更通知
- 管理异步写入队列（`EntityQueue`）

---

## 1. 会话唯一键与生命周期

每个实体会话用键 `ConnName###TableName` 缓存在静态字典：

```csharp
private static readonly ConcurrentDictionary<String, EntitySession<TEntity>> _es
```

通过 `Create(connName, tableName)` 获取，同一键总是同一个会话实例。

### 1.1 为什么按“连接名+表名”区分

- 同一实体可因分库分表路由到不同库表
- 不同库表的缓存、计数、元数据必须隔离
- 会话级 `Items` 可挂载租户/追踪上下文数据

---

## 2. 首次使用初始化：WaitForInitData

核心入口：`WaitForInitData(ms=3000)`。

流程（源码注释同款）：
1. 若已初始化或视图表，直接返回。
2. `Monitor.TryEnter(_wait_lock)` 保证只初始化一次。
3. 记录 `initThread` 防止同线程重入死锁。
4. 执行 `CheckModel()`（建库建表/校验结构）。
5. 执行 `Factory.Default.InitData()`（写入默认数据）。
6. 释放锁，恢复 `initThread=0`。

### 2.1 典型调用链

- `Query/Execute` 前自动 `InitData()`
- `Count/LongCount` 首次读取时会触发建表与初始化
- `BeginTrans` 前也会触发 `InitData`（避免事务内才发现表未建）

---

## 3. 架构检查：CheckModel + CheckTable

`CheckModel` 根据迁移配置决定是否检查：

- `Migration.Off`：跳过
- 编译器生成类型（`IsGenerated`）：跳过
- `ModelCheckMode.CheckTableWhenFirstUse`：首次使用时检查
- `Migration > ReadOnly`：同步建表
- 否则：后台异步建表（LongRunning Task）

`CheckTable` 会：
- 克隆 `DataTable` 防止污染元数据
- 处理分表后表名差异
- 跳过分片策略实体（`ShardPolicy`）
- 最终调用 `dal.SetTables(table)` 对比并修复结构

---

## 4. Count / LongCount 计数策略

`LongCount` 采用“小表精确、中表后台校准、大表估算”的策略：

1. `_Count >= 0` 先返回缓存；到期后异步刷新。
2. 首次读取时，尝试读取 `DataCache.Current.Counts` 里的历史值。
3. 优先快速计数：
   - SQLite 使用自增主键 `MAX(Id) - MIN(Id) + 1` 估算；
   - 其它数据库使用 `QueryCountFast`；
   - `QueryCountFast` 拿不到时，用自增主键 `MIN/MAX` 区间兜底（仅限有索引的单列自增主键，雪花 ID 不适用）。
4. `FullCountFloor` 默认 100 万：
   - FastCount ≤ 10 万：同步精确计数；
   - 10 万 ~ 100 万：返回估算值，后台异步精确；
   - ≥ 100 万：只使用估算值，不执行 `COUNT(*)`。
5. 精确计数使用内部 5 秒超时；失败保留估算值并退避 30 分钟。
6. 只有确认表不存在时才触发建表；超时不再触发 `CheckTable`，也不会把计数写成 0。
7. 视图保持精确计数，避免破坏模板视图语义。

> 精确总数仍可用 `Entity.FindCount()` 获取。将 `FullCountFloor` 设为 0 可让所有表只走 FastCount。

### 4.1 刷新周期

- `_NextCount` 按规模分档：100 万以上 600 秒，10 万以上 300 秒，其它 60 秒。
- 后台精确成功后设置 `_NextFullCount`：100 万以上约 3600 秒，10 万以上约 600 秒，其它约 60 秒。
- 精确计数失败后 `_NextFullCount` 退避 30 分钟，期间同步与后台精确都跳过。

这让大表避免频繁 `COUNT(*)`，小表又能保持较高准确性。

---

## 5. 缓存管理与数据变更

`EntitySession` 维护三层缓存状态：

| 类型 | 字段 | 作用 |
|------|------|------|
| 实体缓存 | `_cache` | 整体对象缓存（列表） |
| 单对象缓存 | `_singleCache` | 主键/唯一键查单对象 |
| 计数缓存 | `_Count` | 总行数快速返回 |

### 5.1 清理策略

`ClearCache(reason, force)`：
- 清理实体缓存
- 清理单对象缓存
- 将 `_NextCount` 置最小值触发下次刷新

`DataChange(reason)` 触发时默认 `force=true`，确保写入后读到最新值。

### 5.2 事务提交/回滚联动

- `Commit()` 最外层归零后触发 `DataChange("Commit")`
- `Rollback()` 最外层归零后触发 `DataChange("Rollback")`

即只有事务真正结束才清缓存，避免中间状态抖动。

---

## 6. 实体增删改与缓存同步

`Insert/Update/Delete` 的会话级实现会在持久化后同步缓存：

- `Insert`：加入实体缓存，`_Count++`
- `Update`：更新实体缓存并移除单对象缓存项
- `Delete`：从两类缓存移除，`_Count--`

并清空实体 `_Extends`，防止扩展属性缓存与新状态不一致。

---

## 7. 视图与模板查询

- 视图（`DataTable.IsView`）禁止 `Insert/Update/Delete`
- 若是视图且查询 `FormatedTableName`，`FixBuilder` 会替换为模板 SQL：

```csharp
builder.Table = $"({Factory.Template.GetSql(Dal.DbType)}) SourceTable";
```

因此视图实体可像普通实体一样查询，但底层是模板子查询。

---

## 8. 事件：OnDataChange

`OnDataChange` 是弱引用事件（`WeakAction<Type>`）：
- 订阅者不会被该事件长期强引用导致内存泄漏
- 适合缓存层、监控层做“表数据变更后刷新”

---

## 9. 实战建议

1. **不要主动 new EntitySession**，统一走 `Entity<T>.Meta.Session`。
2. **分库分表批量操作**时，显式传入目标 `IEntitySession`。
3. **大表统计页**优先用 `LongCount`，不要每次都 `SelectCount`。
4. **初始化阶段**避免在 `InitData` 里使用可能触发二次初始化的缓存 API。
5. **事务前先触发静态引用**（如先 `Entity<T>.Meta.Session.Count`）可减少首次事务开销。
6. **需要精确总数时使用 `FindCount()`**，不要在大表上反复调用 `Meta.Count` 并期待精确值。
