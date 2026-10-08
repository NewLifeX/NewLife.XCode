# 增量同步框架（Transform.Sync）

> ⚠️ **已移除**：`XCode.Transform.Sync` 已随 ETL 引擎于 2026-07-15（v12.1 清理，提交 `f033bd45b`）从仓库删除。本文档作为历史设计参考保留，当前版本不再包含该实现。

`XCode.Transform.Sync` 是 ETL 体系中的“同步执行器”，用于把源实体数据同步到目标实体/目标库目标表。

它提供：
- 批次抽取
- 事务化处理
- 插入/更新判定
- 可扩展同步规则

---

## 1. 类型结构

- `Sync`：基础同步器，继承 `ETL`
- `Sync<TSource>`：同实体跨库跨表同步
- `Sync<TSource, TTarget>`：异构实体同步

---

## 2. 核心流程

单轮处理链路：

1. `Init`：检查目标、决定是否 `InsertOnly`
2. `Fetch`：按抽取器拿一批源数据
3. `OnProcess`：在事务中逐行处理
4. `ProcessItem`：查找目标实体并拷贝字段
5. `SaveItem`：插入或更新（仅脏字段才更新）

在 `Sync` 中，`OnProcess` 会包裹事务：
- `using var tran = Target.CreateTrans();`
- 批次成功后 `Commit()`

---

## 3. InsertOnly 优化

`InsertOnly` 表示“仅插入，不查重”。

触发逻辑：
- 目标表为空时自动开启。
- 如果后续抽取批次为空，会关闭该模式。

价值：
- 避免每行 `FindByKey`，提升首轮灌库速度。

---

## 4. 主键对齐与目标实体定位

`GetItem` 默认策略：

1. 从源实体读取唯一键值
2. 在目标工厂 `FindByKey`
3. 未命中则 `Create()` 新实体并设置唯一键

这让“增量更新”与“新数据插入”在同一流程内完成。

---

## 5. 同步规则扩展点

### 5.1 `SyncItem`

泛型重载里可覆盖：
- 字段映射
- 字段清洗
- 条件跳过

默认实现：同名字段 `CopyFrom(..., true)`。

### 5.2 `SaveItem`

可覆盖：
- Upsert 策略
- 审计字段补写
- 异常重试

默认实现：
- 新对象 `Insert()`
- 旧对象仅 `HasDirty` 时 `Update()`

---

## 6. 跨库跨表同步（Sync<TSource>）

额外参数：
- `SourceConn` / `SourceTable`
- `TargetConn` / `TargetTable`

关键点：
- 读取阶段通过 `Target.Split(SourceConn, SourceTable, ...)` 切到源
- 写入阶段通过 `Target.Split(TargetConn, TargetTable, ...)` 切到目标
- 可选 `syncSchema` 先同步目标结构

---

## 7. 异常与统计

继承自 `ETL` 的能力：
- `MaxError` 连续错误熔断
- `Stat` 统计总量、成功、错误、变更
- `ShowError` 控制异常日志输出

---

## 8. 典型场景

1. 历史库向新库迁移（同构实体）
2. 业务库向分析库同步（异构实体）
3. 定时增量同步（结合 ETL 周期调度）

---

## 9. 实战建议

1. 首次全量同步可利用 `InsertOnly` 提速。
2. 同步键必须稳定唯一，避免重复写入。
3. 大批次时控制事务大小，防止长事务阻塞。
4. 对异构映射，优先在 `SyncItem` 做显式字段转换。
