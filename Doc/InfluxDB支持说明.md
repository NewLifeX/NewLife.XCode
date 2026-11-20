# XCode InfluxDB 时序数据库支持

## 概述

XCode 内置 InfluxDB 提供者，支持 **1.x / 2.x / 3.x**，按服务端版本自动选择写入端点与查询语言：

| 版本 | 写入 | 默认查询语言 | 说明 |
|:----:|------|-------------|------|
| 1.x | `/write` | InfluxQL | 用户名/密码认证，database 概念 |
| 2.x | `/api/v2/write` | Flux | Token + Organization + Bucket |
| 3.x | `/api/v3/write_lp` | SQL | Token + Database；不再支持 Flux 查询 |

> **能力边界**：InfluxDB 不是关系型数据库。当前支持批量 Insert/Upsert、Flux/InfluxQL/SQL 查询和 measurement 列表；单行实体 Insert/Update/Delete、自增主键、JOIN、事务和通用 DDL 不在支持范围。实体 ORM 请使用关系型数据库；InfluxDB 适合通过 DAL 原始命令/批量写入使用。

## 功能特性

- ✅ InfluxDB 1.x（InfluxQL）、2.x（Flux）、3.x（SQL/InfluxQL）
- ✅ Line Protocol 批量写入
- ✅ `Version=Auto` 通过 `/ping` 自动探测版本，也可显式指定
- ✅ Token / Basic 认证
- ✅ 批量数据写入
- ✅ 自动处理 CSV 与 JSON 查询结果
- ✅ 兼容 .NET Framework 4.5 到 .NET 11

## 快速开始

### 1. 安装

```bash
dotnet add package NewLife.XCode
```

### 2. 连接字符串配置

```csharp
// 2.x（默认按 Token/Organization/Bucket 识别）
DAL.AddConnStr("InfluxDB", "Server=http://localhost:8086;Token=your-token;Organization=your-org;Bucket=your-bucket", null, "InfluxDB");

// 1.x
DAL.AddConnStr("InfluxDB1", "Server=http://localhost:8086;Database=telegraf;User=root;Password=root;Version=1", null, "InfluxDB");

// 3.x
DAL.AddConnStr("InfluxDB3", "Server=http://localhost:8181;Database=sensors;Token=your-token;Version=3", null, "InfluxDB");
```

#### 连接字符串参数说明

| 参数 | 必填 | 说明 | 示例 |
|------|------|------|------|
| Server | 是 | 服务地址（包含协议） | `http://localhost:8086` |
| Version | 否 | `Auto`（默认）/ `1` / `2` / `3` | `Version=3` |
| QueryLanguage | 否 | `Auto` / `Flux` / `InfluxQL` / `SQL` | `QueryLanguage=InfluxQL` |
| Token | 2.x/3.x | API Token | `your-influxdb-token` |
| Organization | 2.x | 组织名称或 ID | `my-org` |
| Bucket / Database | 是 | 1.x database，或 2.x/3.x bucket/database | `my-bucket` |
| User / Password | 1.x | Basic 认证 | `root` / `root` |

> 未指定 `Version` 时，首次请求会访问 `/ping`（失败时回退 `/health`）探测主版本，并按 server 缓存 10 分钟。

### 3. 基本使用

#### 3.1 写入数据（Line Protocol）

```csharp
var dal = DAL.Create("InfluxDB");

// 单条数据写入
// 格式：measurement,tag1=value1,tag2=value2 field1=value1,field2=value2 timestamp
var lineProtocol = $"temperature,location=room1,sensor=sensor1 value=23.5,humidity=45 {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000}";
dal.Execute(lineProtocol);

// 批量数据写入
var lines = new[]
{
    "temperature,location=room1 value=21.0",
    "temperature,location=room2 value=22.5",
    "temperature,location=room3 value=23.0",
};
var batchData = String.Join("\n", lines);
dal.Execute(batchData);
```

#### 3.2 查询数据（Flux / InfluxQL / SQL）

```csharp
var dal = DAL.Create("InfluxDB");

// 2.x Flux（默认）
var flux = @"
from(bucket: ""my-bucket"")
  |> range(start: -1h)
  |> filter(fn: (r) => r._measurement == ""temperature"")
  |> limit(n: 100)
";
var dt = dal.Query(flux);

// 1.x / 2.x InfluxQL：QueryLanguage=InfluxQL 或 Version=1
var dt1 = dal.Query("SELECT * FROM temperature WHERE time > now() - 1h LIMIT 100");

// 3.x SQL：Version=3 默认
var dt3 = dal.Query("""SELECT * FROM "home" WHERE room = 'Kitchen' LIMIT 100""");
```

查询结果：

- Flux 返回带 `#datatype` 注释的 CSV，按字符串列读取；
- InfluxQL 返回 `results[].series[]`，展开为行式表，并附带 `_measurement` 列；
- 3.x SQL 使用 JSONL 响应，每行一个对象，列名取字段名。

#### 3.3 获取 Measurements（表）

```csharp
var dal = DAL.Create("InfluxDB");

// 获取所有 measurement
var tables = dal.Tables;
foreach (var table in tables)
{
    Console.WriteLine($"Measurement: {table.TableName}");
}
```

## Line Protocol 格式说明

Line Protocol 是 InfluxDB 的写入格式，语法如下：

```
measurement,tag_key1=tag_value1,tag_key2=tag_value2 field_key1=field_value1,field_key2=field_value2 timestamp
```

### 组成部分

1. **measurement**：测量名称（类似表名），必填
2. **tags**：标签（索引字段），可选，多个用逗号分隔
3. **fields**：字段值，必填，多个用逗号分隔
4. **timestamp**：时间戳（纳秒），可选，不指定则使用当前时间

### 示例

```csharp
// 基本示例
"temperature value=23.5"

// 带标签
"temperature,location=room1,sensor=sensor1 value=23.5"

// 多字段
"temperature,location=room1 value=23.5,humidity=45"

// 指定时间戳（纳秒）
var nanos = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000;
$"temperature,location=room1 value=23.5 {nanos}"
```

## Flux 查询语言

Flux 是 InfluxDB 的查询语言，功能强大且灵活。

### 基本查询示例

```csharp
// 1. 查询最近1小时的数据
var flux = @"
from(bucket: ""my-bucket"")
  |> range(start: -1h)
";

// 2. 按 measurement 过滤
var flux = @"
from(bucket: ""my-bucket"")
  |> range(start: -1h)
  |> filter(fn: (r) => r._measurement == ""temperature"")
";

// 3. 按 tag 过滤
var flux = @"
from(bucket: ""my-bucket"")
  |> range(start: -1h)
  |> filter(fn: (r) => r._measurement == ""temperature"")
  |> filter(fn: (r) => r.location == ""room1"")
";

// 4. 聚合查询（平均值）
var flux = @"
from(bucket: ""my-bucket"")
  |> range(start: -1h)
  |> filter(fn: (r) => r._measurement == ""temperature"")
  |> aggregateWindow(every: 5m, fn: mean)
";

// 5. 限制返回数量
var flux = @"
from(bucket: ""my-bucket"")
  |> range(start: -1h)
  |> limit(n: 100)
";
```

## 使用场景

### 1. 物联网数据采集

```csharp
// 传感器数据写入
var sensorId = "sensor_001";
var location = "warehouse_A";
var temperature = 25.3;
var humidity = 60.5;

var lineProtocol = $"sensor_data,sensor_id={sensorId},location={location} temperature={temperature},humidity={humidity}";
dal.Execute(lineProtocol);
```

### 2. 系统监控指标

```csharp
// 服务器性能指标
var hostname = Environment.MachineName;
var cpuUsage = GetCpuUsage();
var memoryUsage = GetMemoryUsage();

var lineProtocol = $"system_metrics,host={hostname} cpu_usage={cpuUsage},memory_usage={memoryUsage}";
dal.Execute(lineProtocol);
```

### 3. 日志与事件追踪

```csharp
// 应用程序日志
var appName = "MyApp";
var level = "INFO";
var message = "User logged in";

var lineProtocol = $"app_logs,app={appName},level={level} message=\"{message}\"";
dal.Execute(lineProtocol);
```

## 注意事项

1. **时间戳精度**：InfluxDB 使用纳秒级时间戳，需要将毫秒 × 1000000
2. **字符串字段**：在 Line Protocol 中，字符串字段值需要用双引号包裹
3. **特殊字符**：measurement、tag key/value、field key 中的空格、逗号、等号需要转义
4. **Bucket 权限**：确保 Token 有对应 Bucket 的读写权限
5. **查询性能**：合理使用 tag 进行索引，避免在 field 上频繁过滤
6. **批量写入**：多条数据用换行符分隔，提高写入效率

## 与 XCode 实体的集成

InfluxDB 主要用于时序数据存储，与 XCode 的关系型实体模型有所不同。建议：

- **时序数据**：使用 InfluxDB 的原生 Line Protocol 和 Flux 查询
- **元数据**：使用 XCode 的其他数据库（如 MySQL、PostgreSQL）存储设备信息、用户信息等
- **混合场景**：在一个应用中同时使用多个 DAL 连接，InfluxDB 负责时序数据，关系型数据库负责业务数据

## 常见问题

### Q1: 如何生成 InfluxDB Token？

A: 在 InfluxDB UI 中：
1. 进入 `Load Data` > `API Tokens`
2. 点击 `Generate API Token`
3. 选择权限范围（读/写特定 Bucket）
4. 复制生成的 Token

### Q2: 支持 InfluxDB 1.x 和 3.x 吗？

A: 支持。1.x 使用 InfluxQL（`Version=1`），2.x 使用 Flux（默认），3.x 使用 SQL（默认）或 InfluxQL。3.x 已移除 Flux，请勿在 3.x 上指定 `QueryLanguage=Flux`。

### Q3: 如何优化查询性能？

A: 
- 合理使用 tag 作为索引
- 查询时指定明确的时间范围
- 避免使用 `group()` 等高开销操作
- 使用 `limit()` 限制返回数量

### Q4: 数据保留策略如何设置？

A: 数据保留策略在 Bucket 级别设置，通过 InfluxDB UI 或 API 管理，XCode 不直接管理保留策略。

## 参考资源

- [InfluxDB 官方文档](https://docs.influxdata.com/influxdb/v2/)
- [Flux 查询语言](https://docs.influxdata.com/flux/v0/)
- [Line Protocol 参考](https://docs.influxdata.com/influxdb/v2/reference/syntax/line-protocol/)
- [XCode 文档](https://newlifex.com/xcode)

## 开源协议

本实现遵循 NewLife 系列组件的开源协议，详见项目根目录 LICENSE 文件。
