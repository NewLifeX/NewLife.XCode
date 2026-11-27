# Membership 权限体系实体指南

`XCode.Membership` 提供了开箱即用的用户/角色/菜单/部门/租户等权限类实体，是构建多租户管理系统的基础层。本文说明各实体的设计意图、核心字段与扩展点。

> **范围提示：** 本文覆盖 `XCode.Membership` 目录下未被 csproj 排除的实体（用户/角色/菜单/地区/字典参数/日志/租户/部门等）。`访问统计`、`用户在线`、`用户统计` 已在 csproj 中排除，不在讨论范围。

---

## 1. 实体体系概览

```
Tenant（租户）
  └── User（用户）    ← IUser / IAuthUser / IIdentity
        ├── Role（角色）  ← IRole
        ├── 部门（Department）
        └── 日志（Log）

字典参数（Parameter）← 系统配置热更新
地区（Area）         ← 行政区划树形
菜单（Menu）         ← 权限树
```

---

## 2. User（用户）

继承 `LogEntity<User>`，同时实现 `IUser`、`IAuthUser`、`IIdentity`、`IDataScope`、`IFieldScope`。

### 核心字段

| 字段 | 说明 |
|------|------|
| `Name` | 登录账号（唯一） |
| `DisplayName` | 显示名 |
| `Password` | 密码（加密存储） |
| `RoleId` | 主角色 |
| `RoleIds` | 多角色 ID（逗号分隔） |
| `DepartmentId` | 部门 |
| `TenantId` | 所属租户 |
| `Enable` | 是否启用 |
| `Logins` | 累计登录次数（AdditionalField 累加） |

### 内置过滤器

静态构造函数中自动注册（`Add` 继续可用，内部进入过滤器管道）：

```csharp
Meta.Filters.Add<UserFilter>();
Meta.Filters.Add<TimeFilter>();
Meta.Filters.Add<IPFilter>();
```

### 初始化数据

首次建表时自动插入 `admin` 管理员账号（`InitData`）。

### 常用查询

```csharp
// 按账号查
var user = User.FindByName("admin");

// 登录验证（IAuthUser）
var ok = user.Authenticate(password);

// 当前登录用户（通过 ManageProvider）
var me = ManageProvider.Provider.Current as User;
```

### 数据权限

`User` 实现了 `IDataScopeFieldProvider`，可自定义数据权限过滤的字段名（如 `UserId` 映射到不同业务字段）。

---

## 3. Role（角色）

继承 `LogEntity<Role>`，实现 `IRole`、`ITenantScope`。

### 核心字段

| 字段 | 说明 |
|------|------|
| `Name` | 角色名 |
| `IsSystem` | 是否系统角色（系统角色不受数据权限约束） |
| `Type` | 角色类型：系统/普通/租户 |
| `DataScope` | 数据权限范围（全部/本部门及下级/本部门/仅本人/自定义） |
| `ViewSensitive` | 是否可查看敏感字段 |
| `Permission` | 菜单权限字符串（压缩编码） |

### 初始化数据

```
管理员  IsSystem=true  DataScope=全部
高级用户            DataScope=本部门及下级
普通用户            DataScope=本部门
游客                DataScope=仅本人
```

### 权限校验

```csharp
var role = Role.FindByKey(roleId);
var hasPermission = role.Has(menu.ID, PermissionFlags.Detail);
```

---

## 4. Menu（菜单）

实现 `IMenu`，树形结构存储，是权限控制的基础单元。

### 核心字段

| 字段 | 说明 |
|------|------|
| `Name` | 唯一名称（路由标识） |
| `DisplayName` | 显示名 |
| `Url` | 对应 URL（支持通配符） |
| `ParentId` | 父菜单 |
| `Sort` | 排序 |
| `Visible` | 是否显示在导航中 |
| `Permission` | 允许的权限操作位 |

### 典型使用

```csharp
// 按名称或路径查菜单
var menu = Menu.FindByFullName("Admin/User");

// 获取当前用户有权访问的菜单树
var menus = User.Current?.GetMenus();
```

---

## 5. 部门（Department）

树形结构，支持数据权限的部门范围过滤。

### 核心字段

| 字段 | 说明 |
|------|------|
| `ParentId` | 父部门 ID（0 为根） |
| `ManagerId` | 部门负责人用户 ID |
| `Enable` | 是否启用 |

在 `DataScopeContext` 里，部门树被用于计算"本部门及下级"的可访问部门 ID 列表。

---

## 6. Tenant（租户）

`XCode` 内置 SAAS 多租户支持，实现 `ITenantScope`。

### 核心字段

| 字段 | 说明 |
|------|------|
| `Code` | 租户编码（拼音首字母自动生成） |
| `Name` | 租户名 |
| `Type` | 企业/个人等 |
| `Enable` | 是否启用 |
| `ManagerId` | 租户管理员用户 ID |
| `RoleIds` | 租户可用角色组（逗号分隔 ID） |

### 初始化数据

首次建表时自动创建"默认租户"。

### 多租户隔离

通过 `TenantFilter` 实现自动数据隔离（旧 `TenantInterceptor` 是其空壳子类，注册效果相同）：
- 查询时追加 `TenantId=xxx` 条件
- 写入时自动填充 `TenantId`

```csharp
// 设置当前租户上下文（在中间件/过滤器中）
TenantContext.Current = new TenantContext { TenantId = 1002 };
```

---

## 7. 字典参数（Parameter）

轻量的全局配置表，支持热更新。

### 核心字段

| 字段 | 说明 |
|------|------|
| `Category` | 分类（命名空间） |
| `Name` | 参数名 |
| `Value` | 参数值 |
| `Kind` | 类型：文本/整数/浮点/布尔/JSON 等 |
| `Enable` | 是否启用 |

### 典型用法

```csharp
// 读取参数
var val = Parameter.GetValue("系统配置", "MaxRetry");

// 写入参数
Parameter.SetValue("系统配置", "MaxRetry", "5");
```

适用于不想重启服务就能生效的运行时配置项。

---

## 8. 地区（Area）

中国行政区划树形数据，每条记录含省/市/县三级。

```csharp
// 按代码查
var prov = Area.FindByCode(110000);

// 查下级
var cities = Area.GetChildren(110000);
```

初始化数据可从 `Doc/Area.csv` 批量导入。

---

## 9. 日志（Log）

继承 `LogEntity<Log>`，用于记录操作日志。

```csharp
LogProvider.Provider.WriteLog(typeof(User), "登录", true, "管理员登录成功", 0, "admin");
```

通过 `ILogProvider` 接口接入，可替换为写到数据库、文件或远程日志系统。

---

## 10. 扩展实体类

业务系统通常需要在 `User` / `Role` 上扩展字段：

1. 建独立的 `AppUser` 实体，持有 `UserId` 关联，1:1 扩展。

```xml
<Table Name="AppUser" Description="应用用户扩展">
  <Columns>
    <Column Name="Id" DataType="Int32" PrimaryKey="True" Identity="True" Description="编号" />
    <Column Name="UserId" DataType="Int32" Map="User@Id@Name" Description="用户" />
    <Column Name="Score" DataType="Int32" Description="积分" />
    ...
  </Columns>
</Table>
```

2. 不建议直接修改 `Membership` 中已有实体的 `.Biz.cs` 文件，以免与官方更新冲突。

---

## 11. 常见问题

- **初始化数据不触发**：确保至少一次调用 `InitData`（首次建表时自动触发），或手动调用 `User.Meta.Session.InitData()`。
- **管理员无法登录**：执行 `Role.CheckRole()` 修复菜单权限。
- **多租户数据混用**：检查 `TenantFilter` 是否正确注册（旧 `TenantInterceptor` 与它等价），以及请求链路中 `TenantContext.Current` 是否按租户设置。
- **字典参数改了不生效**：`Parameter` 默认启用实体缓存，可调用 `Parameter.Meta.Cache.Clear()` 强制刷新。
