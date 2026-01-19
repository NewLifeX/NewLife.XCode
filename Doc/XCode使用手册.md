# XCode 使用手册

XCode 是新生命团队开发的 .NET 数据中间件，提供 ORM、数据建模、代码生成等功能，支持多种主流数据库。

---

## 目录

1. [快速入门](#1-快速入门)
2. [数据建模](#2-数据建模)
3. [实体类操作](#3-实体类操作)
4. [高级查询](#4-高级查询)
5. [缓存机制](#5-缓存机制)
6. [分表分库](#6-分表分库)
7. [事务处理](#7-事务处理)
8. [实体拦截器](#8-实体拦截器)
9. [数据库支持](#9-数据库支持)
10. [常见问题](#10-常见问题)

---

## 1. 快速入门

### 1.1 安装

```powershell
# 安装 XCode 包
dotnet add package NewLife.XCode

# 安装代码生成工具
dotnet tool install xcodetool -g

# 安装项目模板
dotnet new install NewLife.Templates
```

### 1.2 创建项目

```powershell
# 创建数据类库项目
dotnet new xcode -n MyApp.Data

# 创建 Web 管理后台
dotnet new cube -n MyAppWeb

# 创建控制台应用
dotnet new nconsole -n MyAppConsole
```

### 1.3 配置数据库连接

在 `appsettings.json` 中配置：

```json
{
  "ConnectionStrings": {
    "Default": "Server=.;Database=MyApp;Uid=sa;Pwd=xxx"
  }
}
```

---

## 2. 数据建模

### 2.1 Model.xml 文件结构

```xml
<?xml version="1.0" encoding="utf-8"?>
<EntityModel xmlns:xs="http://www.w3.org/2001/XMLSchema-instance" 
             xs:schemaLocation="https://newlifex.com https://newlifex.com/Model202509.xsd" 
             xmlns="https://newlifex.com/Model202509.xsd">
  <Option>
    <!-- 全局配置 -->
  </Option>
  <Tables>
    <Table>
      <Columns>
        <Column />
      </Columns>
      <Indexes>
        <Index />
      </Indexes>
    </Table>
  </Tables>
</EntityModel>
```

### 2.2 Option 全局配置

| 配置项 | 说明 | 示例 |
|--------|------|------|
| `Namespace` | 命名空间 | `MyApp.Data` |
| `ConnName` | 数据库连接名 | `Default` |
| `Output` | 实体类输出目录 | `.\` |
| `BaseClass` | 实体基类 | `Entity` |
| `ChineseFileName` | 使用中文文件名 | `True` |
| `Nullable` | 生成可空引用类型 | `True` |
| `HasIModel` | 实现 IModel 接口 | `True` |
| `ModelClass` | 模型类模板 | `{name}Model` |
| `ModelsOutput` | 模型类输出目录 | `.\Models\` |
| `ModelInterface` | 模型接口模板 | `I{name}` |
| `InterfacesOutput` | 接口输出目录 | `.\Interfaces\` |
| `NameFormat` | 命名格式 | `Default`/`Upper`/`Lower`/`Underline` |
| `DisplayName` | 魔方区域显示名 | `订单管理` |
| `CubeOutput` | 魔方控制器输出目录 | `../../Web/Areas/Order` |

### 2.3 Table 表属性

| 属性 | 说明 | 示例 |
|------|------|------|
| `Name` | 实体类名 | `User` |
| `TableName` | 数据库表名（可选） | `sys_user` |
| `Description` | 表说明 | `用户。用户账号信息` |
| `ConnName` | 独立连接名 | `Log` |
| `BaseType` | 基类 | `EntityBase` |
| `InsertOnly` | 仅插入模式 | `True` |
| `IsView` | 视图标识 | `True` |

### 2.4 Column 字段属性

#### 基础属性

| 属性 | 说明 | 示例 |
|------|------|------|
| `Name` | 属性名 | `UserName` |
| `ColumnName` | 数据库列名 | `user_name` |
| `DataType` | 数据类型 | `Int32`/`Int64`/`String`/`DateTime`/`Boolean`/`Double`/`Decimal` |
| `Description` | 字段说明 | `用户名。登录账号` |
| `Length` | 字符串长度 | `50`/`200`/`-1`（大文本） |
| `Precision` | 数值精度 | `18` |
| `Scale` | 小数位数 | `2` |

#### 主键与标识

| 属性 | 说明 | 示例 |
|------|------|------|
| `PrimaryKey` | 主键 | `True` |
| `Identity` | 自增标识 | `True` |
| `Master` | 主字段 | `True` |

#### 约束与默认值

| 属性 | 说明 | 示例 |
|------|------|------|
| `Nullable` | 允许空 | `False` |
| `DefaultValue` | 默认值 | `0`/`''`/`getdate()` |

#### 映射关系（Map）

格式：`表名@主键@显示字段@属性名`

```xml
<!-- 基本映射（三段） -->
<Column Name="RoleId" DataType="Int32" Map="Role@Id@Name" Description="角色" />

<!-- 指定属性名（四段） -->
<Column Name="RoleId" DataType="Int32" Map="Role@Id@Name@RoleName" Description="角色" />

<!-- 完整命名空间 -->
<Column Name="AreaId" DataType="Int32" Map="XCode.Membership.Area@Id@Path@AreaPath" Description="地区" />
```

#### 元素类型（ItemType）

用于魔方前端渲染：

| ItemType | 说明 |
|----------|------|
| `image` | 图片上传 |
| `file` | 文件上传 |
| `mail` | 邮箱格式 |
| `mobile` | 手机号格式 |
| `url` | URL 链接 |
| `TimeSpan` | 时间间隔（秒转可读格式） |
| `GMK` | 字节数转 GB/MB/KB |
| `html` | HTML 富文本 |
| `code` | 代码编辑器 |
| `json` | JSON 编辑器 |

#### 显示选项（ShowIn）

控制字段在魔方各区域的显示，五区三态：List、Detail、AddForm、EditForm、Search。

**语法一：具名列表（推荐）**
```xml
<Column Name="Mail" ShowIn="List,Search" />           <!-- List和Search显示 -->
<Column Name="Remark" ShowIn="-EditForm,-Detail" />   <!-- 编辑表单和详情隐藏 -->
<Column Name="Status" ShowIn="All,-Detail" />         <!-- 全部显示，详情隐藏 -->
```

区域别名：
- `List` / `L` - 列表页
- `Detail` / `D` - 详情页
- `AddForm` / `Add` / `A` - 添加表单
- `EditForm` / `Edit` / `E` - 编辑表单
- `Search` / `S` - 搜索区
- `Form` / `F` - 同时控制 Add 和 Edit

**语法二：管道分隔**
```xml
<Column Name="Mobile" ShowIn="Y|Y|A|A|N" />  <!-- List|Detail|Add|Edit|Search -->
```
- `Y` = 显示
- `N` = 隐藏
- `A` = 自动

**语法三：5字符掩码**
```xml
<Column Name="Code" ShowIn="11110" />  <!-- 1=显示, 0=隐藏, A/?/-=自动 -->
```

#### 分表字段（DataScale）

| 值 | 说明 |
|----|------|
| `time` | 大数据单表的时间字段（用于雪花 ID） |
| `timeShard:yyMMdd` | 分表字段，按日期格式分表 |

#### 其他属性

| 属性 | 说明 | 示例 |
|------|------|------|
| `Type` | 枚举类型 | `XCode.Membership.SexKinds` |
| `Category` | 表单分组 | `登录信息` |
| `Attribute` | 额外特性 | `XmlIgnore, IgnoreDataMember` |
| `Model` | 是否包含在模型类中 | `False` |

### 2.5 Index 索引属性

| 属性 | 说明 | 示例 |
|------|------|------|
| `Columns` | 索引列（逗号分隔） | `Name`/`Category,CreateTime` |
| `Unique` | 唯一索引 | `True` |

### 2.6 完整示例

```xml
<?xml version="1.0" encoding="utf-8"?>
<EntityModel xmlns:xs="http://www.w3.org/2001/XMLSchema-instance" 
             xs:schemaLocation="https://newlifex.com https://newlifex.com/Model202509.xsd" 
             xmlns="https://newlifex.com/Model202509.xsd">
  <Option>
    <Namespace>Order.Data</Namespace>
    <ConnName>Order</ConnName>
    <Output>.\</Output>
    <ChineseFileName>True</ChineseFileName>
    <Nullable>True</Nullable>
    <HasIModel>True</HasIModel>
  </Option>
  <Tables>
    <Table Name="Order" Description="订单。电商订单主表">
      <Columns>
        <Column Name="Id" DataType="Int64" PrimaryKey="True" DataScale="time" Description="编号" />
        <Column Name="OrderNo" DataType="String" Master="True" Length="50" Nullable="False" Description="订单号" />
        <Column Name="UserId" DataType="Int32" Map="User@Id@Name" Description="用户" />
        <Column Name="Status" DataType="Int32" Type="Order.Data.OrderStatus" Description="状态" />
        <Column Name="TotalAmount" DataType="Decimal" Precision="18" Scale="2" Description="总金额" />
        <Column Name="Remark" DataType="String" Length="500" Description="备注" Category="扩展" />
        <Column Name="CreateUser" DataType="String" Description="创建者" Model="False" Category="扩展" />
        <Column Name="CreateTime" DataType="DateTime" Nullable="False" Description="创建时间" Category="扩展" />
        <Column Name="UpdateTime" DataType="DateTime" Description="更新时间" Model="False" Category="扩展" />
      </Columns>
      <Indexes>
        <Index Columns="OrderNo" Unique="True" />
        <Index Columns="UserId" />
        <Index Columns="Status,CreateTime" />
      </Indexes>
    </Table>
  </Tables>
</EntityModel>
```

### 2.7 生成代码

在 Model.xml 所在目录执行：

```powershell
xcode
```

生成内容：
- 实体类（`*.cs`）
- 模型类（如配置了 `ModelClass`）
- 接口（如配置了 `ModelInterface`）
- 数据字典（`*.htm`）
- 魔方控制器（如配置了 `CubeOutput`）

---

## 3. 实体类操作

### 3.1 新增

```csharp
// 方式一：直接插入
var entity = new User { Name = "test", Password = "123456" };
entity.Insert();

// 方式二：异步插入
await entity.InsertAsync();

// 方式三：批量插入
var list = new List<User> { user1, user2, user3 };
list.Insert();
```

### 3.2 查询

```csharp
// 按主键查询
var user = User.FindByKey(1);
var user = User.FindByKey(1, "Id,Name");  // 指定返回字段

// 按条件查询单个
var user = User.Find(User._.Name == "test");
var user = User.Find(User._.Status == 1 & User._.Enable == true);

// 查询列表
var list = User.FindAll();
var list = User.FindAll(User._.Status == 1);
var list = User.FindAll(User._.Status == 1, User._.Id.Desc(), null, 0, 10);

// 异步查询
var user = await User.FindAsync(User._.Id == 1);
var list = await User.FindAllAsync(User._.Status == 1);
```

### 3.3 更新

```csharp
// 方式一：修改后更新
var user = User.FindByKey(1);
user.Name = "newName";
user.Update();

// 方式二：异步更新
await user.UpdateAsync();

// 方式三：批量更新
User.Update(User._.Status == 2, User._.Status == 1);  // SET Status=2 WHERE Status=1
```

### 3.4 删除

```csharp
// 方式一：删除实体
var user = User.FindByKey(1);
user.Delete();

// 方式二：异步删除
await user.DeleteAsync();

// 方式三：批量删除
User.Delete(User._.Status == 0);
User.Delete(User._.CreateTime < DateTime.Today.AddYears(-1));
```

### 3.5 保存

```csharp
// Save 自动判断 Insert 或 Update
var entity = new User { Name = "test" };
entity.Save();  // 新实体执行 Insert

entity.Name = "test2";
entity.Save();  // 已存在执行 Update

// 异步保存（用于日志等高频写入）
await entity.SaveAsync();
```

### 3.6 存在性检查

```csharp
// 检查是否存在
if (user.Exist("Name"))
{
    // 同名用户已存在
}

// 检查并抛出异常
user.CheckExist("Name");  // 存在则抛出 ArgumentException

// 检查是否存在（区分新增/更新）
if (user.Exist(isNew: true, "Name", "Mobile"))
{
    // 新增时，名称或手机号已存在
}
```

---

## 4. 高级查询

### 4.1 条件表达式

```csharp
// 字段表达式
var exp = User._.Status == 1;                    // Status = 1
var exp = User._.Status != 0;                    // Status <> 0
var exp = User._.Age > 18;                       // Age > 18
var exp = User._.Age >= 18 & User._.Age <= 60;   // Age >= 18 AND Age <= 60
var exp = User._.Name.Contains("test");          // Name LIKE '%test%'
var exp = User._.Name.StartsWith("test");        // Name LIKE 'test%'
var exp = User._.Name.EndsWith("test");          // Name LIKE '%test'
var exp = User._.Status.In(1, 2, 3);             // Status IN (1, 2, 3)
var exp = User._.Status.NotIn(0, -1);            // Status NOT IN (0, -1)
var exp = User._.CreateTime.Between(start, end); // CreateTime >= start AND CreateTime < end
var exp = User._.Name.IsNullOrEmpty();           // Name IS NULL OR Name = ''
```

### 4.2 组合条件

```csharp
// AND 组合
var where = new WhereExpression();
where &= User._.Status == 1;
where &= User._.Enable == true;
if (!key.IsNullOrEmpty())
    where &= User._.Name.Contains(key) | User._.Mobile.Contains(key);

// OR 组合
var exp = User._.Status == 1 | User._.Status == 2;
```

### 4.3 分页查询

```csharp
// 使用 PageParameter
var page = new PageParameter 
{ 
    PageIndex = 1, 
    PageSize = 20,
    Sort = "Id",
    Desc = true
};
var list = User.FindAll(User._.Status == 1, page);
var total = page.TotalCount;  // 总记录数

// 直接指定分页参数
var list = User.FindAll(User._.Status == 1, "Id desc", null, 0, 20);
```

### 4.4 统计查询

```csharp
// 记录数
var count = User.FindCount();
var count = User.FindCount(User._.Status == 1);

// 最大值/最小值
var maxId = User.FindMax(User._.Id, null);
var minId = User.FindMin(User._.Id, User._.Status == 1);

// 异步统计
var count = await User.FindCountAsync(User._.Status == 1);
```

### 4.5 Search 扩展方法

实体类通常会生成 `Search` 方法用于高级搜索：

```csharp
// 基础搜索（关键字）
var list = User.Search(key, page);

// 时间范围搜索
var list = User.Search(startTime, endTime, key, page);
```

### 4.6 关键字搜索

```csharp
// 在多个字段中搜索关键字
var where = User.SearchWhereByKey(key);  // 自动在主字段和String字段中搜索

// 指定搜索字段
var fields = new[] { User._.Name, User._.Mobile, User._.Mail };
var where = User.SearchWhereByKey(key, fields);

// 多关键字搜索（空格分隔）
var where = User.SearchWhereByKeys("张三 北京");
```

---

## 5. 缓存机制

### 5.1 实体缓存

适用于数据量小、变化少的表（如角色、部门）：

```csharp
// 获取全部缓存数据
var list = User.FindAllWithCache();

// 缓存配置
User.Meta.Session.Cache.Expire = 60;  // 缓存过期时间（秒）
User.Meta.Session.Cache.FillListMethod = () => User.FindAll();  // 自定义填充方法
```

### 5.2 单对象缓存

适用于频繁按主键查询的场景：

```csharp
// 按主键查询（自动缓存）
var user = User.FindByKeyWithCache(1);

// 按从键查询
var user = User.FindBySlaveWithCache("test@example.com");

// 缓存配置
User.Meta.Session.SingleCache.Expire = 60;
User.Meta.Session.SingleCache.MaxEntity = 10000;
```

### 5.3 直接访问缓存

```csharp
// 实体缓存
var cache = User.Meta.Cache;
var list = cache.Entities;

// 单对象缓存
var singleCache = User.Meta.SingleCache;
var user = singleCache[1];
```

---

## 6. 分表分库

### 6.1 配置分表策略

```csharp
// 按时间分表
User.Meta.ShardPolicy = new TimeShardPolicy("CreateTime", "yyMM");

// 自定义分表策略
User.Meta.ShardPolicy = new MyShardPolicy();
```

### 6.2 手动切换表

```csharp
// 临时切换到指定表
using (User.Meta.CreateSplit("Log", "User_202401"))
{
    var list = User.FindAll();
}  // 自动还原

// 根据时间切换
using (User.Meta.CreateShard(DateTime.Now))
{
    entity.Insert();
}
```

### 6.3 跨表查询

```csharp
// 自动遍历多个分表查询
var results = User.Meta.AutoShard(startTime, endTime, () => User.FindAll(where));
foreach (var list in results)
{
    // 处理每个分表的结果
}
```

---

## 7. 事务处理

### 7.1 using 方式（推荐）

```csharp
using var tran = User.Meta.CreateTrans();

var user = new User { Name = "test" };
user.Insert();

var log = new Log { Action = "新增用户" };
log.Insert();

tran.Commit();  // 不调用则自动回滚
```

### 7.2 传统方式

```csharp
User.Meta.BeginTrans();
try
{
    var user = new User { Name = "test" };
    user.Insert();
    
    User.Meta.Commit();
}
catch
{
    User.Meta.Rollback();
    throw;
}
```

---

## 8. 实体拦截器

### 8.1 内置拦截器

XCode 内置了多个实体拦截器：

| 拦截器 | 功能 |
|--------|------|
| `UserModule` | 自动填充 CreateUser/UpdateUser |
| `TimeModule` | 自动填充 CreateTime/UpdateTime |
| `IPModule` | 自动填充 CreateIP/UpdateIP |

### 8.2 自定义拦截器

```csharp
public class MyModule : EntityModule
{
    protected override Boolean OnValid(IEntity entity, DataMethod method)
    {
        if (method == DataMethod.Insert)
        {
            // 新增前处理
        }
        return base.OnValid(entity, method);
    }
}

// 注册拦截器
User.Meta.Interceptors.Add<MyModule>();
```

---

## 9. 数据库支持

### 9.1 支持的数据库

| 数据库 | NuGet 包 | 连接字符串示例 |
|--------|----------|----------------|
| SQLite | NewLife.XCode | `Data Source=data.db` |
| SQL Server | NewLife.XCode | `Server=.;Database=Test;Uid=sa;Pwd=xxx` |
| MySQL | XCode.MySql | `Server=localhost;Database=Test;Uid=root;Pwd=xxx` |
| PostgreSQL | XCode.PostgreSQL | `Host=localhost;Database=Test;Username=postgres;Password=xxx` |
| Oracle | XCode.Oracle | `Data Source=xxx;User Id=test;Password=xxx` |
| 达梦 | XCode.DaMeng | `Server=localhost;Database=Test;Uid=SYSDBA;Pwd=xxx` |
| 人大金仓 | XCode.KingBase | `Host=localhost;Database=Test;Username=system;Password=xxx` |

### 9.2 多数据库配置

```json
{
  "ConnectionStrings": {
    "Default": "Server=.;Database=Main;Uid=sa;Pwd=xxx",
    "Log": "Data Source=log.db",
    "Archive": "Server=192.168.1.100;Database=Archive;Uid=sa;Pwd=xxx"
  }
}
```

```xml
<!-- Model.xml 中指定连接名 -->
<Table Name="Log" ConnName="Log" />
```

---

## 10. 常见问题

### 10.1 如何查看生成的 SQL？

```csharp
// 开启 SQL 日志
XTrace.UseConsole();
DAL.WriteLog = true;
```

### 10.2 如何自动建表？

XCode 默认开启反向工程，首次访问实体时自动创建表。

```csharp
// 关闭自动建表
DAL.InitData = false;
```

### 10.3 如何处理大数据量？

1. 使用 `Int64` 主键 + `DataScale="time"`（雪花 ID）
2. 配置分表策略
3. 使用 `InsertOnly="True"` 优化日志表
4. 批量操作时使用事务

### 10.4 如何优化查询性能？

1. 为查询条件字段添加索引
2. 使用 `selects` 参数只查询需要的字段
3. 合理使用缓存
4. 避免在循环中查询

### 10.5 字段名和表名如何映射？

- `Name` 属性：C# 属性名
- `ColumnName` / `TableName` 属性：数据库列名/表名
- 未指定时默认使用 `Name`

---

## 附录

### A. 字段表达式运算符

| 运算符 | SQL | 示例 |
|--------|-----|------|
| `==` | `=` | `User._.Status == 1` |
| `!=` | `<>` | `User._.Status != 0` |
| `>` | `>` | `User._.Age > 18` |
| `>=` | `>=` | `User._.Age >= 18` |
| `<` | `<` | `User._.Age < 60` |
| `<=` | `<=` | `User._.Age <= 60` |
| `&` | `AND` | `exp1 & exp2` |
| `|` | `OR` | `exp1 | exp2` |

### B. 常用扩展方法

| 方法 | 说明 |
|------|------|
| `.Contains(value)` | `LIKE '%value%'` |
| `.StartsWith(value)` | `LIKE 'value%'` |
| `.EndsWith(value)` | `LIKE '%value'` |
| `.In(values)` | `IN (...)` |
| `.NotIn(values)` | `NOT IN (...)` |
| `.Between(start, end)` | `>= start AND < end` |
| `.IsNullOrEmpty()` | `IS NULL OR = ''` |
| `.Desc()` | 降序排序 |
| `.Asc()` | 升序排序 |

### C. 实体元数据

```csharp
// 获取实体类型
var type = User.Meta.ThisType;

// 获取表信息
var table = User.Meta.Table;
var tableName = User.Meta.TableName;
var connName = User.Meta.ConnName;

// 获取字段信息
var fields = User.Meta.Fields;
var allFields = User.Meta.AllFields;
var unique = User.Meta.Unique;
var master = User.Meta.Master;

// 获取会话
var session = User.Meta.Session;
var count = User.Meta.Count;
```

---

**更多信息请访问**：https://newlifex.com/xcode
