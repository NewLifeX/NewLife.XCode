# ETL统计指标与观测模型（IETLStat）

> ⚠️ **已移除**：ETL 执行引擎已于 2026-07-15（v12.1 清理，提交 `f033bd45b`）从仓库删除，仅保留数据抽取器。本文档作为历史设计参考保留，当前版本不再包含该实现。

`IETLStat` 定义了 ETL 任务的核心统计面：吞吐、成功、变更、错误与错误摘要。

默认实现为 `ETLStat`，字段简单但足够支撑多数运维看板。

---

## 1. 指标定义

| 字段 | 含义 | 典型用途 |
|------|------|----------|
| `Total` | 累计抽取总数 | 输入流量观测 |
| `Success` | 累计成功处理数 | 成功率计算 |
| `Changes` | 更新变更数 | 判断“更新型任务”强度 |
| `Times` | 批次次数 | 任务活跃度 |
| `Error` | 累计错误数 | 异常趋势告警 |
| `Message` | 最近错误摘要 | 快速定位问题 |

---

## 2. 在 ETL/Sync 中的更新点

- 抽取成功一批后：`Total += list.Count`、`Times++`
- 处理成功后：`Success += ctx.Success`
- 更新发生时（如 `Sync.SaveItem`）：`Changes++`
- 错误路径：`Error++`，并更新 `Message`

这样可以区分：
- 抽取到了多少（Total）
- 真正处理成功多少（Success）
- 其中有多少是更新而非新增（Changes）

---

## 3. 常用衍生指标

可在监控系统衍生：

1. 成功率：`Success / Total`
2. 错误率：`Error / Times`
3. 变更占比：`Changes / Success`

> 当 `Changes` 长期接近 0，说明任务可能在做“重复写入”或“全新增场景”。

---

## 4. 使用建议

1. 定时把 `IETLStat` 快照输出到日志或指标库。
2. `Message` 仅保留摘要，完整堆栈由日志系统承载。
3. 多任务并行时建议“每任务独立 Stat 实例”，避免相互污染。
4. 对关键任务建立阈值告警：
   - `Error` 持续上升
   - `Success` 异常下降
   - `Times` 正常但 `Total=0`（疑似空跑）

---

## 5. 扩展方向

如果你需要更细粒度观测，可在自定义实现中增加：

- `FetchCostAvg` / `ProcessCostAvg`
- `LastSuccessTime`
- `ConsecutiveError`
- 分租户/分分片统计明细

并保持对 `IETLStat` 的兼容，避免影响现有 ETL 主流程。

---

## 6. 与其他组件的关系

- `IETLStat`：统计数据容器
- `DataContext`：单批上下文与即时耗时
- `IETLModule`：把统计数据送往外部系统（日志/告警/监控）

三者组合后，可形成“采集-处理-观测”闭环。
