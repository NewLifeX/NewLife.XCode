# 内置实体过滤器（Time/User/IP/Trace）

XCode 提供 4 个开箱即用的 `EntityFilter` 实现，通过**字段名约定**自动识别激活，无需手动赋值即可完成创建时间、操作者、来源 IP、链路追踪 ID 的填充。

> 自 v12.4 起，这些内置项统一进入 `IEntityFilter` 管道。推荐直接注册过滤器：`Meta.Filters.Add<TimeFilter>()`；同名 `*Interceptor` 空壳类已标记 `[Obsolete]`，只为旧实体静态构造函数保留编译兼容，运行一次 `xcode` 即可迁移。

---

## 1. 一览

| 过滤器类 | 兼容壳类（过时） | 自动填充字段 | 填充时机 |
|---------|--------------|-------------|---------|
| `TimeFilter` | `TimeInterceptor` | `CreateTime` / `UpdateTime` | Insert / Update |
| `UserFilter` | `UserInterceptor` | `CreateUserID` / `CreateUser` / `UpdateUserID` / `UpdateUser` | Insert / Update |
| `IPFilter` | `IPInterceptor` | `CreateIP` / `UpdateIP` | Insert / Update |
| `TraceFilter` | `TraceInterceptor` | `TraceId` | Insert / Update |

---

## 2. 注册方式

在实体类的静态构造函数中注册：

```csharp
static Order()
{
    Meta.Filters.Add<TimeFilter>();
    Meta.Filters.Add<UserFilter>();
    Meta.Filters.Add<IPFilter>();
    Meta.Filters.Add<TraceFilter>();
}
```

或在应用启动时全局注册，一次覆盖所有实体：

```csharp
EntityFilters.Global.Add<TimeFilter>();
EntityFilters.Global.Add<UserFilter>();
EntityFilters.Global.Add<IPFilter>();
EntityFilters.Global.Add<TraceFilter>();
```

> 每个过滤器的 `Init` 方法会自动检查当前实体是否含有对应字段，不含则跳过，不影响无关实体。

---

## 3. TimeFilter（时间自动填充）

**匹配字段**（`DateTime` 类型，字段名匹配任意一个即激活）：
- `CreateTime`：Insert 时写入当前时间
- `UpdateTime`：Insert 或 Update 时写入当前时间（使用 `TimerX.Now` 高精度时钟）

```csharp
// Update 时仅 UpdateTime 更新，CreateTime 保持不变
// 使用 SetNoDirtyItem：不触发脏标记，不影响其他字段的 Update 判断
```

---

## 4. UserFilter（操作者自动填充）

**匹配字段**（字段名匹配任意一个即激活）：
- `CreateUserID` / `CreateUser`：Insert 时填充
- `UpdateUserID` / `UpdateUser`：Insert 和 Update 时填充

**字段类型**：
- `Int32` / `Int64` → 填充 `ManageProvider.User?.ID`
- `String` → 填充 `ManageProvider.User?.Name`

**关键属性**：
- `AllowEmpty`（默认 `false`）：无登录用户时，是否允许覆盖为空值。生产环境建议保持 `false`，防止已记录的更新人被意外清空

自定义用户提供者：

```csharp
Meta.Filters.Add(new UserFilter(myProvider));
```

---

## 5. IPFilter（来源 IP 自动填充）

**匹配字段**（`String` 类型，字段名匹配即激活）：
- `CreateIP`：Insert 时填充当前请求 IP
- `UpdateIP`：Insert 和 Update 时填充

**取值逻辑**：
1. 优先使用 `ManageProvider.UserHost`（HTTP 请求的客户端 IP）
2. 若为空且是 Insert，改用 `NetHelper.MyIP()`（本机 IP）
3. 自动清理 `://` 协议前缀，保留纯 IP 字符串

**字段约定**：仅按名称匹配 `CreateIP` / `UpdateIP`，其他命名的字段不会被自动填充；如需处理自定义字段，可在业务代码中手动赋值。

---

## 6. TraceFilter（链路追踪 ID）

**匹配字段**（`String` 类型，字段名为 `TraceId`）：
- Insert 和 Update 时填充 `DefaultSpan.Current?.TraceId`

**AllowMerge 模式**（默认 `false`）：

```csharp
Meta.Filters.Add(new TraceFilter { AllowMerge = true });
```

`AllowMerge=true` 时，同一记录被多个不同 Trace 修改，所有 TraceId 以逗号连接存储，便于在 APM 看板中还原完整调用链。字段长度不足时，自动删除最旧的 TraceId。

---

## 7. 字段命名约定对照

只要实体表含有以下字段名（不区分大小写），过滤器即自动激活：

```
CreateTime    UpdateTime
CreateUserID  UpdateUserID
CreateUser    UpdateUser
CreateIP      UpdateIP
TraceId
```

代码生成器（XCodeTool）在 `Model.xml` 中生成标准字段时，会自动生成这些名称的字段。

---

## 8. 关联阅读

- `/xcode/entity_interceptor`（旧拦截器兼容说明）
- `/xcode/data_scope`（数据权限拦截器）
- `/xcode/membership_guide`（ManageProvider 提供者接口）
