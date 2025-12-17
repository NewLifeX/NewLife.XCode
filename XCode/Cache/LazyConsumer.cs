using System.Collections.Concurrent;

namespace XCode.Cache;

/// <summary>
/// 惰性串行消费者：仅在有任务时启动后台 Task，任务处理完自动退出，空闲时不占线程。
/// 所有任务严格按入队顺序串行执行。
/// </summary>
public class LazyConsumer
{
    private readonly ConcurrentQueue<Action> _queue = new();
    private volatile Int32 _processing = 0; // 0: 空闲, 1: 正在处理

    /// <summary>
    /// 提交一个任务，由内部串行执行
    /// </summary>
    public void Run(Action item)
    {
        _queue.Enqueue(item);

        // 尝试启动处理任务（仅当当前无活跃任务）
        if (Interlocked.CompareExchange(ref _processing, 1, 0) == 0)
        {
            // 成功抢到启动权
            _ = Task.Run(ProcessQueue);
        }
    }

    private void ProcessQueue()
    {
        try
        {
            // 一次性处理当前所有任务（drain）
            while (_queue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch
                {
                    // 可选：记录日志。此处不抛出，避免中断队列
                }
            }
        }
        catch { }
        finally
        {
            // 标记为空闲
            _processing = 0;
        }
    }
}