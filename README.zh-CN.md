# NewLife.XCode - 数据中间件 / 超级ORM

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
🌐 [English](README.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

高性能 .NET 数据中间件，聚焦"添删改查 + 极致性能 + 海量数据"，内置多级缓存、自动建模/迁移、分表分库、强类型查询、跨库迁移，支持 MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / 达梦 / 金仓 / 瀚高 / DB2 / ClickHouse / DuckDB / MongoDB / Hana (SAP HANA) / IRIS (InterSystems) / VastBase / Network 网络虚拟数据库 / NovaDb 自研数据库等。单表生产实践达百亿级，查询吞吐可达十亿级 QPS（配合缓存策略）。

文档：https://newlifex.com/xcode   社区交流QQ群：1600800 / 1600838

> ℹ️ 本中文版与 [English README](README.md) 同批维护；若两者内容不一致，以英文版为对外口径。

**目录**：[快速开始](#快速开始) · [为什么选 XCode](#为什么选-xcode) · [主要功能](#主要功能) · [生态版图](#生态版图) · [文档](#文档) · [参与贡献](#参与贡献) · [新生命开发团队](#新生命开发团队) · [许可证](#许可证)

## 快速开始

### 安装

NuGet 引用（任一项目）：

```
dotnet add package NewLife.XCode
```

或使用模板脚手架：

```
dotnet new install NewLife.Templates
dotnet new xcode --name Demo.Data
cd Demo.Data
dotnet build
```

> 强命名证书已内置（newlife.snk），可自行重编译用于企业私有框架。

### 定义模型（db.xml）

```xml
<?xml version="1.0" encoding="utf-8"?>
<Tables Version="1.0" Output="MyEntity" NameSpace="Model" BaseClass="Entity" ConnName="DB">
  <Table Name="UserInfo" Description="用户信息">
    <Columns>
      <Column Name="Id" DataType="Int32" Identity="True" PrimaryKey="True" Description="会员id" />
      <Column Name="UserName" DataType="String" Nullable="False" Description="会员名称" />
      <Column Name="Password" DataType="String" Nullable="False" Description="会员密码" />
      <Column Name="Age" DataType="Int32" Description="会员年龄" />
    </Columns>
  </Table>
</Tables>
```

### 生成实体类

- 下载 xcodetool.exe 置于目录并双击，或：`dotnet tool install xcodetool -g --prerelease` → `xcode db.xml`。
- 生成：用户信息.cs（数据类，可覆盖） + 用户信息.Biz.cs（业务类，不再覆盖）。

### 增删改查

```csharp
using XCode;

var user = new UserInfo { UserName = "张三", Password = "123456", Age = 18 };
user.Insert();
Console.WriteLine("插入一条新数据，用户id为：" + user.Id);

var u2 = UserInfo.FindById(user.Id);
u2.Age = 19;
u2.Update();

var list = UserInfo.FindAll(UserInfo._.Age > 10 & UserInfo._.UserName == "张三");
UserInfo.FindById(user.Id)?.Delete();
```

> 使用缓存时：实体数 <1000 → 整表缓存；否则普通查询 / 单对象缓存。

## 为什么选 XCode

- **国产原创**：完全由国人开发，未引用任何第三方组件，自主可控，长期维护有保障。
- **化繁为简**：90% 业务只需单表实体操作，无需手写 SQL。
- **兼容 Dapper**：支持执行任意 SQL 查询并映射到任意实体模型，完全兼容 Dapper 用法，同时提供缓存、读写分离和链路追踪等增强能力。
- **极致性能**：核心主路径多级缓存 + 批量优化 + 内存联表，查询速度可远高于直接写 SQL。
- **海量数据**：原生分页与分表策略，正式项目单表百亿级，适配多种数据库分页算法。
- **稳定成熟**：20+ 年演进，广泛生产验证；保持对老版本 .NET Framework 的兼容（net45 起）。
- **自动迁移**：模型/实体即结构，新增字段上线自动同步，无需额外 SQL 脚本。
- **分布式演进**：原生表级 / 时间 / 主键分片策略，可平滑拆分扩容。
- **可插拔**：DAL 抽象 + Provider 分离，支持自定义数据库适配。

## 主要功能

<details>
<summary><b>模型与代码生成</b>（5 项）</summary>

| 功能点 | 主要特点 |
| --- | --- |
| XML 模型（Tables/Columns/Indexes） | 模型即唯一事实源；属性覆盖索引、唯一约束、默认值、描述与扩展属性。 |
| 代码生成器 | `xcodetool`（全局工具 / exe）或 VS T4 模板，把 XML 模型生成实体类。 |
| 数据类（`.cs`） | 每次生成覆盖，承载字段与基础查询。 |
| 业务类（`.Biz.cs`） | 首次生成后不再覆盖，放置自定义逻辑。 |
| 自动方法 | 索引与字段自动生成 `FindByXxx` / `Search` 辅助方法与模板。 |

</details>

<details>
<summary><b>实体运行时与 DAL</b>（7 项）</summary>

| 组件 | 职责 |
| --- | --- |
| `IEntity` / `Entity` | 充血模型，封装字段、验证、生命周期。 |
| `IEntityOperate` | 静态操作入口（Find / FindAll / Query 等）。 |
| `EntityFactory` | 实体元数据缓存工厂。 |
| `EntitySession`（`Meta.Session`） | 实体在特定连接/表上的上下文，带会话级缓存/统计。 |
| `Meta` | 实体静态上下文（表结构、字段、缓存、分片策略等）。 |
| `DAL` / `IDatabase` / `IDbSession` | 统一的数据库访问与元数据操作。 |
| `CheckTables()` | 启动/首次访问时按需异步或同步检查并同步表结构。 |

</details>

<details>
<summary><b>查询 DSL</b>（8 项）</summary>

| 能力 | 主要特点 |
| --- | --- |
| `IQueryable<T>` LINQ 查询（v12.0 新增） | `Entity<T>.Query` 返回 `IQueryable<T>`，支持 `Where/OrderBy/Skip/Take/Count/First/ToList` 等标准 LINQ 操作。 |
| `DAL.Select<T>()` 流式查询（v12.1 新增） | `DAL.Select<T>()` 返回 `IQueryable<T>` 并自动绑定当前连接，支持 `WhereIf/Where/OrderBy/Skip/Take/Page/Count/FirstOrDefault/ToList` 全链式流式调用。 |
| 导航属性（v12.1 新增） | `HasOne`/`HasMany` Fluent API 配置导航关系，`Include` 路径构建 + 批量加载，`SelectBuilder` 支持导航关系 JOIN。 |
| `WhereIf` 内联条件 | `Query.WhereIf(condition, predicate)` 和 `FindAllWhereIf` 实现动态条件筛选。 |
| `WhereExpression` | 链式 `&` / `\|` / `Contains` / `Between` / `Like` 等表达式组合。 |
| `Find` / `FindAll` / `Query` / `PageFindAll` | 查询入口，`PageFindAll` 内置分页。 |
| `Pager` / `PageParameter` | 携带分页、排序、统计 `TotalCount`、数据权限扩展。 |
| 窗口函数（v12.0 新增） | `SelectBuilder.Window` + `WindowFunction` 支持 ROW_NUMBER/RANK/DENSE_RANK 及聚合窗口函数。 |

</details>

<details>
<summary><b>多级缓存体系</b>（6 项）</summary>

针对"读多写少"模式设计：

| 缓存 | 主要特点 |
| --- | --- |
| `Meta.Count` | 快速行数缓存（>1000 时走系统索引；SQLite 有 Max(自增) + 异步精准补偿）。 |
| `EntityCache`（`Meta.Cache.Entities`） | 整表缓存（小表 <1000 行推荐，>10000 行禁止）。 |
| `SingleCache`（`Meta.SingleCache[key]`） | 主键/从键字典级单对象缓存，适合频繁点查用户等。 |
| 过期策略 | 默认 60s。过期访问立即返回旧数据 + 异步刷新。写操作即时使缓存失效 / 更新。 |
| 事务感知 | 事务中累积修改，提交/回滚后统一清空或回滚缓存。 |
| 二级缓存（v12.0 新增） | `CacheInvalidator.Provider` 注入 Redis，基于版本号机制跨进程协调实体缓存失效。 |

</details>

<details>
<summary><b>反向工程与迁移</b>（3 项）</summary>

| 能力 | 主要特点 |
| --- | --- |
| 表结构反向工程 | 根据实体反向创建/修改表结构、索引，自动维持一致。 |
| 实体级迁移策略 | 支持表级 `Migration` 配置（模型 `Table` 属性 / `[BindTable]` / `TableItem` 启动覆盖），可按实体粒度控制反向工程策略（Off / ReadOnly / On / Full）。 |
| 模型抽取与跨库迁移 | 支持任意数据库间模型抽取 + 跨库数据迁移（配套工具 DataMigration）。 |

</details>

<details>
<summary><b>分表分库（ShardPolicy）</b>（3 项）</summary>

| 能力 | 主要特点 |
| --- | --- |
| `ShardPolicy` 抽象 | `TimeShardPolicy` 示例：按日期生成表名 Table_yyyyMMdd。 |
| 透明路由 | 业务层仍调用 Find / Insert / Update，不关心实际表名。 |
| 自定义策略 | 可定制主键哈希 / 时间 / 范围策略。 |

</details>

<details>
<summary><b>事务与缓存一致性</b>（3 项）</summary>

| 场景 | 主要特点 |
| --- | --- |
| 非事务写入 | 写操作立即使相关缓存过期并异步重建，同时尝试直接更新内存对象保持读写一致。 |
| 事务提交 | 事务中暂不清空整表缓存，仅对命中对象做局部变更；提交后统一清空或刷新。 |
| 事务回滚 | 回滚时撤销事务内所做变更。 |

</details>

<details>
<summary><b>性能优化手段</b>（6 项）</summary>

| 手段 | 主要特点 |
| --- | --- |
| 多级缓存 | 避免热点查询落库。 |
| 批量写 | 合并多字段与多记录操作（内部优化）。 |
| 分页算法 | 针对不同 DB / 版本选择最优策略。 |
| 惰性/异步 | 行数、模型检查、缓存刷新均可异步。 |
| 反射缓存 / 委托加速 | 实体属性访问器预热。 |
| 内存关联 | 先缓存主表，再在内存中做"伪联表"。 |

</details>

<details>
<summary><b>数据权限与多租户</b>（5 项）</summary>

| 能力 | 主要特点 |
| --- | --- |
| 数据权限（Data Scope） | 行级数据访问控制，支持"全部 / 本部门及下级 / 本部门 / 仅本人 / 自定义"五种范围。 |
| 权限接线 | `DataScopeContext` + `DataScopeInterceptor` 自动完成查询过滤、新增默认归属、修改删除校验。 |
| 实体接入 | 实体实现 `IUserScope` / `IDepartmentScope` / `IDataScope` 即可接入；部门管理者自动纳入可见范围，本人数据始终可访问。 |
| 多租户 | 实体实现 `ITenantScope` 后，`TenantInterceptor` 自动在查询时附加租户过滤、写入时校验/补充租户标识；EntityBuilder 为含 `TenantId` 列的业务类自动实现 `ITenantScope`。 |
| 详细用法 | 见 `Doc/数据权限.md`、`Doc/权限管理.md`。 |

</details>

<details>
<summary><b>多数据库配置</b>（4 项）</summary>

| 能力 | 主要特点 |
| --- | --- |
| 默认 SQLite | 默认开发 SQLite（零配置）。 |
| 连接配置 | 发布时在配置文件或环境变量配置 `XCode_{ConnName}`。 |
| Provider 标识 | 连接字符串需包含 provider 标识（例：`provider=MySql;server=...;user=...;password=...;database=...;`）。 |
| 多连接共存 | 支持在同一进程中混用多个数据库连接名称（ConnName）。 |

</details>

### 用法示例

#### 动态条件 / 分页

```csharp
public static IList<UserInfo> Search(String name, Int32 age, Pager p)
{
    var exp = new WhereExpression();
    if (!name.IsNullOrEmpty()) exp &= UserInfo._.UserName == name;
    if (age > 0) exp &= UserInfo._.Age == age;
    return UserInfo.FindAll(exp, p);
}
```

#### 指定更新字段（绕过缓存，不推荐常规使用）

```csharp
UserInfo.Update(UserInfo._.UserName == "李四" & UserInfo._.Age == 18, UserInfo._.Id == 1);
```

#### 自定义排序与列投影

```csharp
var list = UserInfo.FindAll(UserInfo._.Age > 10, " Age DESC,Id ASC",
    string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 20);
```

#### 索引 / 自动方法

在模型添加索引：

```xml
<Indexes>
  <Index Columns="UserName" Unique="True" />
  <Index Columns="UserName,Age" />
</Indexes>
```

重新生成后自动出现：`FindByUserName`、组合条件辅助方法 / Search 模板。

实体缓存策略：`Meta.Session.Count < 1000` 使用内存集合查找，否则走数据库或单对象缓存。

```csharp
public static UserInfo FindByUserName(String userName)
{
    if (userName.IsNullOrEmpty()) return null;
    if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.UserName.EqualIgnoreCase(userName));
    return Find(_.UserName == userName);
}
```

#### 分表分库示例

```csharp
Meta.ShardPolicy = new TimeShardPolicy(nameof(UserInfo.Id), Meta.Factory)
{
    ConnPolicy = "{0}",           // 可自定义库名策略
    TablePolicy = "{0}_{1:yyyyMMdd}", // 表名模板
    Step = TimeSpan.FromDays(1)
};
```

其余 Insert / Query 等代码保持不变，自动路由到对应日期表。

#### 原始示例（保留）

<details>
<summary><b>原始增删改查与复杂查询示例</b></summary>

##### 增删改查

```csharp
var user = new UserInfo();
user.UserName = "张三";
user.Password = "123456";
user.Age = 18;
user.Insert();
Console.WriteLine("插入一条新数据，用户id为：" + user.Id);

var u = UserInfo.FindById(1);
u.UserName = "张三";
u.Password = "123456";
u.Age = 19;
u.Update();

u = UserInfo.FindById(1);
Console.WriteLine($"用户ID={u.Id}已修改岁数，岁数为：{u.Age}");
```

##### 复杂查询

```csharp
var list = UserInfo.FindAll(UserInfo._.UserName == "张三" & UserInfo._.Age == 19, UserInfo._.UserName.Desc(), string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 0);
```

</details>

### 实战指标

| 能力 | 指标（典型环境） | 说明 |
| ---- | ---------------- | ---- |
| 单机 Insert | 最高 ~89 万 TPS | 批量 / 缓存 + SQLite / 内存盘组合测试 |
| 单机 Query | 最高 ~18 亿 QPS | 小表 + 实体缓存 / 单对象缓存场景 |
| 单表数据量 | 生产百亿级 | 分页 + 分表策略 |

> 实际性能取决于数据库类型 / 硬件 / 分表策略 / 缓存命中率。

### 常见问题（FAQ）

- 为什么第一次 Count 很快？`Meta.Count` 走系统索引；SQLite 首次可能取自增最大值再异步精确。
- 数据更新后多久缓存一致？本进程写操作立即反映；跨进程需等缓存过期或主动失效策略。
- 可否关闭自动迁移？设置 `Migration=Off`（全局或表级），或按需使用 ReadOnly/On/Full。
- 复杂多表查询？推荐多次单表 + 内存关联（缓存命中更快 & 结构更清晰）。

## 生态版图

NewLife 生态按依赖分层，各项目独立版本、独立发版。下面的版图在所有 NewLife 仓库中逐字一致；每个项目链接到代码仓库与主 NuGet 包。

| 项目 | 一句话定位 | 下载量 |
| --- | --- | --- |
| **基础底座** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | 日志、配置、缓存、网络、序列化、定时调度与 APM 追踪。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 数据中间件：单表百亿级、自动分表、读写分离，支持 10 多种数据库。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 高性能网络库：单机 177 万 pps、14.5 Gbps（TCP 1KB，2026-10 实测），400 万 Tcp 长连接。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 协议通信与 RPC 框架，面向 CS 应用与物联网设备。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 把 .NET 应用安装为操作系统守护进程：Windows 服务与 Linux systemd。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | `dotnet new` 项目模板：控制台、Web、WebApi、服务、数据与物联网。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **数据存储与消息** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis 客户端：微秒级延迟、百万级吞吐，内置消息队列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 纯托管 RocketMQ 客户端，支持 Apache RocketMQ 与阿里云消息队列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT 客户端与服务端，客户端支持阿里云物联网。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 混合数据库：嵌入/网络双模，关系型、时序、消息队列与 KV 于一体。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | MySql 驱动与数据访问辅助层，基于 NewLife 数据栈实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web、AI 与分布式** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 魔方快速开发平台：用户权限、SSO、OAuth 服务端与 CRUD 脚手架。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 星尘分布式服务平台：节点管理、APM、配置中心、注册中心与发布中心。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | 蚂蚁调度：分布式任务与实时数据平台，支持分片、重试与回溯补算。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 统一接入 46 家大模型服务商，附带开箱即用的 ChatAI Web 对话应用。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **工业与物联网** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | IoT 标准库，定义物联网通信协议契约与设备抽象。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Modbus TCP/RTU/ASCII，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 西门子 PLC 协议，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | 欧姆龙 PLC 协议，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | 三菱 Melsec PLC 协议，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | OPC 客户端，用于工业数据采集。 | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet 楼宇自控协议栈。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | 开源轻量级 IoT 平台，含网关与设备管理。 | — |
| **车联网** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 车辆定位协议：70+ 消息体，覆盖 2011/2013/2019 三个版本。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | JT/T 809 部标平台互联协议，面向运输管理部门。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 车载视频：RTP 解析、FLV/fMP4 转封装与直播推送。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **领域组件** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | 一个包覆盖 Excel、Word、PPT、PDF 等 15+ 文档格式。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 跨平台音频：编解码、设备 IO、DSP、容器与流式处理。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | 视频编解码、封装格式、帧处理与流媒体。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 百度/高德/腾讯/天地图统一接口，支持地理编码与坐标转换。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 归属地：离线内存映射库 + 在线 API 双源。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **工具与产品** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 码神工具：网络、串口、加解密、正则、Modbus 与 MQTT。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | 自托管 Git 源代码管理系统。 | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 从低版本宿主启动时，自动安装匹配的 .NET 运行时。 | — |

> 徽章取各项目主包；主题包、扩展包与卫星包见对应 NuGet 页面。未在表中列出的 30 多个包（FTP、LoRa、Office 扩展、各类协议驱动等）见 [NewLifeX 组织](https://github.com/NewLifeX) 与 [newlifex.com](https://newlifex.com)。本表以 NewLife.Core README 为维护源。

## 文档

- [XCode 文档导航](Doc/XCode文档导航.md)：按模块分组的完整文档索引
- [XCode 使用手册](Doc/XCode使用手册.md)：端到端使用流程
- [架构设计](Doc/架构设计.md)：分层、模块组件与关键设计决策
- [多语言本地化](Doc/多语言本地化.md)：消息模型、语言表与运行时覆盖
- 在线文档：https://newlifex.com/xcode
- [ChangeLog.md](ChangeLog.md)：版本记录

## 参与贡献

### 构建与测试

```
dotnet restore
dotnet build -c Release
dotnet test XUnitTest.XCode -c Release
```

- 可在 `XUnitTest.XCode` 中补充案例；性能相关建议附基准或说明。
- 请遵循仓库约定（强命名、多 TFM、XML 注释、条件编译）。
- 公共 API 变更需给出迁移策略（Obsolete → 移除）。

### 发版

推送 `v` 前缀 tag（如 `v2026.1018`）触发 `.github/workflows/publish.yml`。工作流打包 XCode、XCodeTool 与各数据库提供者（`XCode.MySql`、`XCode.SQLite`、`XCode.SqlServer`、`XCode.Oracle`、`XCode.PostgreSQL`、`XCode.DaMeng`、`XCode.DB2`、`XCode.KingBase`、`XCode.HighGo`），并推送到 NuGet.org 与 GitHub Packages。

### 反馈

- 欢迎提交 Issue / PR：优化性能 / 新增数据库 Provider / 改进文档。
- 提交前请确保 `dotnet build` 与相关单元测试通过；代码风格与提交信息见仓库约定。
- 缺陷与建议请提 [Issue](https://github.com/NewLifeX/NewLife.XCode/issues)，欢迎 Star 与 PR。

## 新生命开发团队

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

新生命团队成立于 2002 年，二十余年专注 .NET 与物联网技术生态，主导开源项目近百个（生态版图收录 34 个主干项目），为电力、物流、医疗、工控、互联网等行业提供软硬件一体化解决方案；以“学无先后达者为师”为信条。

开源组件在 NuGet 累计下载 <!--nuget-total:start-->6.48M<!--nuget-total:end--> 次（全生态口径，截至 2026-10）。

网站：https://newlifex.com · 开源组织：https://github.com/NewLifeX

<details>
<summary>微信公众号：智能大石头（扫码关注）</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## 许可证

MIT，完全开放。允许商用 / 修改 / 再发行（可替换"NewLife"为企业品牌实现私有化）。强命名证书 `newlife.snk` 已内置。

> 如果本项目对你有帮助，欢迎 Star、分享给团队，或通过 Issue/PR 反馈改进建议。