# ETL 数据抽取转换（Transform）

> ⚠️ **已移除**：ETL 执行引擎已于 2026-07-15（v12.1 清理，提交 `f033bd45b`）从仓库删除，仅保留数据抽取器。本文档作为历史设计参考保留，当前版本不再包含该实现。

XCode 的 `ETL`（Extract-Transform-Load）框架位于 `XCode.Transform` 命名空间，用于**批量分批抽取源表数据、经转换处理后写入目标**。典型场景包括：跨库数据同步、历史数据归档、离线统计预计算、大批量数据清洗。



## 处理流程

```
Start       启动，初始化抽取器和抽取设置
└── Process     大循环处理（定时或手动驱动）
    ├── Processing  检查是否可以启动本轮
    ├── Fetch       按设置抽取一批数据，并滑动进度游标
    ├── ProcessList 处理本批列表（可异步）
    │   ├── OnProcess   -> ProcessItem  逐条处理
    │   └── OnSync      -> SyncItem     同步到目标
    │       ├── GetItem     查找或新建目标对象
    │       └── SaveItem    保存目标对象
    └── Processed   保存本批进度
Stop        停止处理
```



## 快速上手

### 场景一：纯自定义处理（继承 ETL<TSource>）

```csharp
public class OrderWorker : ETL<Order>
{
    protected override Int32 ProcessItem(Order src, DataContext ctx)
    {
        // 对每条记录做自定义处理
        var report = new DailyReport();
        report.Amount += src.Amount;
        report.Save();
        return 1;
    }
}

// 启动
var worker = new OrderWorker();
worker.Start();
```

### 场景二：同步到目标实体（覆写 SyncItem）

```csharp
public class OrderSync : ETL<Order>
{
    protected override IEntity GetItem(Order src, DataContext ctx)
        => OrderArchive.FindByKey(src.ID) ?? new OrderArchive();

    protected override void SaveItem(IEntity target, Order src, DataContext ctx)
    {
        var arch = (OrderArchive)target;
        arch.CopyFrom(src);
        arch.Save();
    }
}
```



## 抽取器（IExtracter）

抽取器负责从源数据库**按批**取出数据并滑动游标，内置了三种：

| 类型 | 说明 |
|---|---|
| `TimeExtracter` | 按时间字段分批抽取，默认抽取 `CreateTime` / `UpdateTime` 递增数据，适合增量同步 |
| `TimeSpanExtracter` | 按时间区间分批（固定步长），适合补跑历史数据 |
| `PagingExtracter` | 按分页（Row 偏移）抽取，适合无时间字段的全量表 |
| `IdExtracter` | 按自增 ID 分批，适合主键连续的大表 |
| `EntityIdExtracter` | 按实体主键分批 |

默认构造 `ETL<TSource>` 时会自动创建 `TimeExtracter`，也可以手动指定：

```csharp
var etl = new ETL<Order>();
etl.Extracter = new PagingExtracter { Factory = Order.Meta.Factory };
etl.Setting = new ExtractSetting { BatchSize = 1000 };
etl.Start();
```



## 抽取设置（IExtractSetting）

`ExtractSetting` 控制每一轮的抽取范围：

| 属性 | 说明 | 默认 |
|---|---|---|
| `Start` | 起始时间（≥） | 上次处理进度 |
| `End` | 结束时间（<） | 当前时间 - Offset |
| `Offset` | 时间偏移秒数（避免追到实时） | `0` |
| `Row` | 起始行（分页） | `0` |
| `Step` | 每批最大时间步长（秒） | `3600` |
| `BatchSize` | 每批最大记录数 | `5000` |



## 并发与模块化

### 并行处理

设置 `MaxTask > 0` 可让多批数据并发处理：

```csharp
etl.MaxTask = 4;   // 最多 4 批数据同时处理
```

### 模块管道（IETLModule）

通过 `Module` 属性插入处理模块，可以在 `Start`/`Stop`/`Processing`/`Init` 等生命周期钩子上扩展逻辑：

```csharp
etl.Module = new MyFilterModule();
```

### 错误控制

```csharp
etl.MaxError = 5;  // 连续错误超过 5 次后自动停止
```



## 定时驱动

设置 `Period`（秒）后 ETL 会自动以定时器驱动，无需外部调度：

```csharp
etl.Period = 60;   // 每 60 秒执行一轮
etl.Start();
```

不设置 `Period` 时，需手动调用 `Process()` 或 `Start()` 驱动循环。



## 统计信息

`Stat`（实现 `IETLStat`）记录处理进度和成功/失败计数，可对接监控系统：

```csharp
var stat = etl.Stat;
Console.WriteLine($"成功={stat.Success} 错误={stat.Error}");
```



## 注意事项

- `TimeExtracter` 依赖源表有**时间类型**的更新字段，首次运行需确认 `Start` 初始值，避免全量重跑。
- `PagingExtracter` 使用 Row 偏移，源表有删除操作时可能跳过数据，优先用 `IdExtracter` 替代。
- 同步到目标时，`GetItem` 返回 `null` 表示跳过该条记录。
- `MaxTask > 0` 异步处理时，`OnProcess` 会先新增进度记录再异步执行，失败处理需在 `OnError` 中处理。
