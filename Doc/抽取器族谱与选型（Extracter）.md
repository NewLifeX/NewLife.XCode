# 抽取器族谱与选型（Extracter）

> ⚠️ **部分已过时**：第 2 节的 `IdentityExtracter`、`TimeSpanExtracter` 已于 2026-07-15 随 ETL 引擎清理移除；IEntity 级抽取现由 `EntityIdExtracter`（自增/雪花 Id）承担。第 1 节 DbTable 级抽取器（`IdExtracter`/`TimeExtracter`/`PagingExtracter`）仍在使用。

XCode 在数据迁移与 ETL 中提供了两类抽取器体系：

1. `DbTable` 级抽取（常用于 `DbPackage` 备份/恢复/同步）
2. `IEntity` 级抽取（常用于 `ETL/Sync` 增量处理）

本篇给出选型建议与关键实现差异。

---

## 1. DbTable 级抽取器（高吞吐搬运）

### 1.1 IdExtracter（整数主键步进）

适用场景：
- 表有自增主键或整型唯一键。
- 希望最稳定吞吐。

机制：
- SQL 条件：`Id > Row`
- 排序：`Id asc`
- 每页读取 `BatchSize`
- 用“最后一行 Id”推进游标

优点：
- 翻页不依赖 Offset，深分页性能稳定。

注意：
- 要保证排序字段单调增长且索引有效。

### 1.2 TimeExtracter（时间分片 + 分页）

适用场景：
- 表存在时间索引字段。
- 任务以时间窗口增量追平。

机制：
- 首次定位首条时间作为 `StartTime`
- 以动态步长在 `[StartTime, EndTime)` 分片
- 每个分片内再分页读取
- 读取后用最后一行时间 `+1s` 推进

优点：
- 天然支持按时间追数。
- 稀疏区间会自动扩大步长，减少空跑。

注意：
- 时间精度与 `+1s` 推进策略要与业务写入粒度匹配。

### 1.3 PagingExtracter（通用兜底分页）

适用场景：
- 无整型主键、无可用时间索引。

机制：
- 直接 `Query(Row, BatchSize)` 偏移分页。

优点：
- 通用。

不足：
- 随偏移增长，性能可能下降。

---

## 2. IEntity 级抽取器（ETL/Sync）

### 2.1 IdentityExtracter（实体自增抽取）

机制：
- 默认自动选实体自增字段（可指定 `FieldName`）
- 条件 `Field >= set.Row`
- 抽取后将 `set.Row` 更新为最后一条 Id

适合：主键增量同步。

### 2.2 TimeSpanExtracter（实体时间片抽取）

机制：
- 默认自动选 `Factory.MasterTime`
- 条件 `Field >= Start && Field < End`
- 排序：时间升序 + 唯一键升序（防止同秒乱序）
- 分片内用 `set.Row` 分页

适合：时间窗口统计、准实时同步。

---

## 3. 选型决策树（实用版）

1. 有可靠整型主键？
   - 是：优先 `IdExtracter / IdentityExtracter`
2. 否，有稳定时间索引？
   - 是：用 `TimeExtracter / TimeSpanExtracter`
3. 都没有：
   - 使用 `PagingExtracter` 兜底，并尽快补索引

---

## 4. 与 DbPackage / Sync 的关系

- `DbPackage.GetExtracter`：自动在 `IdExtracter -> TimeExtracter -> PagingExtracter` 间选型。
- `ETL/Sync`：由 `Extracter` 属性驱动，可替换为自定义实现。

---

## 5. 实战建议

1. 批大小不要盲目拉满，结合 DB 压力做压测。
2. 时间抽取建议保证时间字段有索引。
3. 同步任务开启日志，关注“空跑步长变化”和“页处理耗时”。
4. 大表优先 Id 抽取；分页兜底只作为过渡方案。
