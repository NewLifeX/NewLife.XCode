using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace XCode.DataAccessLayer;

/// <summary>数据库命令过滤器。在 SQL 命令执行前后进行横切处理</summary>
/// <remarks>
/// 位于 <see cref="IDbSession"/> 的命令执行层，可观察或改写 SQL、读取结果、处理异常。
/// 适合审计、慢 SQL、SQL 改写、命令级限流等场景；行级租户/数据权限优先使用实体层 <see cref="IEntityFilter"/>。
/// </remarks>
public interface IDbCommandFilter
{
    /// <summary>优先级。越小越先执行</summary>
    Int32 Order { get; }

    /// <summary>是否为指定数据库类型启用</summary>
    /// <param name="dbType">数据库类型</param>
    /// <returns>是否启用</returns>
    Boolean Init(DatabaseType dbType);

    /// <summary>命令执行前</summary>
    /// <param name="context">命令上下文</param>
    void OnExecuting(DbCommandContext context);

    /// <summary>命令执行后。即使前置过滤器取消或抛出异常，也会执行</summary>
    /// <param name="context">命令上下文</param>
    void OnExecuted(DbCommandContext context);
}

/// <summary>数据库命令上下文</summary>
public class DbCommandContext
{
    #region 属性
    /// <summary>数据库会话</summary>
    public IDbSession Session { get; set; } = null!;

    /// <summary>数据库命令</summary>
    public DbCommand Command { get; set; } = null!;

    /// <summary>是否查询命令</summary>
    public Boolean IsQuery { get; set; }

    /// <summary>数据库类型</summary>
    public DatabaseType DbType => Session.Database.Type;

    /// <summary>当前 SQL。写入会同步修改命令文本</summary>
    public String Sql
    {
        get => Command.CommandText;
        set => Command.CommandText = value;
    }

    /// <summary>命令类型。写入会同步修改命令</summary>
    public CommandType CommandType
    {
        get => Command.CommandType;
        set => Command.CommandType = value;
    }

    /// <summary>命令结果。查询为 DataSet/DbTable/标量，写入为影响行数等</summary>
    public Object? Result { get; set; }

    /// <summary>影响行数。过滤器直接处理写入时可设置该值</summary>
    public Int32 AffectedRows { get; set; }

    /// <summary>命令执行耗时。在 <see cref="IDbCommandFilter.OnExecuted"/> 中有效</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>异常</summary>
    public Exception? Exception { get; set; }

    /// <summary>异常已处理。为 true 时不再向外抛出</summary>
    public Boolean ExceptionHandled { get; set; }

    /// <summary>取消真实命令。仍会执行后置过滤器</summary>
    public Boolean Cancel { get; set; }

    /// <summary>已处理。跳过真实命令并直接返回 Result/AffectedRows</summary>
    public Boolean Handled { get; set; }

    /// <summary>过滤器之间传递业务数据</summary>
    public IDictionary<String, Object?> Items => _items ??= new Dictionary<String, Object?>();

    private IDictionary<String, Object?>? _items;
    #endregion

    #region 方法
    /// <summary>中止命令。安全校验失败时使用，后置过滤器完成后抛出异常</summary>
    /// <param name="exception">异常</param>
    public void Abort(Exception exception)
    {
        Cancel = true;
        Handled = true;
        Exception = exception;
    }
    #endregion
}

/// <summary>数据库命令过滤器基类</summary>
public abstract class DbCommandFilter : IDbCommandFilter
{
    #region 属性
    /// <summary>优先级。越小越先执行</summary>
    public virtual Int32 Order { get; set; }

    private readonly ConcurrentDictionary<DatabaseType, Boolean> _inited = new();

    /// <summary>是否为指定数据库类型启用</summary>
    /// <param name="dbType">数据库类型</param>
    /// <returns>是否启用</returns>
    public Boolean Init(DatabaseType dbType) => _inited.GetOrAdd(dbType, OnInit);

    /// <summary>初始化过滤器，默认对所有数据库启用</summary>
    /// <param name="dbType">数据库类型</param>
    /// <returns>是否启用</returns>
    protected virtual Boolean OnInit(DatabaseType dbType) => true;
    #endregion

    #region 执行
    /// <summary>命令执行前</summary>
    /// <param name="context">命令上下文</param>
    public virtual void OnExecuting(DbCommandContext context) { }

    /// <summary>命令执行后</summary>
    /// <param name="context">命令上下文</param>
    public virtual void OnExecuted(DbCommandContext context) { }
    #endregion
}

/// <summary>数据库命令过滤器集合</summary>
public static class DbCommandFilters
{
    #region 属性
    private static IDbCommandFilter[] _filters = [];
    private static Int32 _version;
    private static readonly ConcurrentDictionary<DatabaseType, FilterCacheEntry> _cache = new();

    private sealed class FilterCacheEntry
    {
        public Int32 Version;
        public IDbCommandFilter[] Filters = [];
    }

    /// <summary>过滤器集合。按注册顺序保存，执行时按 Order 排序</summary>
    public static IDbCommandFilter[] Filters
    {
        get => Volatile.Read(ref _filters);
        set
        {
            Volatile.Write(ref _filters, value ?? []);
            Interlocked.Increment(ref _version);
            _cache.Clear();
        }
    }

    /// <summary>是否存在过滤器</summary>
    public static Boolean HasFilters => Filters.Length > 0;
    #endregion

    #region 方法
    /// <summary>添加数据库命令过滤器</summary>
    /// <param name="filter">过滤器</param>
    public static void AddFilter(IDbCommandFilter filter)
    {
        if (filter == null) throw new ArgumentNullException(nameof(filter));

        lock (_cache)
        {
            var list = new List<IDbCommandFilter>(Filters);
            if (list.Any(e => ReferenceEquals(e, filter))) return;

            list.Add(filter);
            Filters = list.ToArray();
        }
    }

    /// <summary>添加数据库命令过滤器</summary>
    /// <typeparam name="T">过滤器类型，必须实现 <see cref="IDbCommandFilter"/> 接口</typeparam>
    public static void AddFilter<T>() where T : IDbCommandFilter, new() => AddFilter(new T());

    /// <summary>获取指定数据库类型生效的过滤器</summary>
    /// <param name="dbType">数据库类型</param>
    /// <returns></returns>
    public static IDbCommandFilter[] GetFilters(DatabaseType dbType)
    {
        var filters = Filters;
        if (filters.Length == 0) return [];

        var version = Volatile.Read(ref _version);
        if (_cache.TryGetValue(dbType, out var entry) && entry.Version == version) return entry.Filters;

        var list = new List<IDbCommandFilter>();
        foreach (var item in filters)
        {
            if (item.Init(dbType)) list.Add(item);
        }

        var result = list.OrderBy(e => e.Order).ToArray();
        _cache[dbType] = new FilterCacheEntry { Version = version, Filters = result };

        return result;
    }

    /// <summary>判断指定数据库类型是否存在生效的过滤器</summary>
    /// <param name="dbType">数据库类型</param>
    /// <returns></returns>
    public static Boolean HasFiltersFor(DatabaseType dbType) => GetFilters(dbType).Length > 0;

    internal static DbCommandContext Execute(DbCommandContext context, Action<DbCommandContext> action)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (action == null) throw new ArgumentNullException(nameof(action));

        var filters = GetFilters(context.DbType);
        if (filters.Length == 0)
        {
            action(context);
            return context;
        }

        var active = new List<IDbCommandFilter>(filters.Length);
        foreach (var item in filters)
        {
            if (!DbCommandFilterScope.IsSuppressed(item.GetType())) active.Add(item);
        }
        if (active.Count == 0)
        {
            action(context);
            return context;
        }

        using var depth = DbCommandFilterScope.Enter();

        var executed = new List<IDbCommandFilter>(active.Count);
        var sw = Stopwatch.StartNew();
        try
        {
            foreach (var item in active)
            {
                executed.Add(item);
                item.OnExecuting(context);

                if (context.Cancel || context.Handled) break;
            }

            if (!context.Cancel && !context.Handled)
            {
                try
                {
                    action(context);
                }
                catch (Exception ex)
                {
                    context.Exception = ex;
                }
            }
        }
        catch (Exception ex)
        {
            context.Exception ??= ex;
        }
        finally
        {
            sw.Stop();
            context.Duration = sw.Elapsed;

            for (var i = executed.Count - 1; i >= 0; i--)
            {
                try
                {
                    executed[i].OnExecuted(context);
                }
                catch (Exception ex)
                {
                    context.Exception ??= ex;
                }
            }
        }

        if (context.Exception != null && !context.ExceptionHandled) ExceptionDispatchInfo.Capture(context.Exception).Throw();

        return context;
    }

    internal static async Task<DbCommandContext> ExecuteAsync(DbCommandContext context, Func<DbCommandContext, Task> action)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (action == null) throw new ArgumentNullException(nameof(action));

        var filters = GetFilters(context.DbType);
        if (filters.Length == 0)
        {
            await action(context).ConfigureAwait(false);
            return context;
        }

        var active = new List<IDbCommandFilter>(filters.Length);
        foreach (var item in filters)
        {
            if (!DbCommandFilterScope.IsSuppressed(item.GetType())) active.Add(item);
        }
        if (active.Count == 0)
        {
            await action(context).ConfigureAwait(false);
            return context;
        }

        using var depth = DbCommandFilterScope.Enter();

        var executed = new List<IDbCommandFilter>(active.Count);
        var sw = Stopwatch.StartNew();
        try
        {
            foreach (var item in active)
            {
                executed.Add(item);
                item.OnExecuting(context);

                if (context.Cancel || context.Handled) break;
            }

            if (!context.Cancel && !context.Handled)
            {
                try
                {
                    await action(context).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    context.Exception = ex;
                }
            }
        }
        catch (Exception ex)
        {
            context.Exception ??= ex;
        }
        finally
        {
            sw.Stop();
            context.Duration = sw.Elapsed;

            for (var i = executed.Count - 1; i >= 0; i--)
            {
                try
                {
                    executed[i].OnExecuted(context);
                }
                catch (Exception ex)
                {
                    context.Exception ??= ex;
                }
            }
        }

        if (context.Exception != null && !context.ExceptionHandled) ExceptionDispatchInfo.Capture(context.Exception).Throw();

        return context;
    }
    #endregion
}

/// <summary>数据库过滤器作用域。用于在特定范围内抑制命令过滤器</summary>
public static class DbCommandFilterScope
{
    #region 属性
    private const Int32 MaxDepth = 16;

    private static readonly Type _all = typeof(DbCommandFilterScope);

#if NET45
    private static readonly ThreadLocal<HashSet<Type>?> _suppressed = new();
    private static readonly ThreadLocal<Int32> _depth = new();
#else
    private static readonly System.Threading.AsyncLocal<HashSet<Type>?> _suppressed = new();
    private static readonly System.Threading.AsyncLocal<Int32> _depth = new();
#endif
    #endregion

    #region 方法
    /// <summary>判断指定过滤器是否被抑制</summary>
    /// <param name="filterType">过滤器类型</param>
    /// <returns></returns>
    internal static Boolean IsSuppressed(Type filterType)
    {
        var set = _suppressed.Value;
        if (set == null) return false;

        if (set.Contains(_all) || set.Contains(filterType)) return true;

        foreach (var item in set)
        {
            if (item.IsAssignableFrom(filterType)) return true;
        }

        return false;
    }

    /// <summary>抑制指定类型的命令过滤器</summary>
    /// <typeparam name="TFilter">过滤器类型</typeparam>
    /// <returns></returns>
    public static IDisposable Suppress<TFilter>() where TFilter : IDbCommandFilter => Push(typeof(TFilter));

    /// <summary>抑制全部命令过滤器</summary>
    /// <returns></returns>
    public static IDisposable SuppressAll() => Push(_all);

    private static IDisposable Push(Type type)
    {
        var old = _suppressed.Value;
        var set = old == null ? [] : new HashSet<Type>(old);
        set.Add(type);
        _suppressed.Value = set;

        return new RestoreScope(() => _suppressed.Value = old);
    }

    internal static IDisposable Enter()
    {
        var old = _depth.Value;
        if (old >= MaxDepth) throw new XCodeException($"Database filter recursion depth exceeds {MaxDepth}.");

        _depth.Value = old + 1;

        return new RestoreScope(() => _depth.Value = old);
    }

    private sealed class RestoreScope(Action restore) : IDisposable
    {
        private Action? _restore = restore;

        public void Dispose() => Interlocked.Exchange(ref _restore, null)?.Invoke();
    }
    #endregion
}
