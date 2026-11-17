# 数据库会话接口（IDbSession）

`IDbSession` 是 XCode DAL 层**最底层**的执行接口，直接对应一次数据库连接会话，负责：

- 单条 SQL 执行（Query / Execute）
- 事务管理
- 原生批量插入/更新

业务代码通常不直接使用 `IDbSession`，但理解它有助于调试底层行为、编写自定义 DAL 扩展。

---

## 1. 获取会话

```csharp
// 通过 DAL.Session 拿（懒创建，每线程独立）
var session = DAL.Create("ConnName").Session;

// 通过 IDatabase 创建
var session = db.CreateSession();
```

> 每个线程/协程持有自己的 `IDbSession` 实例，互不干扰。

---

## 2. 单条 SQL 执行

### 2.1 查询

| 方法 | 返回 | 说明 |
|------|------|------|
| `Query(sql, type, ps)` | `DataSet` | 返回完整 DataSet（多表） |
| `Query(cmd)` | `DataSet` | 接受 `DbCommand` 直接查询 |
| `Query(builder)` | `DbTable` | 接受 `SelectBuilder`，返回 `DbTable`（更轻量） |
| `Query(sql, ps)` | `DbTable` | SQL + 参数化查询 |
| `QueryCount(sql, ...)` | `Int64` | 返回计数 |
| `QueryCount(builder)` | `Int64` | `SelectBuilder` 计数 |
| `QueryCountFast(tableName)` | `Int64` | 快速近似计数（稍有偏差） |
| `ExecuteScalar<T>(sql, ...)` | `T` | 取第一行第一列 |

> `QueryCountFast` 是估算值：SQL Server 取 `sysindexes.rows`，MySQL/HANA/IRIS 取 `information_schema.table_rows`，PostgreSQL/VastBase 取 `pg_class.reltuples`，Oracle/DaMeng/DB2 取 `ALL_TABLES.NUM_ROWS`。SQLite 不走该方法，由实体层使用自增主键区间估算。

### 2.2 执行

| 方法 | 返回 | 说明 |
|------|------|------|
| `Execute(sql, type, ps)` | `Int32` | 受影响行数 |
| `Execute(cmd)` | `Int32` | 执行 `DbCommand` |
| `InsertAndGetIdentity(sql, ...)` | `Int64` | 插入并返回自增 ID |
| `Truncate(tableName)` | `Int32` | 清空表并重置标识 |

---

## 3. 事务

```csharp
var n = session.BeginTransaction(IsolationLevel.ReadCommitted);
try
{
    session.Execute("INSERT ...");
    session.Execute("UPDATE ...");
    // n 代表嵌套事务深度，减到 0 才真正提交
    session.Commit();
}
catch
{
    session.Rollback();
}
```

**嵌套事务计数**：每次 `BeginTransaction` 深度 +1，每次 `Commit`/`Rollback` 深度 -1，深度到 0 才发送 COMMIT/ROLLBACK 给数据库。这保证了内层事务不会意外提交。

---

## 4. 批量操作

批量方法接收 `IDataTable`（表结构）+ `IDataColumn[]`（列描述）+ `IEnumerable<IModel>`（数据行）：

| 方法 | 说明 |
|------|------|
| `Insert(table, cols, list)` | 批量 INSERT |
| `InsertIgnore(table, cols, list)` | 批量 INSERT IGNORE（忽略主键冲突） |
| `Replace(table, cols, list)` | 批量 REPLACE（覆盖已有行） |
| `Update(table, cols, updateCols, addCols, list)` | 批量 UPDATE（含累加字段） |
| `Upsert(table, cols, updateCols, addCols, list)` | 批量 INSERT OR UPDATE |

`updateColumns` 传属性名，`addColumns` 是需要 `+= delta` 的累加字段。

---

## 5. 辅助功能

```csharp
// 临时改变 ShowSQL 并自动还原
using var _ = session.SetShowSql(true);
```

```csharp
// 获取数据库架构信息（表结构、列信息等）
var schema = session.GetSchema(conn, "Tables", null);
```

---

## 6. 与上层的调用关系

```
IEntity.Save()
  → EntitySession.Save()
    → DAL.Insert / Update / Upsert
      → IDbSession.Execute / Upsert
        → ConnectionPool.Get → DbConnection.ExecuteNonQuery
```

---

## 7. 关联阅读

- `/xcode/entity_transaction`
- `/xcode/dal_db_operate`
- `/xcode/idatabase_dbbase`
- `/xcode/connection_pool`
