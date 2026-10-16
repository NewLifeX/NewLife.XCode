using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using NewLife;
using NewLife.Caching;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Threading;

namespace XCode.DataAccessLayer;

partial class DAL
{
    #region 属性
    [ThreadStatic]
    private static Int32 _QueryTimes;
    /// <summary>查询次数</summary>
    public static Int32 QueryTimes => _QueryTimes;

    [ThreadStatic]
    private static Int32 _ExecuteTimes;
    /// <summary>执行次数</summary>
    public static Int32 ExecuteTimes => _ExecuteTimes;

    private IList<DAL>? _reads;
    /// <summary>只读连接集合。读写分离时，读取操作分走</summary>
    public IList<DAL>? Reads => _reads;

    /// <summary>只读实例。读写分离时，读取操作分走</summary>
    [Obsolete("=>Reads")]
    public DAL? ReadOnly => _reads?.FirstOrDefault();

    /// <summary>读写分离策略。忽略时间区间和表名</summary>
    public ReadWriteStrategy Strategy { get; set; } = new();
    #endregion

    #region 数据操作方法
    /// <summary>根据条件把普通查询SQL格式化为分页SQL。</summary>
    /// <param name="builder">查询生成器</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns>分页SQL</returns>
    public SelectBuilder PageSplit(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows)
    {
        if (startRowIndex <= 0 && maximumRows <= 0) return builder;

        // 2016年7月2日 HUIYUE 取消分页SQL缓存，此部分缓存提升性能不多，但有可能会造成分页数据不准确，感觉得不偿失
        return Db.PageSplit(builder.Clone(), startRowIndex, maximumRows);
    }

    /// <summary>执行SQL查询，返回记录集</summary>
    /// <param name="sql">SQL语句</param>
    /// <returns></returns>
    public DataSet Select(String sql) => QueryWrap(sql, "", "", (ss, s, k2, k3) => ss.Query(s), nameof(Select));

    /// <summary>执行SQL查询，返回记录集</summary>
    /// <param name="builder">SQL语句</param> 
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns></returns>
    public DataSet Select(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows)
    {
        var sql = PageSplit(builder, startRowIndex, maximumRows).ToString();
        return QueryWrap(sql, builder, "", (ss, sql, sb, k3) => ss.Query(sql, CommandType.Text, sb.Parameters.ToArray()), nameof(Select));
    }

    /// <summary>执行SQL查询，返回记录集</summary>
    /// <param name="builder">SQL语句</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns></returns>
    public DbTable Query(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows)
    {
        builder = PageSplit(builder, startRowIndex, maximumRows);
        return QueryWrap(builder, "", "", (ss, sb, k2, k3) => ss.Query(sb), nameof(Query));
    }

    /// <summary>执行SQL查询，返回记录集</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    public DbTable Query(String sql, IDictionary<String, Object>? ps = null) => QueryWrap(sql, ps, "", (ss, s, p, k3) => ss.Query(s, Db.CreateParameters(p)), nameof(Query));

    /// <summary>执行SQL查询，返回总记录数</summary>
    /// <param name="sb">查询生成器</param>
    /// <returns></returns>
    public Int32 SelectCount(SelectBuilder sb) => (Int32)QueryWrap(sb, "", "", (ss, s, k2, k3) => ss.QueryCount(s), nameof(SelectCount));

    /// <summary>执行SQL查询，返回总记录数</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    public Int32 SelectCount(String sql, CommandType type, params IDataParameter[] ps) => (Int32)QueryWrap(sql, type, ps, (ss, s, t, p) => ss.QueryCount(s, t, p), nameof(SelectCount));

    /// <summary>执行SQL语句，返回受影响的行数</summary>
    /// <param name="sql">SQL语句</param>
    /// <returns></returns>
    public Int32 Execute(String sql) => ExecuteWrap(sql, "", "", (ss, s, t, p) => ss.Execute(s), nameof(Execute));

    /// <summary>执行插入语句并返回新增行的自动编号</summary>
    /// <param name="sql"></param>
    /// <returns>新增行的自动编号</returns>
    public Int64 InsertAndGetIdentity(String sql) => ExecuteWrap(sql, "", "", (ss, s, t, p) => ss.InsertAndGetIdentity(s), nameof(InsertAndGetIdentity));

    /// <summary>执行SQL查询，返回记录集</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    public DataSet Select(String sql, CommandType type, params IDataParameter[] ps) => QueryWrap(sql, type, ps, (ss, s, t, p) => ss.Query(s, t, p), nameof(Select));

    /// <summary>执行SQL语句，返回受影响的行数</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    public Int32 Execute(String sql, CommandType type, params IDataParameter[] ps) => ExecuteWrap(sql, type, ps, (ss, s, t, p) => ss.Execute(s, t, p), nameof(Execute));

    /// <summary>执行插入语句并返回新增行的自动编号</summary>
    /// <param name="sql"></param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public Int64 InsertAndGetIdentity(String sql, CommandType type, params IDataParameter[] ps) => ExecuteWrap(sql, type, ps, (ss, s, t, p) => ss.InsertAndGetIdentity(s, t, p), nameof(InsertAndGetIdentity));

    /// <summary>执行SQL查询，返回记录集</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    public DataSet Select(String sql, CommandType type, IDictionary<String, Object> ps) => QueryWrap(sql, type, ps, (ss, s, t, p) => ss.Query(s, t, Db.CreateParameters(p)), nameof(Select));

    /// <summary>执行SQL语句，返回受影响的行数</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    public Int32 Execute(String sql, CommandType type, IDictionary<String, Object> ps) => ExecuteWrap(sql, type, ps, (ss, s, t, p) => ss.Execute(s, t, Db.CreateParameters(p)), nameof(Execute));

    /// <summary>执行SQL语句，返回受影响的行数</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="commandTimeout">命令超时时间，一般用于需要长时间执行的命令。单位秒</param>
    /// <returns></returns>
    public Int32 Execute(String sql, Int32 commandTimeout)
    {
        return ExecuteWrap(sql, commandTimeout, "", (ss, s, t, p) =>
        {
            using var cmd = ss.CreateCommand(s);
            if (t > 0) cmd.CommandTimeout = t;
            return ss.Execute(cmd);
        }, nameof(Execute));
    }

    /// <summary>执行SQL语句，返回结果中的第一行第一列</summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    public T? ExecuteScalar<T>(String sql, CommandType type, IDictionary<String, Object> ps) => ExecuteWrap(sql, type, ps, (ss, s, t, p) => ss.ExecuteScalar<T>(s, t, Db.CreateParameters(p)), nameof(ExecuteScalar));
    #endregion

    #region 异步操作
    /// <summary>执行SQL查询，返回记录集</summary>
    /// <param name="builder">SQL语句</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns></returns>
    public Task<DbTable> QueryAsync(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows)
    {
        var sql = PageSplit(builder, startRowIndex, maximumRows).ToString();
        return QueryAsyncWrap(sql, builder, "", (ss, sql, sb, k3) => ss.QueryAsync(sql, sb.Parameters.ToArray()), nameof(QueryAsync));
    }

    /// <summary>执行SQL查询，返回记录集</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    public Task<DbTable> QueryAsync(String sql, IDictionary<String, Object>? ps = null) => QueryAsyncWrap(sql, ps, "", (ss, s, p, k3) => ss.QueryAsync(s, Db.CreateParameters(p)), nameof(QueryAsync));

    /// <summary>执行SQL查询，返回总记录数</summary>
    /// <param name="sb">查询生成器</param>
    /// <returns></returns>
    public Task<Int64> SelectCountAsync(SelectBuilder sb) => QueryAsyncWrap(sb, "", "", (ss, s, k2, k3) => ss.QueryCountAsync(s), nameof(SelectCountAsync));

    /// <summary>执行SQL查询，返回总记录数</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    public Task<Int64> SelectCountAsync(String sql, CommandType type, params IDataParameter[] ps) => QueryAsyncWrap(sql, type, ps, (ss, s, t, p) => ss.QueryCountAsync(s, t, p), nameof(SelectCountAsync));

    /// <summary>执行SQL语句，返回受影响的行数</summary>
    /// <param name="sql">SQL语句</param>
    /// <returns></returns>
    public Task<Int32> ExecuteAsync(String sql) => ExecuteAsyncWrap(sql, "", "", (ss, s, t, p) => ss.ExecuteAsync(s), nameof(ExecuteAsync));

    /// <summary>执行插入语句并返回新增行的自动编号</summary>
    /// <param name="sql"></param>
    /// <returns>新增行的自动编号</returns>
    public Task<Int64> InsertAndGetIdentityAsync(String sql) => ExecuteAsyncWrap(sql, "", "", (ss, s, t, p) => ss.InsertAndGetIdentityAsync(s), nameof(InsertAndGetIdentityAsync));

    /// <summary>执行SQL语句，返回受影响的行数</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    public Task<Int32> ExecuteAsync(String sql, CommandType type, params IDataParameter[]? ps) => ExecuteAsyncWrap(sql, type, ps, (ss, s, t, p) => ss.ExecuteAsync(s, t, p), nameof(ExecuteAsync));

    /// <summary>执行插入语句并返回新增行的自动编号</summary>
    /// <param name="sql"></param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type, params IDataParameter[]? ps) => ExecuteAsyncWrap(sql, type, ps, (ss, s, t, p) => ss.InsertAndGetIdentityAsync(s, t, p), nameof(InsertAndGetIdentityAsync));

    /// <summary>执行SQL语句，返回受影响的行数</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    public Task<Int32> ExecuteAsync(String sql, CommandType type, IDictionary<String, Object> ps) => ExecuteAsyncWrap(sql, type, ps, (ss, s, t, p) => ss.ExecuteAsync(s, t, Db.CreateParameters(p)), nameof(ExecuteAsync));

    /// <summary>执行SQL语句，返回受影响的行数</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="commandTimeout">命令超时时间，一般用于需要长时间执行的命令</param>
    /// <returns></returns>
    public Task<Int32> ExecuteAsync(String sql, Int32 commandTimeout)
    {
        return ExecuteAsyncWrap(sql, commandTimeout, "", (ss, s, t, p) =>
        {
            using var cmd = (ss as IDbSession)!.CreateCommand(s);
            if (t > 0) cmd.CommandTimeout = t;
            return ss.ExecuteAsync(cmd);
        }, nameof(ExecuteAsync));
    }

    /// <summary>执行SQL语句，返回结果中的第一行第一列</summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns></returns>
    public Task<T?> ExecuteScalarAsync<T>(String sql, CommandType type, IDictionary<String, Object> ps) => ExecuteAsyncWrap(sql, type, ps, (ss, s, t, p) => ss.ExecuteScalarAsync<T>(s, t, Db.CreateParameters(p)), nameof(ExecuteScalarAsync));
    #endregion

    #region 事务
    /// <summary>开始事务</summary>
    /// <remarks>
    /// Read Uncommitted: 允许读取脏数据，一个事务能看到另一个事务还没有提交的数据。（不会阻止其它操作）
    /// Read Committed: 确保事务读取的数据都必须是已经提交的数据。它限制了读取中间的，没有提交的，脏的数据。
    /// 但是它不能确保当事务重新去读取的时候，读的数据跟上次读的数据是一样的，也就是说当事务第一次读取完数据后，
    /// 该数据是可能被其他事务修改的，当它再去读取的时候，数据可能是不一样的。（数据隐藏，不阻止）
    /// Repeatable Read: 是一个更高级别的隔离级别，如果事务再去读取同样的数据，先前的数据是没有被修改过的。（阻止其它修改）
    /// Serializable: 它做出了最有力的保证，除了每次读取的数据是一样的，它还确保每次读取没有新的数据。（阻止其它添删改）
    /// </remarks>
    /// <param name="level">事务隔离等级</param>
    /// <returns>剩下的事务计数</returns>
    public Int32 BeginTransaction(IsolationLevel level = IsolationLevel.ReadCommitted) => Session.BeginTransaction(level);

    /// <summary>提交事务</summary>
    /// <returns>剩下的事务计数</returns>
    public Int32 Commit() => Session.Commit();

    /// <summary>回滚事务，忽略异常</summary>
    /// <returns>剩下的事务计数</returns>
    public Int32 Rollback() => Session.Rollback();
    #endregion

    #region 缓存&埋点
    /// <summary>缓存存储</summary>
    public ICache? Store { get; set; }

    /// <summary>数据层缓存。默认10秒</summary>
    public Int32 Expire { get; set; }

#if NET45
    private static readonly ThreadLocal<String?> _SpanTag = new();
#else
    private static readonly AsyncLocal<String?> _SpanTag = new();
#endif

    /// <summary>埋点上下文信息。用于附加在埋点标签后的上下文信息</summary>
    public static void SetSpanTag(String? value) => _SpanTag.Value = value;

    private ICache? GetCache()
    {
        var st = Store;
        if (st != null) return st;

        var exp = Expire;
        if (exp == 0) exp = Db.DataCache;
        if (exp == 0) exp = XCodeSetting.Current.DataCacheExpire;
        if (exp <= 0) return null;

        Expire = exp;

        lock (this)
        {
            st = Store;
            if (st == null)
            {
                var p = exp / 2;
                if (p < 30) p = 30;

                st = Store = new MemoryCache { Period = p, Expire = exp };
            }
        }

        return st;
    }

#if NETCOREAPP
    [StackTraceHidden]
#endif
    private TResult QueryWrap<T1, T2, T3, TResult>(T1 k1, T2 k2, T3 k3, Func<IDbSession, T1, T2, T3, TResult> callback, String action)
    {
        // 读写分离
        if (Strategy != null && Strategy.TryGet(this, k1 + "", action, out var rd) && rd != null)
        {
            return rd.QueryWrap(k1, k2, k3, callback, action);
        }

        //CheckDatabase();

        // 读缓存
        var cache = GetCache();
        var key = "";
        if (cache != null)
        {
            var sb = Pool.StringBuilder.Get();
            if (!action.IsNullOrEmpty())
            {
                sb.Append(action);
                sb.Append('#');
            }
            Append(sb, k1);
            Append(sb, k2);
            Append(sb, k3);
            key = sb.Return(true);

            if (cache.TryGetValue<TResult>(key, out var value)) return value!;
        }

        Interlocked.Increment(ref _QueryTimes);
        var rs = Invoke(Session, k1, k2, k3, callback, action);

        cache?.Set(key, rs, Expire);

        return rs;
    }

#if NETCOREAPP
    [StackTraceHidden]
#endif
    private TResult ExecuteWrap<T1, T2, T3, TResult>(T1 k1, T2 k2, T3 k3, Func<IDbSession, T1, T2, T3, TResult> callback, String action)
    {
        if (Db.Readonly) throw new InvalidOperationException($"数据连接[{ConnName}]只读，禁止执行{k1}");

        //CheckDatabase();

        var rs = Invoke(Session, k1, k2, k3, callback, action);

        GetCache()?.Clear();

        Interlocked.Increment(ref _ExecuteTimes);

        return rs;
    }

#if NETCOREAPP
    [StackTraceHidden]
#endif
    private TResult Invoke<T1, T2, T3, TResult>(IDbSession session, T1 k1, T2 k2, T3 k3, Func<IDbSession, T1, T2, T3, TResult> callback, String action)
    {
        var tracer = Tracer ?? GlobalTracer;
        var traceName = "";
        var sql = "";

        // 从sql解析表名，作为跟踪名一部分。正则避免from前后换行的情况
        if (tracer != null)
        {
            sql = (k1 + "").Trim();
            traceName = GetTraceName(sql, action);
        }

        // 使用k1参数作为tag，一般是sql
        using var span = tracer?.NewSpan(traceName, sql);
        try
        {
            var rs = callback(session, k1, k2, k3);
            AppendTag(span, sql, rs, action);

            return rs;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    private String GetTraceName(String sql, String action)
    {
        var act = (action + "").TrimEnd("Async");
        if (act.EqualIgnoreCase("InsertAndGetIdentity"))
        {
            act = "Insert";
        }
        else if (act.EqualIgnoreCase("Execute", "ExecuteScalar"))
        {
            // 使用 Insert/Update/Delete 作为埋点操作名
            var p = sql.IndexOf(' ');
            if (p > 0) act = sql[..p];
        }
        else if (act.EqualIgnoreCase("Query", "Select"))
        {
            // 查询数据时，Group作为独立埋点操作名
            if (sql.ToLower().Contains("group by"))
                act = "Group";
        }

        var traceName = $"db:{ConnName}:{act}";

        var tables = GetTables(sql, true);
        if (tables.Length > 0) traceName += ":" + tables.Join("-");

        return traceName;
    }

    private void AppendTag(ISpan? span, String sql, Object? rs, String action)
    {
        if (span == null) return;

        if (rs is DbTable dt)
        {
            // 数值记录行数，标签记录结果
            var rows = dt.Rows?.Count ?? 0;
            span.Value = rows;

            if (dt.Rows != null && dt.Rows.Count == 1 && dt.Columns != null && dt.Columns.Length <= 3)
                span.Tag = $"{sql} [rows={rows}, result={dt.Rows[0].Join(",")}]";
            else
                span.Tag = $"{sql} [rows={rows}]";
        }
        else if (rs is DataSet ds && ds.Tables.Count > 0)
        {
            // 数值记录行数，标签记录结果
            var dst = ds.Tables[0];
            var rows = dst.Rows.Count;
            span.Value = rows;

            if (dst.Rows != null && dst.Rows.Count == 1 && dst.Columns != null && dst.Columns.Count <= 3)
                span.Tag = $"{sql} [rows={rows}, result={dst.Rows[0].ItemArray.Join(",")}]";
            else
                span.Tag = $"{sql} [rows={rows}]";
        }
        else if (action == nameof(InsertAndGetIdentity) || action == nameof(InsertAndGetIdentityAsync))
        {
            if (rs.ToLong() > 0) span.Value = 1;

            span.Tag = $"{sql} [id={rs}]";
        }
        else
        {
            // 数值和标签都记录结果，大概率是受影响行数
            if (rs != null && rs.GetType().IsInt()) span.Value = rs.ToLong();

            span.Tag = $"{sql} [result={rs}]";
        }

        var stag = _SpanTag.Value;
        if (!stag.IsNullOrEmpty()) span.Tag += " " + stag;
    }

#if NETCOREAPP
    [StackTraceHidden]
#endif
    private async Task<TResult> QueryAsyncWrap<T1, T2, T3, TResult>(T1 k1, T2 k2, T3 k3, Func<IAsyncDbSession, T1, T2, T3, Task<TResult>> callback, String action)
    {
        // 读写分离
        if (Strategy != null && Strategy.TryGet(this, k1 + "", action, out var rd) && rd != null)
        {
            return await rd.QueryAsyncWrap(k1, k2, k3, callback, action).ConfigureAwait(false);
        }

        //CheckDatabase();

        // 读缓存
        var cache = GetCache();
        var key = "";
        if (cache != null)
        {
            var sb = Pool.StringBuilder.Get();
            if (!action.IsNullOrEmpty())
            {
                sb.Append(action);
                sb.Append('#');
            }
            Append(sb, k1);
            Append(sb, k2);
            Append(sb, k3);
            key = sb.Return(true);

            if (cache.TryGetValue<TResult>(key, out var value)) return value!;
        }

        Interlocked.Increment(ref _QueryTimes);
        var rs = await InvokeAsync(AsyncSession, k1, k2, k3, callback, action).ConfigureAwait(false);

        cache?.Set(key, rs, Expire);

        return rs;
    }

#if NETCOREAPP
    [StackTraceHidden]
#endif
    private async Task<TResult> ExecuteAsyncWrap<T1, T2, T3, TResult>(T1 k1, T2 k2, T3 k3, Func<IAsyncDbSession, T1, T2, T3, Task<TResult>> callback, String action)
    {
        if (Db.Readonly) throw new InvalidOperationException($"数据连接[{ConnName}]只读，禁止执行{k1}");

        //CheckDatabase();

        var rs = await InvokeAsync(AsyncSession, k1, k2, k3, callback, action).ConfigureAwait(false);

        GetCache()?.Clear();

        Interlocked.Increment(ref _ExecuteTimes);

        return rs;
    }

#if NETCOREAPP
    [StackTraceHidden]
#endif
    private async Task<TResult> InvokeAsync<T1, T2, T3, TResult>(IAsyncDbSession session, T1 k1, T2 k2, T3 k3, Func<IAsyncDbSession, T1, T2, T3, Task<TResult>> callback, String action)
    {
        var tracer = Tracer ?? GlobalTracer;
        var traceName = "";
        var sql = "";

        // 从sql解析表名，作为跟踪名一部分。正则避免from前后换行的情况
        if (tracer != null)
        {
            sql = (k1 + "").Trim();
            traceName = GetTraceName(sql, action);
        }

        // 使用k1参数作为tag，一般是sql
        using var span = tracer?.NewSpan(traceName, sql);
        try
        {
            var rs = await callback(session, k1, k2, k3).ConfigureAwait(false);
            AppendTag(span, sql, rs, action);

            return rs;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    private static readonly Regex reg_table = new("""(?:\s+from|insert\s+into|update|\s+join|drop\s+table|truncate\s+table)\s+(?:[`'"\[]?[\w]+[`'"\]]?\.)?[`'"\[]?([\w]+)[`'"\]]?""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    /// <summary>从Sql语句中截取表名</summary>
    /// <param name="sql">Sql语句</param>
    /// <param name="trimShard">是否去掉表名后面的分表信息。如日期分表</param>
    /// <returns></returns>
    public static String[] GetTables(String sql, Boolean trimShard)
    {
        var list = new List<String>();
        var ms = reg_table.Matches(sql);
        foreach (Match item in ms)
        {
            //list.Add(item.Groups[1].Value);
            var tableName = item.Groups[1].Value;
            if (trimShard) tableName = TrimTableName(tableName);

            if (!list.Contains(tableName)) list.Add(tableName);
        }
        return list.ToArray();
    }

    /// <summary>去掉表名后面的分表信息。如日期分表</summary>
    /// <param name="tableName"></param>
    /// <returns></returns>
    public static String TrimTableName(String tableName)
    {
        // 从尾部开始找到第一个数字，然后再找到下划线
        var p = -1;
        for (var i = tableName.Length - 1; i >= 0; i--)
        {
            if (!Char.IsDigit(tableName[i])) break;
            p = i;
        }
        if (p > 0 && tableName[p - 1] == '_') p--;
        // 数字长度至少是2，否则不是分表
        if (p > 0 && p + 2 <= tableName.Length)
        {
            tableName = tableName[..p];
        }
        return tableName;
    }

    private static void Append(StringBuilder sb, Object? value)
    {
        if (value == null) return;

        if (value is SelectBuilder builder)
        {
            sb.Append(builder);
            foreach (var item in builder.Parameters)
            {
                sb.Append('#');
                sb.Append(item.ParameterName);
                sb.Append('#');
                sb.Append(item.Value);
            }
        }
        else if (value is IDataParameter[] ps)
        {
            foreach (var item in ps)
            {
                sb.Append('#');
                sb.Append(item.ParameterName);
                sb.Append('#');
                sb.Append(item.Value);
            }
        }
        else if (value is IDictionary<String, Object> dic)
        {
            foreach (var item in dic)
            {
                sb.Append('#');
                sb.Append(item.Key);
                sb.Append('#');
                sb.Append(item.Value);
            }
        }
        else
        {
            sb.Append('#');
            sb.Append(value);
        }
    }
    #endregion

    #region 读写分离
    private IList<DAL>? _bakReads;
    /// <summary>停用只读从库</summary>
    /// <param name="delayTime">延迟恢复的时间。单位秒，默认0等待手动恢复</param>
    /// <returns></returns>
    public Boolean SuspendReadOnly(Int32 delayTime = 0)
    {
        if (_reads == null) return false;
        lock (this)
        {
            if (_reads == null) return false;

            var tracer = Tracer ?? GlobalTracer;
            using var span = tracer?.NewSpan($"db:{ConnName}:SuspendReadOnly", "delayTime=" + delayTime);

            _bakReads = _reads;
            _reads = null;

            if (delayTime > 0) TimerX.Delay(s => ResumeReadOnly(), delayTime * 1000);

            return true;
        }
    }

    /// <summary>恢复只读从库</summary>
    /// <returns></returns>
    public Boolean ResumeReadOnly()
    {
        if (_bakReads == null) return false;
        lock (this)
        {
            if (_bakReads == null) return false;

            var tracer = Tracer ?? GlobalTracer;
            using var span = tracer?.NewSpan($"db:{ConnName}:ResumeReadOnly");

            _reads = _bakReads;
            _bakReads = null;

            return true;
        }
    }
    #endregion
}