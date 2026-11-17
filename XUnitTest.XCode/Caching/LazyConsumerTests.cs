using System.Threading;
using System.Threading.Tasks;
using XCode.Cache;
using Xunit;

namespace XUnitTest.XCode.Caching;

public class LazyConsumerTests
{
    [Fact(DisplayName = "惰性消费者并发入队不丢任务")]
    public void Run_ConcurrentEnqueue_RunsAll()
    {
        var consumer = new LazyConsumer();
        var count = 0;
        var total = 5000;

        Parallel.For(0, total, _ => consumer.Run(() => Interlocked.Increment(ref count)));

        // 等待处理线程把队列清空
        for (var i = 0; i < 200 && Volatile.Read(ref count) < total; i++) Thread.Sleep(20);

        Assert.Equal(total, count);
    }
}
