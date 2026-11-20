# 数据库驱动抽象（IDatabase / DbBase）

XCode 把"一种数据库一个驱动类"的设计封装在 `IDatabase` 接口和 `DbBase` 抽象基类中。

理解这一层有助于：

- 知道数据库特性在哪里被定制
- 添加新数据库驱动时找到扩展点
- 调试 SQL 方言与分页问题

---

## 1. IDatabase 接口

### 1.1 核心属性

| 属性 | 说明 |
|------|------|
| `Type` | `DatabaseType` 枚举（`MySql / SqlServer / SQLite ...`） |
| `ConnName` | 对应 DAL 的连接名 |
| `ConnectionString` | 内部已解密的连接串 |
| `Provider` | 驱动名称（选驱动用） |
| `Migration` | 反向工程模式 |
| `NameFormat` | 标识符大小写（`Default/Upper/Lower/Underline`） |
| `ShowSQL` | 是否打印 SQL |
| `UseParameter` | 参数化查询 |
| `BatchSize` | 批量操作默认行数 |
| `CommandTimeout` | 命令超时 |

### 1.2 核心方法

| 方法 | 说明 |
|------|------|
| `CreateSession()` | 创建普通数据库会话（每线程一个） |
| `CreateMetaData()` | 创建反向工程元数据对象 |
| `OpenConnection()` | 直接打开底层 `DbConnection` |
| `PageSplit(builder, ...)` | 把 `SelectBuilder` 转为数据库方言分页 SQL |
| `FormatName(table/col)` | 标识符格式化（加引号/转大小写） |
| `FormatValue(col, val)` | 值格式化（日期/字符串等转义） |
| `Support(provider)` | 判断是否支持指定驱动 |

---

## 2. DbBase 的职责

`DbBase` 是所有内置数据库驱动的公共抽象基类，承担：

- **工厂缓存**：每个驱动类型的 `DbProviderFactory` 只初始化一次
- **会话绑定**：用 `ThreadLocal` / `AsyncLocal` 实现"每线程/协程独立会话"
- **DLL 目录注册**：在 Windows 下自动设置 x64/x86 驱动目录，支持按需下载
- **默认行为**：从 `XCodeSetting` 读取 `ShowSQL`、`RetryOnFailure` 等初始值

---

## 3. 分页 SQL 方言

`IDatabase.PageSplit` 有两个重载：

1. `PageSplit(sql, start, max, keyColumn)`：原始 SQL + not-in 或 max-min 分页
2. `PageSplit(builder, start, max)`：结构化 `SelectBuilder` 生成更优分页

MS 体系（SqlServer）的分页精髓在于：
- 有唯一键且排序带方向 → 用 `MaxMin` 分页（最小子查询、效率最优）
- 没有 → 退化到 `TopNotIn`

含 `GroupBy` 时两种方案都只能查第一页，需要业务侧规避。

---

## 4. 各方言实现

数据库驱动文件位于 `DataAccessLayer/Database/`，每个文件对应一种数据库：

| 文件 | 对应数据库 |
|------|----------|
| `MySql.cs` | MySQL / MariaDB |
| `SQLite.cs` | SQLite |
| `SqlServer.cs` | SQL Server（含 SqlCe） |
| `PostgreSQL.cs` | PostgreSQL |
| `Oracle.cs` | Oracle |
| `DaMeng.cs` | 达梦 |
| `KingBase.cs` | 人大金仓 |
| `HighGo.cs` | 瀚高 |
| `TDengine.cs` | TDengine |
| `InfluxDB.cs` | InfluxDB 1.x/2.x/3.x |
| `OpenTSDB.cs` | OpenTSDB |
| `IoTDB.cs` | Apache IoTDB |
| `Prometheus.cs` | Prometheus / VictoriaMetrics |
| `Network.cs` | 网络虚拟数据库 |

每个驱动只需重写方言相关方法，不用重复实现连接管理、元数据等通用逻辑。

---

## 5. 扩展自定义驱动

1. 继承 `DbBase` 并实现 `IDatabase`
2. 重写 `CreateFactory()`、`PageSplit(...)`、`FormatName()`
3. 注册到 `DbFactory`

---

## 6. 关联阅读

- `/xcode/connection_string_builder`
- `/xcode/dal_db_operate`
- `/xcode/dal_setting`
