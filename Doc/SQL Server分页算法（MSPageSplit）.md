# SQL Server 分页算法详解（MSPageSplit）

`MSPageSplit` 是 XCode 专为 MS 体系数据库（SQL Server 2000～2019+）实现的四种分页算法，根据查询特征自动选择最优方案。

> **状态说明（2026-10）**：`MSPageSplit.cs` 已排除编译（重构中），当前 SQL Server 分页实现位于 `SqlServer.cs`：SQL Server 2012+ 且带排序时优先 `OFFSET/FETCH`（`PageSplitFor2012`），低版本使用 `PageSplitByTopNotIn`/`PageSplitMaxMin` 方案。本文档保留四种经典分页算法的原理解析作为历史参考，第 5 节调用关系为旧版实现描述。

> `isSql2005` 参数为 `true` 时代表 SQL Server 2005+ 及支持 `ROW_NUMBER()` 的现代数据库。

---

## 1. 算法选择矩阵

| 条件 | 选用算法 | 性能 |
|------|---------|------|
| `isSql2005 = true` | `RowNumber` | ★★★★☆ |
| `isSql2005 = false` + 整型主键 + 主键排序 | `MaxMin` | ★★★★★ |
| `isSql2005 = false` + `maximumRows > 0` | `DoubleTop` | ★★★☆☆ |
| 其他（默认兜底） | `TopNotIn` | ★★☆☆☆ |

---

## 2. 四种算法解析

### 2.1 RowNumber（ROW_NUMBER() 窗口函数）

**适用**：SQL Server 2005+、最常用

```sql
SELECT * FROM (
    SELECT ROW_NUMBER() OVER(ORDER BY Id DESC) AS RowNum, *
    FROM Orders WHERE Status=1
) AS T
WHERE T.RowNum BETWEEN 21 AND 40
```

**优点**：与 `GroupBy` 兼容，排序灵活  
**缺点**：必须整表扫描编号

---

### 2.2 MaxMin（最大最小值分页）

**适用**：整型主键、按主键升序/降序排列的场景（最常见查询模式）

```sql
-- 取第 21-40 行（主键升序）
SELECT TOP 20 * FROM Orders
WHERE Id > (SELECT MAX(Id) FROM (SELECT TOP 20 Id FROM Orders ORDER BY Id ASC) AS T)
ORDER BY Id ASC
```

**优点**：最高效，利用索引跳过前面页面  
**缺点**：要求整型主键且排序必须与主键方向一致；含 `GroupBy` 无效

---

### 2.3 DoubleTop（双重 TOP 分页）

**适用**：无整型主键或排序与主键不一致时

```sql
-- 取第 21-40 行
SELECT TOP 20 * FROM Orders
WHERE Id IN (
    SELECT TOP 40 Id FROM Orders ORDER BY CreateTime DESC
)
ORDER BY CreateTime DESC
```

**优点**：比 `TopNotIn` 更快（IN 比 NOT IN 效率高）  
**缺点**：需要调用 `queryCountCallback` 计算总数

---

### 2.4 TopNotIn（经典 Not-In 分页）

**适用**：兜底方案，兼容一切情况

```sql
-- 取第 21-40 行
SELECT TOP 20 * FROM Orders
WHERE Id NOT IN (SELECT TOP 20 Id FROM Orders ORDER BY Id)
ORDER BY Id
```

**优点**：通用，无额外限制  
**缺点**：效率最差，随页码增大急剧下降；`NOT IN` 子查询随数据量线性增长

---

## 3. 首页特殊优化

`startRowIndex <= 0` 时：

- `maximumRows < 1`：不加分页，返回原 builder
- `KeyIsOrderBy = true`（主键即排序键）：直接用 `SELECT TOP n`，避免子查询

> 注释里保留了 SQL Server 2012+ OFFSET/FETCH 实现方案，但因为 ROW_NUMBER 排序一致性问题已废弃，见防御性注释。

---

## 4. GroupBy 限制

含 `GROUP BY` 的查询：
- `RowNumber` 外层包子查询后仍可工作
- `MaxMin` / `DoubleTop` / `TopNotIn` 均无法正确分页（无主键可跳过）
- 实际场景建议分页聚合时换用 `RowNumber`

---

## 5. 在 XCode 中的调用位置

只有 `SqlServer.cs` 调用 `MSPageSplit.PageSplit`：

```
SqlServer.PageSplit(builder, start, max)
  → MSPageSplit.PageSplit(builder, start, max, isSql2005, ...)
    → RowNumber / MaxMin / DoubleTop / TopNotIn
```

其他数据库（MySQL、PostgreSQL 等）使用各自的 LIMIT/OFFSET 方言，不走这套算法。

---

## 6. 关联阅读

- `/xcode/select_insert_builder`
- `/xcode/idatabase_dbbase`
- `/xcode/dal_db_operate`
