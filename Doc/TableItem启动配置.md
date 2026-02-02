# TableItem 启动配置

## 概述

`TableItem` 是 XCode 实体类的元数据，记录了从特性中读取的连接名和表名。

`TableItem.ConnName` 和 `TableItem.TableName` 支持在启动时修改，用于全局永久配置，区别于运行时的临时动态修改。

XCode 支持两种修改方式：

| 修改方式 | 作用范围 | 使用场景 | 修改方法 |
|---------|---------|---------|---------|
| **临时修改** | 线程级/调用级 | 动态分表分库、读写分离、**多租户** | `Meta.ConnName`、`Meta.TableName` |
| **启动配置** | 全局永久 | 表前缀、分库策略、**单租户部署** | `User.Meta.Table.ConnName`、`User.Meta.Table.TableName` |

---

## 快速对比：启动配置 vs 临时修改

### 启动配置（全局永久）

```csharp
// 启动时配置，全局生效
User.Meta.Table.ConnName = "Tenant_A";

// 后续所有请求都使用 Tenant_A 数据库
var user1 = User.FindByID(1);  // 使用 Tenant_A
var user2 = User.FindByID(2);  // 使用 Tenant_A
```

**适用场景**：整个应用只使用一个数据库（如：单租户部署、固定分库）

### 临时修改（线程级）

```csharp
// 请求A：租户A的数据库
User.Meta.ConnName = "Tenant_A";
var userA = User.FindByID(1);  // 使用 Tenant_A

// 请求B：租户B的数据库
User.Meta.ConnName = "Tenant_B";
var userB = User.FindByID(2);  // 使用 Tenant_B
```

**适用场景**：每个请求使用不同的数据库（如：多租户、动态分表）

---

## 快速开始

### 访问 TableItem

**推荐方式**：通过实体类的 `Meta.Table` 访问

```csharp
// ✅ 推荐：通过 Meta.Table 访问
User.Meta.Table.ConnName = "NewConnection";
Order.Meta.Table.TableName = "sys_order";

// ⚠️ 不推荐：直接创建 TableItem（需要知道类型）
var ti = TableItem.Create(typeof(User), null);
ti.ConnName = "NewConnection";
```

### 典型场景

#### 场景1：表前缀

```csharp
// 启动时为所有表添加统一前缀
User.Meta.Table.TableName = $"sys_{User.Meta.TableName}";
Order.Meta.Table.TableName = $"sys_{Order.Meta.TableName}";
```

#### 场景2：分库策略

```csharp
// 启动时按业务模块分配到不同数据库
User.Meta.Table.ConnName = "UserDB";
Order.Meta.Table.ConnName = "OrderDB";
Product.Meta.Table.ConnName = "ProductDB";
Log.Meta.Table.ConnName = "LogDB";
```

#### 场景3：单租户部署

```csharp
// 启动时指定单个租户的数据库（整个应用只服务一个租户）
var tenantId = Configuration["TenantId"];  // 从配置读取固定租户ID
User.Meta.Table.ConnName = $"Tenant_{tenantId}";
Order.Meta.Table.ConnName = $"Tenant_{tenantId}";
Product.Meta.Table.ConnName = $"Tenant_{tenantId}";
```

---

## 设计理念

### 架构图

```
┌─────────────────────────────────────────────────────┐
│                    TableItem                        │
│  ┌──────────────┐                                   │
│  │ ConnName     │ ←── 默认从特性读取，可在启动时修改   │
│  │ TableName    │     (使用 C# 14 field 关键字)     │
│  └──────┬───────┘                                   │
│         │                                            │
│         │ 自动同步                                    │
│         ↓                                            │
│  ┌──────────────┐                                   │
│  │  DataTable   │ ←── 自动同步 TableItem 的值        │
│  │  .ConnName   │                                    │
│  │  .TableName  │                                    │
│  └──────────────┘                                   │
└─────────────────────────────────────────────────────┘
```

### 核心原则

- `TableItem.ConnName/TableName`：默认从特性读取，可在启动时修改（全局生效）
- 修改 `TableItem` 属性时，会自动同步到 `DataTable`
- XCode 内部直接使用 `TableItem` 的值

### 实现细节（C# 14 field 关键字）

```csharp
public class TableItem
{
    /// <summary>连接名。来自实体类特性，可在启动时修改用于全局分表分库</summary>
    public String ConnName { get => field ??= _Table?.ConnName ?? ""; set; }
    
    /// <summary>表名。来自实体类特性，可在启动时修改用于全局分表分库</summary>
    public String TableName { get => field ??= _Table?.Name ?? EntityType.Name; set; }
}
```

**说明**：
- 使用 C# 14 的 `field` 关键字，自动生成后备字段
- getter：首次访问时从 `_Table` 特性读取并缓存
- setter：修改时会自动同步到 `DataTable`

---

## 使用指南

### 启动配置（全局永久）

#### ASP.NET Core 示例

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // 读取配置
        var config = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        
        // 配置表前缀
        var prefix = config["TablePrefix"];
        if (!String.IsNullOrEmpty(prefix))
        {
            User.Meta.Table.TableName = $"{prefix}_{User.Meta.TableName}";
            Order.Meta.Table.TableName = $"{prefix}_{Order.Meta.TableName}";
        }
        
        // 配置分库策略
        if (config.GetValue<Boolean>("UseSharding"))
        {
            User.Meta.Table.ConnName = "UserDB";
            Order.Meta.Table.ConnName = "OrderDB";
            Product.Meta.Table.ConnName = "ProductDB";
        }
        
        // 单租户部署：整个应用只服务一个租户
        var tenantId = config["TenantId"];
        if (!String.IsNullOrEmpty(tenantId))
        {
            var connName = $"Tenant_{tenantId}";
            User.Meta.Table.ConnName = connName;
            Order.Meta.Table.ConnName = connName;
            Product.Meta.Table.ConnName = connName;
        }
    }
}
```

#### 批量配置扩展方法

```csharp
public static class TableConfigExtensions
{
    /// <summary>批量配置连接名</summary>
    public static void SetConnName(this Type[] types, String connName)
    {
        foreach (var type in types)
        {
            var meta = EntityFactory.CreateOperate(type);
            meta.Table.ConnName = connName;
        }
    }
    
    /// <summary>批量添加表前缀</summary>
    public static void AddTablePrefix(this Type[] types, String prefix)
    {
        foreach (var type in types)
        {
            var meta = EntityFactory.CreateOperate(type);
            var table = meta.Table;
            table.TableName = $"{prefix}_{table.TableName}";
        }
    }
}

// 使用示例
var types = new[] { typeof(User), typeof(Order), typeof(Product) };
types.SetConnName("TenantA_DB");
types.AddTablePrefix("sys");
```

### 动态分表分库（临时修改）

```csharp
// 临时修改，仅影响当前操作
using (var entity = User.Meta.CreateEntity())
{
    entity.Meta.ConnName = "Shard_01";  // 临时修改
    entity.Insert();
}
// 操作完成后，其他地方仍使用 User.Meta.Table.ConnName
```

#### 多租户场景（推荐使用临时修改）

```csharp
// 在 Web 请求中根据当前租户动态切换数据库
public class OrderController : ControllerBase
{
    [HttpGet]
    public IActionResult GetOrders()
    {
        // 从请求头或上下文获取当前租户ID
        var tenantId = HttpContext.Items["TenantId"]?.ToString();
        var connName = $"Tenant_{tenantId}";
        
        // 临时修改连接名，仅影响当前请求
        Order.Meta.ConnName = connName;
        
        // 查询数据（使用租户数据库）
        var orders = Order.FindAll();
        
        return Ok(orders);
    }
}
```

#### 使用中间件统一处理多租户

```csharp
public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    
    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        // 从请求头获取租户ID
        var tenantId = context.Request.Headers["X-Tenant-ID"].FirstOrDefault();
        
        if (!String.IsNullOrEmpty(tenantId))
        {
            // 设置到上下文，供后续使用
            context.Items["TenantId"] = tenantId;
            
            // 临时修改连接名
            var connName = $"Tenant_{tenantId}";
            User.Meta.ConnName = connName;
            Order.Meta.ConnName = connName;
            Product.Meta.ConnName = connName;
        }
        
        await _next(context);
    }
}

// Startup.cs
public void Configure(IApplicationBuilder app)
{
    app.UseMiddleware<TenantMiddleware>();
}
```

---

## 使用场景详解

### 场景1：表前缀场景（启动配置）

```csharp
public class TablePrefixConfig
{
    public static void Configure(String prefix)
    {
        // 为所有表添加统一前缀
        User.Meta.Table.TableName = $"{prefix}_{User.Meta.TableName}";
        Order.Meta.Table.TableName = $"{prefix}_{Order.Meta.TableName}";
        Product.Meta.Table.TableName = $"{prefix}_{Product.Meta.TableName}";
        
        DAL.WriteLog("已为所有表配置前缀：{0}", prefix);
    }
}
```

### 场景2：分库策略（启动配置）

```csharp
public class ShardingConfig
{
    public static void Configure()
    {
        // 按业务模块分配到不同数据库
        User.Meta.Table.ConnName = "UserDB";
        Order.Meta.Table.ConnName = "OrderDB";
        Product.Meta.Table.ConnName = "ProductDB";
        Log.Meta.Table.ConnName = "LogDB";
        
        DAL.WriteLog("已配置分库策略");
    }
}
```

### 场景3：单租户部署（启动配置）

```csharp
public class SingleTenantConfig
{
    public static void Configure(String tenantId)
    {
        // 整个应用只服务一个租户，启动时永久指定租户数据库
        var connName = $"Tenant_{tenantId}";
        
        User.Meta.Table.ConnName = connName;
        Order.Meta.Table.ConnName = connName;
        Product.Meta.Table.ConnName = connName;
        
        DAL.WriteLog("已为单租户 {0} 配置数据库连接：{1}", tenantId, connName);
    }
}
```

### 场景4：多租户场景（临时修改，推荐）

```csharp
public class MultiTenantService
{
    public List<Order> GetOrders(String tenantId)
    {
        // 根据租户ID临时切换数据库
        var connName = $"Tenant_{tenantId}";
        Order.Meta.ConnName = connName;
        
        // 查询数据（使用租户数据库）
        return Order.FindAll();
    }
}

// 使用中间件统一处理
public class TenantMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = context.Request.Headers["X-Tenant-ID"].FirstOrDefault();
        
        if (!String.IsNullOrEmpty(tenantId))
        {
            var connName = $"Tenant_{tenantId}";
            User.Meta.ConnName = connName;
            Order.Meta.ConnName = connName;
            Product.Meta.ConnName = connName;
        }
        
        await _next(context);
    }
}
```

---

## 注意事项

### 1. 调用时机

**必须在首次访问实体类之前配置**，建议在应用启动时（`Startup.ConfigureServices` 或 `Program.Main` 开始处）完成配置。

```csharp
// ✅ 正确：启动时配置
public static void Main(String[] args)
{
    // 先配置
    User.Meta.Table.ConnName = "NewConnection";
    
    // 后使用
    var user = User.FindByID(1);
}

// ❌ 错误：已经使用后再配置（可能不生效）
public static void Main(String[] args)
{
    var user = User.FindByID(1);  // 已经使用了默认连接
    User.Meta.Table.ConnName = "NewConnection";  // 太晚了
}
```

### 2. 线程安全

属性 setter 不是线程安全的，应在启动配置阶段完成，避免并发修改。

### 3. 自动同步

修改 `TableItem.ConnName/TableName` 会自动同步到 `DataTable`，确保反向工程使用正确的表名。

### 4. 优先级

动态修改（`Meta.ConnName`）优先级更高，会覆盖启动配置的值。

---

## 常见问题

### Q1：如何获取原始值？

A：通过 `Meta.ConnName` 和 `Meta.TableName` 获取（这两个属性来自 `BindTableAttribute` 特性）。

```csharp
// 获取原始值（从特性读取）
var originalConn = User.Meta.ConnName;
var originalTable = User.Meta.TableName;

// 修改
User.Meta.Table.ConnName = "NewConnection";

// 恢复原始值
User.Meta.Table.ConnName = originalConn;
```

### Q2：如何检查是否被修改过？

A：对比 `Meta.Table.ConnName` 和 `Meta.ConnName`。

```csharp
if (User.Meta.Table.ConnName != User.Meta.ConnName)
{
    Console.WriteLine($"连接名已被修改：{User.Meta.ConnName} => {User.Meta.Table.ConnName}");
}
```

### Q3：启动配置和动态修改哪个优先级高？

A：动态修改（`Meta.ConnName`）优先级更高。

```csharp
// 启动配置
User.Meta.Table.ConnName = "DefaultDB";

// 运行时动态修改（优先级更高）
using (var entity = User.Meta.CreateEntity())
{
    entity.Meta.ConnName = "TempDB";  // 覆盖启动配置
    entity.Insert();
}
```

### Q4：是否会影响反向工程？

A：会。修改 `TableItem.ConnName/TableName` 会自动同步到 `DataTable`，反向工程会使用新的表名。

```csharp
// 修改 TableItem，自动影响反向工程
User.Meta.Table.TableName = "new_table";
// DataTable.TableName 也会自动更新为 "new_table"
```

### Q5：如何批量配置多个实体？

A：使用扩展方法或循环：

```csharp
// 方式1：扩展方法（见上文）
var types = new[] { typeof(User), typeof(Order), typeof(Product) };
types.SetConnName("TenantA_DB");

// 方式2：循环
foreach (var type in types)
{
    var meta = EntityFactory.CreateOperate(type);
    meta.Table.ConnName = "TenantA_DB";
}
```

### Q6：为什么使用 `User.Meta.Table` 而不是 `TableItem.Create()`？

A：
- `User.Meta.Table`：简洁直观，编译时检查，智能提示
- `TableItem.Create(typeof(User), null)`：需要知道类型，容易出错

```csharp
// ✅ 推荐
User.Meta.Table.ConnName = "NewConnection";

// ⚠️ 不推荐
TableItem.Create(typeof(User), null).ConnName = "NewConnection";
```

---

## 实现原理

### 属性定义（C# 14 field 关键字）

```csharp
public class TableItem
{
    private readonly BindTableAttribute _Table;
    
    /// <summary>连接名。来自实体类特性，可在启动时修改用于全局分表分库</summary>
    public String ConnName
    {
        get => field ??= _Table?.ConnName ?? "";
        set
        {
            field = value;
            // 同步到 DataTable
            if (DataTable != null) DataTable.ConnName = value;
        }
    }
    
    /// <summary>表名。来自实体类特性，可在启动时修改用于全局分表分库</summary>
    public String TableName
    {
        get => field ??= _Table?.Name ?? EntityType.Name;
        set
        {
            field = value;
            // 同步到 DataTable
            if (DataTable != null) DataTable.TableName = value;
        }
    }
}
```

**关键点**：
- `field` 是 C# 14 引入的关键字，指向编译器自动生成的后备字段
- getter：`field ??=` 实现了延迟初始化（首次访问时从特性读取并缓存）
- setter：赋值到 `field` 并自动同步到 `DataTable`

### 初始化流程

```
1. 首次访问 User.Meta.Table.ConnName
   ↓
2. 触发 getter：field ??= _Table?.ConnName ?? ""
   ↓
3. 从 _Table 特性读取默认值并缓存到 field
   ↓
4. 返回值
   
5. 启动时修改：User.Meta.Table.ConnName = "NewConnection"
   ↓
6. 触发 setter：field = value
   ↓
7. 自动同步到 DataTable.ConnName
   ↓
8. 后续访问直接使用修改后的值
```

### 为什么这样设计？

**实用性优先**：XCode 中有 **83 处** `TableName` 使用和 **90 处** `ConnName` 使用。如果让 TableItem 不可变，需要逐个修改为使用 `DataTable`，工作量巨大且容易遗漏。

**最小改动**：直接让 `TableItem.ConnName/TableName` 可修改，现有代码无需改动。

---

## 单元测试示例

```csharp
using System;
using Xunit;
using XCode;

namespace XCode.Tests.Configuration;

public class TableItemConfigTests
{
    [Fact(DisplayName = "设置连接名")]
    public void SetConnName_ShouldModifyConnName()
    {
        // Arrange
        var originalConn = User.Meta.ConnName;
        
        // Act
        User.Meta.Table.ConnName = "NewConnection";
        
        // Assert
        Assert.Equal("NewConnection", User.Meta.Table.ConnName);
        Assert.NotEqual(originalConn, User.Meta.Table.ConnName);
        
        // Cleanup
        User.Meta.Table.ConnName = originalConn;
    }
    
    [Fact(DisplayName = "设置表名")]
    public void SetTableName_ShouldModifyTableName()
    {
        // Arrange
        var originalTable = User.Meta.TableName;
        
        // Act
        User.Meta.Table.TableName = "new_table";
        
        // Assert
        Assert.Equal("new_table", User.Meta.Table.TableName);
        Assert.NotEqual(originalTable, User.Meta.Table.TableName);
        
        // Cleanup
        User.Meta.Table.TableName = originalTable;
    }
    
    [Fact(DisplayName = "批量配置连接名")]
    public void BatchSetConnName_ShouldWork()
    {
        // Arrange
        var types = new[] { typeof(User), typeof(Order), typeof(Product) };
        var connName = "TenantA_DB";
        
        // Act
        foreach (var type in types)
        {
            var meta = EntityFactory.CreateOperate(type);
            meta.Table.ConnName = connName;
        }
        
        // Assert
        Assert.Equal(connName, User.Meta.Table.ConnName);
        Assert.Equal(connName, Order.Meta.Table.ConnName);
        Assert.Equal(connName, Product.Meta.Table.ConnName);
    }
}
```

---

## 总结

### 核心要点

1. **访问方式**：使用 `User.Meta.Table` 而不是 `TableItem.Create()`
2. **调用时机**：启动时配置，首次访问实体类之前
3. **自动同步**：修改 `TableItem` 时自动同步到 `DataTable`
4. **实用性优先**：让 `TableItem` 属性可修改，避免修改 173 处现有代码
5. **C# 14 特性**：使用 `field` 关键字实现延迟初始化和缓存

### 适用场景

- ✅ 表前缀：统一为所有表添加前缀
- ✅ 分库：按业务模块分配到不同数据库
- ✅ 单租户部署：整个应用只服务一个租户
- ✅ 环境切换：开发/测试/生产使用不同配置
- ✅ 数据库迁移：整体切换到新数据库

### 不适用场景

- ❌ 多租户（应使用 `Meta.ConnName` 临时修改）
- ❌ 运行时频繁切换（应使用 `Meta.ConnName` 动态修改）
- ❌ 按数据动态分表（应使用 `TimeShardPolicy` 等分表策略）

---

**文档版本**：v5.0（最终版）  
**更新日期**：2026-02-02  
**适用版本**：XCode 11.x+  
**C# 版本**：14.0+
