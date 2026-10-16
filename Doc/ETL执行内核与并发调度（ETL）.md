# ETL执行内核与并发调度（ETL）

> ⚠️ **已移除**：ETL 执行引擎已于 2026-07-15（v12.1 清理，提交 `f033bd45b`）从仓库删除，仅保留数据抽取器。本文档作为历史设计参考保留，当前版本不再包含该实现。

本文聚焦 `XCode.Transform.ETL` 的执行内核：批次生命周期、并发调度、异常熔断与统计埋点。

> 上游入门可先看：`/xcode/etl_transform`

---

## 1. 生命周期总览

`ETL` 的核心路径：

1. `Start()`：初始化 `Extracter/Stat/Module`，可按 `Period` 启动定时器。
2. `Process()`：执行单轮“抽取 + 处理”。
3. `Stop()`：停表，释放定时器并通知模块。

其中 `Process()` 是关键：

- `Module.Processing(ctx)` 决定是否进入本轮
- 首轮或重启后先执行 `Init(set)`
- 克隆设置 `ctx.Setting = set.Clone()`，避免并行任务污染
- `Fetch(ctx, extracter, set)` 拉取一批数据
- 同步或异步执行 `ProcessList(ctx)` / `ProcessListAsync(ctx)`
- 调用 `Module.Processed(ctx)` 收尾

---

## 2. 批次处理模型

### 2.1 单批处理

`ProcessList(ctx)` 内部流程：

1. `OnProcess(ctx)` 遍历 `ctx.Data`
2. 对每条调用 `ProcessItem(ctx, source)`
3. 成功结果计入 `ctx.Success`
4. `OnFinished(ctx)` 记录统计与日志

### 2.2 OnProcess 默认行为

默认 `OnProcess` 不开事务，仅做逐条处理；
`Sync` 会重载 `OnProcess` 以事务包裹整批（`CreateTrans + Commit`）。

---

## 3. 并发调度（MaxTask）

`MaxTask` 控制批次并发：

- `0`：同步串行（默认）
- `N`：最多 N 个批次并发

并发实现点：

- `_currentTask` + `Interlocked.CompareExchange` 做并发门闩
- 达到上限时短暂 `Sleep(100)` 等待
- 通过线程池投递任务，完成后 `Interlocked.Decrement`

适用建议：

- DB 写入重、事务敏感：建议 `MaxTask=0~1`
- 计算重、写入轻：可逐步提升到 `2~4`

---

## 4. 错误熔断与容错

异常由 `OnError(ctx)` 统一处理：

- 连续错误计数 `_Error` 递增
- 达到 `MaxError` 时终止（返回异常上抛）
- 未达阈值则记入统计并继续下一条/下一批
- `ShowError=true` 时输出错误日志

实现里还做了“同一异常去重”保护，避免重复计数噪声。

---

## 5. 定时驱动与追平

设置 `Period` 后，`ETL` 使用 `TimerX` 定时触发 `Loop`：

- 若本轮 `Process()` 返回 `count > 0`，会立即安排下一次执行（快速追平）
- 若无数据，则按周期等待下一轮

这使 ETL 同时具备“定时巡检 + 堆积追赶”能力。

---

## 6. 统计与可观测性

`DataContext` 提供单批时延/吞吐：

- `FetchCost`、`ProcessCost`
- `FetchSpeed`、`ProcessSpeed`

`IETLStat` 汇总累计指标：

- `Total`、`Success`、`Error`
- `Times`、`Changes`、`Message`

建议把 `WriteLog` 输出接入统一日志与报警平台。

---

## 7. 扩展点清单

1. `Init(set)`：按业务初始化本轮状态
2. `Fetch(...)`：自定义批次拉取策略
3. `ProcessItem(...)`：实现单条业务转换
4. `OnFinished(ctx)`：上报指标、推进外部游标
5. `OnError(ctx)`：细化重试/降级策略

---

## 8. 线上实践建议

1. 先串行跑通，再逐步打开并发。
2. 用小批次验证幂等后再放大 `BatchSize`。
3. 对“更新多于插入”的任务重点关注 `Changes` 指标。
4. 配置 `MaxError` 防止异常风暴长期占用资源。
