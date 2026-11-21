namespace XCode.Cache;

/// <summary>计数学习状态。记录单张表的精确计数上限、实测吞吐率与下次允许时间，供 Meta.Count 自适应使用</summary>
public class CountStat
{
    #region 常量
    /// <summary>精确计数行数上限的最小值</summary>
    internal const Int64 MinLimit = 10_000;

    /// <summary>精确计数行数上限的最大值</summary>
    internal const Int64 MaxLimit = 100_000_000;

    /// <summary>单次成功测量后允许的最大上调倍数</summary>
    internal const Int32 GrowthFactor = 4;

    /// <summary>慢查询阈值（毫秒）。达到该耗时视为昂贵查询，退避一天</summary>
    internal const Int32 SlowDuration = 10_000;

    /// <summary>昂贵查询退避秒数</summary>
    internal const Int32 SlowBackoffSeconds = 24 * 3600;

    /// <summary>参与吞吐率学习的最小样本行数。小表固定开销大，样本不具备外推价值</summary>
    internal const Int64 MinSampleRows = 100_000;

    /// <summary>探针间隔（天）。向上探测与失败重试最多每天一次，与“每表每天最多一次昂贵查询”一致</summary>
    internal const Int32 ProbeIntervalDays = 1;

    /// <summary>向上探测倍数。尚未测量的表，快速统计值不超过上限的该倍数时允许后台试一次</summary>
    internal const Int64 GrowthProbeFactor = 10;

    /// <summary>全局后台精确计数闸门。同一时刻只允许一个后台 count(*)，避免慢库被并发统计压垮</summary>
    internal static readonly SemaphoreSlim Gate = new(1, 1);
    #endregion

    #region 属性
    /// <summary>允许精确计数的参考行数上限。快速统计值不超过该值时才尝试精确计数</summary>
    public Int64 RowLimit { get; set; }

    /// <summary>实测吞吐率（行/秒）。0 表示尚未测量</summary>
    public Double RowsPerSecond { get; set; }

    /// <summary>最近一次精确计数结果</summary>
    public Int64 LastCount { get; set; }

    /// <summary>最近一次精确计数耗时，单位毫秒</summary>
    public Int32 LastDurationMs { get; set; }

    /// <summary>下次允许精确计数的时间。用于跨重启的退避约束</summary>
    public DateTime NextAllowed { get; set; }

    /// <summary>连续超时次数</summary>
    public Int32 Timeouts { get; set; }

    /// <summary>最近一次精确计数尝试时间。用于周期性探测，避免阈值长期卡死</summary>
    public DateTime LastAttempt { get; set; }

    /// <summary>初始化时使用的行数上限。配置值变化时据此重置学习状态</summary>
    public Int64 InitFloor { get; set; }
    #endregion

    #region 方法
    /// <summary>初始化行数上限。配置值未变化时保留学习结果，变化时重置以便重新学习</summary>
    /// <param name="floor">初始行数上限</param>
    internal void Init(Int64 floor)
    {
        if (RowLimit > 0 && InitFloor == floor) return;

        RowLimit = floor > 0 ? (floor > MaxLimit ? MaxLimit : floor) : MaxLimit;
        InitFloor = floor;
        RowsPerSecond = 0;
        LastCount = 0;
        LastDurationMs = 0;
        LastAttempt = default;
        Timeouts = 0;
        NextAllowed = default;
    }

    /// <summary>预测指定行数的精确计数耗时，单位秒。吞吐率未知时返回 -1</summary>
    /// <param name="rows">参考行数</param>
    /// <returns></returns>
    internal Double PredictSeconds(Int64 rows)
    {
        if (RowsPerSecond <= 0 || rows <= 0) return -1;

        return rows / RowsPerSecond;
    }

    /// <summary>是否需要探测。超时后的表每天最多探测一次；尚未测量的表允许向上探测，让上限有机会增长</summary>
    /// <param name="now">当前时间</param>
    /// <param name="fast">快速统计行数</param>
    /// <returns></returns>
    internal Boolean ShouldProbe(DateTime now, Int64 fast)
    {
        // 发生过超时：上限可能偏低，允许探测（实际间隔由 NextAllowed 的 24 小时退避约束）
        if (Timeouts > 0) return true;

        // 尚未测量：快速统计值略高于上限时允许向上探测，避免上限永远长不上去
        // 仅对 >=10 万行的表探测，小表样本不足以学习吞吐率
        if (RowsPerSecond <= 0 && fast >= MinSampleRows && fast > RowLimit && fast <= RowLimit * GrowthProbeFactor)
            return LastAttempt == default || now >= LastAttempt.AddDays(ProbeIntervalDays);

        return false;
    }

    /// <summary>记录一次成功的精确计数，并按实测吞吐率调整上限</summary>
    /// <param name="count">精确行数</param>
    /// <param name="durationMs">实测耗时，单位毫秒</param>
    /// <param name="timeoutSeconds">本次超时预算，单位秒</param>
    /// <param name="refreshSeconds">正常刷新间隔，单位秒</param>
    /// <param name="now">当前时间</param>
    /// <returns>上限是否发生变化</returns>
    internal Boolean OnSuccess(Int64 count, Int32 durationMs, Int32 timeoutSeconds, Int32 refreshSeconds, DateTime now)
    {
        LastCount = count;
        LastDurationMs = durationMs;
        LastAttempt = now;
        Timeouts = 0;

        var changed = false;
        if (count >= MinSampleRows)
        {
            // 指数移动平均，避免单次抖动
            var ms = durationMs > 0 ? durationMs : 1;
            var rps = count * 1000.0 / ms;
            RowsPerSecond = RowsPerSecond <= 0 ? rps : (RowsPerSecond + rps) / 2;

            // 目标耗时取预算的一半，安全系数 0.8，给下次尝试留足余量
            var target = (timeoutSeconds > 0 ? timeoutSeconds : 15) * 0.4;
            var suggest = (Int64)(RowsPerSecond * target);

            // 单次上调限幅，避免偶然一次很快就把上限推到极值
            var current = RowLimit > 0 ? RowLimit : MaxLimit;
            var max = current * GrowthFactor;
            if (max > MaxLimit || max < current) max = MaxLimit;
            if (suggest > max) suggest = max;

            // 已成功测量的行数本身就是可行下界
            if (suggest < count) suggest = count;
            if (suggest > MaxLimit) suggest = MaxLimit;
            if (suggest < MinLimit) suggest = MinLimit;

            // 成功只上调，不因单次测量收缩，收缩交给超时路径
            if (suggest > RowLimit)
            {
                changed = true;
                RowLimit = suggest;
            }
        }

        NextAllowed = now.AddSeconds(durationMs >= SlowDuration ? SlowBackoffSeconds : refreshSeconds);
        return changed;
    }

    /// <summary>记录一次超时，收缩上限并退避一天</summary>
    /// <param name="reference">超时前的参考行数</param>
    /// <param name="now">当前时间</param>
    /// <returns>上限是否发生变化</returns>
    internal Boolean OnTimeout(Int64 reference, DateTime now)
    {
        Timeouts++;
        LastAttempt = now;

        var current = RowLimit > 0 ? RowLimit : MaxLimit;
        var target = reference > 0 ? reference / 2 : current / 2;
        if (target > current) target = current;
        if (target < MinLimit) target = MinLimit;
        if (target > MaxLimit) target = MaxLimit;

        var changed = target != RowLimit;
        RowLimit = target;
        NextAllowed = now.AddSeconds(SlowBackoffSeconds);
        return changed;
    }

    /// <summary>判断异常是否为超时。客户端取消不保证服务器端立即停止，耗时接近预算也按超时处理，避免反复触发</summary>
    /// <param name="ex">异常</param>
    /// <param name="elapsed">已耗时</param>
    /// <param name="timeoutSeconds">超时预算，单位秒</param>
    /// <returns></returns>
    internal static Boolean IsTimeout(Exception ex, TimeSpan elapsed, Int32 timeoutSeconds)
    {
        if (timeoutSeconds > 0 && elapsed.TotalSeconds >= timeoutSeconds * 0.8) return true;

        if (ex.GetType().Name.IndexOf("Timeout", StringComparison.OrdinalIgnoreCase) >= 0) return true;

        var msg = ex.Message;
        if (ex.InnerException != null) msg += " " + ex.InnerException.Message;

        return msg.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
            || msg.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
            || msg.IndexOf("超时", StringComparison.Ordinal) >= 0
            || msg.IndexOf("canceling statement", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>记录一次非超时的失败，保持上限不变，稍后重试</summary>
    /// <param name="now">当前时间</param>
    /// <param name="retrySeconds">重试间隔，单位秒</param>
    internal void OnError(DateTime now, Int32 retrySeconds)
    {
        LastAttempt = now;
        NextAllowed = now.AddSeconds(retrySeconds);
    }
    #endregion
}
