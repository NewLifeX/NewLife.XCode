# SonnetDB 接入与能力验收

本适配使用官方 `SonnetDB` NuGet 包中的 `SonnetDB.Data.SndbProviderFactory`，支持嵌入式数据库目录和远程 Server。数据库基线为已经发布的 **4.0.0**，运行时需要 **.NET 10**。XCode 主包仍保留原有目标框架；驱动依赖包装项目 `XCode.SonnetDB` 使用 net10.0。

跟踪 issue：[NewLife.XCode #100](https://github.com/NewLifeX/NewLife.XCode/issues/100)。适配仍在验收中，数据库缺口解决并发布、XCode 集成验证完成前，不宣称与 PostgreSQL / SQLite 的全部 ORM 能力等价。

## 使用

本 PR 的代码可通过项目引用构建。`XCode.SonnetDB` 正式发布后可安装此适配包；它依赖 XCode 主包和 SonnetDB 4.0.0。

```csharp
using XCode.DataAccessLayer;

// Data Source 指向数据库目录。
DAL.AddConnStr("app", "Data Source=./data/app", null, "SonnetDB");
var dal = DAL.Create("app");
var dbType = dal.DbType; // DatabaseType.SonnetDB

// 远程连接：使用部署提供的 Token，不将真实凭据提交到源代码。
DAL.AddConnStr("remote",
    "Data Source=sonnetdb+http://localhost:5000/app;Token=<token>;Protocol=rest",
    null, "SonnetDB");
```

提供者名称可用 `SonnetDB` / `SonnetDB.Data`。远程连接的 `rest`、`auto`、`frame-http2` 遵循官方驱动合同；强制 HTTP/2 必须连接配置了 HTTP/2 的端点。DML 写入使用官方 REST SQL 通道，不把只读 Frame 查询端点当作写入协议。

## 已验证的适配范围

✅ 表示指定场景通过真实正式包或 Server 验证；🧪 表示仍需验证；🛠️ 表示数据库尚需实现或修复；📦 表示仍需发布；⛔ 表示当前版本不能完整提供该能力。实现、验证、正式发布和 ORM 集成分别确认，跳过测试不计为通过。

| 能力 | 当前适配与验证范围 |
| --- | --- |
| 驱动接入 | ✅ DatabaseType / DbFactory、提供者识别、官方工厂、嵌入式与远程连接 |
| 自动建表与反向工程 | ✅ 关系表、标量列、主键、自增、默认值、普通及复合唯一索引；原生 GetSchema，不依赖 pg_catalog |
| 视图目录 | ✅ 视图名称和定义已可读取；⛔ 声明列类型不完整，反向工程明确拒绝，见 #261 |
| 实体 CRUD | ✅ 同步/异步插入、更新、删除，identity 非首列正确回填，Unicode/引号/NULL/BLOB 往返 |
| 标量值 | ✅ 有符号整数、布尔、常用 Decimal 值和毫秒 DateTime；高精度边界见下表，不能由这项推断全部 Decimal / DateTime 已支持 |
| 查询与分页 | ✅ 参数、COUNT/SUM、LIMIT/OFFSET、仅 OFFSET、基础 LINQ、JOIN、ROW_NUMBER 和表前缀 |
| DataSet 查询 | ✅ 会话用 DbDataReader / DataTable.Load 填充单结果集，补齐官方工厂没有 DataAdapter 的接入差异 |
| 批量写入 | ✅ Insert、InsertIgnore、Upsert；支持冲突更新和累加字段，不将 Upsert 冒称 Replace |
| 有界删除 | ✅ 单主键子查询确保 maximumRows 生效；表达式删除同时修复未传递参数的问题 |
| 迁移与清空 | ✅ 添加/删除普通列、修改可空性/默认值、创建/删除索引、保留已有数据、TRUNCATE 重置自增 |
| 事务 | ✅ ReadCommitted 提交、回滚和读取自身写入；远程使用真实 Server 会话 |
| 模型与代码生成 | ✅ DatabaseType 可序列化为 SonnetDB，继续使用现有通用模型/代码生成链路；🧪 大规模跨库迁移和所有业务实体组合未逐项验证 |
| 缓存、分表、权限与租户 | 使用已有 XCode 运行时；✅ 独立实体 split/表前缀路径，🧪 各业务模块与 SonnetDB 的完整组合仍需验收 |

## 数据库缺口和后续发布

以下 issue 都是明确的能力缺口，不能因本适配成功构建而标为完成。适配中的保护异常表示能力尚未开放。

| 能力 | 4.0.0 行为与适配处理 | 后续跟踪 |
| --- | --- | --- |
| 表/列描述 | ⛔ 不持久化注释。直接注释 API 拒绝；自动迁移输出未完成日志，不将其计为成功 | 🛠️ [#237](https://github.com/IoTSharp/SonnetDB/issues/237) → 🧪 验证 → 📦 发布 → 🔧 集成 |
| 字符串长度 | ⛔ STRING 无界，不能保存或约束模型 Length；建模明确日志，长度变更直接拒绝 | 🛠️ [#240](https://github.com/IoTSharp/SonnetDB/issues/240) → 🧪 → 📦 → 🔧 |
| Decimal 参数完整精度 | ⛔ 高精度值被底层转换为浮点；适配拒绝不能保真的输入，常用低精度值继续使用 | 🛠️ [#235](https://github.com/IoTSharp/SonnetDB/issues/235) → 🧪 → 📦 → 🔧 |
| DATETIME 亚毫秒精度 | ⛔ 存储截到毫秒；适配拒绝带亚毫秒的 DateTime / DateTimeOffset | 🛠️ [#236](https://github.com/IoTSharp/SonnetDB/issues/236) → 🧪 → 📦 → 🔧 |
| Decimal 列迁移精度 | ⛔ ADD/ALTER 的显式 p/s 丢成 (38,28)；拒绝受影响迁移，CREATE 的显式精度单独验证 | 🛠️ [#241](https://github.com/IoTSharp/SonnetDB/issues/241) → 🧪 → 📦 → 🔧 |
| BatchReplace | ⛔ 没有 delete + insert 的原子替换；明确拒绝，BatchCapability 不宣称 Replace | 🛠️ [#242](https://github.com/IoTSharp/SonnetDB/issues/242) → 🧪 → 📦 → 🔧 |
| 复合/无主键有界删除 | ⛔ 缺少等价原子 SQL；明确拒绝，避免退回无界 DELETE | 🛠️ [#243](https://github.com/IoTSharp/SonnetDB/issues/243) → 🧪 → 📦 → 🔧 |
| 无主键表写入 | ⛔ 建空表后 INSERT 被拒绝；不暗加业务主键 | 🛠️ [#255](https://github.com/IoTSharp/SonnetDB/issues/255) → 🧪 → 📦 → 🔧 |
| 无条件全表 Update | ⛔ 原生 UPDATE 要求 WHERE，现有静态批量更新的空条件调用仍有差距 | 🛠️ [#257](https://github.com/IoTSharp/SonnetDB/issues/257) → 🧪 → 📦 → 🔧 |
| 大于 Int32 的分页位置 | ⛔ 原生 LIMIT/OFFSET 限为 Int32；适配明确拒绝更大 Int64 值 | 🛠️ [#256](https://github.com/IoTSharp/SonnetDB/issues/256) → 🧪 → 📦 → 🔧 |
| Rank / DenseRank | ⛔ XCode WindowFunction 已有这些接口，SonnetDB 当前窗口不支持 | 🛠️ [#222](https://github.com/IoTSharp/SonnetDB/issues/222) → 🧪 → 📦 → 🔧 |
| 更高隔离等级 | ⛔ Serializable 标签无对应完整隔离；适配仅开放 ReadCommitted / Unspecified | 🛠️ [#225](https://github.com/IoTSharp/SonnetDB/issues/225) → 🧪 → 📦 → 🔧 |
| 主键演进 | ⛔ 现有列主键/自增定义变化不由普通 ALTER COLUMN 完成，需要完整迁移合同 | 🛠️ [#226](https://github.com/IoTSharp/SonnetDB/issues/226) → 🧪 → 📦 → 🔧 |
| 事务内 DDL | ⛔ 原生轻事务拒绝 DDL，不能称迁移支持事务回滚 | 🛠️ [#227](https://github.com/IoTSharp/SonnetDB/issues/227) → 🧪 → 📦 → 🔧 |
| 多结果集 | ⛔ 多条 SELECT / NextResult 不完整；单结果集 DataSet 补齐不代表这项完成 | 🛠️ [#229](https://github.com/IoTSharp/SonnetDB/issues/229) → 🧪 → 📦 → 🔧 |
| schema 限定表名 | ⛔ 原生关系表 namespace 与限定表名不完整，表名前缀不等于 schema | 🛠️ [#232](https://github.com/IoTSharp/SonnetDB/issues/232) → 🧪 → 📦 → 🔧 |
| 精确引用标识符 | ⛔ 4.0.0 未包含完整大小写/歧义合同；✅ main 已有 #211 实现，尚未正式交付 | 📦 [#230](https://github.com/IoTSharp/SonnetDB/issues/230) 等待 SonnetDB 发布 → 🧪 XCode 验证 → 🔧 集成 |
| 视图声明列 | ⛔ Columns 缺少视图列，零行读取器返回 Object 类型；反向工程拒绝猜类型，含此类视图的全表元数据枚举也受影响 | 🛠️ [#261](https://github.com/IoTSharp/SonnetDB/issues/261) → 🧪 → 📦 → 🔧 |

SonnetDB 4.0.0 的 RETURNING、ON CONFLICT、GetSchema 和基础 DECIMAL 已经发布，✅ 这些能力不标为等待发版。上述缺口尚未实现时以 🛠️ 为主；只有修复已经合入但正式包未包含时，才改为“📦 等待 SonnetDB 发布”。本适配及 `XCode.SonnetDB` 自身仍为 📦 待合入发布。

嵌入式建库通过 Data Source 目录打开，远程建库使用官方控制面 SQL。嵌入式 DropDatabase 不自动删除数据库目录，当前明确拒绝；备份、恢复及各业务模块的全部组合未在此次验证中冒称通过。

## 验证入口

嵌入式测试安装正式 SonnetDB 4.0.0，逐项使用隔离临时目录。未完成的未来合同测试有显式 Skip 及 issue 链接；同时执行拒绝保护测试，保证失败不会偷偷写坏数据。

2026-10-09 验收记录：SonnetDB 嵌入式及真实 Server 共 31 项，27 通过、4 个未来能力合同明确跳过；现有 SQLite / DbBase / DAL / WindowFunction 回归 70 项中 69 通过、1 项 `SQLiteTests.MetaTest` 因空的 Membership 数据库缺少表而失败。原始提交 `280b2a2f` 在隔离 worktree 同样复现该项失败；不将它归为新适配回归，也不把此次检查称为全套测试通过。PostgreSQL 的真实服务回归尚未执行。

```powershell
dotnet test XUnitTest.XCode/XUnitTest.XCode.csproj --filter FullyQualifiedName~SonnetDBTests
```

远程测试需要真实 Server 的 HTTP 和 HTTP/2 端点，凭据来自测试环境：

```powershell
$env:SONNETDB_TEST_CONNECTION = 'Data Source=sonnetdb+http://localhost:5000/app;Token=<test-token>'
$env:SONNETDB_TEST_HTTP2_CONNECTION = 'Data Source=sonnetdb+http://localhost:5001/app;Token=<test-token>'
dotnet test XUnitTest.XCode/XUnitTest.XCode.csproj --filter FullyQualifiedName~SonnetDBRemoteTests
```

没有上述环境配置时，远程测试明确跳过。它分别验证 REST、Auto、强制 HTTP/2 的元数据、同步/异步 identity、DataSet、分页、事务读写/回滚及有界删除；本地验收使用官方 v4.0.0 Windows Server bundle 并验证发布 SHA-256。
