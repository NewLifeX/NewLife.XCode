using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XCode;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Entity;

[Collection("Database")]
public class EntityQueueTests
{
    [Fact(DisplayName = "实体队列并发入队与摘取不丢失实体")]
    public void Drain_ConcurrentAdd_KeepsAllEntities()
    {
        var ss = Entity<User>.Meta.Session;
        using var queue = new ProbeQueue(ss);

        var total = 0;
        var drained = 0;
        var running = true;

        // 消费者：持续摘取，模拟定时器 Work
        var consumer = Task.Run(() =>
        {
            while (Volatile.Read(ref running))
            {
                var list = queue.Drain();
                Interlocked.Add(ref drained, list.Count);
            }
        });

        // 生产者：多线程入队，主键各不相同，避免队列按主键去重
        Parallel.For(0, 8, t =>
        {
            for (var i = 0; i < 1000; i++)
            {
                var user = new User { ID = t * 1000 + i + 1, Name = "queue" };
                Assert.True(queue.Add(user, 0));
                Interlocked.Increment(ref total);
            }
        });

        Volatile.Write(ref running, false);
        consumer.Wait();

        // 收尾：把剩下的全部摘出来
        drained += queue.Drain().Count;

        Assert.Equal(8000, total);
        Assert.Equal(total, drained);
    }

    /// <summary>只验证入队与摘取，定时器触发时不写库，避免污染共享测试库</summary>
    private sealed class ProbeQueue(IEntitySession session) : EntityQueue(session)
    {
        protected override void OnProcess(IList<IEntity> batch) { }
    }

    [Fact(DisplayName = "达到上限的那一条已入队，必须返回true")]
    public void Add_AtCapacity_ReturnsTrue()
    {
        var ss = Entity<User>.Meta.Session;
        using var queue = new ProbeQueue(ss) { MaxEntity = 2, OverflowWaitTime = 100 };

        var first = new User { ID = 1, Name = "q1" };
        var second = new User { ID = 2, Name = "q2" };
        Assert.True(queue.Add(first, 0));
        Assert.True(queue.Add(second, 0));

        // 到达上限的这一条已经入队，若返回 false，调用方会重试而导致重复写入
        var list = queue.Drain();
        Assert.Equal(2, list.Count);
    }

    [Fact(DisplayName = "队列已满时抛出异常，且该实体未入队")]
    public void Add_WhenFull_ThrowsAndSkipsEnqueue()
    {
        var ss = Entity<User>.Meta.Session;
        using var queue = new ProbeQueue(ss) { MaxEntity = 1, OverflowWaitTime = 100 };

        var first = new User { ID = 1, Name = "q1" };
        Assert.True(queue.Add(first, 0));

        // 队列满时向调用方明确报错，而不是静默返回 false 让人误以为已入队
        Assert.Throws<XCodeException>(() => queue.Add(new User { ID = 2, Name = "q2" }, 0));

        // 被拒绝的实体不能留在队列里，否则调用方重试会写重
        var list = queue.Drain();
        Assert.Single(list);
        Assert.Same(first, list[0]);
    }
}
