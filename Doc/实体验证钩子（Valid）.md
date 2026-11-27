# 实体验证钩子（Valid/ValidOperate）

`Valid()` 是 XCode 实体生命周期中**保存前验证**的核心钩子。框架在每次 Insert/Update/Delete 前自动调用，用于数据合法性校验、字段补全、业务规则检查。

---

## 1. 两个重载

| 方法 | 触发时机 | 推荐程度 |
|------|---------|---------|
| `void Valid(Boolean isNew)` | Insert/Update；`isNew=true` 表示新增 | 旧版，仍支持 |
| `Boolean Valid(DataMethod method)` | Insert/Update/Delete；返回 `false` 阻止操作 | ✅ 推荐重写 |

框架调用顺序：过滤器前置 `OnExecuting` → `Valid(DataMethod)` → 内部调用 `Valid(Boolean)`（Insert/Update）→ 检查字符串字段长度 → 雪花 Id → 持久化 → 过滤器后置 `OnExecuted`。

`Valid(DataMethod)` 只做业务与字段校验，不再调用拦截器链；旧拦截器已适配为过滤器，统一由操作管道执行，避免在过滤器内部再次触发过滤器。不再提供 `ValidData` 等第三个公开重载。

---

## 2. 基本重写示例

代码生成器在 `.Biz.cs` 中的典型模板：

```csharp
public override Boolean Valid(DataMethod method)
{
    // 删除不做验证
    if (method == DataMethod.Delete) return true;

    // 没有脏数据则无需处理
    if (!HasDirty) return true;

    // ---- 必填字段校验 ----
    if (Name.IsNullOrEmpty())
        throw new ArgumentNullException(__.Name, "名称不能为空！");
    if (RoleID <= 0)
        throw new ArgumentNullException(__.RoleID, "请选择角色！");

    // ---- 新增时特殊逻辑 ----
    if (method == DataMethod.Insert)
    {
        if (CreateTime.Year < 2000) CreateTime = DateTime.Now;
    }

    // ---- 修改时特殊逻辑 ----
    if (method == DataMethod.Update)
    {
        UpdateTime = DateTime.Now;
    }

    return base.Valid(method);   // 调用基类：字符串长度检查 + 雪花Id
}
```

---

## 3. 常见用法场景

### 3.1 必填字段校验

```csharp
if (Title.IsNullOrEmpty())
    throw new ArgumentNullException(nameof(Title), "标题不能为空！");
```

### 3.2 自动填充默认值

```csharp
if (method == DataMethod.Insert && Code.IsNullOrEmpty())
    Code = Guid.NewGuid().ToString("N");
```

### 3.3 Hash/加密密码

```csharp
var pass = Password;
if (method == DataMethod.Insert && !pass.IsNullOrEmpty() && pass.Length != 32)
    Password = pass.MD5();
```

### 3.4 唯一性校验

```csharp
// 校验名称唯一
var exist = FindByName(Name);
if (exist != null && exist.ID != ID)
    throw new ArgumentException($"名称 [{Name}] 已存在！", nameof(Name));
```

### 3.5 新增时禁止设置某字段

```csharp
if (method == DataMethod.Insert)
    UpdateUserID = 0;   // 新增时不允许设置更新人
```

---

## 4. 阻止操作（返回 false）

```csharp
public override Boolean Valid(DataMethod method)
{
    // 锁定期间禁止删除
    if (method == DataMethod.Delete && IsLocked)
    {
        LogProvider.Provider.WriteLog(this, "删除", false, "记录已锁定，禁止删除");
        return false;   // 返回 false 阻止操作（不抛异常，安静失败）
    }

    return base.Valid(method);
}
```

---

## 5. 跳过验证

某些场景（如批量导入、数据迁移）需要跳过验证：

```csharp
// 方式1：SaveWithoutValid() — 完全跳过 Valid
list.ForEach(e => e.SaveWithoutValid());

// 方式2：批量插入时直接 Insert(null, session) — 绕过实体级 Valid
list.Insert();
```

---

## 6. 执行顺序

```
InsertAsync / Insert / Update / Delete 调用
  ↓
过滤器前置 OnExecuting（含旧拦截器适配的 Valid）
  ↓
Valid(DataMethod)         ← 用户重写，业务校验
  ├─ Valid(Boolean)        ← 旧版兼容调用
  └─ 字符串字段长度检查     ← 基类自动
  ↓
AutoFillSnowIdPrimaryKey() ← 主键自动生成
  ↓
执行 SQL (OnInsert/OnUpdate/OnDelete)
  ↓
过滤器后置 OnExecuted
```

---

## 关联阅读

- `/xcode/entity_interceptor`（拦截器在 Valid 之前运行）
- `/xcode/additional_fields`（累加字段在 Update 时计算差值）
- `/xcode/dirty_collection`（HasDirty 与脏数据）
- `/xcode/initdata_detail`（InitData 与 Valid 的调用时序不同）
