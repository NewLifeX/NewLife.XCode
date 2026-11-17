# 驱动自动加载与下载（DriverLoader）

`DriverLoader` 是 XCode 的驱动自举机制：当特定数据库驱动 DLL 不在本地时，自动从网络下载并加载，实现"零配置启动"。

---

## 1. 适用场景

XCode 核心包故意不内置所有第三方驱动（如 MySQL.Data、Npgsql），以避免版本锁定。
`DriverLoader` 允许用户**按需加载**：

- 首次运行某数据库时自动检测是否有驱动
- 没有则从配置的插件服务器下载
- 下载后加载到进程中，无需重启

---

## 2. 加载流程

```
Load(typeName, disname, dll, linkName, urls, minVersion)
   │
   ├─ 1. Type.GetType(typeName) → 已在进程中 → 返回
   │
   ├─ 2. 多路径搜索 DLL 文件
   │     (EntryAssemblyDir → CWD → BaseDir → PluginPath)
   │
   ├─ 3. 找到 → Assembly.LoadFrom → GetType → 返回
   │
   ├─ 4. DLL 未找到 + linkName 有值
   │     → 锁定 typeName (15秒超时，并发安全)
   │     → WebClientX.DownloadLinkAndExtract(urls, linkName, dir, true)
   │     → 解压到插件目录
   │
   └─ 5. 再次 Load → 返回 Type 或 null
```

---

## 3. 参数说明

| 参数 | 说明 |
|------|------|
| `typeName` | 完整类型名（如 `MySql.Data.MySqlClient.MySqlClientFactory, MySql.Data`） |
| `disname` | 友好显示名（日志用） |
| `dll` | 本地 DLL 文件名（如 `MySql.Data.dll`） |
| `linkName` | 下载页面上供搜索的链接关键词 |
| `urls` | 提供下载页面 URL；为空则用 `NewLife.Setting.PluginServer` |
| `minVersion` | 最低版本限制（`Version`），过滤旧驱动 |

---

## 4. 并发安全

- 使用 `Monitor.TryEnter(typeName, 15_000)` 锁住类型名字符串
- 同一驱动的并发加载请求最多等 15 秒
- 超时放弃，不抛出异常

---

## 5. 下载安全校验

驱动下载会执行 `Assembly.LoadFrom`，一旦下载页或文件被中间人篡改，等同于远程代码执行。因此默认启用以下校验：

| 校验 | 默认行为 | 说明 |
|------|----------|------|
| 传输协议 | 不限制 | http/https 均按配置直接使用。部分嵌入式工控机没有 OpenSSL，无法建立 TLS 连接，强制 https 会让驱动自举直接不可用；完整性改由下列文件校验保证，不依赖传输层 |
| 下载页哈希 | 校验链接指向的文件 | `WebClientX` 解析出的 `Link.Hash` 按 `FileInfo.VerifyHash` 规则比对（支持 `sha256$` 等算法前缀，以及 16/32 位 MD5、SHA1、SHA256、SHA512、CRC32；算法前缀或长度不可识别时跳过）。哈希对应下载页的链接文件；官方插件页给出的是**压缩包**哈希，因此比对归档，解压出的程序集另由托管程序集校验与固定哈希把关 |
| 固定哈希 | 可选 | `DriverLoader.KnownHashes["MySql.Data.dll"] = "<SHA-256>"`，命中后强制校验 |
| 托管程序集 | 强制 | 下载文件必须是可加载的 .NET 程序集，否则删除并拒绝 |
| 自定义回调 | 可选 | `DriverLoader.VerifyFile = file => ...`，返回 `false` 拒绝 |

未登记 `KnownHashes` 时，校验通过后会在日志中输出文件 SHA-256，便于运维登记形成固定校验。

---

## 6. 自定义下载客户端

```csharp
// 默认使用 WebClientX + NewLife AuthKey
// 可替换为自定义客户端（如代理场景）
DriverLoader.CreateClient = linkName => new WebClientX
{
    AuthKey = "MyKey",
    Proxy = myProxy,
};
```

---

## 7. 驱动搜索路径优先级

1. 入口程序集所在目录
2. 调用程序集所在目录
3. 执行程序集所在目录
4. 当前工作目录
5. AppDomain 基础目录
6. `NewLife.Setting.PluginPath`（可配置）

---

## 8. 在各数据库驱动中的使用示例

每个数据库驱动的 `CreateFactory()` 方法调用 `DriverLoader.Load`：

```csharp
// MySQL 驱动示例
protected override DbProviderFactory? CreateFactory()
{
    return DriverLoader.Load(
        "MySql.Data.MySqlClient.MySqlClientFactory, MySql.Data",
        "MySql",
        "MySql.Data.dll",
        "MySql.Data",
        null,
        new Version("8.0")
    ) as DbProviderFactory;
}
```

---

## 9. 关联阅读

- `/xcode/idatabase_dbbase`
- `/xcode/connection_string_builder`
