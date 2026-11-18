# 查询条件表达式（WhereExpression）

XCode 的查询条件表达式以 C# **运算符重载**模拟 SQL 语法，让 WHERE 子句编写既安全（防注入）又直观（无需拼接字符串）。

> **适用范围**：本文的 `实体._.字段` 写法属于数据层（`*.Biz.cs`/生成代码）。控制器、服务、任务等应用层不要直接拼表达式，请调用实体的 `Search`/`FindAllBy*`；需要字段名或排序时直接用字符串字段名（字段名以 Model.xml 为准）。
>
> `NewLife.XCode` 主包已内置 `XC0001` 分析器（无需额外包），引用主包即自动检测跨程序集使用 `Entity._`。默认 Warning，可在 `.editorconfig` 中调整：
>
> ```ini
> dotnet_diagnostic.XC0001.severity = error
> ```

---

## 1. 字段比较操作符

通过 `实体类._.字段名` 取到 `FieldItem`，再用运算符或方法构造条件：

```csharp
// 等于 / 不等于（Field 子类专属运算符）
User._.Role == 1
User._.Role != 0

// 比较
User._.Age > 18
User._.Age < 60
User._.Score >= 90.0
User._.CreateTime <= DateTime.Today

// 等价方法（FieldItem 通用）
User._.Role.Equal(1)
User._.Role.NotEqual(0)
```

---

## 2. 字符串匹配

```csharp
// 前缀匹配 → LIKE 'abc%'
User._.Name.StartsWith("abc")

// 后缀匹配 → LIKE '%abc'
User._.Name.EndsWith("abc")

// 包含 → LIKE '%abc%'
User._.Name.Contains("abc")

// 不包含 → NOT LIKE '%abc%'
User._.Name.NotContains("abc")
```

---

## 3. IN / NOT IN

### 3.1 集合参数

```csharp
var ids = new[] { 1, 2, 3 };
User._.Id.In(ids)          // Id IN (1,2,3)
User._.Id.NotIn(ids)       // Id NOT IN (1,2,3)
```

### 3.2 子查询字符串

```csharp
User._.DeptId.In("SELECT Id FROM Department WHERE Active=1")
```

### 3.3 子查询 SelectBuilder

```csharp
var sub = DAL.Create("db").CreateBuilder("SELECT Id FROM Department WHERE Active=1");
User._.DeptId.In(sub)
```

---

## 4. NULL / 空值检查

```csharp
User._.Avatar.IsNull()          // Avatar IS NULL
User._.Avatar.NotIsNull()       // NOT Avatar IS NULL

User._.Remark.IsNullOrEmpty()   // Remark IS NULL OR Remark = ''
User._.Remark.NotIsNullOrEmpty()
```

---

## 5. 三值布尔

```csharp
// flag 为 null 时不生成条件，适合搜索界面"全部/是/否"下拉
Boolean? activeFilter = true;
User._.IsActive.IsTrue(activeFilter)   // IsActive = 1
User._.IsActive.IsFalse(activeFilter)  // IsActive = 0（flag=false 时）
// activeFilter = null → 不附加条件
```

---

## 6. 组合：AND / OR

使用 `&`（AND）和 `|`（OR）组合多个条件：

```csharp
// AND：同时满足
var where = User._.Role == 1 & User._.Age >= 18;

// OR：满足其中之一
var where = User._.Status == 1 | User._.Status == 2;

// 混合（OR 自动加括号）
var where = User._.Age > 18 & (User._.Role == 1 | User._.Role == 2);
```

**`|` 组成的子表达式会被自动括号包裹**，避免优先级歧义。

---

## 7. 条件累积模式

推荐在搜索场景中累积条件，避免复杂的三目运算嵌套：

```csharp
var exp = new WhereExpression();

if (!keyword.IsNullOrEmpty())
    exp &= User._.Name.Contains(keyword);

if (roleId > 0)
    exp &= User._.RoleId == roleId;

if (startDate > DateTime.MinValue && endDate > DateTime.MinValue)
    exp &= User._.CreateTime.Between(startDate, endDate);

var list = User.FindAll(exp, page);
```

`exp` 为空时，`FindAll` 等价于无 WHERE 条件（全表查询），不会报错。

---

## 8. 直接字符串表达式

有时需要传入原始 SQL 片段（已确认安全时）：

```csharp
var where = new Expression("Status IN (1,2,3)");
var full = User._.Age > 18 & where;
```

> 直接字符串绕过参数化，仅适用于枚举字面量等**不含用户输入**的静态值。

---

## 9. 表达式转 SQL 字符串

调试时可直接将表达式转换为字符串查看：

```csharp
var exp = User._.Age > 18 & User._.Role == 1;
Console.WriteLine(exp.ToString());
// → Age>18 And RoleID=1
```

---

## 10. 参数化查询

当需要参数化（防注入）时，传入 `IDictionary<String, Object>` 给 `GetString`：

```csharp
var ps = new Dictionary<String, Object?>();
var sql = exp.GetString(db, ps);
// sql:    "Age>@Age And RoleID=@RoleID"
// ps:     { "Age": 18, "RoleID": 1 }
```

XCode 框架在执行 `FindAll` 时会自动处理参数化，通常不需要手动调用 `GetString`。

---

## 11. 关联阅读

- `/xcode/field_extension`（时间区间扩展：`Between`、`Today`、`ThisMonth` 等）
- `/xcode/find`（`FindAll` / `FindCount` 使用表达式的方法签名）
- `/xcode/sql_builder`（排序字段安全构造）
- `/xcode/select_insert_builder`（SelectBuilder 内部如何消费 WhereExpression）
