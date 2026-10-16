# 数据同步（Sync）

> ⚠️ **已移除**：`XCode.Sync` 主从同步框架源码已于 2026-07-15（v12.1 清理，提交 `f033bd45b`）从仓库删除，功能清单 M7-2 编码随之废止。本文档作为历史设计参考保留，当前版本不再包含该实现。

`XCode.Sync` 提供一套**主从双向数据同步框架**，用于在两个数据源（主方 / 从方）之间保持数据一致。典型场景：离线系统定期向中心同步、多地部署的数据汇聚、异构数据库间数据镜像。



## 核心概念

| 角色 | 接口 | 说明 |
|---|---|---|
| **主方**（Master） | `ISyncMaster` | 数据提供者，可以是只读的（`ReadOnly=true`） |
| **从方**（Slave） | `ISyncSlave` | 数据消费者，维护本地同步标识字段 |
| **同步管理器** | `SyncManager` | 协调主从双方，执行同步流程 |



## 同步机制

同步框架采用**增量时间戳**策略：

1. 从方记录上次同步时间，向主方请求该时间点之后修改的数据主键集合（分批）。
2. 逐条处理：新增或更新本地数据，发生冲突时按策略解决。
3. 遍历本次未被主方提及的本地数据，逐批询问主方是否仍存在，确认已删除的则从本地移除。

从方建议增加三个辅助字段：

| 字段 | 说明 |
|---|---|
| 最后修改（`LastUpdate`） | 本地数据最后被修改的时间 |
| 最后同步（`LastSync`） | 最近一次成功同步的时间 |
| 同步状态（`SyncStatus`） | `1` 新增、`2` 删除 |



## 快速上手

### 1. 让实体类实现主方接口

```csharp
// 主方实体只需有 LastUpdate 字段，框架自动识别
public partial class Order : Entity<Order>
{
    // 需要有 LastUpdate DateTime 字段（框架自动使用）
}

var master = new SyncMaster { Facotry = Order.Meta.Factory };
```

### 2. 让目标实体实现从方接口

```csharp
public class OrderSlaveAdapter : ISyncSlave
{
    public ISyncSlaveEntity[] GetAllNew(Int32 start, Int32 max) { ... }
    public ISyncSlaveEntity[] GetAllDeleted(Int32 start, Int32 max) { ... }
    public ISyncSlaveEntity[] GetOthers(DateTime last, Int32 start, Int32 max) { ... }
    public String[] GetNames() => Order.Meta.FieldNames.ToArray();
    // ... 其余接口实现
}
```

### 3. 执行同步

```csharp
var manager = new SyncManager
{
    Master    = master,
    Slave     = new OrderSlaveAdapter(),
    BatchSize = 200,
    UpdateConflictByLastUpdate = true,
};

manager.Start();
```



## SyncManager 属性

| 属性 | 说明 | 默认 |
|---|---|---|
| `Master` | 主方（数据提供者） | — |
| `Slave` | 从方（数据消费者） | — |
| `BatchSize` | 每批处理记录数 | `100` |
| `Names` | 参与同步的字段集合（默认取 Master 与 Slave 字段的交集） | 自动计算 |
| `UpdateConflictByLastUpdate` | 双方同时修改时以 `LastUpdate` 较新的为准；`false` 时强制以主方为准 | `false` |



## 同步流程（Start()）

```
Start()
├── ProcessNew()     先处理从方新增（避免与主方主键冲突）
├── ProcessDelete()  再处理从方删除
├── ProcessItems()   处理主方更新的数据（增量拉取，分批）
└── ProcessOthers()  检查本次未涉及的本地数据是否在主方仍存在，处理主方删除
```

> 若 `Master.ReadOnly == true`，跳过 `ProcessNew()` 和 `ProcessDelete()`，仅做单向同步（主 → 从）。



## 冲突处理策略

| 场景 | 处理方式 |
|---|---|
| 从方改、主方不变 | 推送到主方 |
| 从方不变、主方改 | 更新本地 |
| 双方同时修改 | `UpdateConflictByLastUpdate=true` 时比较 `LastUpdate`，取较新值；否则以主方覆盖 |
| 从方删、主方改 | 报告冲突（由子类处理） |
| 从方改、主方删 | 报告冲突 |
| 双方都删 | 删除本地 |



## 只读主方（单向同步）

当主方系统不允许修改（如第三方 API、只读数据库副本）时，设置主方为只读：

```csharp
// SyncMaster.ReadOnly 由 LastUpdateName 是否为空决定
// 无 LastUpdate 字段的主方自动为只读
var master = new SyncMaster { Facotry = ThirdPartyData.Meta.Factory };
// master.ReadOnly == true（如果 ThirdPartyData 没有 LastUpdate 字段）
```



## 注意事项

- 主方需要有 `LastUpdate`（`DateTime` 类型）字段，框架才能进行增量同步；否则每次全量同步。
- 从方建议用接口（`ISyncSlave`）封装适配，而非直接继承，以保持业务逻辑与同步逻辑解耦。
- `ProcessOthers` 阶段会批量向主方询问数据是否存在，高频调用时注意主方接口的性能。
- 本框架适用于数据量适中（百万以内）的定期同步场景；超大规模实时同步建议配合 ETL 框架（见 [ETL数据抽取转换](ETL数据转换.md)）。
