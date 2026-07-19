# Shards 分配策略与路由（Shards）

分表分库是 XCode 处理海量数据的核心能力之一。`XCode.Shards` 提供策略接口 `IShardPolicy` 与两个内置实现 `TimeShardPolicy`（时间分片）和 `FieldShardPolicy`（业务字段分片），用于把"实体对象/时间/雪花Id/查询表达式"映射到目标库表。

## 核心对象

- `IShardPolicy`
  - `Shard(Object value)`：单值路由（实体、时间、雪花Id、直接字段值）
  - `Shards(DateTime start, DateTime end)`：按时间区间给出多分片
  - `Shards(Expression expression)`：从查询条件推导多分片
- `ShardModel(String ConnName, String TableName)`：目标分片描述
- `TimeShardPolicy`：内置时间分片策略，支持连接名与表名双路由
- `FieldShardPolicy`：内置业务字段分片策略，按任意字段值（如 UserId）路由到对应分表

## 1）策略挂载位置

策略挂在实体元操作器上：`Meta.ShardPolicy`。

```csharp
Meta.ShardPolicy = new TimeShardPolicy(nameof(Id), Meta.Factory)
{
    ConnPolicy = "{0}_{1:yyyy}",
    TablePolicy = "{0}_{1:yyyyMMdd}",
    Step = TimeSpan.FromDays(1),
};
```

其中：
- `ConnPolicy`：连接名格式，`{0}` 基础连接名，`{1}` 时间
- `TablePolicy`：表名格式，`{0}` 基础表名，`{1}` 时间
- `Step`：区间扫描步长（默认 1 天）

## 2）路由输入类型

`TimeShardPolicy.Shard(Object value)` 支持：

- `IModel`：读取指定分片字段（`Field`）
- `DateTime`：按时间格式直接计算
- `Int64`：按雪花Id解析业务时间，再计算分片

不支持类型会抛出 `XCodeException`，例如时间字段为空、雪花Id解析失败等。

## 3）分片字段与类型约束

策略字段来自 `FieldItem`：

- 若字段类型是 `DateTime`：直接用值分片
- 若字段类型是 `Int64`：按雪花算法解析时间分片
- 其它类型：不支持（抛异常）

因此大数据表常见两种建模方式：

- 时间字段分片：`CreateTime` / `DataTime`
- 雪花Id分片：`Id(Int64)`

## 4）区间分片算法（核心）

`Shards(start, end)` 会：

1. 根据 `Step` 推断级别（年/月/日/小时）
2. 对 `start` 做对齐（例如按天分片时对齐到 `Date`）
3. 逐步迭代生成 `ShardModel`
4. 用 `ConnName#TableName` 去重
5. 若是倒序区间，最终反转结果

> 这意味着你可以安全传入较长时间区间，策略会生成稳定且不重复的分片集合。

## 5）表达式分片

`Shards(Expression)` 会从查询条件中提取分片字段条件：

- 时间字段：识别 `= / >= / > / <= / <`
- 雪花Id字段：识别范围或等值
- 条件不足时抛异常：`分表策略因条件不足无法执行分表查询操作`

当范围刚好覆盖完整单分片时，会尝试裁剪原始时间边界条件，减少重复过滤。

## 6）批量写入的自动路由

批量 `Insert` 时，如果未显式指定会话且配置了 `ShardPolicy`：

- 框架会先按分片策略分组
- 再按分组分别执行批量插入

这保证同一批数据可自动分流到多个库表，同时仍然使用批量插入能力。

## 7）与代码生成联动

当模型字段 `DataScale` 使用 `timeShard:*` 时，代码生成器会自动生成：

- `Meta.ShardPolicy = new TimeShardPolicy(...)`
- 对应 `Step`（按小时/天/月/年推断）

建议优先通过 `Model.xml` 声明分片意图，让生成器维护策略代码，避免手工漂移。

## 8）推荐配置模板

### 按天分表（单库）

```csharp
Meta.ShardPolicy = new TimeShardPolicy(nameof(CreateTime), Meta.Factory)
{
    TablePolicy = "{0}_{1:yyyyMMdd}",
    Step = TimeSpan.FromDays(1),
};
```

### 按月分库分表

```csharp
Meta.ShardPolicy = new TimeShardPolicy(nameof(Id), Meta.Factory)
{
    ConnPolicy = "{0}_{1:yyyy}",
    TablePolicy = "{0}_{1:yyyyMM}",
    Step = TimeSpan.FromDays(30),
};
```

## 9）业务字段分表策略（FieldShardPolicy）

`FieldShardPolicy` 适用于多租户等按业务字段固定路由的场景。整张分表内，该字段的值保持一致。

### 核心特性

- 按任意字段（`Int32`/`Int64`/`String` 等）的**等值**路由
- 写入（Insert/Update/Delete）和等值查询均自动路由到对应分表
- 非等值查询（`>`、`<` 等）不路由，回落到主表（安全降级）
- 不支持时间区间扫描（`Shards(start, end)` 返回空数组）

### 配置示例

```csharp
// 按 UserId 字段分表，UserId=1000 时表名为 UserLog_1000
Meta.ShardPolicy = new FieldShardPolicy(nameof(UserId), Meta.Factory)
{
    TablePolicy = "{0}_{1}",
};

// 同时分库分表
Meta.ShardPolicy = new FieldShardPolicy(nameof(TenantId), Meta.Factory)
{
    ConnPolicy = "{0}_{1}",
    TablePolicy = "{0}_{1}",
};
```

其中：
- `ConnPolicy`：连接名格式，`{0}` 基础连接名，`{1}` 字段值（如 `{0}_{1}` → `mydb_1000`）
- `TablePolicy`：表名格式，`{0}` 基础表名，`{1}` 字段值（如 `{0}_{1}` → `Log_1000`）
- 默认 `TablePolicy = "{0}_{1}"`，`ConnPolicy` 为空（不切库）

### 路由输入类型

`FieldShardPolicy.Shard(Object value)` 支持：

- `IModel`：从实体对象读取指定分片字段值
- 任意直接值（`Int32`/`Int64`/`String` 等）：直接格式化进策略

### 查询路由

`Shards(Expression expression)` 从查询条件中寻找分片字段的**等值条件**：

- `UserId == 1000` → 路由到 `Log_1000`
- `Success == true & UserId == 2000` → 路由到 `Log_2000`
- `UserId > 0`（非等值）→ 返回空，回落主表
- 不含分片字段条件 → 返回空，回落主表

### 推荐配置模板

#### 多租户按 TenantId 分表

```csharp
// 一般在实体类的静态构造函数中配置
static UserLog()
{
    Meta.ShardPolicy = new FieldShardPolicy(nameof(TenantId), Meta.Factory)
    {
        TablePolicy = "{0}_{1}",
    };
}
```

#### 多租户按 UserId 分库分表

```csharp
static Log()
{
    Meta.ShardPolicy = new FieldShardPolicy(nameof(UserId), Meta.Factory)
    {
        ConnPolicy = "db_{1}",
        TablePolicy = "{0}_{1}",
    };
}
```

## 10）常见问题

- **为何查询没走分片？**
  - 查询条件里缺少分片字段，或表达式无法提取范围。
- **为何写入落到主表？**
  - `ShardPolicy` 未配置，或输入对象分片字段值无效。
- **雪花Id分片报错？**
  - Id 不是合法雪花值，无法解析时间。
- **FieldShardPolicy 范围查询不分表？**
  - `FieldShardPolicy` 仅支持等值条件路由，范围查询（`>`、`<`）会回落主表，这是预期行为。

## 11）实践建议

- 分片键必须稳定、可提取区间（`TimeShardPolicy` 推荐时间或雪花Id，`FieldShardPolicy` 推荐租户/用户Id）。
- `TimeShardPolicy` 的 `Step` 尽量与分片粒度一致，避免过度扫描。
- `FieldShardPolicy` 适合整表固定属于某个业务主体的场景（如多租户日志）。
- 批量写入时优先交给框架自动分组，不要手工拆库拆表。
- 分片策略应与数据保留策略（按天/按月清理）统一设计。