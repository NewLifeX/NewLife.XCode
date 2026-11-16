# 配置中心 DbConfigProvider（Parameter 参数表）

`DbConfigProvider` 让配置项存储在数据库参数表中，并提供本地缓存与热更新能力，适合多实例、集中治理场景。

## 1）定位

继承 `ConfigProvider`，核心能力：

- 从参数表加载配置树
- 保存配置回数据库
- 周期刷新（热更新）
- 本地缓存容灾（Json/加密）

## 2）关键属性

- `UserId`：配置所属用户，`0` 表示全局
- `Category`：配置分类
- `CacheLevel`：本地缓存级别（NoCache/Json/Encrypted）
- `Period`：刷新周期（秒），默认 15

## 3）加载流程

1. `Init()`：先尝试加载本地缓存文件（全局配置）
2. `LoadAll()`：从 `Parameter` 表读取配置
3. `Build()`：把 `key:value` 构造成配置树（支持 `:` 分层）
4. `SaveCache()`：把当前配置落本地缓存
5. `InitTimer()`：启动定时刷新

## 4）参数表字段约定

读取时：
- 优先取 `Value`
- `Value` 为空时回退 `LongValue`
- `Remark` 写入 `#key` 注释节点

保存时：
- 值长度 `< 200` → `Value`
- 否则 → `LongValue`

这保证长配置（如JSON）也能安全存储。

## 5）热更新机制

定时任务 `DoRefresh()` 会：

- 对比数据库配置与内存 `_cache`
- 只要键值变化即重建 `Root`
- 调用 `NotifyChange()` 触发绑定对象更新

因此业务对象可直接 `Bind(model, autoReload: true)` 实现无重启更新。

## 6）本地缓存容灾

默认路径在 `NewLife.Setting.Current.DataPath`：

- 文件名：`dbConfig_{Category}.json`
- `Encrypted` 模式下做 AES 加密

意义：
- 数据库短暂不可用时，系统可使用上次配置继续运行。

加密密钥直接使用配置分类名，与 Core 的 `HttpConfigProvider` 用 AppId 作密钥保持一致，便于离线排查：码神工具「对称加密」选 AES、CBC、PKCS7，密文框填文件内容，密码框填分类名即可解密。`Encrypted` 只用于避免配置明文落盘被直接读取，不构成对抗本机攻击者的保护；不使用 `ProtectedKey`/机器指纹派生密钥。解密失败（分类名变更或文件损坏）时，程序会记录日志并忽略该缓存、回退数据库加载，下一次保存时自动重建。

## 7）典型用法

```csharp
var provider = new DbConfigProvider
{
    UserId = 0,
    Category = "MyApp",
    CacheLevel = ConfigCacheLevel.Json,
    Period = 15,
};

Config.SetProvider(provider);
provider.LoadAll();
provider.Bind(MySetting.Current, autoReload: true);
```

## 8）实践建议

- 生产环境优先使用全局配置（`UserId=0`）+ 本地缓存。
- 配置分层命名采用 `:`，如 `Redis:Host`、`Redis:Port`。
- 大字段配置（JSON）让系统自动落 `LongValue`，不要手工截断。
- 配置更新频繁时适当增大 `Period`，减少数据库压力。

## 9）常见问题

- **更新了参数表但业务没变？**
  - 检查 `Period` 是否为 0，或是否开启 `autoReload` 绑定。
- **重启后配置回退？**
  - 检查 `Category`、`UserId` 是否一致；确认本地缓存文件路径和权限。