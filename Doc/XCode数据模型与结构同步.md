# XCode 数据模型与结构同步

## 概述

**XCode** 是新生命团队开发的 .NET 数据中间件，提供 ORM、缓存、分表分库、数据同步等多种数据访问能力。本文档聚焦于 XCode 的**数据模型管理**与**数据库结构同步**机制。

> **国产原创**：XCode 完全由国人开发，未引用任何第三方组件，从底层到上层全部自主实现。

### XCode 的数据访问能力

除了强类型实体操作外，XCode 还支持：
- **任意实体模型的增删改查**：支持执行任意 SQL 并映射到任意实体模型，完全兼容 Dapper 用法
- **多级缓存**：实体缓存、对象缓存、全表缓存，自动更新
- **读写分离**：自动识别读写操作，路由到主从库
- **链路追踪**：内置埋点，支持 OpenTelemetry 等 APM 系统

### 核心概念

XCode 使用 **IDataTable/IDataColumn/IDataIndex** 接口体系来描述数据库结构，这是整个元数据管理的核心：

```
                    ┌─────────────────┐
                    │   IDataTable    │  ← 核心：衔接所有部位
                    │   IDataColumn   │
                    │   IDataIndex    │
                    └────────┬────────┘
                             │
        ┌────────────────────┼────────────────────┐
        │                    │                    │
        ↓                    ↓                    ↓
   ┌─────────┐         ┌─────────┐         ┌─────────┐
   │ 数据库  │         │  XML    │         │ 实体类  │
   │ 正向读取 │         │ 持久化  │         │ 特性提取 │
   └─────────┘         └─────────┘         └─────────┘
```

**IDataTable 是真正的核心**：
- 正向工程从数据库读取结构，转换为 IDataTable
- 反向工程从实体类提取特性，构建 IDataTable，再同步到数据库
- XML 模型文件只是 IDataTable 的一种持久化形式
- 代码生成器读取 XML（即 IDataTable 的持久化），生成实体类

### 正向工程与反向工程

| 工程类型 | 数据流向 | 核心方法 |
|---------|---------|---------|
| **正向工程** | 数据库 → IDataTable → XML/实体类 | `DbMetaData.GetTables()` |
| **反向工程** | 实体类 → IDataTable → 数据库 | `DbMetaData.SetTables()` |

---

## 一、IDataTable 模型体系

### 1.1 核心接口

```csharp
// 表结构
public interface IDataTable : ICloneable
{
    String Name { get; set; }           // 实体类名
    String TableName { get; set; }      // 数据库表名
    String? ConnName { get; set; }      // 连接名
    DatabaseType DbType { get; set; }   // 数据库类型
    List<IDataColumn> Columns { get; }  // 字段集合
    List<IDataIndex> Indexes { get; }   // 索引集合
}

// 字段结构
public interface IDataColumn
{
    String Name { get; set; }           // 属性名
    String ColumnName { get; set; }     // 数据库列名
    Type DataType { get; set; }         // .NET 类型
    String? RawType { get; set; }       // 数据库原始类型
    Boolean Identity { get; set; }      // 自增
    Boolean PrimaryKey { get; set; }    // 主键
    Int32 Length { get; set; }          // 长度
    Boolean Nullable { get; set; }      // 允许空
}

// 索引结构
public interface IDataIndex
{
    String Name { get; set; }           // 索引名
    String[] Columns { get; set; }      // 字段列表
    Boolean Unique { get; set; }        // 唯一索引
}
```

### 1.2 实体类特性与 IDataTable 的关系

代码生成器根据 IDataTable 生成实体类，在类和属性上添加特性：

```csharp
[BindTable("User", Description = "用户", ConnName = "Membership", DbType = DatabaseType.MySql)]
[BindIndex("IX_User_Name", true, "Name")]
public partial class User : Entity<User>
{
    [BindColumn("ID", Identity = true, PrimaryKey = true)]
    public Int32 ID { get; set; }
    
    [BindColumn("Name", Master = true, Length = 50)]
    public String Name { get; set; }
}
```

运行时，XCode 通过反射提取这些特性，重新构建 IDataTable，用于反向工程。

### 1.3 TableItem vs IDataTable

| 特性 | TableItem | IDataTable |
|------|-----------|------------|
| **来源** | 反射实体类特性 | 从 TableItem 构建或从数据库读取 |
| **可变性** | **不可变**（编译时固定） | **可变**（运行时可修改） |
| **用途** | 记录实体类的原始绑定 | 执行正向/反向工程 |

**设计理念**：TableItem 是"原始设定"，IDataTable 是"工作副本"。通过这种分离，实现一个实体类动态映射到不同表名/数据库的能力。

---

## 二、类型映射体系

每种数据库都有自己独立的 .NET 类型与数据库类型的双向映射规则。

### 2.1 映射表结构

```csharp
// 每个数据库驱动定义自己的 Types 映射表
protected IDictionary<Type, String[]> Types { get; set; }
```

### 2.2 各数据库映射示例

| .NET 类型 | MySQL | SQLite | SQL Server | Oracle | PostgreSQL |
|----------|-------|--------|------------|--------|------------|
| String | varchar, longtext | nvarchar, text | nvarchar, nvarchar(max) | varchar2, clob | varchar, text |
| Int32 | int | integer | int | number(10) | integer |
| Int64 | bigint | integer | bigint | number(19) | bigint |
| Boolean | tinyint(1) | boolean | bit | number(1) | boolean |
| DateTime | datetime | datetime | datetime | date | timestamp |
| Decimal | decimal | decimal | decimal | number | numeric |
| Byte[] | blob | blob | varbinary | blob | bytea |

### 2.3 RawType 的作用

`RawType` 记录字段在源数据库中的原始类型，用于跨库部署时的最佳兼容：

```
场景：实体类在 MySQL 生成，部署到不同数据库

MySQL 生产环境：DbType=MySql → 使用 RawType="varchar(50)"（精确匹配）
SQLite 测试环境：DbType≠MySql → 重新映射为 nvarchar(50)
```

---

## 三、正向工程

正向工程从数据库读取表结构，生成 IDataTable。

### 3.1 流程

```
数据库
  │
  │ GetSchema("Tables"/"Columns"/"Indexes")
  ↓
DataRow[] ──→ 解析 ──→ IDataTable
                          │
                          ├──→ DAL.Export() ──→ XML 文件
                          └──→ XCodeTool ──→ 实体类代码
```

### 3.2 核心实现

```csharp
// DbMetaData_Positive.cs
public IList<IDataTable> GetTables()
{
    // 1. 获取表列表
    var dt = GetSchema("Tables", null);
    
    // 2. 遍历每个表
    foreach (var row in dt.Rows)
    {
        var table = new XTable();
        table.TableName = row["TABLE_NAME"];
        
        // 3. 获取字段
        table.Columns = GetFields(table);
        
        // 4. 获取索引
        table.Indexes = GetIndexes(table);
    }
}
```

---

## 四、反向工程

反向工程从实体类的 IDataTable 与数据库现有结构对比，自动执行 DDL 操作。

### 4.1 流程

```
实体类特性 ──→ TableItem ──→ IDataTable (实体)
                                  │
                                  │ 对比
                                  ↓
数据库 ──→ GetTables() ──→ IDataTable (数据库)
                                  │
                                  │ 差异检测
                                  ↓
                           生成并执行 DDL
```

### 4.2 Migration 模式

| 模式 | 建表 | 加字段 | 改字段 | 删字段 | 加索引 | 删索引 | 适用场景 |
|------|------|--------|--------|--------|--------|--------|---------|
| Off | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | 生产环境（手工管理） |
| ReadOnly | 📝 | 📝 | 📝 | 📝 | 📝 | 📝 | 生产环境（审核 SQL） |
| On | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ | 默认。仅新建（安全模式） |
| Full | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | 开发环境 |

*📝 = 仅输出 SQL，不执行*

> **表级覆盖**：单表可在模型 `<Table Migration="On">`、实体特性 `[BindTable(..., Migration = "On")]` 或启动时 `User.Meta.Table.Migration = Migration.On` 指定；表级配置只能收紧、不能放大全局模式（如全局 `On`、表级 `Full` 不生效），置 `null` 恢复继承。

### 4.3 DDL 操作类型

- **数据库**：CreateDatabase、DatabaseExist
- **表**：CreateTable、DropTable、AddTableDescription
- **字段**：AddColumn、AlterColumn、DropColumn、AddColumnDescription
- **索引**：CreateIndex、DropIndex

### 4.4 与主流 ORM/数据中间件对比

#### 结构同步机制对比

| 特性 | XCode | EF Core | FreeSql | SqlSugar | Dapper |
|------|-------|---------|---------|----------|--------|
| **正向工程** | ✅ 内置 | ✅ Scaffold | ✅ DbFirst | ✅ DbFirst | ❌ 无 |
| **反向工程** | ✅ 运行时自动 | ✅ Migration 文件 | ✅ CodeFirst | ✅ CodeFirst | ❌ 无 |
| **迁移文件** | 不需要 | 需要 | 可选 | 可选 | - |
| **运行时对比** | ✅ 自动 | ❌ 需显式调用 | ✅ 可配置 | ✅ 可配置 | - |
| **差异检测** | 字段级 | 模型级 | 字段级 | 字段级 | - |
| **回滚支持** | ❌ | ✅ | ❌ | ❌ | - |

#### 跨数据库能力对比

| 特性 | XCode | EF Core | FreeSql | SqlSugar | Dapper |
|------|-------|---------|---------|----------|--------|
| **支持数据库数** | 10+ | 10+ | 15+ | 10+ | 任意 |
| **类型自动映射** | ✅ | ✅ | ✅ | ✅ | ❌ 手写 |
| **同一模型跨库** | ✅ 原生 | 需切换 Provider | ✅ | ✅ | 手写 |
| **RawType 保留** | ✅ | ❌ | ❌ | ❌ | - |

#### 分表分库能力对比

| 特性 | XCode | EF Core | FreeSql | SqlSugar | ShardingSphere |
|------|-------|---------|---------|----------|----------------|
| **分表支持** | ✅ 原生 | 需第三方 | ✅ 原生 | ✅ 原生 | ✅ 原生 |
| **分库支持** | ✅ 原生 | 需多 Context | ✅ 原生 | ✅ 原生 | ✅ 原生 |
| **运行时切换** | ✅ 任意时刻 | 需重建 Context | ✅ | ✅ | ✅ |
| **自动建表** | ✅ | ❌ | ✅ | ✅ | ❌ |
| **时间分表策略** | ✅ 内置 | ❌ | ✅ 内置 | ✅ 内置 | ✅ 配置 |

#### 设计理念差异

| 框架 | 定位 | 结构管理理念 | 国产 |
|------|------|-------------|------|
| **XCode** | 数据中间件 | 运行时自动同步，零迁移文件，动态灵活 | ✅ 原创 |
| **EF Core** | 重量级 ORM | 迁移文件版本管理，历史可追溯，团队协作 | ❌ |
| **FreeSql** | 轻量级 ORM | 兼顾自动同步与迁移文件，灵活可选 | ✅ |
| **SqlSugar** | 轻量级 ORM | 自动同步为主，简单易用 | ✅ |
| **Dapper** | 微型 ORM | 不管理结构，专注 SQL 执行 | ❌ |
| **ShardingSphere** | 分布式中间件 | 专注分片路由，不管理表结构 | ✅ |

#### XCode 的独特优势

1. **国产原创**：完全由国人开发，未引用任何第三方组件，自主可控
2. **IDataTable 中间层**：统一的模型抽象，衔接数据库、XML、实体类
3. **RawType 机制**：保留原始数据库类型，跨库部署时精确匹配
4. **运行时完全可变**：表名、连接名、字段信息均可动态修改
5. **正反向一体**：同一套 IDataTable 体系支撑正向和反向工程
6. **兼容 Dapper 用法**：支持任意 SQL 映射到任意实体，同时具备缓存、读写分离、埋点能力

---

## 五、动态分表分库

XCode 的一大亮点是支持运行时动态修改连接名、表名、字段信息，这是实现分表分库的基础。

### 5.1 设计原理

```
TableItem (不可变)          IDataTable (可变)
     │                           │
     │ 编译时固定                │ 运行时可修改
     │ TableName="Log"          │ TableName="Log_202602"
     │ ConnName="Main"          │ ConnName="Shard_01"
     │                           │
     └──────────────→ 构建 ──────┘
                                 │
                                 ↓
                          反向工程使用修改后的值
```

### 5.2 分表示例

```csharp
// 按月分表
var tableName = $"Log_{DateTime.Now:yyyyMM}";  // Log_202602

// 修改 IDataTable
var dt = TableItem.Create(typeof(Log)).DataTable;
dt.TableName = tableName;

// 确保表存在（按全局 Migration 模式，表级配置可进一步收紧）
DAL.Create("Main").SetTables(dt);
```

### 5.3 分库示例

```csharp
// 根据用户 ID 选择分库
var shardIndex = userId % 4;
var connName = $"Shard_{shardIndex:D2}";  // Shard_00 ~ Shard_03

// 切换连接
User.Meta.ConnName = connName;
```

### 5.4 内置分表策略

XCode 提供了 `TimeShardPolicy` 等内置分表策略，自动处理表名计算和表结构创建。

---

## 六、核心文件

| 文件 | 说明 |
|-----|------|
| `DataAccessLayer\Model\IDataTable.cs` | 表结构接口（核心） |
| `DataAccessLayer\Model\IDataColumn.cs` | 字段结构接口 |
| `DataAccessLayer\Model\IDataIndex.cs` | 索引结构接口 |
| `DataAccessLayer\MetaData\DbMetaData_Positive.cs` | 正向工程 |
| `DataAccessLayer\MetaData\DbMetaData_Negative.cs` | 反向工程 |
| `DataAccessLayer\Model\ModelHelper.cs` | XML 序列化 |
| `Configuration\TableItem.cs` | 表元数据（不可变） |
| `Shards\TimeShardPolicy.cs` | 时间分表策略 |

---

## 七、总结

### 核心要点

1. **国产原创**：完全由国人开发，未引用任何第三方组件，自主可控
2. **IDataTable 是核心**：不是 XML，IDataTable 才是衔接数据库、实体类、XML 的中枢
3. **双向映射**：每种数据库有独立的类型映射规则，RawType 保证跨库兼容
4. **运行时可变**：TableItem 不可变记录原始设定，IDataTable 可变支持动态调整
5. **零迁移文件**：不同于 EF 的迁移模式，XCode 运行时自动对比同步
6. **动态分表分库**：运行时修改表名/连接名，是 XCode 的核心亮点
7. **兼容 Dapper**：支持任意 SQL 映射，同时具备缓存、读写分离、链路追踪能力

### 框架定位差异

| 框架 | 核心理念 | 国产 |
|------|---------|------|
| **XCode** | 零配置、运行时灵活、跨库部署、动态分表分库 | ✅ 原创 |
| **EF Core** | 迁移历史可追溯、团队协作、版本管理 | ❌ |
| **FreeSql/SqlSugar** | 轻量易用、自动同步、国产生态 | ✅ |
| **Dapper** | 极简、手写 SQL、高性能 | ❌ |

---

**文档版本**：v2.0  
**更新日期**：2026-02-02  
**适用版本**：XCode 11.x+

