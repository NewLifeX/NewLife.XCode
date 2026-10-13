# NewLife.XCode AOT 兼容性分析

> 基线：v12.2（依赖 NewLife.Core 12.0.2026.1013-beta0732）｜评估日期：2026-10-09（2026-10-13 复核：各指标与首评一致）
> 评估方式：**分析器实测 + NativeAOT 真实发布编译 + 原生运行实测**（win-x64，.NET 10）
> 冒烟工程：`AotSmoke/`（已入库，9 场景 JIT/AOT 双跑，由 `.github/workflows/aot.yml` 自动执行）

---

## 1. 现状摘要

- **结论先行**：核心实体回路（元数据扫描、工厂创建、属性/索引器、行映射）已在 NativeAOT 下**真实可用**——原生发布运行冒烟 9/9 全部通过，三处断裂消除；库内 AOT/裁剪分析器告警由 131 处收敛至 **0**，NativeAOT 发布 XCode 源侧告警清零；冒烟与 CI 已落地（`AotSmoke` 工程 + `aot.yml`，含 NovaDb 嵌入库真库 CRUD 与实体缓存）。
- **有利条件**：① 生成实体代码的成员访问已是静态实现（`this[name]` switch、显式属性），最难的成员存取通道无需改造；② 上游 NewLife.Core v12 已完成 AOT 改造（net8+ 常驻 `IsAotCompatible`、源生成器体系、0 告警），依赖升级即可继承。
- **实测基线数据**：临时启用 net10.0 目标 + `IsAotCompatible` 后，库内分析器告警 **188 条**（IL2xxx/IL3xxx）；NativeAOT 真实发布（含 RID 编译与 ILC 阶段）告警 **55 条**；6 场景运行冒烟 **3 失败**（含 1 处静默降级）。
- **目标框架现状**：库现多目标 `net45;net461;netstandard2.0;netstandard2.1;net8.0;net10.0`（2026-10-13 新增现代目标并常驻 `IsAotCompatible`）；net8/net10 首次编译暴露的 14 处错误（`NETCOREAPP` 死分支 `StackTraceHiddenAttribute` 缺 using）已修复。

---

## 2. 实测数据

### 2.1 分析器（net10.0 + `IsAotCompatible`，Release）

- 首次编译：**14 错误**（全部为 `Entity.cs`/`DAL_DbOperate.cs` 中 `#if NETCOREAPP` 分支的 `StackTraceHiddenAttribute` 找不到，历史遗留死分支从未参与编译）；补 2 行 `using System.Diagnostics;` 后 0 错误。
- 告警总量 421 条，其中 **AOT/裁剪分析器告警 188 条**：

| 告警码 | 数量 | 含义 |
|--------|:---:|------|
| IL2026 | 76 | 调用声明了 `RequiresUnreferencedCode` 的成员（多为 `DataTable.Select(表达式)`、驱动动态成员） |
| IL3050 | 24 | 调用声明了 `RequiresDynamicCode` 的成员（动态代码生成） |
| IL2075 | 22 | 反射取得的返回值不满足保留要求（属性/方法间接访问） |
| IL2046 | 14 | 重写/实现成员缺少与接口一致的裁剪声明 |
| IL2091 | 10 | 泛型实参不满足泛型参数的保留要求 |
| IL3000 | 8 | `Assembly.Location`（单文件/AOT 不支持） |
| IL2057 | 6 | `Type.GetType` 传入非常量字符串 |
| IL2067/IL2070/IL2093/IL2111/IL2060/IL2072/IL2095/IL3002 | 28 | 参数/返回值保留要求不匹配、反射访问带注解成员、`MakeGenericMethod` 等 |

- 告警最集中文件（前 11）：`EntityBase_Interface.cs` 38、`MySql.cs` 22、`DriverLoader.cs` 20、`DbMetaData_Positive.cs` 10、`SqlServer.cs` 10、`EntityQueryProvider.cs` 8、`DaMeng.cs` 8、`DB2.cs` 8、`Oracle.cs` 8、`DataConversion.cs` 6、`DAL_Mapper.cs` 6。
- **2026-10-13 阶段 3 复测**：以唯一告警处统计，收敛前 131 处（IL2026×38、IL2091×35、IL3050×12、IL2075×11、IL2072×9、IL2046×7、IL3000×4、IL2057×3、IL2067×3、IL2070×2、IL2093×2、IL2111×2、IL2060/IL2095/IL3002 各 1）；收敛后 **0 处**（net8.0/net10.0 双目标均 0）。

### 2.2 NativeAOT 真实发布（win-x64，含 RID 编译 + ILC）

- 发布成功，原生可执行文件 14.8 MB；发布过程告警 **55 条**（XCode 源 45、ILC 阶段 9、冒烟工程 1；**Core 侧 0 条**）：
  `IL2026×27、IL2046×7、IL3000×5、IL2057×3、IL3050×3、IL2070×2、IL2075×2、IL2093×2、IL2111×2、IL2067×1、IL2071×1`。
- IL2026 主要来源：`DbMetaData/DaMengMeta/DB2Meta/OracleMeta/SqlServerMetaData` 系列方法中的 `DataTable.Select(String)`（元数据正反向工程）。
- **2026-10-13 阶段 3 复测**：NativeAOT 发布 XCode 源侧告警 **0 条**（仅冒烟工程自身 2 条 IL3000 判真输出）；`DataTable.Select` 面收敛为 `DataTableHelper.SelectRows` 单点边界，其余动态面以 `RequiresUnreferencedCode` 声明或抑制收口。

### 2.3 原生运行实测（6 场景冒烟）

| # | 场景 | 结果 | 说明 |
|---|------|:----:|------|
| 1 | Utility 基础类型转换 | ✅ | Core 能力，AOT 正常 |
| 2 | DAL.CreateTable 元数据对象创建 | ✅ | 非实体表路径正常 |
| 3 | 实体属性/索引器（生成代码静态路径） | ❌ | 类型初始抛 `TypeInitializationException`，内层 `MissingMethodException`：`DefaultEntityFactory[User]` 无参数构造函数被裁剪 |
| 4 | 实体元数据 `Meta.Table` | ❌（静默） | **不抛异常但字段数为 0**——特性反射被裁剪后静默产出空元数据（比抛错更危险） |
| 5 | 实体工厂 `Meta.Factory.Create()` | ❌ | 同 3 的根因 |
| 6 | XTrace 日志 | ✅ | Core 能力，AOT 正常 |

- AOT 判真：发布件 `Assembly.Location` 为空、`IsDynamicCodeSupported=False`；同样 6 场景在 JIT（严格 AOT 姿态）下全部通过，确认失败均源于裁剪/原生编译，而非功能开关。
- **2026-10-13 阶段 2 复测**（冒烟扩充为 7 场景，新增实体行映射 LoadData）：NativeAOT 原生运行**全部通过**，三处断裂消除——元数据 `Fields.Length=2`、`Meta.Factory.Create()` 正常、行映射正确装载（静态工厂路径 + `DynamicallyAccessedMembers` 保留注解传导）。
- **2026-10-13 阶段 5 复测**（冒烟扩充为 9 场景，新增 NovaDb 嵌入库真库 CRUD 与实体缓存；工程入库 `AotSmoke/`）：JIT 与 NativeAOT 原生运行均 **9/9 全部通过**（insert/find/update/表达式查询/delete 全链路 + 缓存命中）。期间修复驱动加载器 AOT 缺陷：`Assembly.GetCallingAssembly()` 在 NativeAOT 下抛 `PlatformNotSupportedException`、`Assembly.LoadFrom` 动态加载不可用——新增"程序集限定名"静态引用降级路径并防护平台异常。

---

## 3. 机制清单与 AOT 现状（按机制归并）

| 机制 | 代表位置 | AOT 现状与处置 |
|------|---------|---------------|
| 实体元数据反射构建 | `TableItem.Create(Type)`、`FieldItem(PropertyInfo)`（特性扫描实体属性） | 泛型入口 + `DynamicallyAccessedMembers` 保留注解；NativeAOT 下字段完整 |
| 实体工厂动态实例化 | `EntityFactory.CreateFactory`（`MakeGenericType` + `CreateInstance`） | `GetFactory<TEntity>()` 静态构造默认工厂（AOT 友好）；`CreateFactory(Type)` 保留为反射兜底 |
| 驱动动态装载 | `DriverLoader`（`Assembly.Load`/`Type.GetType`/在线下载）、`SqlCe.EngineType` | 在线下载/`LoadFrom` 设计性不支持；静态引用驱动走"程序集限定名"降级加载 |
| 驱动私有反射 | `MySql.QueryModelsAsync`（`GetMethod`+`MakeGenericMethod`）、`SqlServer` 批量（`GetMethodEx`） | 探测失败自动回退公共 API（已声明边界） |
| LINQ 表达式编译 | `EntityQueryProvider`（`lambda.Compile()`、`InvokeMember`） | AOT 下不可用（RDC）；`WhereExpression`/`FindAll` 主链不受影响 |
| 通用映射/转换 | `DAL_Mapper`、`DataConversion`、`EntityBase.GetCachedProperties` | 声明边界；AOT/裁剪下由应用侧保证目标类型成员保留 |
| ComponentModel 面 | `EntityBase_Interface`（`ICustomTypeDescriptor`/`TypeDescriptor`） | 与接口注解逐一对齐（RUC 声明）；设计器场景主链不依赖 |
| `DataTable.Select` 表达式 | 元数据正反向工程各 `*Meta` 类 | 收敛 `DataTableHelper.SelectRows` 单点抑制；筛选语义 100% 不变 |
| 程序集/类型发现 | `EntityFactory.LoadEntities`（`GetAllSubclasses`） | 单点声明（`CreateTableItem`）；扫描依赖应用侧保留实体类型 |
| 模型 XML 反射填充 | `StatModel`、`ModelHelper` 等 | 声明边界 |

---

## 4. 与 NewLife.Core 的机制对标

| 通道 | Core v12 现状 | XCode 现状 | 差距 |
|------|--------------|-----------|------|
| 目标框架/分析器 | net8.0+ 常驻 `IsAotCompatible`，分析器 0 告警 | 无 net6+ 目标 | 需新增现代 TFM + 垫片 |
| 成员访问 | 源生成器四通道（DI/Json/Config/Http）+ `Reflect` 泛型入口 | **生成实体已静态**（`this[name]` switch） | 基本持平（优势项） |
| 元数据/工厂 | 生成器静态表 + 泛型保留入口 | 反射构建（阻断） | 主要工作量 |
| 动态能力 | `RequiresDynamicCode` 声明 + 运行守卫 | 无声明 | 需逐点声明 |
| 验证与 CI | AotSmoke 25 场景 + `aot.yml`（分析器硬约束） | 无 | 需新建 |

> Core 的 `Doc/AOT兼容性分析.md` 与 `AotSmoke` 工程可直接作为方法论与验收标准模板。
>
> 2026-10-13：上述差距均已补齐（阶段 1/3/5）；驱动矩阵（各 XCode.* 提供者）按需专项。

---

## 5. 工作分解与进展

| 阶段 | 任务 | 状态 |
|:----:|------|------|
| 1 基础设施 | 新增 net8.0/net10.0 目标；修复 `NETCOREAPP` 死分支编译；低版本 TFM 补 `DynamicallyAccessedMembers`/`UnconditionalSuppressMessage` 垫片；net8+ 启用 `IsAotCompatible` | ✅ 完成（2026-10-13） |
| 2 元数据保留主线 | `Entity<TEntity>` 泛型参数加保留注解并传导；`TableItem.Create`/`GetFields` 增加泛型入口；`EntityFactory` 工厂静态注册/生成具体工厂 | ✅ 完成（2026-10-13） |
| 3 动态能力边界声明 | LINQ `Expression.Compile`、驱动动态装载/私有反射、`DataTable.Select` 表达式、映射/转换反射面：`RequiresDynamicCode`/`RequiresUnreferencedCode` 声明 + 明确降级或异常 | ✅ 完成（库内告警 131→0，发布侧 0） |
| 4 静态化通道（可选增强） | 由 XCodeTool 生成静态元数据表与工厂注册（开发者侧零配置；缺失时回退反射，语义不变） | 🧊 暂缓：核心目标已由阶段 2/3 达成，待真实诉求再评估 |
| 5 冒烟与 CI | `AotSmoke` 工程（JIT/AOT 双跑）+ `aot.yml`（分析器阻断 + win-x64 阻断/linux-x64 观察期） | ✅ 完成（9 场景；NovaDb 真库 CRUD 纳入阻断） |

**驱动与提供者专项**：各 `XCode.*` 提供者的现代 TFM 与 AOT 验证按需推进；`System.Data.SQLite`（原生互操作 + 动态装载）不适用于 NativeAOT，可评估 `Microsoft.Data.Sqlite`（SQLitePCLRaw）路线；NovaDb 嵌入库已通过 AotSmoke 验证。

---

## 6. 已知边界（AOT 下受限能力与范围）

| 项 | 位置 | 说明 |
|----|------|------|
| 实体 LINQ 表达式 | `EntityQueryProvider`、`EntityExtension.ToDictionary` | AOT 下不可用（表达式树运行时编译）；`WhereExpression`/`FindAll` 主链不受影响 |
| 驱动在线下载/动态装载 | `DriverLoader` | 设计性不支持（`Assembly.LoadFrom`）；静态引用驱动可正常加载，探测失败自动回退 |
| 运行时反射面 | `DAL_Mapper.Update`、`DataConversion`、`StatModel.Fill`、`ClassBuilder`、`TableItem.GetFields`、`EntityFactory.LoadEntities` | 依赖目标类型成员；AOT/裁剪下由应用侧保证保留 |
| 设计器/ComponentModel 场景 | `EntityBase_Interface` | 主链不依赖；相关成员已声明裁剪语义 |
| 范围外 | 未注册数据库驱动（Firebird 已移除、SqlCe 等）、XCodeTool 开发期工具 | 不纳入 AOT 范围 |

---

## 7. 复现方法

```powershell
# 1) 分析器：库内 AOT/裁剪分析器告警应为 0（net8.0/net10.0 双目标，阶段 1 起常驻 IsAotCompatible）
dotnet build XCode\XCode.csproj -f net8.0 -c Release
dotnet build XCode\XCode.csproj -f net10.0 -c Release
# CI 硬约束等价命令（-warnaserror 仅对 IL 告警生效，任一新增即阻断）
dotnet build XCode\XCode.csproj -f net10.0 -c Release -warnaserror:IL2026,IL2046,IL2057,IL2060,IL2067,IL2070,IL2071,IL2072,IL2075,IL2091,IL2093,IL2095,IL2111,IL3000,IL3002,IL3050

# 2) 冒烟（9 场景 JIT/AOT 双跑；任一失败即非 0 退出码）
dotnet run -c Release --project AotSmoke/AotSmoke.csproj
dotnet publish AotSmoke/AotSmoke.csproj -c Release -r win-x64 -o AotSmoke/bin/aot-out
.\AotSmoke\bin\aot-out\AotSmoke.exe
```

> CI 由 `.github/workflows/aot.yml` 自动执行：analyzer 阻断（IL 告警 -warnaserror）+ win-x64 冒烟阻断 + linux-x64 观察期（稳定后转阻断）。

---

## 8. 结论

1. **AOT 可用性**：核心实体回路（元数据/工厂/索引器/行映射/CRUD/缓存）已在 NativeAOT 下真实可用（9 场景双跑全通过）；库内 AOT/裁剪分析器告警 0 条，CI 以 `-warnaserror` 硬约束防回归。
2. **路线执行**：阶段 1/2/3/5 完成（2026-10-13）；阶段 4（静态化通道）为可选增强，暂缓待真实诉求再评估。
3. **设计经验**：元数据静默为空属"剪裁后不报错"类型缺陷，关键路径应有显式校验（冒烟已按此设计探针）。
4. **后续跟踪**：各 `XCode.*` 提供者的 AOT 验证按需专项推进。
