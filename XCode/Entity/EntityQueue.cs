using System.Collections.Concurrent;
using System.Diagnostics;
using NewLife;
using NewLife.Log;
using NewLife.Threading;
using XCode.DataAccessLayer;

namespace XCode;

/// <summary>实体队列。支持凑批更新数据，包括Insert/Update/Delete/Upsert</summary>
public class EntityQueue(IEntitySession session) : DisposeBase
{
    #region 属性
    /// <summary>需要近实时保存的实体队列</summary>
    private ConcurrentDictionary<IEntity, IEntity> Entities { get; } = new ConcurrentDictionary<IEntity, IEntity>();

    /// <summary>需要延迟保存的实体队列</summary>
    private ConcurrentDictionary<IEntity, DateTime> DelayEntities { get; } = new ConcurrentDictionary<IEntity, DateTime>();

    /// <summary>调试开关，默认false</summary>
    public Boolean Debug { get; set; }

    /// <summary>数据会话，分表分库时使用</summary>
    public IEntitySession Session { get; } = session;

    /// <summary>是否仅插入。默认false</summary>
    public Boolean InsertOnly { get; set; }

    /// <summary>数据更新方法。Insert/Update/Delete/Upsert/Replace</summary>
    public DataMethod Method { get; set; }

    /// <summary>
    /// 是否显示SQL
    /// </summary>
    public Boolean? ShowSQL { get; set; }

    /// <summary>周期。默认1000毫秒，根据繁忙程度动态调节，尽量靠近每次持久化1000个对象</summary>
    public Int32 Period { get; set; } = 1000;

    /// <summary>最大个数，超过该个数时，进入队列将产生堵塞。默认1_000_000</summary>
    public Int32 MaxEntity { get; set; } = 1_000_000;

    /// <summary>队列满时最长等待时间。单位毫秒，默认15秒，避免死等阻塞业务处理</summary>
    public Int32 OverflowWaitTime { get; set; } = 15_000;

    /// <summary>保存速度，每秒保存多少个实体</summary>
    public Int32 Speed { get; private set; }

    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; } = DAL.GlobalTracer;

    private TimerX? _Timer;
    //private String? _lastTraceId;

    #endregion
    #region 构造
    /// <summary>销毁时，持久化队列</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _Timer.TryDispose();

        var count = DelayEntities.Count + Entities.Count;
        if (count > 0)
        {
            var task = Task.Run(() =>
            {
                // 等待正在进行的 Work 退出，否则本次摘取被并发保护直接跳过，队列剩余数据丢失
                for (var i = 0; i < 30 && Volatile.Read(ref _working) != 0; i++) Thread.Sleep(100);

                Work(null);
            });
            if (!task.Wait(3_000))
            {
                using var span = DefaultTracer.Instance?.NewError("db:EntityQueueNotEmptyOnDispose", L.T("Entity queue of [{0}] still has [{1:n0}] items unsaved on dispose.", Session.TableName, count));
            }
        }
    }
    #endregion

    #region 方法
    /// <summary>添加实体对象进入队列</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="msDelay">延迟保存的时间</param>
    /// <returns>返回是否添加成功，实体对象已存在于队列中则返回false</returns>
    /// <exception cref="XCodeException">队列已满且等待超时</exception>
    public Boolean Add(IEntity entity, Int32 msDelay)
    {
        // 先判断溢出再入队，避免实体已进队列却向调用方报失败，重试时写重
        if (_count >= MaxEntity) WaitForFree(true);

        //using var span = Tracer?.NewSpan($"db:{Session.ConnName}:AddQueue:{DAL.TrimTableName(Session.TableName)}", new { entity = entity + "", msDelay });

        // 首次使用时初始化定时器
        if (_Timer == null)
        {
            lock (this)
            {
                _Timer ??= new TimerX(Work, null, Period, Period, "EQ") { Async = true };
            }
        }

        //_lastTraceId = DefaultSpan.Current?.ToString();

        // 先加计数再入队，否则与 Drain 的扣减交错会让计数永久偏大
        Interlocked.Increment(ref _count);

        var rs = false;
        if (msDelay <= 0)
            rs = Entities.TryAdd(entity, entity);
        else
            rs = DelayEntities.TryAdd(entity, TimerX.Now.AddMilliseconds(msDelay));
        if (!rs)
        {
            Interlocked.Decrement(ref _count);
            return false;
        }

        // 刚刚推过上限，等待消费形成背压；实体已入队，因此不再报错也不返回false
        if (_count >= MaxEntity) WaitForFree(false);

        return true;
    }

    /// <summary>队列满时等待消费。超时仍满则抛出异常，避免调用方误以为入队成功</summary>
    /// <param name="throwOnFull">仍然满时是否抛出异常。实体已入队后只等待背压，不再抛错</param>
    private void WaitForFree(Boolean throwOnFull)
    {
        var ss = Session;
        using var span = DefaultTracer.Instance?.NewError("db:MaxQueueOverflow", L.T("Entity queue of [{0}] overflowed; exceeded the maximum of {1:n0}.", ss.TableName, MaxEntity), _count);

        // 最多等待一段时间，避免死等阻塞业务处理
        var times = OverflowWaitTime / 100;
        while (_count >= MaxEntity && --times > 0)
        {
            Thread.Sleep(100);
        }

        // 仍然满就明确报错，不再静默返回false让调用方以为已经入队
        if (throwOnFull && _count >= MaxEntity)
            throw new XCodeException(L.T("Entity queue of [{0}] overflowed; exceeded the maximum of {1:n0}.", ss.TableName, MaxEntity));
    }

    /// <summary>当前缓存个数</summary>
    private volatile Int32 _count;

    /// <summary>摘取当前待保存的实体。定时器与单元测试共用</summary>
    /// <remarks>
    /// 逐条原子摘取，不整体替换字典。并发 Add 可能先拿到字典引用再写入，
    /// 一旦替换，这些实体就落在被弃用的字典里，既不会被保存也不会被下一轮枚举到。
    /// </remarks>
    internal List<IEntity> Drain()
    {
        var list = new List<IEntity>();

        // 检查是否有延迟保存
        var ds = DelayEntities;
        if (!ds.IsEmpty)
        {
            var now = TimerX.Now;
            foreach (var item in ds)
            {
                if (item.Value < now && !list.Contains(item.Key)) list.Add(item.Key);
            }
            // 从列表删除过期，只有摘到手的才进入待保存列表
            foreach (var item in list)
            {
                ds.TryRemove(item, out _);
            }
        }

        // 检查是否有近实时保存
        var es = Entities;
        if (!es.IsEmpty)
        {
            foreach (var key in es.Keys)
            {
                if (es.TryRemove(key, out _) && !list.Contains(key)) list.Add(key);
            }
        }

        return list;
    }

    private Int32 _working;

    private void Work(Object? state)
    {
        // 定时器与 Dispose 可能同时触发，两个 Work 同时摘取会让同一实体被写两次
        if (Interlocked.CompareExchange(ref _working, 1, 0) != 0) return;

        try { WorkCore(); }
        finally { Interlocked.Exchange(ref _working, 0); }
    }

    private void WorkCore()
    {
        var list = Drain();

        if (list.Count > 0)
        {
            Interlocked.Add(ref _count, -list.Count);

            var ss = Session;
            DefaultSpan.Current = null;
            using var span = Tracer?.NewSpan($"db:{ss.ConnName}:Queue:{DAL.TrimTableName(ss.TableName)}", list.Count, list.Count);
            //if (_lastTraceId != null) span?.Detach(_lastTraceId);
            //_lastTraceId = null;

            try
            {
                Process(list);
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);
                throw;
            }
        }
    }

    private void Process(Object state)
    {
        if (state is not ICollection<IEntity> list) return;

        var ss = Session;

        var speed = Speed;
        if (Debug || list.Count > 100_000)
        {
            var cost = speed == 0 ? 0 : list.Count * 1000 / speed;
            XTrace.WriteLine("Entity queue [{0}/{1}] saving {2:n0}, estimated {3:n0}ms", ss.TableName, ss.ConnName, list.Count, cost);
        }

        var dss = ss.Dal.Session;
        var old = dss.ShowSQL;
        if (ShowSQL != null) dss.ShowSQL = ShowSQL.Value;

        var sw = Stopwatch.StartNew();

        // 分批
        var batchSize = 10_000;
        for (var i = 0; i < list.Count;)
        {
            var batch = list.Skip(i).Take(batchSize).ToList();
            DefaultSpan.Current?.AppendTag($"batch={batch.Count}");

            try
            {
                OnProcess(batch);
            }
            catch (Exception ex)
            {
                OnError(batch, ex);
            }

            i += batch.Count;
        }

        sw.Stop();

        dss.ShowSQL = old;

        // 根据繁忙程度动态调节
        // 大于1000个对象时，说明需要加快持久化间隔，缩小周期
        // 小于1000个对象时，说明持久化太快了，加大周期
        var p = Period;
        if (list.Count > 1000)
            p = p * 1000 / list.Count;
        else
            p = p * 1000 / list.Count;

        // 最小间隔
        if (p < 500) p = 500;
        // 最大间隔
        if (p > 5000) p = 5000;

        if (p != Period && _Timer != null)
        {
            Period = p;
            _Timer.Period = p;
        }

        var ms = sw.Elapsed.TotalMilliseconds;
        Speed = ms == 0 ? 0 : (Int32)(list.Count * 1000 / ms);
        if (Debug || list.Count > 10000)
        {
            DefaultSpan.Current?.AppendTag($"Cost: {ms}ms");
            XTrace.WriteLine("Entity queue [{0}/{1}] saved {2:n0}, elapsed {3:n0}ms, speed {4:n0}tps, period {5:n0}ms", ss.TableName, ss.ConnName, list.Count, ms, speed, p);
        }

        // 马上再来一次，以便于连续处理数据
        _Timer?.SetNext(-1);
    }

    /// <summary>处理一批数据。插入或更新</summary>
    /// <param name="batch"></param>
    protected virtual void OnProcess(IList<IEntity> batch)
    {
        var ss = Session;

        if (Method > 0)
        {
            switch (Method)
            {
                case DataMethod.Insert:
                    batch.Insert(null, ss);
                    break;
                case DataMethod.Update:
                    batch.Update(null, ss);
                    break;
                case DataMethod.Delete:
                    batch.Delete(null, ss);
                    break;
                case DataMethod.Upsert:
                    batch.Upsert(null, null, null, ss);
                    break;
                case DataMethod.Replace:
                    batch.BatchReplace(option: null, ss);
                    break;
                default:
                    batch.Save(null, ss, false);
                    break;
            }
        }
        else
        {
            // 实体队列SaveAsync异步保存时，如果只插入表，直接走批量Insert，而不是Upsert
            if (InsertOnly)
                batch.Insert(null, ss);
            else
                batch.Save(null, ss, false);
        }
    }

    /// <summary>发生错误</summary>
    /// <param name="list"></param>
    /// <param name="ex"></param>
    protected virtual void OnError(IList<IEntity> list, Exception ex) => XTrace.WriteException(ex);
    #endregion
}