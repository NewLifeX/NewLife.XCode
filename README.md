# NewLife.XCode - Data Middleware / Super ORM

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.xcode?logo=github)


![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.XCode?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.XCode?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.XCode?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.XCode?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.XCode/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.XCode/actions/workflows/test.yml)
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](Doc/多语言本地化.md)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.XCode?logo=github)](https://github.com/NewLifeX/NewLife.XCode/graphs/contributors)

<!-- lang:start -->
🌐 [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

High-performance .NET data middleware focused on "CRUD + extreme performance + massive data". It includes multi-level caching, automatic modeling/migration, sharding, strongly-typed queries and cross-database migration. It supports MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB / Hana (SAP HANA) / IRIS (InterSystems) / VastBase / Network virtual database / NovaDb and more, and provides [SonnetDB adaptation and capability verification](Doc/SonnetDB.md). Production deployments reach tens of billions of rows per table, and query throughput can reach billions of QPS (with caching strategies).


Docs: https://newlifex.com/xcode   Community QQ groups: 1600800 / 1600838

---
## Contents
- 1. Why XCode
- 2. Installation and Quick Start
- 3. Architecture and Core Concepts
  - 3.1 Model and Code Generation
  - 3.2 Entity Runtime (Meta / Session / Operate)
  - 3.3 DAL Abstraction Layer
  - 3.4 Query DSL (IQueryable LINQ / WhereExpression / Window Functions)
  - 3.5 Multi-level Cache System (Meta.Count / EntityCache / SingleCache / distributed second-level cache)
  - 3.6 Reverse Engineering & Migration
  - 3.7 Sharding (ShardPolicy)
  - 3.8 Transactions and Cache Consistency
  - 3.9 Performance Optimizations at a Glance
  - 3.10 Data Scope and Multi-Tenancy (DataScope / Tenant)
- 4. Quick Start (Model → Code → CRUD)
- 5. Advanced Usage Examples
- 6. Indexes / Auto-generated Methods
- 7. Sharding Example
- 8. Multi-database Configuration and Containerization
- 9. Benchmarks / Production Metrics
- 10. FAQ Highlights
- 11. Ecosystem Matrix
- 12. Contributing
- 13. License

---
## 1. Why XCode
- **Homegrown and original**: fully developed in China with no third-party components, self-controlled and backed by long-term maintenance.
- **Simplicity**: 90% of business scenarios need only single-table entity operations, with no hand-written SQL.
- **Dapper compatible**: executes arbitrary SQL queries and maps them to any entity model; fully compatible with Dapper usage while adding caching, read/write splitting and tracing.
- **Extreme performance**: multi-level caching on the core path + batch optimization + in-memory joins; query speed can be far higher than raw SQL.
- **Massive data**: native paging and sharding strategies; production projects reach tens of billions of rows per table; paging algorithms adapt to multiple databases.
- **Stable and mature**: 20+ years of evolution and broad production validation; keeps compatibility with older .NET Framework versions (from net45).
- **Automatic migration**: model/entity is the schema; new fields are synchronized automatically on deployment, with no extra SQL scripts.
- **Distributed evolution**: native table-level / time / primary-key sharding strategies for smooth splitting and scaling.
- **Pluggable**: DAL abstraction + separated providers; custom database adapters are supported.

---
## 2. Installation and Quick Start
NuGet reference (any project):
```
dotnet add package NewLife.XCode
```
Or use the template scaffold:
```
dotnet new install NewLife.Templates
dotnet new xcode --name Demo.Data
cd Demo.Data
dotnet build
```
> The strong-name certificate is built in (newlife.snk); you can recompile for a private enterprise framework.

---
## 3. Architecture and Core Concepts
XCode uses a layered architecture of "model-driven + runtime metadata + multi-level cache + optional sharding".

### 3.1 Model and Code Generation
XML model (Tables/Columns/Indexes) → code generator (xcodetool or VS T4) → entity data class (.cs) + business partial class (.Biz.cs).
- Data class: overwritten on every generation; holds fields and basic queries.
- Business class: not overwritten after the first generation; place custom logic here.
- Supports: indexes, unique constraints, default values, descriptions, extended properties, auto-generated methods (FindByXxx / Search).

### 3.2 Entity Runtime
Core interfaces/types:
- IEntity / Entity: rich domain model encapsulating fields, validation and lifecycle.
- IEntityOperate: static operation entry point (Find / FindAll / Query, etc.).
- EntityFactory: entity metadata cache factory.
- EntitySession (Meta.Session): context of an entity on a specific connection/table, with session-level cache/statistics.
- Meta: entity static context (table schema, fields, caches, sharding policy, etc.).

### 3.3 DAL Abstraction Layer
- DAL / IDatabase / IDbSession: unified database access and metadata operations.
- Migration support (Off / ReadOnly / On / Full) controls automatic creation of databases, tables and columns.
- CheckTables(): checks and synchronizes the schema on startup/first access, asynchronously or synchronously as needed.

### 3.4 Query DSL
- **IQueryable&lt;T&gt; LINQ queries (new in v12.0)**: `Entity<T>.Query` returns `IQueryable<T>`, supporting standard LINQ operations such as `Where/OrderBy/Skip/Take/Count/First/ToList`.
- **DAL.Select&lt;T&gt;() fluent queries (new in v12.1)**: `DAL.Select<T>()` returns `IQueryable<T>` bound to the current connection, supporting fully chained fluent calls `WhereIf/Where/OrderBy/Skip/Take/Page/Count/FirstOrDefault/ToList`.
- **Navigation properties (new in v12.1)**: `HasOne`/`HasMany` Fluent API configures navigation relationships; `Include` builds paths and batch-loads; `SelectBuilder` supports JOINs across navigation relationships.
- **WhereIf inline conditions**: `Query.WhereIf(condition, predicate)` and `FindAllWhereIf` enable dynamic filtering.
- WhereExpression: chainable & / | / Contains / Between / Like expression composition.
- Find / FindAll / Query / PageFindAll paging.
- Pager / PageParameter: carries paging, sorting, TotalCount statistics and data-scope extensions.
- **Window functions (new in v12.0)**: `SelectBuilder.Window` + `WindowFunction` supports ROW_NUMBER/RANK/DENSE_RANK and aggregate window functions.

### 3.5 Multi-level Cache System
Designed for read-heavy, write-light workloads:
- Meta.Count: fast row-count cache (uses system indexes when >1000; SQLite uses Max(identity) plus asynchronous exact compensation).
- EntityCache (Meta.Cache.Entities): full-table cache (recommended for small tables <1000 rows; forbidden above 10000 rows).
- SingleCache (Meta.SingleCache[key]): dictionary-level single-object cache by primary/alternate key; suitable for frequent point lookups such as users.
- Expiration policy: 60s by default. Expired reads return stale data immediately + refresh asynchronously. Writes invalidate/update caches immediately.
- Transaction support: changes accumulate inside a transaction; caches are cleared or rolled back consistently after commit/rollback.
- **Second-level cache (new in v12.0)**: `CacheInvalidator.Provider` injects Redis and coordinates entity cache invalidation across processes through a version mechanism.

### 3.6 Reverse Engineering & Migration
- Creates/modifies table schemas and indexes from entities in reverse, keeping them consistent automatically.
- Table-level `Migration` configuration (model `Table` attribute / `[BindTable]` / `TableItem` startup override) controls the reverse-engineering strategy per entity (Off / ReadOnly / On / Full).
- Supports model extraction from any database + cross-database data migration (with the DataMigration companion tool).

### 3.7 Sharding (ShardPolicy)
- ShardPolicy abstraction; TimeShardPolicy example (table name Table_yyyyMMdd by date).
- Transparent routing: business code still calls Find / Insert / Update and does not care about the actual table name.
- Customizable primary-key hash / time / range strategies.

### 3.8 Transactions and Cache Consistency
- Non-transactional: writes immediately expire related caches and rebuild them asynchronously, while attempting to update in-memory objects directly for read/write consistency.
- Inside a transaction: full-table caches are not cleared; only hit objects are changed locally; after commit they are cleared or refreshed uniformly, and on rollback changes are reverted.

### 3.9 Performance Optimizations at a Glance
- Multi-level caching: keeps hot queries off the database.
- Batch writes: merges multi-field and multi-record operations (internal optimization).
- Paging algorithms: chooses the optimal strategy per database/version.
- Lazy/asynchronous: row counts, model checks and cache refreshes can all run asynchronously.
- Reflection cache / delegate acceleration: pre-warms entity property accessors.
- In-memory association: caches the main table first, then performs "pseudo joins" in memory.

### 3.10 Data Scope and Multi-Tenancy
- **Data Scope**: row-level data access control supporting five scopes: "All / Department and sub-departments / Department / Self only / Custom". `DataScopeContext` + `DataScopeInterceptor` automatically apply query filters, default ownership on insert, and validation on update/delete. Entities opt in by implementing `IUserScope` / `IDepartmentScope` / `IDataScope`; department managers are automatically included in the visible scope, and one's own data is always accessible.
- **Multi-tenancy**: after an entity implements `ITenantScope`, `TenantInterceptor` automatically appends tenant filters on queries and validates/fills the tenant identifier on writes; EntityBuilder automatically implements `ITenantScope` for business classes that contain a `TenantId` column.
- See `Doc/数据权限.md` and `Doc/权限管理.md` for details.

---
## 4. Quick Start
### 4.1 Creating a Model (db.xml)
```xml
<?xml version="1.0" encoding="utf-8"?>
<Tables Version="1.0" Output="MyEntity" NameSpace="Model" BaseClass="Entity" ConnName="DB">
  <Table Name="UserInfo" Description="User information">
    <Columns>
      <Column Name="Id" DataType="Int32" Identity="True" PrimaryKey="True" Description="Member id" />
      <Column Name="UserName" DataType="String" Nullable="False" Description="Member name" />
      <Column Name="Password" DataType="String" Nullable="False" Description="Member password" />
      <Column Name="Age" DataType="Int32" Description="Member age" />
    </Columns>
  </Table>
</Tables>
```
### 4.2 Code Generation
- Download xcodetool.exe into the directory and double-click it, or run: `dotnet tool install xcodetool -g --prerelease` → `xcode db.xml`.
- Generates: 用户信息.cs (data class, overwritten) + 用户信息.Biz.cs (business class, not overwritten).

### 4.3 CRUD Example
```csharp
using XCode;

var user = new UserInfo { UserName="Zhang San", ******, Age=18 };
user.Insert();

var u2 = UserInfo.FindById(user.Id);
u2.Age = 19; 
u2.Update();

var list = UserInfo.FindAll(UserInfo._.Age > 10 & UserInfo._.UserName == "Zhang San");
UserInfo.FindById(user.Id)?.Delete();
```
> When using caches: fewer than 1000 entities → full-table cache; otherwise use regular queries / single-object cache.

---
## 5. Advanced Usage
### 5.1 Dynamic Conditions / Paging
```csharp
public static IList<UserInfo> Search(String name, Int32 age, Pager p)
{
    var exp = new WhereExpression();
    if (!name.IsNullOrEmpty()) exp &= UserInfo._.UserName == name;
    if (age > 0) exp &= UserInfo._.Age == age;
    return UserInfo.FindAll(exp, p);
}
```
### 5.2 Updating Specified Fields (bypasses cache; not recommended for regular use)
```csharp
UserInfo.Update(UserInfo._.UserName == "Li Si" & UserInfo._.Age == 18, UserInfo._.Id == 1);
```
### 5.3 Custom Sorting and Column Projection
```csharp
var list = UserInfo.FindAll(UserInfo._.Age > 10, " Age DESC,Id ASC",
    string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 20);
```

---
## 6. Indexes / Auto-generated Methods
Add to the model:
```xml
<Indexes>
  <Index Columns="UserName" Unique="True" />
  <Index Columns="UserName,Age" />
</Indexes>
```
After regeneration, `FindByUserName`, combined-condition helper methods / Search templates appear automatically.
Entity cache strategy: when `Meta.Session.Count < 1000`, use in-memory collection lookups; otherwise go to the database or single-object cache.

---
## 7. Sharding Example
```csharp
Meta.ShardPolicy = new TimeShardPolicy(nameof(UserInfo.Id), Meta.Factory)
{
    ConnPolicy = "{0}",           // customizable database name policy
    TablePolicy = "{0}_{1:yyyyMMdd}", // table name template
    Step = TimeSpan.FromDays(1)
};
```
All other code such as Insert / Query stays unchanged and is routed to the corresponding date table automatically.

---
## 8. Multi-database Configuration and Containerization
- SQLite is the default for development (zero configuration).
- At deployment, configure `XCode_{ConnName}` in the configuration file or environment variables.
- The connection string must include the provider identifier (for example: `provider=MySql;server=...;user=...;******;database=...;`).
- Multiple database connection names (ConnName) can be used together in the same process.

---
## 9. Benchmarks / Production Metrics (Excerpt)
| Capability | Metric (typical environment) | Notes |
| ---- | ---------------- | ---- |
| Single-machine Insert | up to ~890k TPS | batch / cache + SQLite / RAM disk combination tests |
| Single-machine Query | up to ~1.8 billion QPS | small tables + entity cache / single-object cache scenarios |
| Data volume per table | tens of billions in production | paging + sharding strategies |
> Actual performance depends on database type / hardware / sharding strategy / cache hit rate.

---
## 10. FAQ Highlights
- Why is the first Count fast? Meta.Count uses system indexes; SQLite may first take the identity maximum and then refine asynchronously.
- How soon is the cache consistent after an update? Writes in the current process are reflected immediately; across processes, wait for cache expiration or use an active invalidation strategy.
- Can automatic migration be disabled? Set Migration=Off (globally or per table), or use ReadOnly/On/Full as needed.
- Complex multi-table queries? Multiple single-table queries + in-memory association are recommended (faster with cache hits and clearer structure).

---
## 11. Ecosystem Matrix (Excerpt)
| Component | Description |
| ---- | ---- |
| NewLife.Core | Foundation: logging / configuration / caching / serialization / APM / networking |
| NewLife.Cube | Rapid development platform + admin backend (built-in code generation + permissions) |
| NewLife.Redis | High-performance Redis client and multi-purpose message queue |
| Stardust | Distributed service platform: registry / configuration / tracing / release center |
| AntJob | Distributed jobs & big-data offline/real-time processing |
| ... | See the project matrix below |

---
## 12. Contributing
Issues / PRs are welcome:
- Performance optimization / new database providers / documentation improvements.
- Please follow the coding standards in the repository's .github/copilot-instructions (strong naming, multi-TFM, XML comments, conditional compilation).
- Public API changes require a migration strategy (Obsolete → removal).

Development build:
```
dotnet restore
dotnet build -c Release
```
Add cases under Test / XUnitTest.XCode; performance-related suggestions should include benchmarks or explanations.

---
## 13. License
MIT, fully open. Commercial use / modification / redistribution are allowed (you may replace "NewLife" with your enterprise brand for private deployments).

---
## Appendix: Original Quick Examples (Retained)

### Create, Read, Update, Delete (Original Example)
```csharp
var user = new UserInfo();
user.UserName = "Zhang San";
user.Password = "123456";
user.Age = 18;
user.Insert();
Console.WriteLine("Inserted a new record, user id: " + user.Id);

var u = UserInfo.FindById(1);
u.UserName = "Zhang San";
u.Password = "123456";
u.Age = 19;
u.Update();

u = UserInfo.FindById(1);
Console.WriteLine($"User ID={u.Id} age updated to: {u.Age}");
```

### Complex Query (Original Example)
```csharp
var list = UserInfo.FindAll(UserInfo._.UserName == "Zhang San" & UserInfo._.Age == 19, UserInfo._.UserName.Desc(), string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 0);
```

### Index Query (Original Example)
```csharp
public static UserInfo FindByUserName(String userName)
{
    if (userName.IsNullOrEmpty()) return null;
    if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.UserName.EqualIgnoreCase(userName));
    return Find(_.UserName == userName);
}
```

---
# NewLife Project Matrix

All projects support net8.0/netstandard2.1/netstandard2.0/net4.61/net4.5 by default; older versions (2023.0308) support net4.0/net2.0.

|                               Project                               | Year  | Description                                                                                   |
| :--------------------------------------------------------------: | :---: | -------------------------------------------------------------------------------------- |
|                             Foundation Components                             |       | Supports other middleware and product projects                                                             |
|          [NewLife.Core](https://github.com/NewLifeX/X)           | 2002  | Core library: logging, configuration, caching, networking, serialization, APM performance tracing                                    |
|    [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode)    | 2005  | Big-data middleware, tens of billions of rows per table, MySql/SQLite/SqlServer/Oracle/TDengine/DaMeng, automatic sharding        |
|      [NewLife.Net](https://github.com/NewLifeX/NewLife.Net)      | 2005  | Networking library, 22.66 million tps single-machine throughput, 4 million TCP connections per machine                      |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011  | RPC communication framework for high-throughput intranet or IoT hardware scenarios                                            |
|     [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube)     | 2010  | Cube rapid development platform with user permissions, SSO login and OAuth server; validated on projects with 10 billion rows per table          |
|    [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent)    | 2008  | Service management component that installs applications as OS daemons: Windows services, Linux Systemd              |
|     [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero)     | 2020  | Zero-generation scaffold, project templates based on the NewLife ecosystem: Web, WebApi, Service                    |
|                              Middleware                              |       | Integrates with well-known middleware platforms                                                                     |
|    [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis)    | 2017  | Redis client, microsecond latency, million-level throughput, rich message queues, validated on 10-billion-scale data projects              |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018  | Pure managed RocketMQ client supporting Apache RocketMQ and Alibaba Cloud Message Queue, validated on billion-scale projects                |
|     [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT)     | 2019  | IoT messaging protocol, MqttClient/MqttServer, client supports Alibaba Cloud IoT                          |
|      [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT)      | 2022  | IoT standard library defining communication protocol standards for the IoT domain                                        |
|   [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus)   | 2022  | ModbusTcp/ModbusRTU/ModbusASCII based on the IoT standard library, supports IoT platforms and IoTEdge               |
|  [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens)  | 2022  | Siemens PLC protocol based on the IoT standard library, supports IoT platforms and IoTEdge                                 |
|      [NewLife.Map](https://github.com/NewLifeX/NewLife.Map)      | 2022  | Map component library wrapping Baidu Maps, Amap and Tencent Maps                                           |
|       [NewLife.IP](https://github.com/NewLifeX/NewLife.IP)       | 2022  | IP address library, IP address to physical location                                                             |
|                             Product Platforms                             |       | Product-platform level, ready to compile and deploy, with customization                                                 |
|           [AntJob](https://github.com/NewLifeX/AntJob)           | 2019  | Ant scheduling, distributed big-data computing platform (real-time/offline), ant-movement sharding idea, validated on trillion-scale data projects    |
|         [Stardust](https://github.com/NewLifeX/Stardust)         | 2018  | Stardust, distributed service platform: node management, APM monitoring center, configuration center, registry, release center              |
|      [NewLife.ERP](https://github.com/NewLifeX/NewLife.ERP)      | 2021  | Enterprise ERP: product management, customer management, sales management, supplier management                                      |
|         [CrazyCoder](https://github.com/NewLifeX/XCoder)         | 2006  | Code god tool, many developer utilities: network, serial port, encryption/decryption, regular expressions, Modbus                       |
|           [XProxy](https://github.com/NewLifeX/XProxy)           | 2005  | Product-grade reverse proxy: NAT proxy, Http proxy                                                      |
|        [HttpMeter](https://github.com/NewLifeX/HttpMeter)        | 2022  | HTTP stress testing tool                                                                       |
|         [GitCandy](https://github.com/NewLifeX/GitCandy)         | 2015  | Git source code management system                                                                      |
|          [SmartOS](https://github.com/NewLifeX/SmartOS)          | 2014  | Embedded operating system, fully independent, supports ARM Cortex-M chips                                 |
|          [SmartA2](https://github.com/NewLifeX/SmartA2)          | 2019  | Embedded industrial computer, IoT edge gateway, high-performance .NET6 host, used in industry, agriculture, transportation and healthcare        |
|                           菲凡物联FIoT                           | 2020  | Complete IoT solution for buildings, environmental protection and agriculture; integrated software, hardware and big-data analytics; validated on 100k-point single-machine projects |
|                           NewLife.UWB                            | 2020  | Centimeter-level (10~20cm) high-precision indoor positioning, integrated hardware and software, interoperates with other systems, validated in large exhibition halls        |

---
# NewLife Development Team
![XCode](https://newlifex.com/logo.png)

The NewLife team was founded in 2002 and is an IoT industry solution provider in the new era. We are dedicated to software/hardware application consulting, system architecture planning and development services. NuGet cumulative downloads: 4.13 million+. Core components (XCode / Redis / Stardust / AntJob / IoT, etc.) have been widely deployed at scale in power, industry, transportation, environmental protection, healthcare, cultural heritage and other industries.

We keep improving, striving to be a long-term trusted .NET / IoT / distributed technology partner.

Website: https://newlifex.com   Open-source organization: https://github.com/NewLifeX  
WeChat official account:  
![智能大石头](https://newlifex.com/stone.jpg)

---
> If this project helps you, please Star it, share it with your team, or send improvement suggestions via Issue/PR.
