# 集群配置同步（ConfigSync）

`ConfigSyncManager` 用于多实例部署时，把**启动期配置文件**的最新值从数据库参数表同步到每个实例的本地文件。

它解决的是 `XCodeSetting`、`NewLife.Setting`（Core）、`SysConfig` 这类配置：

- 必须在进程启动时从本地文件读取，不能把运行时 Provider 换成 `DbConfigProvider`；
- 多台服务器部署时，逐台改文件容易遗漏；
- 又希望保留“某台机器临时改本地文件调试”的能力。

本地文件仍然是运行时唯一来源；数据库只作为下发通道。

## 1. 存储模型

`Category = "{AppId}-{Scope}"`，`UserId = 0`，每个配置项一行：

```
Category=StarWeb-Core     Name=ShowSQL    Value=true
Category=StarWeb-Core     Name=LogPath    Value=../LogWeb
Category=StarServer-Core  Name=DataPath   Value=../Data
Category=StarServer-XCode Name=Migration  Value=On
```

- `Name` 用 `:` 表示层级（与 `DbConfigProvider` 一致），如 `Sys:Name`。
- 值 <200 字符存 `Value`，否则存 `LongValue`。
- 复用 `Parameter` 的唯一索引 `(UserID,Category,Name)` 与 `UpdateTime` 索引。
- `AppId` 默认由宿主指定（通常为 `SysConfig.Current.Name`），不同应用用不同前缀，互不影响。

## 2. 基本用法

```csharp
var sync = new ConfigSyncManager("StarServer");
sync.AddCommon()                  // Core + Sys + XCode
    .Add<StarSetting>("Star", skip: [nameof(StarSetting.Secret)]);

sync.Start();                     // 首次延迟5秒，之后默认每60秒检查
sync.CheckNow("Core");            // 事件到达时立即检查
sync.Stop();
```

常用封装：

| 方法 | 配置类型 | 默认跳过字段 |
| --- | --- | --- |
| `AddCommon(skip)` | `NewLife.Setting`(Core) + `NewLife.Common.SysConfig`(Sys) + `XCodeSetting`(XCode) | Core：`DataPath`、`LogPath`、`BackupPath`、`PluginPath`；Sys：`Instance`、`InstallTime` |
| `Add<TConfig>(scope, skip)` | 任意 `Config<T>` | 由调用方指定 |

自定义 `skip` 会与各组默认跳过字段合并，不会覆盖默认值。`Period` 最小 60 秒。

## 3. 同步规则

1. 读取本地配置文件与 `Category` 下所有参数行，取 `Max(UpdateTime)`。
2. 首次检查（或参数表为空）时，把本地配置的非跳过键补写进参数表，保证参数表拥有全部配置项。
3. 之后不再自动补缺失键，避免把本机后加的键变成全局配置；新增配置项在下次进程启动时补齐。
4. 判断是否覆盖：
   - `ParameterMax > 本地文件修改时间` → 用参数表所有非跳过键整份覆盖本地文件，并把本地文件时间推进到 `ParameterMax`；
   - 本地文件在参数之后修改过 → 保留本地，允许两边不一致；
   - 本地文件不存在时视为最旧，确保新节点拿到历史配置。
5. 应用完成后把本地文件修改时间推进到 `ParameterMax`，相当于“该库版本已应用”的标记，因此不需要额外的标记文件；库里没有新版本时不会重复应用。
6. 参数表任意一行更新都会触发整份覆盖；`skip` 字段不读取、不写入、不覆盖。整份覆盖时若内容与本地完全一致，文件提供者按内容比较后不会重写文件。

## 4. 配置页读写

`ConfigSyncManager` 还为 Cube 配置页提供两个方法：

- `LoadView<TConfig>(scope)`：以本地配置为底，合并参数表里的非跳过键，返回给页面显示。
- `SaveBoth<TConfig>(scope, model)`：先把模型写入参数表（排除 skip），再把非跳过键写回本地文件（skip 保留本机值），最后触发 `Changed` 事件。

宿主可订阅 `Changed`，通过 Redis/星尘事件总线通知其它实例立即调用 `CheckNow`；没有总线时依靠 60 秒轮询最终一致。

`LoadView`/`SaveBoth` 只适用于 `FileConfigProvider`；`CubeSetting`、`StarServerSetting` 等数据库 Provider 配置不参与。

## 5. 生效范围

- 立即热更新：`CommandTimeout`、`BatchSize`、`SQLPath`、缓存过期、`LogMaskMode` 等运行时每次读取的项。
- 需要重启：`ShowSQL`、`Migration`、`NameFormat`、`UseParameter`、`TraceSQLTime`、`Debug` 等在 `DbBase` 构造或类型初始化时复制的项。本地文件已写回，滚动重启后生效。

## 6. 注意事项

- 各服务器需要 NTP 时钟同步：`Parameter.UpdateTime` 由写库应用生成，本地文件时间由读取应用生成。
- `Parameter.UpdateTime` 明显超前于本机时间（时钟漂移）时，本机对已同步键的临时修改会被库值反复覆盖，直到本机时间追上该时间戳；需要长期保留本机取值请改用 `skip`。
- 数据库时间精度到秒时，与本地文件同秒的边界可能漏一次，下一轮参数更新会纠正。
- 多实例首次启动同时播种时，依赖参数表唯一索引兜底；建议首次部署前先对齐本地文件。
- 删除或重命名配置项后，Parameter 中的旧键不会自动删除；需手工清理，避免旧键被重新写回本地文件。
- 不接管 `appsettings.json`、连接字符串、星尘 Server/Secret 等启动必需项。
- 整份覆盖会抹掉节点对已同步键的本地修改；这是“参数表更新后全局一致”的预期语义。

## 7. 与 DbConfigProvider 的区别

| | `DbConfigProvider` | `ConfigSyncManager` |
| --- | --- | --- |
| 运行时来源 | 直接读数据库 | 本地文件 |
| 适用配置 | 业务配置、可随时改 | 启动期配置（Core/Sys/XCode/Star） |
| 热更新 | Provider 定时刷新 | 写本地文件后由文件监听热更新 |
| 是否允许各实例不同 | 否（共享同一份） | 是（Parameter 未更新时本地优先） |
