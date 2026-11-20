# XCode Prometheus / VictoriaMetrics 支持

## 概述

Prometheus 是监控与时序数据系统，VictoriaMetrics 兼容其查询 API。XCode 提供只读 PromQL 查询与指标名发现，不实现 remote write。

> **能力边界**：只读。`Execute` 会明确失败；实体 ORM、事务、DDL 与自动迁移均不支持。

## 连接字符串

```
Server=http://localhost:9090;Mode=Range;Start=-1h;End=now;Step=60s
```

| 参数 | 必填 | 说明 |
|------|------|------|
| Server | 是 | 服务地址，未写协议时自动补 `http://` |
| Mode | 否 | `Instant`（默认）或 `Range` |
| Start / End / Step | 否 | 范围查询参数，默认 `-5m` / `now` / `15s` |
| BearerToken | 否 | Bearer 认证；VictoriaMetrics 常用 |
| User / Password | 否 | Basic 认证 |

```csharp
DAL.AddConnStr("Prometheus", "Server=http://localhost:9090", null, "Prometheus");
var dal = DAL.Create("Prometheus");
```

供应商名称支持 `Prometheus`、`VictoriaMetrics` 与 `Victoria`，都落到同一提供者。

## 查询

`CommandText` 就是 PromQL：

```csharp
// 即时查询：/api/v1/query
var dt = dal.Query("up");

// 范围查询：连接串 Mode=Range，或使用 range: 前缀
var dt2 = dal.Query("range: rate(http_requests_total[5m])");

// 强制即时查询
var dt3 = dal.Query("instant: up");
```

结果表列顺序：`time`、`metric`、各标签列（按名称排序）、`value`。`matrix` 每个采样点一行，`vector` 每个序列一行，`scalar/string` 单行。

## 指标名发现

`dal.Tables` 通过 `/api/v1/label/__name__/values` 返回全部指标名，每个指标给出 `time` 与 `value` 两列占位。

## 能力边界

| 能力 | 支持 |
|------|:----:|
| instant / range 查询 | ✅ |
| 指标名发现 | ✅ |
| 写入 | ❌ 只读 |
| 事务 / DDL / 自增 | ❌ |
| 自动迁移 | ❌ `Migration=Off` |

## 注意事项

1. NaN 与 ±Inf 会保留为 `Double.NaN` / `Double.PositiveInfinity` / `Double.NegativeInfinity`。
2. 范围查询跨度大时注意 `Step` 与返回点数，避免一次拉取过多数据。
3. VictoriaMetrics 的连接串示例：`Server=http://localhost:8428;BearerToken=xxx`。
