# DAL 与 SQL 构建器

`DAL`（Data Access Layer）是 XCode 数据库访问的总入口，每个连接字符串对应一个唯一 `DAL` 实例。`SelectBuilder` 和 `InsertBuilder` 是它内部用于构造 SQL 的核心工具类。

---

## 第一部分：DAL

### 1.1 获取 DAL 实例

```csharp
// 按连接名获取（连接字符串在配置文件中）
var dal = DAL.Create("MyDb");

// 临时添加连接字符串后获取
DAL.AddConnStr("Tmp", "Data Source=:memory:;", null, "SQLite");
var tmpDal = DAL.Create("Tmp");
```

一个连接名对应全局唯一一个 `DAL` 实例（`ConcurrentDictionary` 管理）。

### 1.2 关键属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `ConnName` | `String` | 连接名 |
| `DbType` | `DatabaseType` | 实际数据库类型 |
| `ConnStr` | `String` | 连接字符串（内部密码可能加密） |
| `Db` | `IDatabase` | 数据库对象（延迟初始化） |
| `Session` | `IDbSession` | 当前线程数据库会话 |
| `ReadOnly` | `DAL?` | 只读副本 DAL |
| `Strategy` | `ReadWriteStrategy` | 读写分离策略 |
| `QueryTimes` | `Int64` | 累计查询次数 |
| `ExecuteTimes` | `Int64` | 累计执行次数 |
| `ShowSQL` | `Boolean` | 是否输出 SQL 日志（覆盖全局设置） |

### 1.3 常用操作

```csharp
// 直接查询（返回 DataSet）
var ds = dal.Select("SELECT * FROM User WHERE Status=1");

// 执行 SQL
dal.Execute("UPDATE User SET Status=0 WHERE Id=@id", CommandType.Text,
    dal.CreateParameter("id", userId));

// 查询标量
var count = dal.SelectCount("SELECT COUNT(*) FROM User");

// 获取表结构
var tables = dal.Tables;
var table = dal.GetTable("User");
```

### 1.4 连接字符串格式

```json
{
  "ConnectionStrings": {
    "MyDb": "Server=.;Database=MyApp;Uid=sa;Pwd=xxx",
    "MyDbReadonly": "Server=slave;Database=MyApp;Uid=sa;Pwd=xxx"
  }
}
```

也支持 `XCode.json` 中的自定义格式：

```json
{
  "XCode": {
    "ConnStr": {
      "MyDb": {
        "ConnectionString": "...",
        "Provider": "MySql"
      }
    }
  }
}
```

### 1.5 全局统计

```csharp
// 全局累计查询/执行次数（用于性能监控）
var totalQuery = DAL.QueryTimes;
var totalExec  = DAL.ExecuteTimes;
```

常用于在请求拦截器中记录单次请求的 SQL 消耗数量（原 Web 模块 `DbRuntimeModule` 即此思路，该模块已于 v12.1 移除）。

---

## 第二部分：SelectBuilder

`SelectBuilder` 用于构造复杂的 `SELECT` 语句，支持解析现有 SQL 后进行二次修改（分页/排序/子查询包装）。

### 2.1 构造查询

```csharp
var sb = new SelectBuilder
{
    Table = "User",
    Column = "Id, Name, Status",
    Where = "Status = 1",
    OrderBy = "CreateTime Desc",
};

// 生成 SQL：SELECT Id, Name, Status FROM User WHERE Status = 1 ORDER BY CreateTime Desc
var sql = sb.ToString();
```

### 2.2 解析已有 SQL

```csharp
var sb = new SelectBuilder("SELECT Id, Name FROM User WHERE Status=1 ORDER BY Id");
sb.Where += " AND RoleId=2";
sb.Limit = "LIMIT 0,20";
```

### 2.3 支持 GroupBy / Having

```csharp
var sb = new SelectBuilder
{
    Table = "Order",
    Column = "UserId, COUNT(*) AS Cnt, SUM(Amount) AS Total",
    GroupBy = "UserId",
    Having = "COUNT(*) > 5",
    OrderBy = "Total Desc"
};
```

### 2.4 主要属性

| 属性 | 说明 |
|------|------|
| `Column` | SELECT 列（不含 SELECT 关键字） |
| `Table` | FROM 子句 |
| `Where` | WHERE 条件（赋值时会自动从中解析 GROUP BY） |
| `GroupBy` | GROUP BY 子句 |
| `Having` | HAVING 子句 |
| `OrderBy` | ORDER BY 子句（赋值时若无 Key 则自动提取排序字段为 Key） |
| `Limit` | LIMIT 子句（SQLite/MySQL 分页用） |
| `Key` | 分页主键（SqlServer ROW_NUMBER 分页使用） |
| `Parameters` | 参数化查询参数集合 |

### 2.5 分页封装

通常不需要手动用 `SelectBuilder` 分页，XCode 实体查询链路会根据数据库类型选择合适分页策略。直接使用 `PageParameter` 即可：

```csharp
var page = new PageParameter { PageIndex = 2, PageSize = 20 };
var list = User.FindAll(User._.Status == 1, page);
```

---

## 第三部分：InsertBuilder

`InsertBuilder` 生成 `INSERT`、`INSERT OR REPLACE`（SQLite）、`MERGE`（Upsert）等 SQL，是批量写入的核心。

### 3.1 主要属性

| 属性 | 说明 |
|------|------|
| `Mode` | `SaveModes.Insert` / `Upsert` / `InsertIgnore` 等 |
| `AllowInsertIdentity` | 是否允许插入自增标识字段 |
| `UseParameter` | 是否使用参数化 SQL |
| `Parameters` | 参数化时生成的参数数组（`GetSql` 后填充） |

### 3.2 SaveModes 枚举

| 值 | 说明 |
|----|------|
| `Insert` | 标准插入 |
| `Upsert` | 存在则更新，不存在则插入（按唯一键） |
| `InsertIgnore` | 存在则忽略，不存在则插入 |

### 3.3 批量插入示例（内部机制）

实体 `list.Insert()` 调用链：

```
IList<TEntity>.Insert()
  → EntityPersistence.Insert(entities)
       → InsertBuilder.GetSql(...)  ×N（合并为多行 VALUES 批量）
       → dal.Execute(sql)
```

> 开发者通常不直接使用 `InsertBuilder`，而是通过 `list.Insert()` 或 `entity.Upsert()` 触发。

---

## 第四部分：保护连接字符串

DAL 支持通过 `ProtectedKey` 对连接字符串中的密码进行对称加密保护：

```csharp
// 加密密码存储（运维侧操作）
var key = ProtectedKey.Instance;
var encrypted = key.Protect("myPassword");

// XCode.json 中存储加密后的密码
// Password=AES_XXXX...
```

`ProtectedKey` 的默认密钥来自环境变量 `NEWLIFE_ProtectedKey` 或配置文件 `ProtectedKey` 节，机器级或容器级注入，不随代码发布。

---

## 第五部分：常见问题

- **DAL 实例何时释放**：全局唯一，不需要手动释放；会话（`Session`）是线程级，请求结束自动归还。
- **多数据库并发写入**：各 `ConnName` 对应独立 `DAL`，互不干扰。
- **想看生成的 SQL**：设置 `ShowSQL=true`（配置文件或代码均可），SQL 输出到日志。
- **慢查询定位**：设置 `TraceSQLTime`（毫秒），超过阈值的 SQL 自动写入慢查询日志。
