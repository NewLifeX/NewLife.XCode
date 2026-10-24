<!-- generated from README.md by sync-readme-package.ps1; do not edit -->

# NewLife.XCode - Data Middleware / Super ORM

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.xcode?logo=github)

![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.XCode?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.XCode?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.XCode?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.XCode?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.XCode/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.XCode/actions/workflows/test.yml)
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](https://github.com/NewLifeX/NewLife.XCode/blob/master/Doc/多语言本地化.md)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.XCode?logo=github)](https://github.com/NewLifeX/NewLife.XCode/graphs/contributors)

<!-- lang:start -->
🌐 [简体中文](https://github.com/NewLifeX/NewLife.XCode/blob/master/README.zh-CN.md) | [繁體中文](https://github.com/NewLifeX/NewLife.XCode/blob/master/README.zh-TW.md) | [Español](https://github.com/NewLifeX/NewLife.XCode/blob/master/README.es.md) | [Deutsch](https://github.com/NewLifeX/NewLife.XCode/blob/master/README.de.md) | [日本語](https://github.com/NewLifeX/NewLife.XCode/blob/master/README.ja.md) | [Français](https://github.com/NewLifeX/NewLife.XCode/blob/master/README.fr.md) | [Português](https://github.com/NewLifeX/NewLife.XCode/blob/master/README.pt.md) | [Русский](https://github.com/NewLifeX/NewLife.XCode/blob/master/README.ru.md) | [한국어](https://github.com/NewLifeX/NewLife.XCode/blob/master/README.ko.md) | [العربية](https://github.com/NewLifeX/NewLife.XCode/blob/master/README.ar.md)
<!-- lang:end -->

**NewLife.XCode** is a high-performance .NET data middleware focused on "CRUD + extreme performance + massive data". It includes multi-level caching, automatic modeling/migration, sharding, strongly-typed queries and cross-database migration. It supports MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB / Hana (SAP HANA) / IRIS (InterSystems) / VastBase / Network virtual database / NovaDb / SonnetDB and more. Production deployments reach tens of billions of rows per table, and query throughput can reach billions of QPS (with caching strategies).

Docs: https://newlifex.com/xcode   Community QQ groups: 1600800 / 1600838

**Contents**: [Quick Start](#quick-start) · [Why XCode](#why-xcode) · [Main Features](#main-features) · [Ecosystem Map](#ecosystem-map) · [Documentation](#documentation) · [Contributing](#contributing) · [Team](#team) · [License](#license)

## Quick Start

### Install

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

### Define the model (db.xml)

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

### Generate entity classes

- Download xcodetool.exe into the directory and double-click it, or run: `dotnet tool install xcodetool -g --prerelease` → `xcode db.xml`.
- Generates: 用户信息.cs (data class, overwritten) + 用户信息.Biz.cs (business class, not overwritten).

### CRUD

```csharp
using XCode;

var user = new UserInfo { UserName = "Zhang San", Password = "123456", Age = 18 };
user.Insert();
Console.WriteLine("Inserted a new record, user id: " + user.Id);

var u2 = UserInfo.FindById(user.Id);
u2.Age = 19;
u2.Update();

var list = UserInfo.FindAll(UserInfo._.Age > 10 & UserInfo._.UserName == "Zhang San");
UserInfo.FindById(user.Id)?.Delete();
```

> When using caches: fewer than 1000 entities → full-table cache; otherwise use regular queries / single-object cache.

## Why XCode

- **Homegrown and original**: fully developed in China with no third-party components, self-controlled and backed by long-term maintenance.
- **Simplicity**: 90% of business scenarios need only single-table entity operations, with no hand-written SQL.
- **Dapper compatible**: executes arbitrary SQL queries and maps them to any entity model; fully compatible with Dapper usage while adding caching, read/write splitting and tracing.
- **Extreme performance**: multi-level caching on the core path + batch optimization + in-memory joins; query speed can be far higher than raw SQL.
- **Massive data**: native paging and sharding strategies; production projects reach tens of billions of rows per table; paging algorithms adapt to multiple databases.
- **Stable and mature**: 20+ years of evolution and broad production validation; keeps compatibility with older .NET Framework versions (from net45).
- **Automatic migration**: model/entity is the schema; new fields are synchronized automatically on deployment, with no extra SQL scripts.
- **Distributed evolution**: native table-level / time / primary-key sharding strategies for smooth splitting and scaling.
- **Pluggable**: DAL abstraction + separated providers; custom database adapters are supported.

## Main Features

<details>
<summary><b>Modeling &amp; Code Generation</b> (5)</summary>

| Feature | Key Characteristics |
| --- | --- |
| XML model (Tables/Columns/Indexes) | The model is the single source of truth; attributes cover indexes, unique constraints, default values, descriptions and extended properties. |
| Code generator | `xcodetool` (global tool / exe) or the VS T4 template turns the XML model into entity classes. |
| Data class (`.cs`) | Overwritten on every generation; holds fields and basic queries. |
| Business class (`.Biz.cs`) | Not overwritten after the first generation; place custom logic here. |
| Auto-generated methods | Indexes and columns produce `FindByXxx` / `Search` helper methods and templates automatically. |

</details>

<details>
<summary><b>Entity Runtime &amp; DAL</b> (7)</summary>

| Component | Responsibility |
| --- | --- |
| `IEntity` / `Entity` | Rich domain model encapsulating fields, validation and lifecycle. |
| `IEntityOperate` | Static operation entry point (Find / FindAll / Query, etc.). |
| `EntityFactory` | Entity metadata cache factory. |
| `EntitySession` (`Meta.Session`) | Context of an entity on a specific connection/table, with session-level cache/statistics. |
| `Meta` | Entity static context (table schema, fields, caches, sharding policy, etc.). |
| `DAL` / `IDatabase` / `IDbSession` | Unified database access and metadata operations. |
| `CheckTables()` | Checks and synchronizes the schema on startup/first access, asynchronously or synchronously as needed. |

</details>

<details>
<summary><b>Query DSL</b> (8)</summary>

| Capability | Key Characteristics |
| --- | --- |
| `IQueryable<T>` LINQ (v12.0) | `Entity<T>.Query` returns `IQueryable<T>`, supporting standard LINQ operations such as `Where/OrderBy/Skip/Take/Count/First/ToList`. |
| `DAL.Select<T>()` fluent queries (v12.1) | `DAL.Select<T>()` returns `IQueryable<T>` bound to the current connection, supporting fully chained fluent calls `WhereIf/Where/OrderBy/Skip/Take/Page/Count/FirstOrDefault/ToList`. |
| Navigation properties (v12.1) | `HasOne`/`HasMany` Fluent API configures navigation relationships; `Include` builds paths and batch-loads; `SelectBuilder` supports JOINs across navigation relationships. |
| `WhereIf` inline conditions | `Query.WhereIf(condition, predicate)` and `FindAllWhereIf` enable dynamic filtering. |
| `WhereExpression` | Chainable `&` / `\|` / `Contains` / `Between` / `Like` expression composition. |
| `Find` / `FindAll` / `Query` / `PageFindAll` | Query entry points, with paging built into `PageFindAll`. |
| `Pager` / `PageParameter` | Carries paging, sorting, `TotalCount` statistics and data-scope extensions. |
| Window functions (v12.0) | `SelectBuilder.Window` + `WindowFunction` supports ROW_NUMBER/RANK/DENSE_RANK and aggregate window functions. |

</details>

<details>
<summary><b>Multi-level Cache System</b> (6)</summary>

Designed for read-heavy, write-light workloads:

| Cache | Key Characteristics |
| --- | --- |
| `Meta.Count` | Fast row-count cache (uses system indexes when >1000; SQLite uses Max(identity) plus asynchronous exact compensation). |
| `EntityCache` (`Meta.Cache.Entities`) | Full-table cache (recommended for small tables <1000 rows; forbidden above 10000 rows). |
| `SingleCache` (`Meta.SingleCache[key]`) | Dictionary-level single-object cache by primary/alternate key; suitable for frequent point lookups such as users. |
| Expiration policy | 60s by default. Expired reads return stale data immediately + refresh asynchronously. Writes invalidate/update caches immediately. |
| Transaction awareness | Inside a transaction, modifications accumulate; caches are cleared or rolled back uniformly after commit/rollback. |
| Second-level cache (v12.0) | `CacheInvalidator.Provider` injects Redis and coordinates entity cache invalidation across processes through a version mechanism. |

</details>

<details>
<summary><b>Reverse Engineering &amp; Migration</b> (3)</summary>

| Capability | Key Characteristics |
| --- | --- |
| Schema reverse engineering | Creates/modifies table schemas and indexes from entities in reverse, keeping them consistent automatically. |
| Per-entity migration policy | Table-level `Migration` configuration (model `Table` attribute / `[BindTable]` / `TableItem` startup override) controls the reverse-engineering strategy per entity (Off / ReadOnly / On / Full). |
| Model extraction & cross-database migration | Supports model extraction from any database + cross-database data migration (with the DataMigration companion tool). |

</details>

<details>
<summary><b>Sharding (ShardPolicy)</b> (3)</summary>

| Capability | Key Characteristics |
| --- | --- |
| `ShardPolicy` abstraction | `TimeShardPolicy` example: table name Table_yyyyMMdd by date. |
| Transparent routing | Business code still calls Find / Insert / Update and does not care about the actual table name. |
| Custom strategies | Customizable primary-key hash / time / range strategies. |

</details>

<details>
<summary><b>Transactions &amp; Cache Consistency</b> (3)</summary>

| Scenario | Key Characteristics |
| --- | --- |
| Non-transactional writes | Writes immediately expire related caches and rebuild them asynchronously, while attempting to update in-memory objects directly for read/write consistency. |
| Commit | Inside a transaction, full-table caches are not cleared; only hit objects are changed locally; after commit they are cleared or refreshed uniformly. |
| Rollback | On rollback, changes made inside the transaction are reverted. |

</details>

<details>
<summary><b>Performance Optimizations</b> (6)</summary>

| Technique | Key Characteristics |
| --- | --- |
| Multi-level caching | Keeps hot queries off the database. |
| Batch writes | Merges multi-field and multi-record operations (internal optimization). |
| Paging algorithms | Chooses the optimal strategy per database/version. |
| Lazy/asynchronous | Row counts, model checks and cache refreshes can all run asynchronously. |
| Reflection cache / delegate acceleration | Pre-warms entity property accessors. |
| In-memory association | Caches the main table first, then performs "pseudo joins" in memory. |

</details>

<details>
<summary><b>Data Scope &amp; Multi-Tenancy</b> (5)</summary>

| Capability | Key Characteristics |
| --- | --- |
| Data scope | Row-level data access control supporting five scopes: "All / Department and sub-departments / Department / Self only / Custom". |
| Scope wiring | `DataScopeContext` + `DataScopeInterceptor` automatically apply query filters, default ownership on insert, and validation on update/delete. |
| Entity opt-in | Entities implement `IUserScope` / `IDepartmentScope` / `IDataScope`; department managers are automatically included in the visible scope, and one's own data is always accessible. |
| Multi-tenancy | After an entity implements `ITenantScope`, `TenantInterceptor` automatically appends tenant filters on queries and validates/fills the tenant identifier on writes; EntityBuilder automatically implements `ITenantScope` for business classes that contain a `TenantId` column. |
| Details | See `Doc/数据权限.md` and `Doc/权限管理.md` for details. |

</details>

<details>
<summary><b>Multi-database Configuration</b> (4)</summary>

| Capability | Key Characteristics |
| --- | --- |
| SQLite by default | SQLite is the default for development (zero configuration). |
| Connection settings | At deployment, configure `XCode_{ConnName}` in the configuration file or environment variables. |
| Provider identifier | The connection string must include the provider identifier (for example: `provider=MySql;server=...;user=...;******;database=...;`). |
| Multiple connections | Multiple database connection names (ConnName) can be used together in the same process. |

</details>

### Usage examples

#### Dynamic conditions and paging

```csharp
public static IList<UserInfo> Search(String name, Int32 age, Pager p)
{
    var exp = new WhereExpression();
    if (!name.IsNullOrEmpty()) exp &= UserInfo._.UserName == name;
    if (age > 0) exp &= UserInfo._.Age == age;
    return UserInfo.FindAll(exp, p);
}
```

#### Updating specified fields (bypasses cache; not recommended for regular use)

```csharp
UserInfo.Update(UserInfo._.UserName == "Li Si" & UserInfo._.Age == 18, UserInfo._.Id == 1);
```

#### Custom sorting and column projection

```csharp
var list = UserInfo.FindAll(UserInfo._.Age > 10, " Age DESC,Id ASC",
    string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 20);
```

#### Indexes and generated methods

Add indexes to the model:

```xml
<Indexes>
  <Index Columns="UserName" Unique="True" />
  <Index Columns="UserName,Age" />
</Indexes>
```

After regeneration, `FindByUserName`, combined-condition helper methods / Search templates appear automatically.

Entity cache strategy: when `Meta.Session.Count < 1000`, use in-memory collection lookups; otherwise go to the database or single-object cache.

```csharp
public static UserInfo FindByUserName(String userName)
{
    if (userName.IsNullOrEmpty()) return null;
    if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.UserName.EqualIgnoreCase(userName));
    return Find(_.UserName == userName);
}
```

#### Sharding example

```csharp
Meta.ShardPolicy = new TimeShardPolicy(nameof(UserInfo.Id), Meta.Factory)
{
    ConnPolicy = "{0}",           // customizable database name policy
    TablePolicy = "{0}_{1:yyyyMMdd}", // table name template
    Step = TimeSpan.FromDays(1)
};
```

All other code such as Insert / Query stays unchanged and is routed to the corresponding date table automatically.

#### Original examples (retained)

<details>
<summary><b>Original CRUD and complex query examples</b></summary>

##### Create, read, update, delete

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

##### Complex query

```csharp
var list = UserInfo.FindAll(UserInfo._.UserName == "Zhang San" & UserInfo._.Age == 19, UserInfo._.UserName.Desc(), string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 0);
```

</details>

### Benchmarks

| Capability | Metric (typical environment) | Notes |
| ---- | ---------------- | ---- |
| Single-machine Insert | up to ~890k TPS | batch / cache + SQLite / RAM disk combination tests |
| Single-machine Query | up to ~1.8 billion QPS | small tables + entity cache / single-object cache scenarios |
| Data volume per table | tens of billions in production | paging + sharding strategies |

> Actual performance depends on database type / hardware / sharding strategy / cache hit rate.

### FAQ

- Why is the first Count fast? `Meta.Count` uses system indexes; SQLite may first take the identity maximum and then refine asynchronously.
- How soon is the cache consistent after an update? Writes in the current process are reflected immediately; across processes, wait for cache expiration or use an active invalidation strategy.
- Can automatic migration be disabled? Set `Migration=Off` (globally or per table), or use ReadOnly/On/Full as needed.
- Complex multi-table queries? Multiple single-table queries + in-memory association are recommended (faster with cache hits and clearer structure).

## Ecosystem Map

NewLife is a layered ecosystem of independently versioned open-source projects. The map below is shared verbatim by every NewLife repository; each project links to its repository and its primary NuGet package.

| Project | What it does | Downloads |
| --- | --- | --- |
| **Foundation** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Logging, configuration, caching, networking, serialization, scheduling and APM tracing. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Data middleware: single tables at the tens-of-billions scale, automatic sharding, read-write splitting, 10+ databases. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | High-performance network library: 1.77M pps / 14.5 Gbps on a single machine (TCP 1KB, 2026-10), 4M long-lived TCP connections. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Protocol communication and RPC framework for client/server applications and IoT devices. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Installs .NET applications as OS daemons: Windows services and Linux systemd. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | `dotnet new` project templates for console, web, API, service, data and IoT projects. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Data & Messaging** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis client with microsecond latency, millions of ops/s and built-in message queues. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Pure managed RocketMQ client for Apache RocketMQ and Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT client and server, including Alibaba Cloud IoT connectivity. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Hybrid database in C#: embedded or networked, relational, time-series, message queue and KV in one. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | MySql driver and helper layer built on the NewLife data stack. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, AI & Distributed** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Rapid admin-platform framework: permissions, SSO, OAuth server and CRUD scaffolding. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Distributed service platform: node management, APM, configuration center, registry and release center. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Distributed task and real-time data scheduling platform with sharding, retries and backfill. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Unified client for 46 LLM providers plus ChatAI, a ready-to-run web chat application. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Industrial & IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | IoT standard library defining protocol contracts and device abstractions. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Modbus TCP/RTU/ASCII implementation on top of NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Siemens PLC protocol on top of NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Omron PLC protocol on top of NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Mitsubishi Melsec PLC protocol on top of NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | OPC client for industrial data access. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet building-automation protocol stack. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Lightweight open-source IoT platform with gateway and device management. | — |
| **Vehicle Telematics** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 vehicle positioning protocol: 70+ message bodies, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | JT/T 809 platform-to-platform protocol for transport authorities. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 in-vehicle video: RTP parsing, FLV/fMP4 remuxing and live streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Domain Components** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | One package for Excel, Word, PPT, PDF and 15+ document formats. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Cross-platform audio: codecs, device I/O, DSP, containers and streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Video codecs, container formats, frame processing and streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Unified interface over Baidu, AMap, Tencent and Tianditu with geocoding and coordinate conversion. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 geolocation with an offline memory-mapped database and an online API fallback. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Tools & Products** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Developer toolbox: network, serial port, crypto, regex, Modbus and MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Self-hosted Git source code management system. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Installs the matching .NET runtime automatically when launched from an older host. | — |

> Badges show the primary package of each project; theme, extension and satellite packages are listed on their NuGet pages. The 30+ packages not listed here (FTP, LoRa, Office extensions, protocol drivers and more) are available under the [NewLifeX organization](https://github.com/NewLifeX) and on [newlifex.com](https://newlifex.com). This table is maintained in the NewLife.Core README.

## Documentation

- [XCode documentation navigation](https://github.com/NewLifeX/NewLife.XCode/blob/master/Doc/XCode文档导航.md): complete document index grouped by module
- [XCode user manual](https://github.com/NewLifeX/NewLife.XCode/blob/master/Doc/XCode使用手册.md): end-to-end usage walkthrough
- [Architecture design](https://github.com/NewLifeX/NewLife.XCode/blob/master/Doc/架构设计.md): layering, components and key design decisions
- [Multi-language localization](https://github.com/NewLifeX/NewLife.XCode/blob/master/Doc/多语言本地化.md): message model, language tables and runtime overrides
- Online documentation: https://newlifex.com/xcode
- [ChangeLog.md](https://github.com/NewLifeX/NewLife.XCode/blob/master/ChangeLog.md): version history

## Contributing

### Build and test

```
dotnet restore
dotnet build -c Release
dotnet test XUnitTest.XCode -c Release
```

- Add cases under `XUnitTest.XCode`; performance-related suggestions should include benchmarks or explanations.
- Follow the repository conventions (strong naming, multi-TFM, XML comments, conditional compilation).
- Public API changes require a migration strategy (Obsolete → removal).

### Release

Push a `v`-prefixed tag (for example `v2026.1018`) to trigger `.github/workflows/publish.yml`. The workflow packs XCode, XCodeTool and the provider packages (`XCode.MySql`, `XCode.SQLite`, `XCode.SqlServer`, `XCode.Oracle`, `XCode.PostgreSQL`, `XCode.DaMeng`, `XCode.DB2`, `XCode.KingBase`, `XCode.HighGo`) and pushes them to NuGet.org and GitHub Packages.

### Feedback

- Issues / PRs are welcome: performance optimization / new database providers / documentation improvements.
- Before submitting, make sure `dotnet build` and the relevant unit tests pass; follow the repository conventions for code style and commit messages.
- Report bugs and suggestions via [Issue](https://github.com/NewLifeX/NewLife.XCode/issues); Stars and PRs are welcome.

## Team

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

Founded in 2002, the NewLife team has spent more than two decades on the .NET and IoT ecosystem, maintains nearly 100 open-source projects (34 of them core to this map), and delivers integrated software and hardware solutions for power, logistics, healthcare, industrial control and internet industries. We live by the motto "learning has no seniority — mastery earns respect".

Open-source components have been downloaded <!--nuget-total:start-->6.48M<!--nuget-total:end--> times from NuGet (ecosystem-wide, as of 2026-10).

Website: https://newlifex.com · Open source organization: https://github.com/NewLifeX

<details>
<summary>WeChat Official Account: 智能大石头 (scan to follow)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## License

MIT, fully open. Commercial use / modification / redistribution are allowed (you may replace "NewLife" with your enterprise brand for private deployments). The strong-name certificate `newlife.snk` is built in.

> If this project helps you, please Star it, share it with your team, or send improvement suggestions via Issue/PR.
