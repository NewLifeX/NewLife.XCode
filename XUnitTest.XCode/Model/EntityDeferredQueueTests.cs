using System;
using XCode.Model;
using Xunit;

namespace XUnitTest.XCode.Model;

/// <summary>实体延迟队列测试</summary>
public class EntityDeferredQueueTests
{
    [Fact(DisplayName = "EntityDeferredQueue_创建实例")]
    public void CreateInstance()
    {
        var q = new EntityDeferredQueue();
        Assert.NotNull(q);
    }

    [Fact(DisplayName = "EntityDeferredQueue_默认周期")]
    public void DefaultPeriod()
    {
        var q = new EntityDeferredQueue();
        // 默认周期 10 秒
        Assert.Equal(10_000, q.Period);
    }

    [Fact(DisplayName = "EntityDeferredQueue_最大单行保存")]
    public void MaxSingle()
    {
        var q = new EntityDeferredQueue();
        // 默认最大单行保存大小 2
        Assert.Equal(2, q.MaxSingle);
    }
}
