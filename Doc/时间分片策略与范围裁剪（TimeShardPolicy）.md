# 时间分片策略与范围裁剪（TimeShardPolicy）

`TimeShardPolicy` 是 XCode 默认时间分片策略，实现了：
- 按时间路由分库分表
- 按时间区间推导“需要扫描的分片集合”
- 在单分片命中时自动裁剪查询条件（减少冗余条件）

---

## 1. 策略参数

| 参数 | 说明 |
|------|------|
| `Field` | 时间字段（`DateTime` 或雪花 `Int64`） |
| `ConnPolicy` | 连接名模板，如 `{0}_{1:yyyy}` |
| `TablePolicy` | 表名模板，如 `{0}_{1:yyyyMM}` |
| `Step` | 区间扫描步进，默认 1 天 |
| `Level` | 分片粒度（Year/Month/Day/Hour） |

示例：

```csharp
Meta.ShardPolicy = new TimeShardPolicy(nameof(CreateTime), Meta.Factory)
{
    ConnPolicy = "{0}",
    TablePolicy = "{0}_{1:yyyyMM}",
    Step = TimeSpan.FromDays(1)
};
```

---

## 2. Shard(value) 的三种输入

`Shard(Object value)` 支持：
1. 实体对象 `IModel`：读取 `Field` 对应值
2. `DateTime`：直接按模板格式化
3. `Int64`：按雪花 ID 解析时间后再分片

如果时间无效（`Year<=1970`）会抛异常，避免路由到错误分表。

---

## 3. Shards(start, end) 区间推导

调用 `Shards(start,end)` 时：

1. 根据 `Step` 推断 `Level`（若未显式设置）；
2. 把 `start` 归整到粒度边界；
3. 从 `start` 循环到 `end`，按 `GetNext(level,dt,step)` 推进；
4. 对每个时间点计算 `ShardModel(conn,table)`；
5. 用哈希去重得到最终分片数组。

这使范围查询只扫描必要分表，避免全表 Union。

---

## 4. 表达式分片（Shards(expression)）

当查询条件包含时间字段表达式（`>=`,`<`,`=`）时：

- 会解析出起止时间并计算分片集合；
- 若是雪花 ID 字段，会先解析出时间；
- 若条件不足（没有时间范围）会抛错，防止盲扫所有分片。

---

## 5. 条件裁剪优化（Trim）

一个很实用的优化：

当表达式是标准区间 `start <= field < end`，且计算结果只命中**单个分片且刚好完整覆盖**，策略会把这两个时间条件从 where 中移除。

收益：
- SQL 更短
- 索引更容易命中
- 避免重复条件影响优化器

---

## 6. 时间步进与级别推断

`Step` 到 `Level` 的默认映射：
- `>=360天` → Year
- `28~31天` → Month
- `1天` → Day
- `1小时` → Hour
- 其它 → 自定义步进

建议保持 `Step` 与 `TablePolicy` 粒度一致，例如：
- 月分表就用 `Step=1天` 或 `1月`（至少别小于小时）
- 日分表建议 `Step=1天`

---

## 7. 常见异常与处理

1. **分片字段为空**：插入/查询前确保时间字段已赋值。
2. **雪花解析失败**：确认主键确实由同一雪花算法生成。
3. **条件不足无法分片**：在查询层强制要求时间范围，推不出分片时直接抛异常，不回落基准表（回落会漏掉其它分片数据）。确需跨分片查询时，自行用 `Shards(start, end)` 枚举目标分片逐表查询。
4. **跨时区差异**：策略按传入时间原值路由，不做时区换算；业务层要统一时区标准。

---

## 8. 推荐实践

- 分片表查询接口必须携带起止时间
- 对于热数据，优先查最近分片；历史区间走离线任务
- `ConnPolicy` 用于跨库，`TablePolicy` 用于同库多表，组合使用可扩展到超大规模
