# NewLife.XCode AOT 兼容性分析

> 基线：v12.2（依赖 NewLife.Core 12.0.2026.1013-beta0732）｜更新：2026-10-14
> 验证方式：分析器（net8.0/net10.0 常驻 `IsAotCompatible`）+ NativeAOT 真实发布 + 原生运行冒烟（win-x64，.NET 10）
> 冒烟工程：`AotSmoke/`（14 场景 JIT/AOT 双跑，`.github/workflows/aot.yml` 自动执行）

---

## 1. 支持现状

- 目标框架：`net45;net461;netstandard2.0;netstandard2.1;net8.0;net10.0`，net8+ 常驻 `IsAotCompatible`
- 库内 AOT/裁剪分析器告警 **0 条**（net8.0/net10.0 双目标），CI 以 `-warnaserror` 硬约束防回归
- NativeAOT 发布：XCode 源侧告警 **0 条**；原生运行冒烟 **14/14 全通过**

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
| 数据库访问 | DAL 元数据对象创建、SQL 执行 |
| 驱动加载 | 静态引用驱动（工程引用/deps）按程序集限定名降级加载 |
| 基础能力 | Utility 类型转换、XTrace 日志 |

---

## 3. 未实现功能（AOT 待办）

| 功能 | 现状 | 方向 |
|------|------|------|
| 驱动矩阵 AOT 验证 | 嵌入库已验证：NovaDb、SQLite（Microsoft.Data.Sqlite 路线）；其余 `XCode.*` 提供者需外部服务器，未验证；瀚高内置 `Nhgdb.dll` 为 .NET Framework 4.5.1 构建，现代 .NET 需厂商提供新驱动 | 具备服务器环境时逐个验证（SqlServer/MySql/PostgreSQL/Oracle/达梦/DB2/人大金仓/瀚高） |
| 静态化通道（可选增强） | 未实施（暂缓，待真实诉求） | XCodeTool 生成静态元数据表与工厂注册；配套全实体扫描的注册表替代 |
| 驱动私有反射优化入口 | AOT 下探测失败回退常规路径 | 改为公共 API 直调（MySql 零拷贝读取/批量执行） |

---

## 4. 已知边界（设计性不支持与使用条件）

| 项 | 位置 | 说明 |
|----|------|------|
| 驱动在线下载/动态装载 | `DriverLoader` | 设计性不支持（`Assembly.LoadFrom`）；静态引用驱动可正常加载，探测失败自动回退 |
| 运行时反射面 | `StatModel.Fill`、`ClassBuilder`、`TableItem.GetFields`、`EntityFactory.LoadEntities` | 依赖运行时类型成员，无静态类型可泛型化；AOT/裁剪下由应用侧保证保留（`DAL_Mapper.Query<T>`、`DataConversion.CopyProperty<TSource,TTarget>` 已提供泛型入口） |
| 设计器/ComponentModel 场景 | `EntityBase_Interface` | 主链不依赖；相关成员已声明裁剪语义 |
| 范围外 | 未注册数据库驱动（Firebird 已移除、SqlCe 等）、XCodeTool 开发期工具 | 不纳入 AOT 范围 |

---

## 5. 复现与 CI

```powershell
# 分析器（应为 0 告警）
dotnet build XCode\XCode.csproj -f net8.0 -c Release
dotnet build XCode\XCode.csproj -f net10.0 -c Release

# 冒烟（14 场景 JIT/AOT 双跑；任一失败即非 0 退出码）
dotnet run -c Release --project AotSmoke/AotSmoke.csproj
dotnet publish AotSmoke/AotSmoke.csproj -c Release -r win-x64 -o AotSmoke/bin/aot-out
.\AotSmoke\bin\aot-out\AotSmoke.exe
```

> 判真探针：原生发布件 `IsDynamicCodeSupported=False` 且 `Assembly.Location` 为空。
> CI：`.github/workflows/aot.yml`（IL 告警 `-warnaserror` 阻断 + win-x64 冒烟阻断 + linux-x64 观察期）。

