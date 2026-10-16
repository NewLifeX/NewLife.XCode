# Web 行为模块与运行时监测（Web）

> ⚠️ **已移除**：`XCode.Web` 模块已于 2026-07-15（v12.1 清理，提交 `f033bd45b`）从仓库删除，其依赖的 `VisitStat` 内置实体同期移除。本文档作为历史设计参考保留，当前版本不再包含该模块；ASP.NET Core 场景见《ASP.NET Core行为监测迁移指南》。

`XCode.Web` 提供针对传统 ASP.NET（非 `__CORE__`）场景的轻量 HTTP 模块，用于采集请求耗时、数据库执行次数、用户行为日志与访问统计。

## 模块清单

- `DbRunTimeModule`
  - 统计单次请求内数据库查询次数、执行次数与总耗时
- `UserBehaviorModule`
  - 记录在线状态、访问行为、访问统计
  - 采集用户、页面、IP、耗时、异常信息

> 代码通过 `#if !__CORE__` 包裹，仅在 .NET Framework Web 应用生效。

## 1）DbRunTimeModule

核心思路：

1. 请求开始记录 `DAL.QueryTimes / DAL.ExecuteTimes`
2. 请求结束计算差值 + 总耗时
3. 输出格式化字符串

可配置输出模板：

```csharp
DbRunTimeModule.DbRunTimeFormat = "查询{0}次，执行{1}次，耗时{2:n0}毫秒！";
```

适合用于页面底部调试信息、性能巡检。

## 2）UserBehaviorModule

挂接事件：

- `AcquireRequestState`
- `PostRequestHandlerExecute`
- `Error`
- `BeginRequest`
- `PostReleaseRequestState`

主要能力：

- `WebOnline`：更新用户在线状态（`UserOnline.SetWebStatus`）
- `WebBehavior`：写访问日志（`LogProvider.Provider.WriteLog`）
- `WebStatistics`：写页面访问统计（`VisitStat.Process`）

三项可独立开关。

## 3）IP 与页面提取策略

IP 获取顺序大致为：

1. `HTTP_X_FORWARDED_FOR`
2. `X-Real-IP`
3. `X-Forwarded-For`
4. `REMOTE_ADDR`
5. `UserHostName/UserHostAddress`

页面路径会自动做简单归一化：
- 对末段纯数字路径做裁剪（减少维度爆炸）
- 过滤静态资源后缀（js/css/png/svg/woff...）

## 4）行为消息结构

记录信息通常包括：

- 页面标题
- HTTP 方法 + RawUrl
- 异常信息（若有）
- 请求耗时（ms）

便于在日志系统中快速定位慢请求和错误请求。

## 5）启用建议

- 生产环境默认开启 `WebBehavior`，谨慎开启高频 `WebStatistics`（视流量而定）。
- 反向代理场景务必确认真实 IP 头可信来源。
- 统计维度过多时，先做页面归一化，避免表增长过快。

## 6）与 Statistics 模块联动

`UserBehaviorModule` 里统计写入调用了 `VisitStat.Process(model)`，可与 `XCode.Statistics` 的分层聚合模型联动，形成日/月/年趋势报表。

## 7）注意事项

- 模块代码为经典 ASP.NET HttpModule 形态，ASP.NET Core 需用中间件重写同等逻辑。
- 项目中存在文件名 `UserBehaviorModule..cs`（双点），不影响编译引用时请关注 IDE 显示与脚本工具兼容性。

## 8）排障清单

- 没有日志：检查 `WebBehavior` 是否启用。
- 统计缺失：检查 `WebStatistics` 开关与 `VisitStat` 实现。
- IP 不准：检查代理头传递与信任链设置。