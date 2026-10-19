# NewLife.XCode AOT 兼容性分析

> 基线：v12.2（依赖 NewLife.Core 12.0.2026.1013-beta0732）｜更新：2026-10-16
> 验证方式：分析器（net8.0/net10.0 常驻 `IsAotCompatible`）+ NativeAOT 真实发布 + 原生运行冒烟（win-x64/linux-x64/linux-arm64，.NET 10；linux-la64 QEMU JIT）
> 冒烟工程：`AotSmoke/`（16 场景 JIT/AOT 双跑，`.github/workflows/aot.yml` 自动执行）

---

## 1. 支持现状

- 目标框架：`net45;net461;netstandard2.0;netstandard2.1;net8.0;net10.0`，net8+ 常驻 `IsAotCompatible`
- 库内 AOT/裁剪分析器告警 **0 条**（net8.0/net10.0 双目标），CI 以 `-warnaserror` 硬约束防回归
- NativeAOT 发布：XCode 源侧告警 **0 条**；原生运行冒烟 **16/16 全通过**（MySQL/SqlServer 场景在提供 `XCode_mysql`/`XCode_sqlserver` 环境变量时执行，否则跳过）

---

## 2. 已支持功能（NativeAOT 原生实测）

| 功能 | 说明 |
|------|------|
| 实体属性/索引器 | 生成代码静态访问（`this[name]` switch） |
| 实体元数据 `Meta.Table` | 字段扫描完整（`DynamicallyAccessedMembers` 保留注解传导） |
| 实体工厂 `Meta.Factory` | `Create()` 可用（静态工厂路径） |
| 实体行映射 | `Accessor.LoadData<T>`（DataTable → 实体列表） |
| 实体 CRUD | NovaDb 嵌入库：建表/Insert/FindByKey/Update/Delete |
| 表达式查询 | `FindAll(_.Field == value)` 等 WhereExpression 条件 |
| 实体 LINQ（IQueryable） | Where/OrderBy/Skip/Take/First/Count；闭包变量与方法调用求值（AOT 下运行时解释器回退） |
| 实体列表转字典 | `ToDictionary()`（运行时构造 `Dictionary<主键类型, 实体类型>`） |
| 泛型属性拷贝 | `DataConversion.CopyProperty<TSource,TTarget>`（AOT/裁剪下自动保留源与目标的公开属性） |
| 实体缓存 | `FindAllWithCache` |
| SQLite 嵌入库 | Microsoft.Data.Sqlite（SQLitePCLRaw）路线：建表/CRUD/反向工程；System.Data.SQLite 为混合编译不支持 AOT |
| MySQL 真库 | 官方 MySql.Data 26.7.0：建表/CRUD/DAL 查询；裁剪根需含 `MySql.Data` 与 `System.Configuration.ConfigurationManager` |
| SqlServer 真库 | 官方 System.Data.SqlClient 4.9.1：建表/CRUD/DAL 查询（本机 LocalDB 实例实测）；裁剪根需含 `System.Data.SqlClient` |
| 数据库访问 | DAL 元数据对象创建、SQL 执行 |
| 驱动加载 | 静态引用驱动（工程引用/deps）按程序集限定名降级加载 |
| 基础能力 | Utility 类型转换、XTrace 日志 |

---

## 3. 未实现功能（AOT 待办）

| 功能 | 现状 | 方向 |
|------|------|------|
| 驱动矩阵 AOT 验证 | 已验证：NovaDb、SQLite（Microsoft.Data.Sqlite）、MySQL（MySql.Data）、SqlServer（System.Data.SqlClient，本机 LocalDB 实测、CI MySQL/SqlServer 服务容器常态化验证）；MySQL/SqlServer 场景连接串分别经 `XCode_mysql`/`XCode_sqlserver` 环境变量提供，未设置时冒烟自动跳过；其余 `XCode.*` 提供者需外部服务器，未验证；瀚高内置 `Nhgdb.dll` 为 .NET Framework 4.5.1 构建，现代 .NET 需厂商提供新驱动 | 具备服务器环境时逐个验证（PostgreSQL/Oracle/达梦/DB2/人大金仓/瀚高） |
| 静态化通道（可选增强） | 未实施（暂缓，待真实诉求） | XCodeTool 生成静态元数据表与工厂注册；配套全实体扫描的注册表替代 |
| 驱动私有反射优化入口 | 受阻于驱动侧：`NewLife.MySql 1.4.2026.801` 与 Core 12.0.2026.1013 二进制不兼容（`OwnerPacket.Slice(Int32,Int32)` 已改为显式接口实现，任意操作即抛 `MissingMethodException`）；该入口需待上游重编译发版后再验证 | 改为公共 API 直调（MySql 零拷贝读取/批量执行） |

---

## 4. 已知边界（设计性不支持与使用条件）

| 项 | 位置 | 说明 |
|----|------|------|
| 驱动在线下载/动态装载 | `DriverLoader` | 设计性不支持（`Assembly.LoadFrom`）；静态引用驱动可正常加载，探测失败自动回退 |
| 运行时反射面 | `StatModel.Fill`、`ClassBuilder`、`TableItem.GetFields`、`EntityFactory.LoadEntities` | 依赖运行时类型成员，无静态类型可泛型化；AOT/裁剪下由应用侧保证保留（`DAL_Mapper.Query<T>`、`DataConversion.CopyProperty<TSource,TTarget>` 已提供泛型入口） |
| 设计器/ComponentModel 场景 | `EntityBase_Interface` | 主链不依赖；相关成员已声明裁剪语义 |
| 范围外 | 未注册数据库驱动（Firebird 代码保留但未注册、SqlCe 等）、XCodeTool 开发期工具 | 不纳入 AOT 范围 |

---

## 5. 复现与 CI

```powershell
# 分析器（应为 0 告警）
dotnet build XCode\XCode.csproj -f net8.0 -c Release
dotnet build XCode\XCode.csproj -f net10.0 -c Release

# 冒烟（16 场景 JIT/AOT 双跑；任一失败即非 0 退出码）
# MySQL/SqlServer 场景分别读取环境变量 XCode_mysql/XCode_sqlserver，未设置时自动跳过
dotnet run -c Release --project AotSmoke/AotSmoke.csproj
dotnet publish AotSmoke/AotSmoke.csproj -c Release -r win-x64 -o AotSmoke/bin/aot-out
.\AotSmoke\bin\aot-out\AotSmoke.exe
```

> 判真探针：原生发布件 `IsDynamicCodeSupported=False` 且 `Assembly.Location` 为空。
> CI：`.github/workflows/aot.yml`（`aot-analyzer` IL 告警 `-warnaserror` 阻断 + 四 RID 原生发布矩阵：win-x64/linux-arm64 基础场景、linux-x64 附 MySQL/SqlServer 真库场景、linux-la64 QEMU JIT 冒烟；均随 push/PR 触发）。

---

## 6. 多 RID 支持矩阵与 CI

> 目标：单元测试与 AOT 冒烟覆盖 **win-x64 / linux-x64 / linux-arm64 / linux-la64（龙芯）**。
> 前提：**NativeAOT 官方不支持跨 OS 编译**，每个 RID 均使用原生 runner 发布运行；linux-la64 无托管 runner、无 NativeAOT 后端，采用 QEMU 用户态仿真跑 JIT 冒烟与单元测试。

### 6.1 覆盖分层

| 目标 RID | 单元测试（test.yml） | AOT 冒烟（aot.yml） | runner / 载体 | 排除项 |
|---|---|---|---|---|
| win-x64 | 全量（镜像自带 MySQL 8.0/PostgreSQL 17 + LocalDB 支撑真库组） | JIT + NativeAOT | windows-latest | 无 |
| linux-x64 | 全量（services：MySQL/PostgreSQL/SQL Server） | JIT + NativeAOT（真库场景含 MySQL/SqlServer） | ubuntu-latest | 无 |
| linux-arm64 | 全量（services：MySQL/PostgreSQL，均有 arm64 官方镜像） | JIT + NativeAOT | ubuntu-24.04-arm | SQL Server 无 arm64 镜像 |
| linux-la64 | 全量（除外部数据库组） | JIT 冒烟 | ubuntu-latest + QEMU（ghcr.io/loong64/debian:trixie 容器） | 外部数据库三组（MySQL/PostgreSQL/SQL Server） |

- **NovaDb 网络组**在测试进程内启动 `NewLife.NovaServer`（随机端口、临时目录），无需外部服务；也可用环境变量 `XCode_novadb` 指向外部实例。
- win-x64 的三类真库为**尽力启用**：任一不可用时自动降级为排除该组，不阻塞任务。
- **SQLite 驱动分层**：x86/x64（win/linux）由 `XCode.SQLite` 适配包提供 System.Data.SQLite；arm64/loongarch64 缺少该驱动原生库，自动降级为 Microsoft.Data.Sqlite（SQLitePCLRaw），单元测试按驱动分支断言连接串与 DataAdapter 能力。

### 6.2 LA64（linux-loongarch64，社区支持平台）

- **现状**：官方不发 runtime pack、无 NativeAOT 支持；龙芯 [loongson/dotnet](https://github.com/loongson/dotnet) 提供 .NET 8/9/10 SDK（当前 v10.0.113）。
- **CI 方案**：`ghcr.io/loong64/binfmt` 注册 QEMU 用户态仿真 + `ghcr.io/loong64/debian:trixie`（loong64 社区官方镜像移植）容器内运行龙芯 SDK；
  - 原生库缺口：SQLitePCLRaw 未提供 linux-loongarch64 包，容器内安装 `libsqlite3-0` 并软链为 `libe_sqlite3.so` 供 Microsoft.Data.Sqlite 使用；
  - 无 LA64 原生 ICU，以 `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` 运行；
  - 单元测试 `timeout 300m` 守护（QEMU 较慢，实测时长以 CI 日志为准）。
- **自托管接入（有真机后升级为原生阻断）**：龙芯机器接入 GitHub Actions self-hosted runner 后，替换容器步骤并去掉仿真环境变量即可。

### 6.3 单元测试稳定性保障

- `dotnet test` 统一启用 `--blame-crash --blame-crash-dump-type mini --blame-hang-timeout 10m --blame-hang-dump-type mini`：崩溃或挂起时自动产出 mini 转储与 `Sequence_*.xml`，随 `TestResults/**` 一并上传，并打印逐用例日志（`console;verbosity=detailed`）便于定位崩溃点。
- **三个 dotnet test 任务（win-x64 / linux-x64 / linux-arm64）失败均自动重试一次**（`--no-build`，重试结果另存 `*-retry.trx`）：偶发 SQLite 原生崩溃、驱动联网下载超时、慢库命令超时等环境抖动不再直接判红；重试成功仅告警，重试仍失败则任务失败，转储与日志保留在制品中。
- 驱动（Oracle / HANA / MySQL 等）缺失时由 `DriverLoader` 联网下载，网络抖动会导致工厂创建失败；各驱动的 `InitTest` 已统一在驱动不可用时跳过（`XCodeException` / `FileNotFoundException` / `FileLoadException` / `BadImageFormatException`）。
- 已知观察：首次全绿（2026-10-16）前，linux-x64 曾出现一次 testhost 原生崩溃（执行 25/1109 后中止，候选 `DataSimulationTests.CreateInstance` / `ConnectionPoolTests.GetReturn_BasicFlow`），WSL linux-x64 连续 6 轮全量（1109 用例）未复现；暂不调整 SQLite 驱动分层，复发时以转储定位。

### 6.4 明确排除（避免反复讨论）

| 目标 | 排除理由 |
|---|---|
| Android / iOS / tvOS / WASM | 应用模型类目标，无法用 `dotnet test` 在本仓验证 |
| win-arm64 / osx-x64 / osx-arm64 / linux-musl | 非本仓要求范围；需要时按同一配方扩展 |
| Windows x64→Linux 交叉 NativeAOT | 官方不支持跨 OS 编译 |


