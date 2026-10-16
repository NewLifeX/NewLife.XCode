# 字段时间区间扩展（FieldExtension）

`FieldExtension` 是 `XCode.Configuration.FieldItem` 的扩展方法库，专门用于**时间类型字段的区间查询**，消除手动计算日期边界的样板代码。

---

## 1. 核心方法：`Between`

### 1.1 时间字段版本

```csharp
// 查询 2025-01-01 00:00:00 ～ 2025-01-31 23:59:59 的记录
var list = Order.FindAll(_.CreateTime.Between(
    new DateTime(2025, 1, 1),
    new DateTime(2025, 1, 31)
));
```

**边界规则**（左闭右开，日期自动 +1 天）：
- `start >= end`：如果 start 和 end 都是整日期（`.Date == 自身`），结束日期自动加 1 天，实现"包含当天"
- `start` 为 `MinValue` 或 `MaxValue`：跳过左端，仅生成 `<` 条件
- `end` 为 `MinValue` 或 `MaxValue`：跳过右端，仅生成 `>=` 条件

### 1.2 雪花 Id 字段版本（`Between(start, end, snow)`）

对 `Int64` 类型的雪花 Id 字段，将时间范围转为雪花 Id 范围，精度可达毫秒级：

```csharp
var snow = Entity<Order>.Meta.Factory.Snow;
var list = Order.FindAll(_.Id.Between(startTime, endTime, snow));
```

---

## 2. 快速时段方法

### 2.1 天

| 方法 | 范围 |
|------|------|
| `Today()` | 今天全天 |
| `Yesterday()` | 昨天全天 |
| `Tomorrow()` | 明天全天 |
| `LastDays(n)` | 过去 n 天 |
| `NextDays(n)` | 未来 n 天 |

```csharp
Order.FindAll(_.CreateTime.Today())       // 今天下单
Order.FindAll(_.CreateTime.LastDays(7))  // 近7天
```

### 2.2 周

| 方法 | 范围 |
|------|------|
| `ThisWeek()` | 本周（周日~周六） |
| `LastWeek()` | 上周 |
| `NextWeek()` | 下周 |

### 2.3 月

| 方法 | 范围 |
|------|------|
| `ThisMonth()` | 本月 |
| `LastMonth()` | 上月 |
| `NextMonth()` | 下月 |

### 2.4 季度

| 方法 | 范围 |
|------|------|
| `ThisQuarter()` | 本季度 |
| `LastQuarter()` | 上季度 |
| `NextQuarter()` | 下季度 |

### 2.5 年

框架暂未提供 `ThisYear/LastYear/NextYear` 年度快捷方法，可用 `Between` 手工组合年度区间（见 §3 组合示例）。

---

## 3. 查询组合示例

```csharp
// 本月 + 状态过滤
var list = Order.FindAll(
    _.CreateTime.ThisMonth() & _.Status == 1,
    _.Id.Desc(),
    null,
    0, 20
);

// 自定义区间 + 用户过滤
var exp = _.CreateTime.Between(startDate, endDate) & _.UserId == userId;
```

---

## 4. 与 `SqlBuilder` 的关系

`FieldExtension.Between` 生成的是 `WhereExpression`，可以直接传给所有接受 `Expression` 的 `FindAll` / `FindCount` 方法，也可以用 `&` / `|` 与其他条件组合，最终由 `SelectBuilder` / `SqlBuilder` 聚合成完整 WHERE 子句。

---

## 5. 关联阅读

- `/xcode/field_item_showin`
- `/xcode/find`
- `/xcode/sql_builder`
