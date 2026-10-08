# 抽取配置与运行上下文（ExtractSetting_DataContext）

> ⚠️ **部分已过时**：`DataContext` 已于 2026-07-15（v12.1 清理）移除，`ExtractSetting`/`IExtractSetting` 仍在使用。文中 DataContext 相关章节（第 3~5 节）为历史参考。

`ExtractSetting` 与 `DataContext` 是 Transform 管道中的“状态中枢”：

- `ExtractSetting` 负责定义“本轮抽取窗口与批大小”
- `DataContext` 负责承载“本轮执行过程数据与指标”

---

## 1. ExtractSetting 字段语义

接口 `IExtractSetting` 的关键字段：

| 字段 | 含义 |
|------|------|
| `Start` | 起始时间（含） |
| `End` | 结束时间（不含） |
| `Offset` | 追实时偏移（秒） |
| `Row` | 当前分页游标 |
| `Step` | 时间窗口步长（秒） |
| `BatchSize` | 每批最大处理量 |

`ExtractSetting` 还提供 `Copy/Clone` 扩展，便于并发场景下做“快照执行”。

---

## 2. 为什么 ETL 要克隆 Setting

`ETL.Process()` 中会把当前设置克隆到 `ctx.Setting`：

- 避免并行批次互相覆盖同一个游标
- 保证本批次日志、统计、错误回滚都有独立上下文
- 后续模块可以按上下文追踪具体批次

这也是 `MaxTask>0` 时保证状态正确性的关键。

---

## 3. DataContext 字段语义

`DataContext` 贯穿单轮执行：

| 字段 | 说明 |
|------|------|
| `Setting` | 当前批次配置快照 |
| `Data` | 本批源数据列表 |
| `Entity` | 正在处理的当前实体 |
| `Error` | 当前异常 |
| `Success` | 本批成功数 |
| `FetchCost` | 抽取耗时（ms） |
| `ProcessCost` | 处理耗时（ms） |
| `StartTime` | 批次起始时间 |

并提供派生速度：

- `FetchSpeed = Data.Count / FetchCost * 1000`
- `ProcessSpeed = Data.Count / ProcessCost * 1000`

---

## 4. 用户扩展数据槽（索引器）

`DataContext` 内置 `this[String key]` 索引器，可挂载自定义状态：

- 上游批次号
- 业务租户标识
- 额外统计对象

建议 key 使用统一前缀（如 `etl.xxx`）避免冲突。

---

## 5. 与模块管道的协作

`IETLModule` 的生命周期方法都基于同一个 `DataContext`：

- `Processing(ctx)`
- `Fetched(ctx)`
- `Processed(ctx)`
- `OnFinished(ctx)`
- `OnError(ctx)`

因此 `DataContext` 可作为模块间“轻量共享总线”。

---

## 6. 实战建议

1. `BatchSize` 从小到大压测，不要一开始拉满。
2. 时间增量任务务必设置 `Offset`，避免追到实时写入竞争区。
3. 监控 `FetchSpeed/ProcessSpeed`，定位瓶颈是在读还是写。
4. 并发模式下不要跨线程复用同一 `IExtractSetting` 实例。
