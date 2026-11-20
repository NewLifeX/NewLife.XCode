# 数据库工厂与提供者注册（DbFactory）

`DbFactory` 是 XCode DAL 层的**驱动注册中心**：维护 `DatabaseType → IDatabase` 的映射，负责按连接字符串自动识别并实例化正确的数据库驱动。

---

## 1. 内置注册表

`DbFactory` 在静态构造函数里注册了全部内置驱动：

| `DatabaseType` | 驱动类 | 说明 |
|---------------|--------|------|
| `SQLite` | `SQLite` | 默认内嵌，无需额外驱动 |
| `MySql` | `MySql` | 需 `NewLife.MySql` 或 `MySql.Data` |
| `SqlServer` | `SqlServer` | 需 `Microsoft.Data.SqlClient` |
| `Oracle` | `Oracle` | 需 Oracle 驱动 |
| `PostgreSQL` | `PostgreSQL` | 需 `Npgsql` |
| `DaMeng` | `DaMeng` | 达梦数据库 |
| `DB2` | `DB2` | IBM DB2 |
| `TDengine` | `TDengine` | TDengine 时序库 |
| `KingBase` | `KingBase` | 人大金仓 |
| `HighGo` | `HighGo` | 瀚高 |
| `IRIS` | `IRIS` | InterSystems IRIS |
| `VastBase` | `VastBase` | VastBase |
| `InfluxDB` | `InfluxDB` | InfluxDB 时序库 |
| `NovaDb` | `NovaDb` | NovaDb |
| `Network` | `NetworkDb` | HTTP 虚拟数据库 |
| `Hana` | `Hana` | SAP HANA |
| `ClickHouse` | `ClickHouse` | 列式分析库，需 `ClickHouse.Client` |
| `DuckDB` | `DuckDB` | 嵌入式 OLAP，需 `DuckDB.NET.Data` |
| `MongoDB` | `MongoDB` | 文档型数据库 |
| `SonnetDB` | `SonnetDB` | 官方 ADO.NET 驱动 |
| `Presto` | `Presto` | 联邦查询引擎，优先 `NewLife.Presto`，未安装时回退 `NReco.PrestoAdo`；不支持事务与自动建表 |
| `Trino` | `Trino` | 联邦查询引擎，需 `Trino.Client.ADO`；不支持事务与自动建表 |
| `Firebird` | `Firebird` | 开源关系型库，需 `FirebirdSql.Data.FirebirdClient` |
| `Snowflake` | `Snowflake` | 云原生数据仓库，需 `Snowflake.Data` |
| `Vertica` | `Vertica` | 列式分析型数据仓库，需 `Vertica.Data` |
| `Ase` | `Ase` | SAP ASE(Sybase)，需 `AdoNetCore.AseClient` |
| `Teradata` | `Teradata` | 企业级数据仓库，需 `Teradata.Client.Provider` |
| `YashanDB` | `YashanDB` | 国产 Oracle 兼容库，需 `Yashandb.Data.YashandbClient` |
| `MonetDB` | `MonetDB` | 列式关系型库，需 `MonetDb.Mapi`（反射工厂适配） |
| `Spanner` | `Spanner` | Google Cloud Spanner，需 `Google.Cloud.Spanner.Data`；强制 `Migration=Off` |
| `OpenTSDB` | `OpenTSDB` | 时序库，内置 HTTP JSON 提供者，只支持追加写 |
| `IoTDB` | `IoTDB` | Apache IoTDB，内置 REST v2 提供者，只做 DAL 原生 SQL |
| `Prometheus` | `Prometheus` | 只读 PromQL 查询，兼容 VictoriaMetrics |

> MySQL / PostgreSQL 协议兼容的数据库（TiDB、Doris、StarRocks、openGauss、Redshift 等）通过 `Support` 别名复用对应提供者，详见 [数据库支持矩阵](数据库支持矩阵.md)。

---

## 2. 自定义注册

外部驱动可通过 `Register` 注入：

```csharp
// 注册自定义数据库驱动
DbFactory.Register<MyCustomDb>(DatabaseType.Other);
```

---

## 3. 提供者识别算法

`GetProviderType(connStr, provider)` 按优先级识别驱动类型：

1. **连接字符串中的 `Provider` 键**（最高优先）  
   如 `Server=xxx;Provider=MySql` → 回调 `IDatabase.Support("MySql")`
2. **外部 `provider` 参数**  
   遍历已注册驱动依次调用 `Support(provider)`
3. **全类型名**（如 `MySql.Data.MySqlClient.MySqlClientFactory`）  
   用反射直接 `Type.GetType` / `GetTypeEx` 加载
4. **默认 SQLite**  
   上述均失败时回退到 SQLite

---

## 4. 驱动实例化策略

- `_dbs` 字典存原型（prototype），每次 `Create(dbType)` 通过 `CreateInstance()` 克隆新实例
- `GetDefault(dbType)` 返回原型单例（仅用于格式化 SQL 等只读场景）
- 每个 DAL 连接名对应自己的 `IDatabase` 实例，驱动配置互不干扰

---

## 5. 调用链

```
DAL.Create("connName")
  → DbFactory.GetProviderType(connStr, provider)
  → DbFactory.Create(dbType) → clone → 设置 ConnName / ConnStr
  → IDatabase 实例注入 DAL
```

---

## 6. 关联阅读

- `/xcode/idatabase_dbbase`
- `/xcode/driver_loader`
- `/xcode/connection_string_builder`
- `/xcode/dal_setting`
