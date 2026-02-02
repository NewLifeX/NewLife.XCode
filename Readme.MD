# NewLife.XCode - 数据中间件 / 超级ORM

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.xcode?logo=github)
![GitHub License](https://img.shields.io/github/license/newlifex/newlife.xcode?logo=github)
![Nuget Downloads](https://img.shields.io/nuget/dt/newlife.xcode?logo=nuget)
![Nuget](https://img.shields.io/nuget/v/newlife.xcode?logo=nuget)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/newlife.xcode?label=dev%20nuget&logo=nuget)

高性能 .NET 数据中间件，聚焦“添删改查 + 极致性能 + 海量数据”，内置多级缓存、自动建模/迁移、分表分库、强类型查询、跨库迁移，支持 MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / 达梦 / 金仓 / 瀚高 / DB2 等。单表生产实践达百亿级，查询吞吐可达十亿级 QPS（配合缓存策略）。

文档：https://newlifex.com/xcode   社区交流QQ群：1600800 / 1600838

---
## 目录
- 1. 为什么选 XCode
- 2. 安装与快速体验
- 3. 架构与核心概念
  - 3.1 模型与代码生成
  - 3.2 实体运行时 (Meta / Session / Operate)
  - 3.3 DAL 抽象层
  - 3.4 查询 DSL 与 WhereExpression
  - 3.5 多级缓存体系 (Meta.Count / EntityCache / SingleCache)
  - 3.6 反向工程 & 迁移机制
  - 3.7 分表分库 (ShardPolicy)
  - 3.8 事务与缓存一致性
  - 3.9 性能优化手段一览
- 4. 快速入门（模型→代码→CRUD）
- 5. 高级用法示例
- 6. 索引 / 自动方法生成
- 7. 分表分库示例
- 8. 多数据库配置与容器化
- 9. Benchmarks / 实战指标
- 10. 常见问题（FAQ 精要）
- 11. 生态矩阵
- 12. 参与与贡献
- 13. 许可证

---
## 1. 为什么选 XCode
- 国产原创：完全由国人开发，未引用任何第三方组件，自主可控，长期维护有保障。
- 化繁为简：90% 业务只需单表实体操作，无需手写 SQL。
- 兼容 Dapper：支持执行任意 SQL 查询并映射到任意实体模型，完全兼容 Dapper 用法，同时提供缓存、读写分离和链路追踪等增强能力。
- 极致性能：核心主路径多级缓存 + 批量优化 + 内存联表，查询速度可远高于直接写 SQL。
- 海量数据：原生分页与分表策略，正式项目单表百亿级，适配多种数据库分页算法。
- 稳定成熟：20+ 年演进，广泛生产验证；保持对老版本 .NET Framework 的兼容（net45 起）。
- 自动迁移：模型/实体即结构，新增字段上线自动同步，无需额外 SQL 脚本。
- 分布式演进：原生表级 / 时间 / 主键分片策略，可平滑拆分扩容。
- 可插拔：DAL 抽象 + Provider 分离，支持自定义数据库适配。

---
## 2. 安装与快速体验
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

---
## 3. 架构与核心概念
XCode 采用“模型驱动 + 运行时元数据 + 多级缓存 + 可选分片”的分层架构。

### 3.1 模型与代码生成
XML 模型 (Tables/Columns/Indexes) → 代码生成器（xcodetool 或 VS T4）→ 实体数据类(.cs) + 业务分部类(.Biz.cs)。
- 数据类：每次生成覆盖，承载字段与基础查询。
- 业务类：首次生成后不再覆盖，放置自定义逻辑。
- 支持：索引、唯一约束、默认值、描述、扩展属性、自动方法（FindByXxx / Search）。

### 3.2 实体运行时
核心接口/类型：
- IEntity / Entity：充血模型，封装字段、验证、生命周期。
- IEntityOperate：静态操作入口（Find / FindAll / Query 等）。
- EntityFactory：实体元数据缓存工厂。
- EntitySession (Meta.Session)：实体在特定连接/表上的上下文，会话级缓存/统计。
- Meta：实体静态上下文（表结构、字段、缓存、分片策略等）。

### 3.3 DAL 抽象层
- DAL / IDatabase / IDbSession：提供统一的数据库访问与元数据操作。
- Migration 支持 (Off / ReadOnly / On / Full) 控制自动建库建表建字段。
- CheckTables()：启动/首次访问按需异步或同步检查并同步结构。

### 3.4 查询 DSL
- WhereExpression：链式 & / | / Contains / Between / Like 等表达式组合。
- Find / FindAll / Query / PageFindAll 分页。
- Pager / PageParameter：携带分页、排序、统计 TotalCount、数据权限扩展。

### 3.5 多级缓存体系
针对“读多写少”模式设计：
- Meta.Count：快速行数缓存（>1000 时走系统索引；SQLite 有 Max(自增) + 异步精准补偿）。
- EntityCache (Meta.Cache.Entities)：整表缓存（小表 <1000 行推荐，>10000 行禁止）。
- SingleCache (Meta.SingleCache[key])：主键/从键字典级单对象缓存，适合频繁点查用户等。
- 过期策略：默认 60s。过期访问立即返回旧数据 + 异步刷新。写操作即时使缓存失效 / 更新。
- 事务支持：事务中累积修改，提交/回滚后统一处理缓存清空或回滚。

### 3.6 反向工程 & 迁移
- 根据实体反向创建/修改表结构、索引，自动维持一致。
- 支持任意数据库间模型抽取 + 跨库数据迁移（配套工具 DataMigration）。

### 3.7 分表分库 (ShardPolicy)
- ShardPolicy 抽象，TimeShardPolicy 示例（按日期生成表名 Table_yyyyMMdd）。
- 透明路由：业务层仍调用 Find / Insert / Update，不关心实际表名。
- 可定制主键哈希 / 时间 / 范围等策略。

### 3.8 事务与缓存一致性
- 非事务：写操作立即使相关缓存过期并异步重建，同时尝试直接更新内存对象保持读写一致。
- 事务中：暂不清空整表缓存，仅对命中对象做局部变更；提交后统一清空或刷新，回滚撤销变更。

### 3.9 性能优化手段一览
- 多级缓存：避免热点查询落库。
- 批量写：合并多字段与多记录操作（内部优化）。
- 分页算法：针对不同 DB / 版本选择最优策略。
- 惰性/异步：行数、模型检查、缓存刷新均可异步。
- 反射缓存 / 委托加速：实体属性访问器预热。
- 内存关联：先缓存主表，再在内存中做“伪联表”。

---
## 4. 快速入门
### 4.1 创建模型 (db.xml)
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
### 4.2 代码生成
- 下载 xcodetool.exe 置于目录并双击，或：`dotnet tool install xcodetool -g --prerelease` → `xcode db.xml`。
- 生成：用户信息.cs（数据类，可覆盖） + 用户信息.Biz.cs（业务类，不再覆盖）。

### 4.3 CRUD 示例
```csharp
using XCode;

var user = new UserInfo { UserName="张三", Password="123456", Age=18 };
user.Insert();

var u2 = UserInfo.FindById(user.Id);
u2.Age = 19; 
u2.Update();

var list = UserInfo.FindAll(UserInfo._.Age > 10 & UserInfo._.UserName == "张三");
UserInfo.FindById(user.Id)?.Delete();
```
> 使用缓存时：实体数 <1000 → 整表缓存；否则普通查询 / 单对象缓存。

---
## 5. 高级用法
### 5.1 动态条件 / 分页
```csharp
public static IList<UserInfo> Search(String name, Int32 age, Pager p)
{
    var exp = new WhereExpression();
    if (!name.IsNullOrEmpty()) exp &= UserInfo._.UserName == name;
    if (age > 0) exp &= UserInfo._.Age == age;
    return UserInfo.FindAll(exp, p);
}
```
### 5.2 指定更新字段（绕过缓存，不推荐常规使用）
```csharp
UserInfo.Update(UserInfo._.UserName == "李四" & UserInfo._.Age == 18, UserInfo._.Id == 1);
```
### 5.3 自定义排序与列投影
```csharp
var list = UserInfo.FindAll(UserInfo._.Age > 10, " Age DESC,Id ASC",
    string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 20);
```

---
## 6. 索引 / 自动方法
在模型添加：
```xml
<Indexes>
  <Index Columns="UserName" Unique="True" />
  <Index Columns="UserName,Age" />
</Indexes>
```
重新生成后自动出现：`FindByUserName`、组合条件辅助方法 / Search 模板。
实体缓存策略：`Meta.Session.Count < 1000` 使用内存集合查找。否则走数据库或单对象缓存。

---
## 7. 分表分库示例
```csharp
Meta.ShardPolicy = new TimeShardPolicy(nameof(UserInfo.Id), Meta.Factory)
{
    ConnPolicy = "{0}",           // 可自定义库名策略
    TablePolicy = "{0}_{1:yyyyMMdd}", // 表名模板
    Step = TimeSpan.FromDays(1)
};
```
其余 Insert / Query 等代码保持不变，自动路由到对应日期表。

---
## 8. 多数据库配置与容器化
- 默认开发 SQLite（零配置）。
- 发布时在配置文件或环境变量：`XCode_{ConnName}`。
- 连接字符串需包含 provider 标识（例：`provider=MySql;server=...;user=...;password=...;database=...;`）。
- 支持在同一进程中混用多个数据库连接名称（ConnName）。

---
## 9. Benchmarks / 实战指标（节选）
| 能力 | 指标（典型环境） | 说明 |
| ---- | ---------------- | ---- |
| 单机 Insert | 最高 ~89 万 TPS | 批量 / 缓存 + SQLite / 内存盘组合测试 |
| 单机 Query | 最高 ~18 亿 QPS | 小表 + 实体缓存 / 单对象缓存场景 |
| 单表数据量 | 生产百亿级 | 分页 + 分表策略 |
> 实际性能取决于数据库类型 / 硬件 / 分表策略 / 缓存命中率。

---
## 10. FAQ 精要
- 为什么第一次 Count 很快？Meta.Count 走系统索引；SQLite 首次可能取自增最大值再异步精确。
- 数据更新后多久缓存一致？本进程写操作立即反映；跨进程需等缓存过期或主动失效策略。
- 可否关闭自动迁移？设置 Migration=Off，或按需使用 ReadOnly/On/Full。
- 复杂多表查询？推荐多次单表 + 内存关联（缓存命中更快 & 结构更清晰）。

---
## 11. 生态矩阵（节选）
| 组件 | 说明 |
| ---- | ---- |
| NewLife.Core | 基础能力：日志 / 配置 / 缓存 / 序列化 / APM / 网络 |
| NewLife.Cube | 快速开发平台 + 后台管理（内置代码生成 + 权限）|
| NewLife.Redis | 高性能 Redis 客户端与多功能消息队列 |
| Stardust | 分布式服务平台：注册 / 配置 / 追踪 / 发布中心 |
| AntJob | 分布式任务 & 大数据离线/实时处理 |
| ... | 详见下文项目矩阵 |

---
## 12. 参与与贡献
欢迎提交 Issue / PR：
- 优化性能 / 新增数据库 Provider / 改进文档。
- 请遵循仓库 .github/copilot-instructions 中代码规范（强命名、多 TFM、XML 注释、条件编译）。
- 公共 API 变更需给出迁移策略（Obsolete → 移除）。

开发构建：
```
dotnet restore
dotnet build -c Release
```
可在 Test / XUnitTest.XCode 中补充案例；性能相关建议附基准或说明。

---
## 13. 许可证
MIT，完全开放。允许商用 / 修改 / 再发行（可替换“NewLife”为企业品牌实现私有化）。

---
## 追加：原始快速示例（保留）

### 增删改查（原示例）
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

### 复杂查询（原示例）
```csharp
var list = UserInfo.FindAll(UserInfo._.UserName == "张三" & UserInfo._.Age == 19, UserInfo._.UserName.Desc(), string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 0);
```

### 索引查询（原示例）
```csharp
public static UserInfo FindByUserName(String userName)
{
    if (userName.IsNullOrEmpty()) return null;
    if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.UserName.EqualIgnoreCase(userName));
    return Find(_.UserName == userName);
}
```

---
# 新生命项目矩阵

各项目默认支持net8.0/netstandard2.1/netstandard2.0/net4.61/net4.5，旧版（2023.0308）支持net4.0/net2.0  
|                               项目                               | 年份  | 说明                                                                                   |
| :--------------------------------------------------------------: | :---: | -------------------------------------------------------------------------------------- |
|                             基础组件                             |       | 支撑其它中间件以及产品项目                                                             |
|          [NewLife.Core](https://github.com/NewLifeX/X)           | 2002  | 核心库，日志、配置、缓存、网络、序列化、APM性能追踪                                    |
|    [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode)    | 2005  | 大数据中间件，单表百亿级，MySql/SQLite/SqlServer/Oracle/TDengine/达梦，自动分表        |
|      [NewLife.Net](https://github.com/NewLifeX/NewLife.Net)      | 2005  | 网络库，单机千万级吞吐率（2266万tps），单机百万级连接（400万Tcp）                      |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011  | RPC通信框架，内网高吞吐或物联网硬件设备场景                                            |
|     [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube)     | 2010  | 魔方快速开发平台，集成了用户权限、SSO登录、OAuth服务端等，单表100亿级项目验证          |
|    [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent)    | 2008  | 服务管理组件，把应用安装成为操作系统守护进程，Windows服务、Linux的Systemd              |
|     [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero)     | 2020  | Zero零代脚手架，基于NewLife组件生态的项目模板，Web、WebApi、Service                    |
|                              中间件                              |       | 对接知名中间件平台                                                                     |
|    [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis)    | 2017  | Redis客户端，微秒级延迟，百万级吞吐，丰富的消息队列，百亿级数据量项目验证              |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018  | RocketMQ纯托管客户端，支持Apache RocketMQ和阿里云消息队列，十亿级项目验                |
|     [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT)     | 2019  | 物联网消息协议，MqttClient/MqttServer，客户端支持阿里云物联网                          |
|      [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT)      | 2022  | IoT标准库，定义物联网领域的各种通信协议标准规范                                        |
|   [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus)   | 2022  | ModbusTcp/ModbusRTU/ModbusASCII，基于IoT标准库实现，支持IoT平台和IoTEdge               |
|  [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens)  | 2022  | 西门子PLC协议，基于IoT标准库实现，支持IoT平台和IoTEdge                                 |
|      [NewLife.Map](https://github.com/NewLifeX/NewLife.Map)      | 2022  | 地图组件库，封装百度地图、高德地图和腾讯地图                                           |
|       [NewLife.IP](https://github.com/NewLifeX/NewLife.IP)       | 2022  | IP地址库，IP地址转物理地址                                                             |
|                             产品平台                             |       | 产品平台级，编译部署即用，个性化自定义                                                 |
|           [AntJob](https://github.com/NewLifeX/AntJob)           | 2019  | 蚂蚁调度，分布式大数据计算平台（实时/离线），蚂蚁搬家分片思想，万亿级数据量项目验证    |
|         [Stardust](https://github.com/NewLifeX/Stardust)         | 2018  | 星尘，分布式服务平台，节点管理、APM监控中心、配置中心、注册中心、发布中心              |
|      [NewLife.ERP](https://github.com/NewLifeX/NewLife.ERP)      | 2021  | 企业ERP，产品管理、客户管理、销售管理、供应商管理                                      |
|         [CrazyCoder](https://github.com/NewLifeX/XCoder)         | 2006  | 码神工具，众多开发者工具，网络、串口、加解密、正则表达式、Modbus                       |
|           [XProxy](https://github.com/NewLifeX/XProxy)           | 2005  | 产品级反向代理，NAT代理、Http代理                                                      |
|        [HttpMeter](https://github.com/NewLifeX/HttpMeter)        | 2022  | Http压力测试工具                                                                       |
|         [GitCandy](https://github.com/NewLifeX/GitCandy)         | 2015  | Git源代码管理系统                                                                      |
|          [SmartOS](https://github.com/NewLifeX/SmartOS)          | 2014  | 嵌入式操作系统，完全独立自主，支持ARM Cortex-M芯片架构                                 |
|          [SmartA2](https://github.com/NewLifeX/SmartA2)          | 2019  | 嵌入式工业计算机，物联网边缘网关，高性能.NET6主机，应用于工业、农业、交通、医疗        |
|                           菲凡物联FIoT                           | 2020  | 物联网整体解决方案，建筑、环保、农业，软硬件及大数据分析一体化，单机十万级点位项目验证 |
|                           NewLife.UWB                            | 2020  | 厘米级（10~20cm）高精度室内定位，软硬件一体化，与其它系统联动，大型展厅项目验证        |

---
# 新生命开发团队
![XCode](https://newlifex.com/logo.png)

新生命团队（NewLife）成立于2002年，是新时代物联网行业解决方案提供者，致力于提供软硬件应用方案咨询、系统架构规划与开发服务。NuGet 累计下载 413万+。团队核心组件（XCode / Redis / Stardust / AntJob / IoT 等）已在电力、工业、交通、环保、医疗、文博等行业大规模应用。

我们持续改进，力求成为值得长期信赖的 .NET / IoT / 分布式技术伙伴。

网站：https://newlifex.com   开源组织：https://github.com/NewLifeX  
微信公众号：  
![智能大石头](https://newlifex.com/stone.jpg)

---
> 如果本项目对你有帮助，欢迎 Star、分享给团队，或通过 Issue/PR 反馈改进建议。
