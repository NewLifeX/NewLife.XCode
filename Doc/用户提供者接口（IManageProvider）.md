# 用户提供者接口（IManageProvider）

`IManageProvider` 是 XCode Membership 的**认证与用户访问统一门面**，无论是 Web 请求、后台任务还是 API 接口，都通过它获取当前用户、执行登录/注销、校验权限。

---

## 1. 全局访问入口

`ManageProvider` 抽象基类持有进程级单例：

```csharp
// 取当前登录用户（适用于任何线程，ASP.NET 场景下从 HttpContext 取）
var user = ManageProvider.User;

// 访问当前用户主机 IP（由框架在请求中填充）
var ip = ManageProvider.UserHost;

// 当前正在访问的菜单（由 Cube 中间件填充）
var menu = ManageProvider.CurrentMenu;

// 通过接口实例访问
var provider = ManageProvider.Provider;
```

---

## 2. 接口成员速查

```csharp
public interface IManageProvider
{
    // 当前登录用户（赋值 null 等价于注销）
    IManageUser? Current { get; set; }

    // 获取 / 设置当前用户（带 IServiceProvider 上下文，用于依赖注入场景）
    IManageUser? GetCurrent(IServiceProvider context);
    void SetCurrent(IManageUser? user, IServiceProvider context);

    // 密码算法（默认 SaltPasswordProvider，BCrypt 风格）
    IPasswordProvider PasswordProvider { get; set; }

    // 用户查找
    IManageUser? FindByID(Object userid);
    IManageUser? FindByName(String name);

    // 认证
    IManageUser Login(String username, String password, Boolean rememberme = false);
    void Logout();
    IManageUser Register(String username, String password, Int32 roleid = 0, Boolean enable = false);
    IManageUser ChangePassword(String username, String newPassword, String oldPassword);

    // 权限检查
    Boolean Has(IMenu menu, params PermissionFlags[] flags);

    // 多租户
    IList<ITenantUser> GetTenants();

    // 服务定位
    TService GetService<TService>();
}
```

---

## 3. 典型用法

### 3.1 获取当前用户

```csharp
var user = ManageProvider.User as User;
if (user == null) throw new InvalidOperationException("未登录");

var deptId = user.DepartmentID;
```

### 3.2 登录

```csharp
try
{
    var user = ManageProvider.Provider.Login(username, password, rememberMe);
    // 登录成功，user != null
}
catch (Exception ex)
{
    // 登录失败：用户不存在 / 密码错误 / 账号禁用
}
```

### 3.3 权限检查

```csharp
var menu = ManageProvider.Menu.FindByFullName("admin/order/list");
var allowed = ManageProvider.Provider.Has(menu, PermissionFlags.Detail);
if (!allowed) throw new NoPermissionException();
```

### 3.4 在后台服务中手动设置用户上下文

```csharp
// 任务队列中使用系统账号执行操作
var systemUser = User.FindByName("system");
ManageProvider.Provider.Current = systemUser;
try
{
    // ...业务逻辑
}
finally
{
    ManageProvider.Provider.Current = null;
}
```

---

## 4. 注册自定义提供者

XCode 内置 `ManageProvider` 抽象基类，需要应用层实现 `GetCurrent` / `SetCurrent`：

```csharp
public class AppManageProvider : ManageProvider
{
    public override IManageUser? GetCurrent(IServiceProvider? context = null)
    {
        // 从 JWT 声明或 Session 读取当前用户
        var httpContext = context?.GetService<IHttpContextAccessor>()?.HttpContext;
        var userId = httpContext?.User?.FindFirst("userId")?.Value?.ToInt();
        return userId > 0 ? FindByID(userId) : null;
    }

    public override void SetCurrent(IManageUser? user, IServiceProvider? context = null)
    {
        // 若不需要持久化 Session，可留空
    }
}

// 注册
ManageProvider.Provider = new AppManageProvider();
```

宿主框架（如 NewLife.Cube）通常已提供实现并自动注册，此类应用不需要手动设置 `ManageProvider.Provider`。

---

## 5. 线程上下文字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `ManageProvider.User` | `IUser?` | 当前用户（`AsyncLocal`/`AsyncLocal`） |
| `ManageProvider.UserHost` | `String` | 请求来源 IP（由中间件填充） |
| `ManageProvider.CurrentMenu` | `IMenu?` | 当前访问菜单（由 Cube 路由中间件填充） |

---

## 6. 关联阅读

- `/xcode/membership_guide`（Membership 全套实体体系）
- `/xcode/data_scope`（DataScopeInterceptor 中通过 Provider 获取当前用户）
- `/xcode/membership_rbac`（用户、角色、菜单 RBAC 完整介绍）
