# 类型与数组转换助手（ValidHelper）

`ValidHelper` 为业务输入和数据库结果提供统一转换入口，重点是“宽容输入、稳定输出”。

---

## 1. 标量转换

已提供常用转换：
- `ToInt32 / ToInt64 / ToDouble / ToBoolean / ToDateTime`
- `ToString / ToByte / ToDecimal / ToInt16 / ToUInt64`
- `ToEnum<T>`

这些方法底层大量复用 `NewLife` 扩展（如 `ToInt()/ToDateTime()`），支持字符串、数字、部分字节场景。

### 示例

```csharp
var id = ValidHelper.ToInt32("123");
var dt = ValidHelper.ToDateTime("2026-03-13 10:00:00");
var ok = ValidHelper.ToBoolean("true");
```

---

## 2. 数组转换（Array 分部）

核心模板：

```csharp
private static T[]? ToArray<T>(Object? value, Func<Object?, T> converter)
```

支持输入：
- 已经是 `T[]`
- `IEnumerable<T>`
- 单个 `T`
- 非泛型 `IEnumerable`（逐项 converter 转换）

导出的快捷方法：
- `ToInt32Array / ToInt64Array / ToDoubleArray / ToBooleanArray`
- `ToDateTimeArray / ToStringArray / ToByteArray`
- `ToDecimalArray / ToInt16Array / ToUInt64Array`
- `ToEnumArray<T> / ToObjectArray<T>`

---

## 3. 典型用法

### 3.1 Web 参数数组

```csharp
var ids = ValidHelper.ToInt64Array(query["ids"]) ?? [];
```

### 3.2 枚举过滤

```csharp
var states = ValidHelper.ToEnumArray<OrderState>(query["state"]) ?? [];
```

### 3.3 混合输入归一化

```csharp
Object value = new Object[] { "1", 2, 3L };
var arr = ValidHelper.ToInt32Array(value);   // [1,2,3]
```

---

## 4. 行为边界

- 输入 `null/DBNull`：返回 `default`（通常是 `null`）
- `ToObject<T>` 当前是占位实现，返回 `default`
- 转换失败多由底层 `Convert` 或扩展方法决定

因此业务层建议对返回值做空值兜底：

```csharp
var values = ValidHelper.ToStringArray(raw) ?? [];
```

---

## 5. 实战建议

1. 控制器层统一走 `ValidHelper`，不要各处手写 `Convert`。
2. 对数组参数优先使用数组助手（如 `ToInt32Array`/`ToStringArray`），避免 `Split+Parse` 重复代码。
3. 关键业务字段转换后立即做范围校验（如 ID>0、时间区间合法）。
4. 如需复杂对象反序列化，不要依赖 `ToObject<T>`，应走 JSON 序列化器。
