# 内置实体拦截器（Time/User/IP/Trace）

XCode 提供 4 个开箱即用的 `EntityInterceptor` 实现，通过**字段名约定**自动识别激活，无需手动赋值即可完成创建时间、操作者、来源 IP、链路追踪 ID 的填充。

---

## 1. 一览

| 拦截器类 | 旧名（已弃用） | 自动填充字段 | 填充时机 |
|---------|--------------|-------------|---------|
| `TimeInterceptor` | `TimeModule` | `CreateTime` / `UpdateTime` | Insert / Update |
| `UserInterceptor` | `UserModule` | `CreateUserID` / `CreateUser` / `UpdateUserID` / `UpdateUser` | Insert / Update |
| `IPInterceptor` | `IPModule` | `CreateIP` / `UpdateIP` | Insert / Update |
| `TraceInterceptor` | `TraceModule` | `TraceId` | Insert / Update |

---

## 2. 注册方式

在实体类的静态构造函数中注册：

```csharp
static Order()
{
    Meta.Interceptors.Add<TimeInterceptor>();
    Meta.Interceptors.Add<UserInterceptor>();
    Meta.Interceptors.Add<IPInterceptor>();
    Meta.Interceptors.Add<TraceInterceptor>();
}
```

或在应用启动时全局注册，一次覆盖所有实体：

```csharp
EntityInterceptors.Global.Add<TimeInterceptor>();
EntityInterceptors.Global.Add<UserInterceptor>();
EntityInterceptors.Global.Add<IPInterceptor>();
EntityInterceptors.Global.Add<TraceInterceptor>();
```

> 每个拦截器的 `Init` 方法会自动检查当前实体是否含有对应字段，不含则跳过，不影响无关实体。

---

## 3. TimeInterceptor / TimeModule（时间自动填充）

**匹配字段**（`DateTime` 类型，字段名匹配任意一个即激活）：
- `CreateTime`：Insert 时写入当前时间
- `UpdateTime`：Insert 或 Update 时写入当前时间（使用 `TimerX.Now` 高精度时钟）

```csharp
// Update 时仅 UpdateTime 更新，CreateTime 保持不变
// 使用 SetNoDirtyItem：不触发脏标记，不影响其他字段的 Update 判断
```

---

## 4. UserInterceptor / UserModule（操作者自动填充）

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
Meta.Interceptors.Add(new UserInterceptor(myProvider));
```

---

## 5. IPInterceptor / IPModule（来源 IP 自动填充）

**匹配字段**（`String` 类型，字段名匹配即激活）：
- `CreateIP`：Insert 时填充当前请求 IP
- `UpdateIP`：Insert 和 Update 时填充

**取值逻辑**：
1. 优先使用 `ManageProvider.UserHost`（HTTP 请求的客户端 IP）
2. 若为空且是 Insert，改用 `NetHelper.MyIP()`（本机 IP）
3. 自动清理 `://` 协议前缀，保留纯 IP 字符串

**字段约定**：仅按名称匹配 `CreateIP` / `UpdateIP`，其他命名的字段不会被自动填充；如需处理自定义字段，可在业务代码中手动赋值。

---

## 6. TraceInterceptor / TraceModule（链路追踪 ID）

**匹配字段**（`String` 类型，字段名为 `TraceId`）：
- Insert 和 Update 时填充 `DefaultSpan.Current?.TraceId`

**AllowMerge 模式**（默认 `false`）：

```csharp
Meta.Interceptors.Add(new TraceInterceptor { AllowMerge = true });
```

`AllowMerge=true` 时，同一记录被多个不同 Trace 修改，所有 TraceId 以逗号连接存储，便于在 APM 看板中还原完整调用链。字段长度不足时，自动删除最旧的 TraceId。

---

## 7. 字段命名约定对照

只要实体表含有以下字段名（不区分大小写），拦截器即自动激活：

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

- `/xcode/entity_interceptor`（拦截器框架原理）
- `/xcode/data_scope`（数据权限拦截器）
- `/xcode/membership_guide`（ManageProvider 提供者接口）
