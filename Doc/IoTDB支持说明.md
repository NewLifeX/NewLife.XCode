# XCode Apache IoTDB 支持

## 概述

Apache IoTDB 是面向工业物联网的时序数据库，使用树模型 `root.{database}.{device}.{measurement}`。XCode 通过 REST v2 接口访问，SQL 原样转发，不引入第三方驱动。

> **能力边界**：只提供 DAL 原生 SQL 查询与非查询，不提供实体 ORM；事务、自增主键与自动迁移均不支持。

## 连接字符串

```
Server=http://localhost:18080;User=root;Password=root;RowLimit=10000
```

| 参数 | 必填 | 说明 |
|------|------|------|
| Server | 是 | IoTDB REST 地址，未写协议时自动补 `http://` |
| User / Password | 否 | Basic 认证，默认 root/root |
| RowLimit | 否 | 查询最大行数，0 表示使用服务端默认值 |

```csharp
DAL.AddConnStr("IoTDB", "Server=http://localhost:18080;User=root;Password=root", null, "IoTDB");
var dal = DAL.Create("IoTDB");
```

## 查询

```csharp
var dt = dal.Query("SELECT s1, s2 FROM root.sg.d1 WHERE time >= 2024-01-01T00:00:00");
```

结果表规则：

- 数据查询：第一列 `Time`（毫秒时间戳转本地时间），其余列为 `expressions` 中的时间序列；
- 元数据查询（如 `SHOW DATABASES`、`SHOW TIMESERIES`）：列名来自 `column_names`，没有 `Time` 列。

## 写入与 DDL

```csharp
dal.Execute("CREATE DATABASE root.sg");
dal.Execute("INSERT INTO root.sg.d1(timestamp, s1, s2) VALUES(1704067200000, 1.0, 2.0)");
```

所有非查询语句走 `/rest/v2/nonQuery`。

## 能力边界

| 能力 | 支持 |
|------|:----:|
| 原生 SQL 查询 / DDL / DML | ✅ |
| 架构发现 | ⚠️ 仅 database 级 |
| 实体 ORM | ❌ 能力门禁拒绝 |
| 事务 / 自增 | ❌ |
| 自动迁移 | ❌ `Migration=Off` |

## 注意事项

1. 服务端默认 `rest_query_default_row_size_limit` 可能截断结果，大结果集请设置 `RowLimit` 并注意内存。
2. IoTDB 的“表”是设备路径，XCode 元数据只返回 database 级占位；复杂建模请手写 `Model.xml`。
3. 服务地址默认端口为 18080，`/ping` 可用于探活。
