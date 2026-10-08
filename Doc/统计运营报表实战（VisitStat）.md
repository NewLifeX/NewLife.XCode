# 统计运营报表实战（VisitStat）

> ⚠️ **示例实体已移除**：`VisitStat`（访问统计）及其模型随 Membership 清理于 2026-07-15（v12.1，提交 `f033bd45b`）移除；`StatHelper`/`StatModel`/`FieldCache`/`DictionaryCache` 等统计框架能力仍在，示例实体与模型需按本文模式自建。

本文基于原内置实现 `XCode/Membership/访问统计.Biz.cs`（已移除），演示如何用 `VisitStat + StatHelper` 快速落地一个“日/月/年”运营报表链路。

## 1. 现成能力盘点

`VisitStat` 已内置：

- 维度模型：`VisitStatModel : StatModel<VisitStatModel>`
- 唯一键：`Page + Level + Time`
- 聚合入口：`VisitStat.Process(model, levels...)`
- 查找缓存：`DictionaryCache<VisitStatModel, VisitStat>`
- 字段缓存：`FieldCache<VisitStat>`（页面维度）

这意味着你无需从零设计统计框架，直接在 `Process` 前后补业务逻辑即可。

## 2. 数据模型语义

`VisitStatModel` 字段建议解释如下：

- `Page`：页面或业务对象维度
- `Title`：显示名
- `Cost`：耗时（毫秒）
- `User`：用户标识
- `IP`：访问IP
- `Error`：错误信息（非空表示异常）

在 `Process` 内会自动生成：

- 指标：`Times/Users/IPs/Error/Cost/MaxCost`
- 全局汇总：把 `Page` 改成“全部”再做同层级统计

## 3. 写入链路（在线）

典型在线入口：

```csharp
var model = new VisitStatModel
{
    Time = DateTime.Now,
    Page = "/Order/List",
    Title = "订单列表",
    Cost = 86,
    User = "admin",
    IP = "10.1.2.3",
    Error = null
};

VisitStat.Process(model, StatLevels.Day, StatLevels.Month, StatLevels.Year);
```

注意：
- `Process` 内部会并行处理多个层级。
- 写入使用 `SaveAsync(5000)`，降低请求线程阻塞。

## 4. 查询链路（报表）

### 4.1 指定层级和时间范围

```csharp
var model = new VisitStatModel
{
    Level = StatLevels.Day,
    Page = "/Order/List"
};

var list = VisitStat.Search(model, DateTime.Today.AddDays(-30), DateTime.Today, new PageParameter
{
    PageIndex = 1,
    PageSize = 50,
    Sort = "Time Desc"
});
```

### 4.2 页面维度下拉

```csharp
var pageNames = VisitStat.FindAllPageName();
```

底层使用 `FieldCache`，可直接用于筛选控件。

## 5. 指标口径建议

- PV：`Times`
- UV：`Users`
- 独立IP：`IPs`
- 错误率：`Error / Times`
- 平均耗时：`Cost`
- 最大耗时：`MaxCost`

建议统一口径后再做图表，避免前后端各算各的。

## 6. 唯一索引与并发

`VisitStat` 的唯一索引（`Page,Level,Time`）是并发安全关键。`StatHelper.GetOrAdd` 会在插入冲突时二次查询兜底，避免重复行。

## 7. 常见扩展

### 7.1 增加业务维度

可在模型和实体增加：
- `TenantId`
- `AppId`
- `Channel`

并把唯一索引扩展为 `(TenantId, Page, Level, Time)`。

### 7.2 细粒度层级

对于秒级高频接口，不建议直接用 `Minute` 做在线统计；建议写入 `Day`，分钟级改离线聚合。

## 8. 运营看板推荐面板

- 趋势：近30天 PV/UV
- Top10：页面访问排行（按 `Times`）
- 质量：错误率排行（按 `Error/Times`）
- 性能：慢页面排行（按 `Cost` 或 `MaxCost`）

## 9. 排障清单

- 统计不增长：检查 `VisitStat.Process` 是否被调用。
- 维度异常：检查 `Page` 归一化规则是否一致。
- 数据重复：核对唯一索引与时间规整逻辑。
- 报表慢：给查询条件加 `Level/Time`，避免全表扫描。