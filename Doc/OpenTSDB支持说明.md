# XCode OpenTSDB 时序数据库支持

## 概述

OpenTSDB 是构建在 HBase 之上的时序数据库，使用 `metric + timestamp + value + tags` 存储数据点。XCode 内置 HTTP JSON 提供者，不依赖第三方 ADO.NET 驱动。

> **能力边界**：只支持追加写入与原生 JSON 查询；更新、删除、事务、自增主键、JOIN 与自动建表均不支持。实体 ORM 只用于写入，查询请使用 DAL 原始命令。

## 连接字符串

```
Server=http://localhost:4242;Timeout=30
```

| 参数 | 必填 | 说明 |
|------|------|------|
| Server | 是 | OpenTSDB TSD 地址，未写协议时自动补 `http://` |
| User / Password | 否 | 仅用于前置代理的 Basic 认证；OpenTSDB 本身没有认证 |
| Timeout | 否 | 超时秒数，默认 30 |

```csharp
DAL.AddConnStr("OpenTSDB", "Server=http://localhost:4242", null, "OpenTSDB");
var dal = DAL.Create("OpenTSDB");
```

## 写入

写入内容为 `/api/put` 的 JSON 数组：

```csharp
var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
var json = $@"[
  {{""metric"":""sys.cpu"",""timestamp"":{now},""value"":18.5,""tags"":{{""host"":""web01"",""dc"":""lga""}}}}
]";
dal.Execute(json);
```

新建指标需要服务端开启 `tsd.core.auto_create_metrics=true`，否则写入会返回 `No such metric`。

## 查询

`CommandText` 支持两种写法：

```csharp
// 1. 简化写法：GET /api/query，缺省 start=1h-ago
var dt = dal.Query("m=sum:sys.cpu{host=web01}&start=1h-ago");

// 2. 原生 JSON：POST /api/query
var dt2 = dal.Query("""{"start":1356998400,"queries":[{"metric":"sys.cpu","aggregator":"sum"}]}""");
```

查询结果展开为行式表：`time`、`metric`、各 tag 列、`value`。`dps` 的时间戳按位数识别秒/毫秒，统一转为本地 `DateTime`。

## 实体写入

- 时间列（`Time`/`CreateTime`/`UpdateTime`）映射为 timestamp，缺失时使用当前时间。
- 主键、`Master` 与字符串字段映射为 tags。
- 数值字段映射为 value：
  - 只有一个数值字段时，metric 使用表名；
  - 有多个数值字段时，拆成 `表名.字段名` 子指标。
- 更新与删除会抛出能力门禁异常。

## 能力边界

| 能力 | 支持 |
|------|:----:|
| 批量 Insert / Upsert | ✅ |
| 原生 JSON 查询 | ✅ |
| 指标名发现 | ✅ `/api/suggest?type=metrics` |
| 更新 / 删除 | ❌ |
| 事务 / 自增 / DDL | ❌ |
| 自动迁移 | ❌ `Migration=Off` |

## 注意事项

1. metric 与 tag 值不能包含空白字符，写入前会校验并报错。
2. OpenTSDB 没有数据库概念，`Database` 参数无效。
3. 同一 `metric + timestamp + tags` 的重复写入会覆盖旧值，但 HBase 合并时机由服务端决定。
