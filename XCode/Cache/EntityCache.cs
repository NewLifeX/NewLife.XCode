using System.ComponentModel;
using NewLife.Collections;
using NewLife.Log;
using NewLife.Threading;
using XCode.DataAccessLayer;
using System.Diagnostics.CodeAnalysis;

namespace XCode.Cache;

/// <summary>实体缓存</summary>
/// <remarks>
/// 缓存更新逻辑：
/// 1，初始化。首次访问阻塞等待，确保得到有效数据。
/// 2，定时过期。过期后异步更新缓存返回旧数据，保障性能。但若过期两倍时间，则同步更新缓存阻塞等待返回新数据。
/// 3，主动清除。外部主动清除缓存，强制清除后下次访问时同步更新缓存，非强制清除后下次访问时异步更新缓存。
/// 4，添删改过期。添删改期间，仅修改缓存，不改变过期更新，避免事务中频繁更新缓存，提交回滚事务后强制清除缓存。
/// </remarks>
/// <typeparam name="TEntity">实体类型</typeparam>
[DisplayName("实体缓存")]
public class EntityCache<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TEntity> : CacheBase<TEntity>, IEntityCache where TEntity : Entity<TEntity>, new()
{
    #region 基础属性
    /// <summary>缓存过期时间</summary>
    DateTime ExpiredTime { get; set; } = DateTime.Now.AddHours(-1);

    private volatile Int32 _Times;
    /// <summary>缓存更新次数</summary>
    public Int32 Times => _Times;

    /// <summary>本地记录的分布式缓存版本号</summary>
    private Int64 _distributedVersion;

    /// <summary>过期时间。单位是秒，默认10秒</summary>
    public Int32 Expire { get; set; }

    /// <summary>填充数据的方法</summary>
    public Func<IList<TEntity>> FillListMethod { get; set; } = Entity<TEntity>.FindAll;

    /// <summary>是否等待第一次查询。如果不等待，第一次返回空集合。默认true</summary>
    public Boolean WaitFirst { get; set; } = true;

    /// <summary>是否在使用缓存，在不触发缓存动作的情况下检查是否有使用缓存</summary>
    public Boolean Using { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化实体缓存</summary>
    public EntityCache()
    {
        var exp = XCodeSetting.Current.EntityCacheExpire;
        if (exp <= 0) exp = 10;

        Expire = exp;

        LogPrefix = $"EntityCache<{typeof(TEntity).Name}>";
    }
    #endregion

    #region 缓存核心
    private TEntity[] _Entities = [];
    /// <summary>缓存同步锁。保护快照交换与刷新期间的变更日志，避免刷新旧快照覆盖并发添删改</summary>
    private readonly Object _sync = new();
    /// <summary>刷新期间的变更日志。刷新完成后按顺序回放到新快照，避免被读库旧快照覆盖</summary>
    private List<CacheChange>? _journal;
    /// <summary>刷新锁。串行化缓存刷新，避免同步刷新与异步刷新并发时相互覆盖</summary>
    private readonly Object _refresh = new();

    /// <summary>缓存变更类型</summary>
    private enum CacheChangeKind
    {
        /// <summary>新增或更新</summary>
        Upsert,

        /// <summary>删除</summary>
        Remove,
    }

    /// <summary>缓存变更记录</summary>
    /// <param name="Kind">变更类型</param>
    /// <param name="Entity">目标实体</param>
    private readonly record struct CacheChange(CacheChangeKind Kind, TEntity Entity);

    /// <summary>实体集合。无数据返回空集合而不是null</summary>
    public IList<TEntity> Entities
    {
        get
        {
            CheckCache();

            return _Entities;
        }
    }

    void CheckCache()
    {
        // 更新统计信息
        CheckShowStatics(ref Total, ShowStatics);

        // 检查分布式缓存版本号（二级缓存失效通知）
        CheckDistributedVersion();

        var sec = (TimerX.Now - ExpiredTime).TotalSeconds;
        if (sec < 0)
        {
            Interlocked.Increment(ref Success);
            return;
        }

        /*
         * 来到这里，都是缓存超时的线程
         * 1，第一次请求，还没有缓存数据，需要等待首次数据
         *      WaitFirst   加锁等待
         *      !WaitFirst  跳过等待
         * 2，缓存过期，开个异步更新，继续走
         */

        if (_Times == 0)
        {
            if (WaitFirst)
            {
                lock (_refresh)
                {
                    if (_Times == 0) UpdateCache("first time");
                }
            }
            else
            {
                if (Monitor.TryEnter(_refresh, 5000))
                {
                    try
                    {
                        if (_Times == 0) UpdateCache("first time");
                    }
                    finally
                    {
                        Monitor.Exit(_refresh);
                    }
                }
            }
        }
        else
        {
            var s = ExpiredTime == DateTime.MinValue ? "forcibly expired" : $"expired for {sec:n2}s";
            var msg = $"validity {Expire}s, {s}";

            // 频繁更新下，采用异步更新缓存，以提升吞吐。非频繁访问时（2倍超时），同步更新
            if (sec < Expire)
                UpdateCacheAsync(msg);
            else
                UpdateCache(msg);
        }

        // 只要访问了实体缓存数据集合，就认为是使用了实体缓存，允许更新缓存数据期间向缓存集合添删数据
        Using = true;
    }

    /// <summary>检查分布式缓存版本号，如果有更新则强制过期本地缓存</summary>
    private void CheckDistributedVersion()
    {
        if (CacheInvalidator.Provider == null) return;

        var remoteVer = CacheInvalidator.GetVersion(typeof(TEntity));
        if (remoteVer > _distributedVersion)
        {
            _distributedVersion = remoteVer;
            ExpiredTime = DateTime.MinValue;
            WriteLog("Detected secondary cache version change {0} => {1}; expiring local cache", _distributedVersion, remoteVer);
        }
    }

    /// <summary>检索与指定谓词定义的条件匹配的所有元素。</summary>
    /// <param name="match">条件</param>
    /// <returns></returns>
    public TEntity Find(Predicate<TEntity> match)
    {
        var list = Entities;
        if (list is List<TEntity> list2) return list2.Find(match);

        return list.FirstOrDefault(e => match(e));
    }

    /// <summary>检索与指定谓词定义的条件匹配的所有元素。</summary>
    /// <param name="match">条件</param>
    /// <returns></returns>
    public IList<TEntity> FindAll(Predicate<TEntity> match)
    {
        var list = Entities;
        if (list is List<TEntity> list2) return list2.FindAll(match);

        return list.Where(e => match(e)).ToList();
    }
    #endregion

    #region 缓存操作
    private volatile Int32 _updating;

    void UpdateCacheAsync(String reason)
    {
        // 控制只有一个线程能更新
        if (Interlocked.CompareExchange(ref _updating, 1, 0) != 0) return;

        reason = $"asynchronous cache refresh, {reason}";
        WriteLog(reason);
        Consumer.Run(() => UpdateCache(reason, true));
    }

    void UpdateCache(String reason, Boolean isAsync = false)
    {
        // 串行化缓存刷新，避免同步刷新与异步刷新并发时相互覆盖
        lock (_refresh)
        {
            Interlocked.Increment(ref _updating);

            //var act = LogPrefix;
            //var p = act.IndexOf("<");
            //if (p > 0) act = act[..p];
            //if (isAsync) act += "Async";
            //using var span = DAL.GlobalTracer?.NewSpan($"cache:{ConnName}:{act}:{TableName}", reason);

            var ts = _Times;

            // 这里直接计算有效期，避免每次判断缓存有效期时进行的时间相加而带来的性能损耗
            // 设置时间放在获取缓存之前，让其它线程不要空等
            if (ts > 0) ExpiredTime = TimerX.Now.AddSeconds(Expire);

            WriteLog("Updating (#{0}), reason: {1}", ts + 1, reason);
            DAL.SetSpanTag($"{LogPrefix}Updating (#{ts + 1}), reason: {reason}");

            IList<TEntity>? loaded = null;
            try
            {
                loaded = LoadSnapshot();
            }
            finally
            {
                _updating = 0;
                DAL.SetSpanTag(null);
            }

            ApplySnapshot(loaded);

            ts = Interlocked.Increment(ref _Times);
            ExpiredTime = TimerX.Now.AddSeconds(Expire);
            WriteLog("Completed [{0}] (#{1})", _Entities.Length, ts);
        }
    }

    /// <summary>读库加载新快照，同时开始记录刷新期间的添删改</summary>
    /// <returns>新快照，加载失败时返回空</returns>
    private IList<TEntity>? LoadSnapshot()
    {
        // 刷新期间发生的添删改记录到日志，稍后回放到新快照，避免被读库旧快照覆盖
        lock (_sync) _journal ??= [];

        try
        {
            return Invoke<Object, IList<TEntity>>(s => FillListMethod(), null!);
        }
        catch (Exception ex)
        {
            XTrace.Log?.InfoPrefixed(LogPrefix, "[{0}/{1}] {2}", TableName, ConnName, ex.GetTrue());
            return null;
        }
    }

    /// <summary>回放变更日志并原子替换快照</summary>
    /// <param name="loaded">读库得到的新快照</param>
    private void ApplySnapshot(IList<TEntity>? loaded)
    {
        lock (_sync)
        {
            try
            {
                if (loaded == null) return;

                var list = loaded.ToList();

                // 回放刷新期间的添删改，确保并发写入不被旧快照覆盖
                if (_journal != null)
                {
                    foreach (var change in _journal) Apply(list, change);
                }

                _Entities = list.ToArray();
            }
            finally
            {
                // 无论成功失败都要结束日志，避免后续写入记录到过期日志
                _journal = null;
            }
        }
    }

    /// <summary>清除缓存</summary>
    /// <param name="reason">清除原因</param>
    /// <param name="force">强制清除，下次访问阻塞等待。默认false仅置为过期，下次访问异步更新</param>
    public void Clear(String reason, Boolean force = false)
    {
        if (!Using) return;

        //var factory = Entity<TEntity>.Meta.Factory;
        //using var span = DAL.GlobalTracer?.NewSpan($"db:{factory.ConnName}:ClearCache:{factory.TableName}", new { reason, force });

        //// 直接执行异步更新，明明白白，确保任何情况下数据最新，并且不影响其它任务的性能
        //UpdateCacheAsync(reason);

        // 强迫下一次访问阻塞等待刷新
        if (force)
        {
            ExpiredTime = DateTime.MinValue;
            WriteLog("Cache forcibly cleared; the next access blocks and waits for a synchronous update");
        }
        else
        {
            ExpiredTime = DateTime.Now;
            WriteLog("Cache cleared without force; the next access updates asynchronously without blocking");
        }

        // 通知分布式缓存其它进程
        CacheInvalidator.Invalidate(typeof(TEntity), reason);
    }

    private readonly IEntityFactory _factory = Entity<TEntity>.Meta.Factory;
    /// <summary>添加对象到缓存</summary>
    /// <param name="entity">实体对象</param>
    public void Add(TEntity entity)
    {
        if (!Using) return;

        Mutate(CacheChangeKind.Upsert, entity);
    }

    /// <summary>从缓存中删除对象</summary>
    /// <param name="entity">实体对象</param>
    /// <returns>被删除的实体，不存在时返回空</returns>
    public TEntity? Remove(TEntity entity)
    {
        if (!Using) return null;

        return Mutate(CacheChangeKind.Remove, entity);
    }

    internal TEntity? Update(TEntity entity)
    {
        if (!Using) return null;

        return Mutate(CacheChangeKind.Upsert, entity);
    }

    /// <summary>在快照副本上执行变更并原子替换，刷新期间同步记录变更日志</summary>
    /// <param name="kind">变更类型</param>
    /// <param name="entity">目标实体</param>
    /// <returns>被替换或删除的旧实体</returns>
    private TEntity? Mutate(CacheChangeKind kind, TEntity entity)
    {
        lock (_sync)
        {
            var change = new CacheChange(kind, entity);

            var list = _Entities.ToList();
            var old = Apply(list, change);
            _Entities = list.ToArray();

            // 刷新期间的变更需要记录，随后回放到新快照，避免被旧快照覆盖
            _journal?.Add(change);

            return old;
        }
    }

    /// <summary>把一条变更应用到集合，返回被替换或删除的旧实体</summary>
    /// <param name="list">目标集合</param>
    /// <param name="change">变更</param>
    /// <returns>被替换或删除的旧实体</returns>
    private TEntity? Apply(List<TEntity> list, CacheChange change)
    {
        var index = IndexOf(list, change.Entity);
        if (index < 0)
        {
            // 删除不存在的实体无需处理，新增和更新则追加
            if (change.Kind != CacheChangeKind.Remove) list.Add(change.Entity);

            return null;
        }

        var old = list[index];
        if (change.Kind == CacheChangeKind.Remove)
            list.RemoveAt(index);
        else
            list[index] = change.Entity;

        return old;
    }

    /// <summary>按同引用或唯一键查找实体下标</summary>
    /// <param name="list">目标集合</param>
    /// <param name="entity">目标实体</param>
    /// <returns>下标，未找到返回-1</returns>
    private Int32 IndexOf(IList<TEntity> list, TEntity entity)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], entity)) return i;
        }

        var fi = _factory.Unique;
        if (fi == null) return -1;

        var value = entity[fi.Name];
        for (var i = 0; i < list.Count; i++)
        {
            if (Equals(list[i][fi.Name], value)) return i;
        }

        return -1;
    }
    #endregion

    #region 统计
    /// <summary>总次数</summary>
    public Int32 Total;

    /// <summary>命中</summary>
    public Int32 Success;

    /// <summary>显示统计信息</summary>
    public void ShowStatics()
    {
        if (Total > 0)
        {
            var sb = Pool.StringBuilder.Get();
            var type = GetType();
            var name = $"{type.GetDisplayName() ?? type.Name}<{typeof(TEntity).Name}>({_Entities.Length:n0})";
            sb.AppendFormat("{0,-24}", name);
            sb.AppendFormat("总次数{0,11:n0}", Total);
            if (Success > 0) sb.AppendFormat("，命中{0,11:n0}（{1,6:P02}）", Success, (Double)Success / Total);
            sb.AppendFormat("\t[{0}]", typeof(TEntity).FullName);

            XTrace.WriteLine(sb.Return(true));
        }
    }
    #endregion

    #region IEntityCache 成员
    IList<IEntity> IEntityCache.Entities => new List<IEntity>(Entities);
    #endregion

    #region 辅助
    internal EntityCache<TEntity> CopySettingFrom(EntityCache<TEntity> ec)
    {
        Expire = ec.Expire;
        FillListMethod = ec.FillListMethod;

        return this;
    }

    /// <summary>输出名称</summary>
    /// <returns></returns>
    public override String ToString()
    {
        var type = GetType();
        return $"{type.GetDisplayName() ?? type.Name}<{typeof(TEntity).Name}>";
    }
    #endregion
}