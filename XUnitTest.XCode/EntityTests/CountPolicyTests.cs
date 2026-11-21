using System;
using System.IO;
using XCode.Cache;
using Xunit;

namespace XUnitTest.XCode.EntityTests;

/// <summary>计数自适应策略测试</summary>
public class CountPolicyTests
{
    [Fact(DisplayName = "成功测量后按吞吐率上调上限")]
    public void Success_RaisesLimit()
    {
        var stat = new CountStat { RowLimit = 10_000_000 };
        var now = new DateTime(2026, 1, 1, 0, 0, 0);

        var changed = stat.OnSuccess(990_000, 500, 15, 600, now);

        Assert.True(changed);
        Assert.True(stat.RowLimit > 10_000_000);
        Assert.Equal(1_980_000, stat.RowsPerSecond, 0);
        Assert.Equal(990_000, stat.LastCount);
        Assert.Equal(500, stat.LastDurationMs);
        Assert.Equal(0, stat.Timeouts);
        Assert.Equal(now.AddSeconds(600), stat.NextAllowed);
    }

    [Fact(DisplayName = "单次上调不超过限幅")]
    public void Success_GrowthCapped()
    {
        var stat = new CountStat { RowLimit = 1_000_000 };

        stat.OnSuccess(150_000, 10, 15, 600, DateTime.Now);

        Assert.Equal(4_000_000, stat.RowLimit);
    }

    [Fact(DisplayName = "成功但耗时超过慢查询阈值时退避一天")]
    public void SlowSuccess_BacksOffOneDay()
    {
        var stat = new CountStat { RowLimit = 10_000_000 };
        var now = DateTime.Now;

        stat.OnSuccess(20_000_000, 12_000, 15, 3600, now);

        Assert.True(stat.RowLimit >= 20_000_000);
        Assert.True(stat.NextAllowed >= now.AddHours(23));
    }

    [Fact(DisplayName = "小表样本不参与吞吐率学习")]
    public void SmallSample_KeepsLimit()
    {
        var stat = new CountStat { RowLimit = 10_000_000 };

        var changed = stat.OnSuccess(4, 1, 5, 60, DateTime.Now);

        Assert.False(changed);
        Assert.Equal(10_000_000, stat.RowLimit);
        Assert.Equal(0, stat.RowsPerSecond);
        Assert.Equal(4, stat.LastCount);
    }

    [Fact(DisplayName = "超时后上限减半并退避一天")]
    public void Timeout_HalvesLimit()
    {
        var stat = new CountStat { RowLimit = 10_000_000 };
        var now = DateTime.Now;

        var changed = stat.OnTimeout(9_900_000, now);

        Assert.True(changed);
        Assert.Equal(4_950_000, stat.RowLimit);
        Assert.Equal(1, stat.Timeouts);
        Assert.True(stat.NextAllowed >= now.AddHours(23));
    }

    [Fact(DisplayName = "连续超时继续减半但有下限")]
    public void Timeout_RepeatsWithFloor()
    {
        var stat = new CountStat { RowLimit = 20_000 };
        var now = DateTime.Now;

        stat.OnTimeout(10_000, now);
        Assert.Equal(10_000, stat.RowLimit);

        stat.OnTimeout(10_000, now);
        Assert.Equal(10_000, stat.RowLimit);
        Assert.Equal(2, stat.Timeouts);
    }

    [Fact(DisplayName = "吞吐率未知时不做耗时预测")]
    public void Predict_Unknown()
    {
        var stat = new CountStat();

        Assert.Equal(-1, stat.PredictSeconds(1_000_000), 3);

        stat.RowsPerSecond = 1_000_000;

        Assert.Equal(1, stat.PredictSeconds(1_000_000), 3);
    }

    [Fact(DisplayName = "配置初始值只在未学习时生效")]
    public void Init_UsesFloorOnce()
    {
        var stat = new CountStat();
        stat.Init(10_000_000);
        Assert.Equal(10_000_000, stat.RowLimit);

        stat.RowLimit = 5_000_000;
        stat.Init(10_000_000);
        Assert.Equal(5_000_000, stat.RowLimit);
    }

    [Fact(DisplayName = "配置值变化时重置学习状态")]
    public void Init_ResetsOnConfigChange()
    {
        var stat = new CountStat();
        stat.Init(10_000_000);
        stat.RowLimit = 5_000_000;
        stat.RowsPerSecond = 1_000_000;
        stat.Timeouts = 2;
        stat.NextAllowed = DateTime.Now.AddDays(1);

        stat.Init(1_000_000);

        Assert.Equal(1_000_000, stat.RowLimit);
        Assert.Equal(1_000_000, stat.InitFloor);
        Assert.Equal(0, stat.RowsPerSecond);
        Assert.Equal(0, stat.Timeouts);
        Assert.Equal(default, stat.LastAttempt);
        Assert.Equal(default, stat.NextAllowed);
    }

    [Fact(DisplayName = "探测覆盖超时恢复与向上学习")]
    public void Probe_CoversTimeoutAndGrowth()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0);

        // 未测量且略高于上限：允许向上探测，但不超过上限 10 倍
        var stat = new CountStat { RowLimit = 1_000_000 };
        Assert.True(stat.ShouldProbe(now, 5_000_000));
        Assert.False(stat.ShouldProbe(now, 20_000_000));

        // 小表不探测：样本不足以学习吞吐率
        Assert.False(stat.ShouldProbe(now, 4));

        // 失败后至少间隔一天
        stat.LastAttempt = now;
        Assert.False(stat.ShouldProbe(now.AddHours(12), 5_000_000));
        Assert.True(stat.ShouldProbe(now.AddDays(1), 5_000_000));

        // 已测量过的表不再向上探测
        stat.RowsPerSecond = 1_000_000;
        Assert.False(stat.ShouldProbe(now.AddDays(2), 5_000_000));

        // 发生过超时的表允许探测，实际间隔由 NextAllowed 的 24 小时退避约束
        var timeout = new CountStat { RowLimit = 1_000_000, Timeouts = 1 };
        Assert.True(timeout.ShouldProbe(now, 100_000_000));
    }

    [Fact(DisplayName = "超时判定覆盖耗时与关键字")]
    public void Timeout_Detection()
    {
        var ex = new InvalidOperationException("permission denied");

        Assert.False(CountStat.IsTimeout(ex, TimeSpan.FromSeconds(1), 15));

        // 耗时达到预算 80% 即按超时处理
        Assert.True(CountStat.IsTimeout(ex, TimeSpan.FromSeconds(12), 15));

        Assert.True(CountStat.IsTimeout(new InvalidOperationException("Execution Timeout Expired"), TimeSpan.FromSeconds(1), 15));
        Assert.True(CountStat.IsTimeout(new InvalidOperationException("查询超时"), TimeSpan.FromSeconds(1), 15));
        Assert.True(CountStat.IsTimeout(new InvalidOperationException("canceling statement due to statement timeout"), TimeSpan.FromSeconds(1), 15));
    }

    [Fact(DisplayName = "非超时错误只延后重试不改变上限")]
    public void Error_KeepsLimit()
    {
        var stat = new CountStat { RowLimit = 10_000_000 };
        var now = DateTime.Now;

        stat.OnError(now, 1_800);

        Assert.Equal(10_000_000, stat.RowLimit);
        Assert.Equal(now.AddSeconds(1_800), stat.NextAllowed);
    }

    [Fact(DisplayName = "计数学习状态可持久化还原")]
    public void DataCache_RoundTrip()
    {
        var file = Path.GetTempFileName();
        try
        {
            var dc = new DataCache();
            dc.CountStats["conn_Table_Entity"] = new CountStat
            {
                RowLimit = 12_345_678,
                InitFloor = 10_000_000,
                RowsPerSecond = 2_000_000,
                LastCount = 12_000_000,
                LastDurationMs = 6000,
                NextAllowed = new DateTime(2030, 1, 2, 3, 4, 5),
                LastAttempt = new DateTime(2029, 12, 31, 1, 2, 3),
                Timeouts = 2,
            };
            DataCache.Save(file, dc);

            var dc2 = DataCache.Load(file);

            Assert.NotNull(dc2);
            var stat = dc2!.CountStats["conn_Table_Entity"];
            Assert.Equal(12_345_678, stat.RowLimit);
            Assert.Equal(10_000_000, stat.InitFloor);
            Assert.Equal(2_000_000, stat.RowsPerSecond, 0);
            Assert.Equal(12_000_000, stat.LastCount);
            Assert.Equal(6000, stat.LastDurationMs);
            Assert.Equal(new DateTime(2030, 1, 2, 3, 4, 5), stat.NextAllowed);
            Assert.Equal(new DateTime(2029, 12, 31, 1, 2, 3), stat.LastAttempt);
            Assert.Equal(2, stat.Timeouts);
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }
}
