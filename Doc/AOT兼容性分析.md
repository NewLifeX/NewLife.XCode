# NewLife.XCode AOT 兼容性分析

> 基线：v12.2（依赖 NewLife.Core 12.0.2026.1009-beta0916）｜评估日期：2026-10-09
> 评估方式：**分析器实测 + NativeAOT 真实发布编译 + 原生运行实测**（win-x64，.NET 10）
> 冒烟工程：临时工程 `Bin/AotSmoke/`（不入库，复现方法见 §7）

---

## 1. 现状摘要

- **结论先行**：XCode 当前**不能**在 NativeAOT 下工作；核心实体回路存在 3 处断裂（实体静态构造、实体工厂实例化、元数据静默为空）。改造**可行性中等偏高**，主要工作集中在「元数据保留」与「动态能力边界声明」两条主线。
- **有利条件**：① 生成实体代码的成员访问已是静态实现（`this[name]` switch、显式属性），最难的成员存取通道无需改造；② 上游 NewLife.Core v12 已完成 AOT 改造（net8+ 常驻 `IsAotCompatible`、源生成器体系、0 告警），依赖升级即可继承。
- **实测基线数据**：临时启用 net10.0 目标 + `IsAotCompatible` 后，库内分析器告警 **188 条**（IL2xxx/IL3xxx）；NativeAOT 真实发布（含 RID 编译与 ILC 阶段）告警 **55 条**；6 场景运行冒烟 **3 失败**（含 1 处静默降级）。
- **目标框架现状**：库仍多目标 `net45;net461;netstandard2.0;netstandard2.1`，**无 net6+ 目标**，`IsAotCompatible` 无从启用；同时 net10 目标暴露 14 处编译错误（`NETCOREAPP` 死分支，`StackTraceHiddenAttribute` 缺 using，2 行可修）。

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

### 2.2 NativeAOT 真实发布（win-x64，含 RID 编译 + ILC）

- 发布成功，原生可执行文件 14.8 MB；发布过程告警 **55 条**（XCode 源 45、ILC 阶段 9、冒烟工程 1；**Core 侧 0 条**）：
  `IL2026×27、IL2046×7、IL3000×5、IL2057×3、IL3050×3、IL2070×2、IL2075×2、IL2093×2、IL2111×2、IL2067×1、IL2071×1`。
- IL2026 主要来源：`DbMetaData/DaMengMeta/DB2Meta/OracleMeta/SqlServerMetaData` 系列方法中的 `DataTable.Select(String)`（元数据正反向工程）。

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

---

## 3. 阻塞面清单（按机制归并）

| 机制 | 代表位置 | 现状 | AOT 处置方向 |
|------|---------|------|-------------|
| 实体元数据反射构建 | `TableItem.Create(Type)`、`FieldItem(PropertyInfo)`（特性扫描实体属性） | 运行时反射，AOT 下静默为空 | 泛型入口 + 保留注解；或生成器静态元数据（见 §5） |
| 实体工厂动态实例化 | `EntityFactory.CreateFactory`：`typeof(Entity<>.DefaultEntityFactory).MakeGenericType(type)` + `CreateInstance` | AOT 下 ctor 被裁剪抛异常 | 每个实体由代码生成器生成具体工厂类型 / 静态注册；泛型入口 |
| 驱动动态装载 | `DriverLoader`（`Assembly.Load`/`Type.GetType`/在线下载）、`SqlCe.EngineType` | AOT 设计性不支持 | 声明动态语义 + 静态引用驱动降级路径 |
| 驱动私有反射 | `MySql.QueryModelsAsync`（`GetMethod`+`MakeGenericMethod`）、`SqlServer` 批量（`GetMethodEx`） | AOT 下不可用 | 声明边界 + 回退公共 API 路径 |
| LINQ 表达式编译 | `EntityQueryProvider`（`lambda.Compile()`、`InvokeMember`） | AOT 下不可用 | `RequiresDynamicCode` 声明 + 解释执行/降级 |
| 通用映射/转换 | `DAL_Mapper`、`DataConversion`、`EntityBase.GetCachedProperties` | 运行时 `GetProperties` | 泛型入口（`Reflect.*<T>` 风格）或声明边界 |
| ComponentModel 面 | `EntityBase_Interface`（`ICustomTypeDescriptor`/`TypeDescriptor`） | 设计器/DataTable 适配场景反射 | 声明裁剪语义；主链不依赖 |
| `DataTable.Select` 表达式 | 元数据正反向工程各 `*Meta` 类 | `RequiresUnreferencedCode` | 改用 LINQ/循环替代，或边界声明 |
| 程序集/类型发现 | `EntityFactory.LoadEntities`（`GetAllSubclasses`） | 全扫描发现 | 注册表/生成清单替代，或声明不支持 |
| 其他 | `StatModel`、`ModelHelper`（模型 XML 反射填充）等 | 运行时反射 | 泛型入口/声明边界 |

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

---

## 5. 工作分解（建议分阶段推进）

| 阶段 | 任务 | 说明 | 预估 |
|:----:|------|------|:----:|
| 1 基础设施 | 新增 net8.0/net10.0 目标；修复 `NETCOREAPP` 死分支编译（`StackTraceHidden` using 等，建议全库排查同类分支）；低版本 TFM 补 `DynamicallyAccessedMembers`/`DynamicDependency`/`UnconditionalSuppressMessage` 垫片；net8+ 启用 `IsAotCompatible` | 对齐 Core 的 csproj 策略 | 小 |
| 2 元数据保留主线 | `Entity<TEntity>` 泛型参数加保留注解并传导；`TableItem.Create`/`GetFields` 增加泛型入口；`EntityFactory` 工厂静态注册/生成具体工厂；`EntityBase.GetCachedProperties` 泛型化 | 消除 §2.3 的 3 处断裂 | 中（核心） |
| 3 动态能力边界声明 | LINQ `Expression.Compile`、驱动动态装载/私有反射、`DataTable.Select` 表达式、映射/转换反射面：`RequiresDynamicCode`/`RequiresUnreferencedCode` 声明 + 明确降级或异常 | 对标 Core 收口记录做法 | 中 |
| 4 静态化通道（可选增强） | 由 XCodeTool/Build.tt 生成静态元数据表与工厂注册（类似 Core 源生成器，开发者侧零配置；缺失时回退反射，语义不变） | XCode 独有优势：实体代码本就是生成的 | 中 |
| 5 冒烟与 CI | 正式 `XCode.AotSmoke` 工程（JIT/AOT 双跑：实体 CRUD/元数据/缓存/LINQ 子集）；`aot.yml`（分析器阻断 + win-x64 冒烟，linux-x64 观察期）；驱动矩阵 AOT 验证（MySql/PostgreSQL/SqlServer/SQLite…） | 对标 Core | 中 |

**驱动与提供者专项**（阶段 5 前置调研）：各 `XCode.*` 提供者需补现代 TFM 并逐个验证；`System.Data.SQLite`（原生互操作 + 动态装载）在 NativeAOT 下不适用，需评估 `Microsoft.Data.Sqlite`（SQLitePCLRaw）路线或明确不支持；`NewLife.MySql`/`Npgsql` 等驱动需按各自 AOT 语义确认。

---

## 6. 已知边界（拟收口候选）

| 项 | 位置 | 处置建议 |
|----|------|---------|
| 驱动在线下载/动态装载 | `DriverLoader` | AOT 设计性不支持；静态引用驱动 + 声明动态语义 |
| 全实体扫描发现 | `EntityFactory.LoadEntities` | 注册表/生成清单替代，或声明不支持 |
| 设计器/ComponentModel 场景 | `EntityBase_Interface` | 声明裁剪语义，不逐点改造 |
| 编译排除模块（Firebird/SqlCe 等） | 已移除/未编译 | 不纳入 AOT 范围 |
| XCodeTool 运行期形态 | 开发期工具 | 不要求 AOT（可另行评估 PublishAot 工具链收益） |

---

## 7. 复现方法

```powershell
# 1) 分析器：临时在 XCode/XCode.csproj 的 TargetFrameworks 追加 net10.0，并按 net8+ 条件启用 IsAotCompatible
#    <PropertyGroup Condition="'$(TargetFramework)' == 'net10.0'"><IsAotCompatible>true</IsAotCompatible></PropertyGroup>
#    另需给 Entity.cs / DAL_DbOperate.cs 补 using System.Diagnostics;（修复 NETCOREAPP 死分支编译）
dotnet build XCode\XCode.csproj -f net10.0 -c Release

# 2) 冒烟：临时控制台工程（PublishAot=true 写入工程文件，勿用命令行 -p:PublishAot=true 全局属性）
dotnet publish <冒烟目录>\AotSmoke.csproj -c Release -r win-x64 -o out
.\out\AotSmoke.exe
```

> 本次实测的完整告警清单与发布日志保存在评估会话工作区，未随仓库提交。

---

## 8. 结论与建议

1. **可行性**：中等偏高。最难的成员访问通道已由代码生成器静态化；剩余为「元数据保留 + 边界声明」两条主线，工作量与 Core v12 的 AOT 改造（IMP-24 系列）大体相当。
2. **建议路线**：阶段 1→2→3 先行取得「核心回路 AOT 可用」，阶段 4 静态化与阶段 5 冒烟/CI 随后收口；期间维持 JIT 语义不变（生成器缺失/未覆盖自动回退反射）。
3. **风险提示**：元数据静默为空属"剪裁后不报错"类型缺陷，评估/实施阶段应对关键路径增加显式校验（本次冒烟已按此设计探针）。
4. **立项决策点**：云原生/Serverless 场景需求是否明确；是否接受为 AOT 新增驱动依赖策略（如 SQLite 驱动替换）。
