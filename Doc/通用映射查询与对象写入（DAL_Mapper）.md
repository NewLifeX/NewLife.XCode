# 通用映射查询与对象写入（DAL_Mapper）

`DAL_Mapper` 让 `DAL` 具备了类似轻量 ORM 的能力：
- SQL + 参数对象 -> 强类型对象列表
- 对象 -> Insert/Update/Delete
- 同步/异步双通道

适合“非实体模型”或临时 SQL 场景。

---

## 1. 查询映射

### 1.1 基础查询

```csharp
var list = dal.Query<UserDto>("select Id,Name from User where Enable=@Enable", new { Enable = 1 });
```

行为特点：
- 自动参数化（`Db.CreateParameters(param)`）
- `ReadModels<T>()` 自动按列名映射属性
- 基础类型 `T`（如 `Int32/String`）走第一列快速映射
- 明确禁止 `ValueTuple`（会抛异常）

### 1.2 分页查询

```csharp
var page = new PageParameter { PageIndex = 1, PageSize = 20, Sort = "Id Desc" };
var rows = dal.Query<UserDto>(sql, new { Enable = 1 }, page);
```

`page.RetrieveTotalCount=true` 时会自动执行 `SelectCount`。

排序子句（`OrderBy`/`Sort`+`Desc`）在拼入原生 SQL 前会严格校验：只允许逗号分隔的字段名（可带表别名、方括号或引号）与 `Asc`/`Desc`，出现括号、分号、注释等一律抛 `XCodeException`，避免前端排序参数造成盲注。

---

## 2. 执行与标量

- `Execute(sql, param)`：执行 DML
- `ExecuteScalar<T>(sql, param)`：第一行第一列
- `ExecuteReader(sql, param)`：返回 reader（`CloseConnection`）

异步对应：`ExecuteAsync / ExecuteScalarAsync / ExecuteReaderAsync`。

---

## 3. 对象级写入（无实体类）

### 3.1 Insert

```csharp
dal.Insert(new { Id = 1, Name = "Stone" }, tableName: "User");
```

规则：
- 若没传 `tableName`，优先 `DAL.GetTableName` 回调
- 再退化到对象类型名

### 3.2 Update

```csharp
dal.Update(new { Name = "Neo" }, new { Id = 1 }, tableName: "User");
```

- 支持 `where` 参数对象
- 不传 `where` 时尝试主键名（默认 `Id` 或 `GetKeyName` 回调）

### 3.3 Delete

```csharp
dal.Delete("User", new { Id = 1 });
```

---

## 4. 表名/主键名回调

可自定义：

```csharp
DAL.GetTableName = t => t.Name + "s";
DAL.GetKeyName = t => "Id";
```

内部带缓存（`ConcurrentDictionary<Type,String>`），避免反复回调损耗。

---

## 5. IModel + IDataTable 精细写入

还支持：
- `Insert(IModel, IDataTable, columns, mode)`
- `Update(IModel, IDataTable, columns, updateColumns, addColumns)`

适合：
- ETL 导入
- 自定义 Upsert
- 明确控制更新列与累加列

---

## 6. 常见注意事项

1. `ValueTuple` 不支持，建议用 class/record DTO。
2. `Update(data, null)` 要确保对象含主键字段。
3. 表名/字段名尽量使用数据库实际名称，减少方言兼容风险。
4. 大批量写入优先 `InsertBuilder/Batch` 方案，Mapper 更偏灵活性。
