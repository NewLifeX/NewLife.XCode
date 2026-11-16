# 数据库 HTTP 服务（DbService）

`XCode.Services` 提供一套基于 HTTP 的数据库远程访问方案，将 DAL 操作封装为 REST 接口，让**非 .NET 客户端或跨进程场景**也能安全访问 XCode 管理的数据库。



## 架构概览

```
┌─────────────────────────────────────────┐
│             客户端（任意进程）            │
│  DbClient  →  POST/GET http://host:3305 │
└──────────────────┬──────────────────────┘
                   │ HTTP / REST
┌──────────────────▼──────────────────────┐
│               服务端                     │
│  DbServer（HttpServer）                  │
│    └── DbController（路由 /Db/*）        │
│          └── DbService（业务逻辑）       │
│                └── DAL（XCode 核心）     │
└─────────────────────────────────────────┘
```

三个核心类各司其职：

| 类 | 职责 |
|---|---|
| `DbServer` | 监听 HTTP 端口，注册路由和控制器 |
| `DbController` | 将 HTTP 请求映射到 `DbService` 方法，处理令牌缓存 |
| `DbService` | 验证令牌、创建 DAL、执行 SQL / 获取元数据 |
| `DbClient` | 封装 HTTP 调用，提供与本地 DAL 一致的调用风格 |



## 启动服务端

```csharp
var server = new DbServer();
server.Port = 3305;                                  // 默认端口
server.Log  = XTrace.Log;

// 配置令牌权限（空列表 = 允许访问所有数据库）
server.Service.Tokens["readonly-token"] = new[] { "Membership" };
server.Service.Tokens["admin-token"]    = [];         // 允许所有库

server.Start();
Console.WriteLine("DbServer 已启动，端口 " + server.Port);
```

> 默认**不允许零鉴权**：`DbService.Tokens` 为空且未显式允许匿名时，`DbServer.Start()` 直接抛异常拒绝启动，`DbService.ValidateToken` 也会拒绝所有登录。
> 开发环境确需免令牌时，显式设置 `server.Service.AllowAnonymous = true;`。



## REST 接口

| 方法 | 路径 | 说明 |
|---|---|---|
| `POST` | `/Db/Login` | 令牌验证，返回数据库类型和版本 |
| `POST` | `/Db/Query` | 执行 SQL 查询，返回 `DbTable` 二进制包 |
| `POST` | `/Db/Execute` | 执行 SQL 语句，返回受影响行数 |
| `POST` | `/Db/InsertAndGetIdentity` | 插入并返回自增 ID |
| `GET` | `/Db/QueryCount` | 快速查询指定表的记录数 |
| `GET` | `/Db/GetTables` | 获取数据库的表结构列表 |

请求需携带令牌和 `db`（数据库连接名）。令牌优先放在 `Authorization: Bearer {token}` 请求头，`DbClient` 已自动通过 `ApiHttpClient.Token` 发送；服务端仍兼容旧版把 `token` 放在查询串或参数中的调用方式，便于平滑升级。

`QueryCount` 只接受当前库已存在的表名（表名白名单），未知表名会被拒绝，避免表名被拼入各数据库的元数据查询。



## 使用客户端

### 直接使用 DbClient

```csharp
var client = new DbClient("http://127.0.0.1:3305", "Membership", "mytoken");

// 登录（可选，Query/Execute 会自动触发）
await client.LoginAsync();

// 查询
var table = await client.QueryAsync("SELECT * FROM User WHERE ID=@id",
    new Dictionary<String, Object?> { ["id"] = 1 });

// 执行
var rows = await client.ExecuteAsync(
    "UPDATE User SET Name=@name WHERE ID=@id",
    new Dictionary<String, Object?> { ["name"] = "NewName", ["id"] = 1 });

// 查询记录数
var count = await client.QueryCountAsync("User");

// 获取表结构
var tables = await client.GetTablesAsync();
```

### 通过连接字符串接入（Provider=Network）

XCode 连接字符串支持 `Provider=Network`，底层自动使用 `DbClient`：

```xml
<connectionStrings>
  <add name="RemoteDB"
       connectionString="Server=http://127.0.0.1:3305;Database=Membership;Password=mytoken"
       providerName="Network" />
</connectionStrings>
```

配置后可以像本地数据库一样使用实体类，透明地通过 HTTP 访问远端数据库。



## 在 ASP.NET 中集成

`DbService` 是一个普通的可注入服务，无需 `DbServer` 也可在 ASP.NET 应用中使用：

```csharp
// Program.cs
builder.Services.AddSingleton<DbService>(sp =>
{
    var svc = new DbService();
    svc.Tokens["api-token"] = new[] { "Membership" };
    return svc;
});

// Controller
public class DataController : ControllerBase
{
    private readonly DbService _db;
    public DataController(DbService db) => _db = db;

    [HttpPost("query")]
    public IActionResult Query([FromBody] QueryRequest req)
    {
        var dal = _db.Login(req.Token, req.Db);
        var rs  = _db.Query(dal, req.Sql, req.Parameters);
        return Ok(rs);
    }
}
```



## 令牌与权限

`DbService.Tokens` 是一个 `IDictionary<String, String[]>`（线程安全的 `ConcurrentDictionary`）：

```csharp
// 允许 token1 访问所有数据库
svc.Tokens["token1"] = [];

// 限制 token2 只能访问 OrderDB 和 LogDB
svc.Tokens["token2"] = new[] { "OrderDB", "LogDB" };
```

- `Tokens` 为空时默认拒绝所有请求（fail-closed）；仅当显式设置 `AllowAnonymous = true` 时放行。
- 令牌校验不区分大小写。
- `DbController` 对令牌校验结果缓存 10 分钟（`TokenCacheExpire`），减少重复验证开销。



## 注意事项

- `DbServer` 基于 `NewLife.Http.HttpServer`，轻量自托管，无需 IIS / Kestrel。
- 查询结果通过二进制包（`DbTable.ToPacket()`）返回，减少 JSON 序列化开销，`DbClient` 自动解包。
- 服务端不限制 SQL 内容，部署时务必通过令牌和网络层（防火墙/VPN）限制访问范围，避免 SQL 注入风险。
- 跨进程传输时，敏感字段的加解密需在业务层自行处理，框架不内置字段级加密。
