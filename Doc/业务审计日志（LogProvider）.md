# 业务审计日志（LogProvider/LogEntity）

XCode Membership 内置**业务审计日志**机制，将增删改操作自动记录到数据库，提供字段级变更追踪、用户溯源和操作链接。

---

## 1. 核心组件

| 类 | 职责 |
|---|---|
| `LogProvider` | 日志写入门面，自动填充用户信息，构建变更摘要 |
| `LogEntity<T>` | 实体基类，重写 Insert/Update/Delete 后自动调用 `LogProvider` |
| `Log`（Membership 实体） | 日志数据库表，存储所有操作记录 |

---

## 2. LogProvider 用法

### 2.1 全局访问点

```csharp
LogProvider.Provider.WriteLog(typeof(User), "登录", true, "管理员登录成功");
```

`LogProvider.Provider` 是静态单例，Cube 在启动时可替换为子类实现。

### 2.2 主要方法

| 方法 | 说明 |
|------|------|
| `WriteLog(type, action, success, remark)` | 以类型为分类写日志 |
| `WriteLog(category, action, success, remark)` | 以字符串为分类写日志 |
| `WriteLog(action, entity, error?)` | **实体对象日志**，自动构建字段摘要（见下） |
| `CreateLog(…)` | 构造但不写入，适合批量 / 延迟写 |
| `AsLog(category)` | 转换为标准 `ILog` 接口，接入 NewLife 日志体系 |

### 2.3 实体对象日志（WriteLog 字段级追踪）

```csharp
// 在 Valid() 或重写方法中调用
LogProvider.Provider.WriteLog("修改", this);
// 输出示例：Name=旧名 -> 新名,Age=25 -> 26
```

- **"添加"/"删除"**：记录所有非空、非零字段
- **"修改"**：对比 `Dirtys`（脏数据字典，记录旧值）和当前值，输出 `字段=旧值 -> 新值`
- **密码字段**（名称含 `pass`/`password`）：自动脱敏，记录为空值

### 2.4 自动填充用户信息

`CreateLog` 内部会调用：

```csharp
var user = ManageProvider.Provider?.Current;
if (user != null)
{
    log.CreateUserID = user.ID;
    log.UserName     = user.ToString();
}
log.CreateIP = ManageProvider.UserHost;
```

无需手动传入用户，Cube 环境下自动来自当前请求上下文。

---

## 3. LogEntity 基类

继承 `LogEntity<TEntity>` 让实体**自动在每次 Insert/Update/Delete 时写日志**，无需任何额外代码：

```csharp
[DisplayName("产品")]
[BindTable("Product", Description = "产品")]
public class Product : LogEntity<Product>
{
    // 无需任何额外代码，Insert/Update/Delete 均自动写日志
}
```

实现原理（`LogEntity` 内部）：

```csharp
public override Int32 Insert()
{
    var err = "";
    try   { return base.Insert(); }
    catch (Exception ex) { err = ex.Message; throw; }
    finally { LogProvider.Provider.WriteLog("添加", this, err); }
}

public override Int32 Update()
{
    // 必须在 base.Update() 前记录，因为保存后脏数据会清空
    if (HasDirty) LogProvider.Provider.WriteLog("修改", this);
    return base.Update();
}
```

> ⚠️ `Update()` 中**必须在保存前**调用 `WriteLog`，因修改后脏字典会清空，日志将记不到旧值。

---

## 4. 手动写日志示例

### Valid() 中的操作审计

```csharp
public override Boolean Valid(DataMethod method)
{
    if (method == DataMethod.Delete)
    {
        // 记录删除前的实体数据
        LogProvider.Provider.WriteLog("删除", this);
        return true;
    }
    return base.Valid(method);
}
```

### 自定义业务操作

```csharp
// 审批操作
LogProvider.Provider.WriteLog(typeof(Order), "审批", true,
    $"订单 {OrderNo} 审批通过，金额 {Amount} 元");
```

### 与标准日志接口互通

```csharp
// 获取 ILog 实例写不带实体上下文的消息
var log = LogProvider.Provider.AsLog("系统管理");
log.Info("启动 重置缓存 操作");
```

---

## 5. Enable 开关

```csharp
// 全局禁用（如压测/迁移场景）
LogProvider.Provider.Enable = false;
```

---

## 关联阅读

- `/xcode/membership_guide`（Membership 实体用法总览）
- `/xcode/manage_provider`（用户上下文、`ManageProvider.Provider.Current`）
- `/xcode/entity_valid`（Valid 中触发日志的典型位置）
