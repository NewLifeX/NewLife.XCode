# TDengine 数据库支持

XCode 支持 TDengine 时序数据库，采用 HTTP REST API 方式连接，无需安装 native 驱动，具有更好的跨平台兼容性。

驱动已拆分为独立仓库 [NewLife.TDengine](https://github.com/NewLifeX/NewLife.TDengine)，以同名 NuGet 包发布，XCode 运行时按类型名反射加载。未安装时首次访问数据库会直接提示需要添加的包名。

```shell
dotnet add package NewLife.TDengine
```

## 连接字符串

TDengine 驱动按地址协议自动选择传输方式，XCode 的裸地址（不带协议）默认按 REST 处理，端口 6041：

```csharp
// 基本连接字符串（REST，经 taosAdapter）
var connStr = "Server=localhost;Port=6041;Database=db;User=root;Password=taosdata";

// 也可以显式指定协议
// var connStr = "Server=http://localhost:6041;Database=db;User=root;Password=taosdata";
// var connStr = "Server=ws://localhost:6041;Database=db;User=root;Password=taosdata";
// var connStr = "Server=tcp://localhost:6030;Database=db;User=root;Password=taosdata"; // 2.x 原生 RPC

// 完整连接字符串示例
DAL.AddConnStr("td", "Server=192.168.1.100;Port=6041;Database=power;User=root;Password=taosdata", null, "TDengine");
```

### 连接字符串参数

| 参数 | 说明 | 默认值 | 必填 |
|------|------|--------|------|
| Server | TDengine 服务器地址，可带 `http://`、`ws://`、`tcp://` 等协议前缀 | localhost | 是 |
| Port | 端口。REST/WebSocket 默认 6041，原生 RPC 默认 6030 | 6041 | 否 |
| Database | 数据库名称 | - | 是 |
| User | 用户名 | root | 是 |
| Password | 密码 | taosdata | 是 |

## 基本使用

### 1. 添加连接

```csharp
// 添加 TDengine 连接
DAL.AddConnStr("td", "Server=localhost;Port=6041;Database=power;User=root;Password=taosdata", null, "TDengine");

// 获取 DAL 实例
var dal = DAL.Create("td");
```

### 2. 执行 SQL

```csharp
// 创建表
dal.Execute("CREATE TABLE meters (ts TIMESTAMP, current FLOAT, voltage INT, phase FLOAT) TAGS (location BINARY(64), groupId INT)");

// 插入数据
dal.Execute("INSERT INTO d001 USING meters TAGS('California.SanFrancisco', 2) VALUES (NOW, 10.3, 219, 0.31)");

// 查询数据
var table = dal.Query("SELECT * FROM meters WHERE ts > NOW - 1h");
```

### 3. 使用实体类

```csharp
// 定义实体类
public class Meter : Entity<Meter>
{
    public Int64 Id { get; set; }
    public DateTime Ts { get; set; }
    public Single Current { get; set; }
    public Int32 Voltage { get; set; }
    public Single Phase { get; set; }
    
    // 超级表的标签
    [DataObjectField(false, false, false, 64)]
    public String Location { get; set; }
    public Int32 GroupId { get; set; }
}

// 查询数据
var list = Meter.FindAll(Meter._.Ts > DateTime.Now.AddHours(-1), Meter._.Ts.Desc(), null, 0, 10);

// 插入数据
var meter = new Meter
{
    Ts = DateTime.Now,
    Current = 10.3f,
    Voltage = 219,
    Phase = 0.31f,
    Location = "California.SanFrancisco",
    GroupId = 2
};
meter.Insert();
```

## HTTP 模式优势

相比 native 驱动（taos.dll），HTTP 模式具有以下优势：

1. **跨平台兼容**：无需安装 native 驱动，支持 Windows、Linux、macOS 等所有平台
2. **部署简单**：只需配置连接字符串，无需复制 DLL 文件
3. **容器友好**：在 Docker 容器中运行更加方便
4. **防火墙友好**：HTTP 协议更容易穿透防火墙
5. **负载均衡**：可以使用标准的 HTTP 负载均衡器

## 注意事项

1. REST/WebSocket 默认使用 6041 端口（taosAdapter），原生 RPC 默认使用 6030 端口
2. REST/WebSocket 需要 taosAdapter 处于运行状态
3. REST 模式的性能略低于原生连接，但对大多数应用来说差异可忽略；大批量读写建议使用 `ws://`
4. 连接字符串中的 Database 参数对应 TDengine 的数据库名
5. 驱动需随应用安装（`NewLife.TDengine`），XCode 包本身不携带驱动

## 从 Native 模式迁移

如果你之前使用 native 模式，迁移到 HTTP 模式非常简单：

1. 修改连接字符串端口：从 6030 改为 6041
2. 无需安装或部署 taos.dll 文件
3. 代码无需任何修改

```csharp
// 旧的 native 模式连接字符串
// var connStr = "Server=localhost;Port=6030;Database=db;User=root;Password=taosdata";

// 新的 HTTP 模式连接字符串（仅修改端口）
var connStr = "Server=localhost;Port=6041;Database=db;User=root;Password=taosdata";
```

## 更多信息

- [NewLife.TDengine 驱动仓库](https://github.com/NewLifeX/NewLife.TDengine)
- [TDengine 官方文档](https://docs.taosdata.com/)
- [TDengine REST API 文档](https://docs.taosdata.com/reference/rest-api/)
- [XCode 使用手册](./XCode使用手册.md)
