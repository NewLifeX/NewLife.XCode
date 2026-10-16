# ASP.NET Core 行为监测迁移指南

> ⚠️ **部分过时**：迁移源 `XCode.Web`（`DbRunTimeModule`/`UserBehaviorModule`）已于 2026-07-15（v12.1 清理，提交 `f033bd45b`）从仓库移除；示例中的 `VisitStat`/`VisitStatModel`（原内置访问统计实体）同期移除，需替换为自建统计实体。中间件设计思路仍可参考。

原 `XCode.Web`（已移除）中的 `DbRunTimeModule` 与 `UserBehaviorModule` 使用的是经典 `HttpModule`（`#if !__CORE__`）。本文给出在 ASP.NET Core 中等价落地的迁移方案。

## 1. 迁移目标

把以下能力迁移到中间件：

- 请求耗时 + SQL 次数统计
- 在线状态更新
- 行为日志
- 访问统计（`VisitStat.Process`）

## 2. 中间件设计

建议拆成两层：

1. `DbRuntimeMiddleware`
   - 记录请求前后 `DAL.QueryTimes/ExecuteTimes`
   - 计算总耗时
   - 输出到日志或响应 Header
2. `UserBehaviorMiddleware`
   - 获取用户/IP/路径/标题
   - 记录在线、行为、统计

## 3. 建议数据流

```text
Request Begin
  -> 采集起始时间 + 起始SQL计数
  -> next()
  -> 采集结束计数与异常
  -> 组装 VisitStatModel
  -> VisitStat.Process(...)
Request End
```

## 4. 核心示例（简化）

```csharp
public class UserBehaviorMiddleware
{
    private readonly RequestDelegate _next;

    public UserBehaviorMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx)
    {
        var begin = DateTime.Now;
        var q1 = DAL.QueryTimes;
        var e1 = DAL.ExecuteTimes;
        Exception? ex = null;

        try
        {
            await _next(ctx).ConfigureAwait(false);
        }
        catch (Exception err)
        {
            ex = err;
            throw;
        }
        finally
        {
            var cost = (Int32)(DateTime.Now - begin).TotalMilliseconds;
            var page = NormalizePath(ctx.Request.Path);
            var ip = GetIP(ctx);
            var user = ctx.User?.Identity?.Name;

            var model = new VisitStatModel
            {
                Time = DateTime.Now,
                Page = page,
                Title = page,
                Cost = cost,
                User = user,
                IP = ip,
                Error = ex?.Message
            };

            VisitStat.Process(model, StatLevels.Day, StatLevels.Month, StatLevels.Year);

            var q = DAL.QueryTimes - q1;
            var c = DAL.ExecuteTimes - e1;
            ctx.Response.Headers["X-DbRuntime"] = $"Q={q};E={c};Cost={cost}ms";
        }
    }

    private static String NormalizePath(PathString path)
    {
        var p = path.Value + "";
        return p;
    }

    private static String GetIP(HttpContext ctx)
    {
        var ip = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (ip.IsNullOrEmpty()) ip = ctx.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (ip.IsNullOrEmpty()) ip = ctx.Connection.RemoteIpAddress + "";
        return ip + "";
    }
}
```

> 说明：示例用于迁移思路，实际项目请按团队日志规范、隐私合规与代理信任链做增强。

## 5. 注册顺序建议

放在认证之后、终结点之前：

```csharp
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<UserBehaviorMiddleware>();
app.MapControllers();
```

## 6. 与原 HttpModule 对照

- `BeginRequest` -> 中间件进入前
- `PostRequestHandlerExecute` -> `await _next` 之后
- `Error` -> catch/finally
- `PostReleaseRequestState` -> finally

## 7. 迁移注意事项

- Core 下没有 `HttpContext.Current`，全部走 DI/参数注入。
- 标题提取需由业务自己提供（如 endpoint metadata）。
- 静态资源过滤应在中间件内快速返回，避免统计噪声。
- 高流量场景建议把 `VisitStat.Process` 包装为后台队列异步落库。

## 8. 渐进迁移策略

1. 先迁移 `DbRuntime` Header（最低风险）。
2. 再迁移行为日志。
3. 最后迁移统计写入，并观察数据库写入压力。

这样可以平滑替换老 HttpModule，且便于逐步验收。