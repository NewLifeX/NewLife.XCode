# 字段元数据 FieldItem 与 ShowIn

`FieldItem` 是 XCode 查询表达式、动态筛选、聚合统计、字段映射的基础对象；`ShowInOption` 则是字段展示位控制器，决定字段在 List/Detail/Add/Edit/Search 五区的显示策略。

## 1）FieldItem 的职责

`FieldItem` 封装了字段定义、反射信息与常用表达式构造能力：

- 元信息：`Name/Type/ColumnName/Length/IsNullable/PrimaryKey/IsIdentity/Master`
- 展示信息：`DisplayName/Description/Category`
- 关系信息：`Map/OriField`
- SQL 信息：`FormatedName`

`Field` 继承 `FieldItem`，仅用于重载 `==`/`!=` 运算符，支持 `_.Name == "abc"` 这种写法。

## 2）表达式能力（高频）

### 基础比较

- `Equal / NotEqual`
- 运算符重载：`> < >= <= == !=`

### 字符串

- `StartsWith/EndsWith/Contains/NotContains`
- `IsNull/NotIsNull/IsNullOrEmpty/NotIsNullOrEmpty`

### 集合

- `In(IEnumerable)` / `NotIn(IEnumerable)`
- `In(String)` / `In(SelectBuilder)`（注意注入风险）

### 布尔

- `IsTrue(Boolean?)`
- `IsFalse(Boolean?)`

## 3）FieldExtension 增强

`FieldExtension` 提供了面向业务的扩展：

- 时间区间：`Between/Today/LastDays/ThisMonth/ThisQuarter...`
- 分组聚合：`GroupBy/Count/Sum/Avg/CountDistinct`
- 排序：`Asc/Desc/Sort`
- 文本检索：`ContainsAll/ContainsAny`
- 雪花Id区间：`Between(start,end,snow)`

建议把复杂筛选沉淀为扩展表达式，而不是在业务层拼接 SQL 字符串。

## 4）ShowIn 三态展示控制

`ShowInOption` 支持三态：

- `Show` 显示
- `Hide` 隐藏
- `Auto` 自动（默认）

覆盖区域：
- `List`
- `Detail`
- `AddForm`
- `EditForm`
- `Search`

### 支持三种语法

1. 具名列表（推荐）
   - `ShowIn="List,Search"`
   - `ShowIn="All,-Detail"`
2. 管道五段
   - `ShowIn="Y|Y|N||A"`
3. 五字符掩码
   - `ShowIn="10A?-"`

解析器会自动识别语法，未指定部分默认 `Auto`。

## 5）常见建模建议

- 业务主检索字段设 `Master=true`，利于生成器和显示层识别。
- 对需要前端特化渲染的字段设置 `ItemType`（如 `json/code/html`）。
- 对展示位有明确需求的字段配置 `ShowIn`，避免后期页面规则分散。

## 6）常见问题

- **`Contains` 报不支持？**
  - 字段类型不是 `String`。
- **`IsTrue/IsFalse` 报不支持？**
  - 字段类型不是 `Boolean`。
- **`ShowIn` 不生效？**
  - 语法写错，或后续页面层有显式覆盖。

## 7）推荐模板

```csharp
[BindColumn("status", "状态", "", ItemType = "", ShowIn = "List,Search,EditForm")]
public Int32 Status { get; set; }

[BindColumn("remark", "备注", "", ShowIn = "Detail,EditForm")]
public String? Remark { get; set; }
```

通过统一的 Field 元数据，你可以把“查询、展示、统计”三套逻辑收敛到同一字段定义。