using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Reflection;
using NewLife;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Reflection;

namespace XCode.DataAccessLayer;

/// <summary>根据实体类获取表名或主键名的委托</summary>
/// <param name="entityType">实体类</param>
/// <returns></returns>
public delegate String GetNameCallback(Type entityType);

public partial class DAL
{
    /// <summary>根据实体类获取表名的委托，用于Mapper的Insert/Update</summary>
    public static GetNameCallback? GetTableName { get; set; }

    /// <summary>根据实体类获取主键名的委托，用于Mapper的Update</summary>
    public static GetNameCallback? GetKeyName { get; set; }

    #region 添删改查
    /// <summary>查询Sql并映射为结果集</summary>
    /// <typeparam name="T">实体类</typeparam>
    /// <param name="sql">Sql语句</param>
    /// <param name="param">参数对象</param>
    /// <returns></returns>
    public IEnumerable<T> Query<T>(String sql, Object? param = null)
    {
        if (IsValueTuple(typeof(T))) throw new InvalidOperationException($"不支持ValueTuple类型[{typeof(T).FullName}]");

        //var ps = param?.ToDictionary();
        var dt = QueryWrap(sql, param, "", (ss, s, p, k3) => ss.Query(s, Db.CreateParameters(p)), nameof(Query));

        // 优先特殊处理基础类型，选择第一字段
        var type = typeof(T);
        var utype = Nullable.GetUnderlyingType(type);
        if (utype != null) type = utype;
        if (type.GetTypeCode() != TypeCode.Object) return dt.Rows.Select(e => e[0].ChangeType<T>()!);

        return dt.ReadModels<T>();
    }

    /// <summary>查询Sql并映射为结果集，支持分页</summary>
    /// <typeparam name="T">实体类</typeparam>
    /// <param name="sql">Sql语句</param>
    /// <param name="param">参数对象</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns></returns>
    public IEnumerable<T> Query<T>(String sql, Object? param, Int64 startRowIndex, Int64 maximumRows)
    {
        if (IsValueTuple(typeof(T))) throw new InvalidOperationException($"不支持ValueTuple类型[{typeof(T).FullName}]");

        // SqlServer的分页需要知道主键
        var sql2 =
            DbType == DatabaseType.SqlServer ?
            Db.PageSplit(sql, startRowIndex, maximumRows, new SelectBuilder(sql).Key) :
            Db.PageSplit(sql, startRowIndex, maximumRows, null);

        return Query<T>(sql2, param);
    }

    /// <summary>查询Sql并映射为结果集，支持分页</summary>
    /// <typeparam name="T">实体类</typeparam>
    /// <param name="sql">Sql语句</param>
    /// <param name="param">参数对象</param>
    /// <param name="page">分页参数</param>
    /// <returns></returns>
    public IEnumerable<T> Query<T>(String sql, Object? param, PageParameter page)
    {
        if (IsValueTuple(typeof(T))) throw new InvalidOperationException($"不支持ValueTuple类型[{typeof(T).FullName}]");

        // 查询总行数
        if (page.RetrieveTotalCount)
        {
            page.TotalCount = SelectCount(sql, CommandType.Text);
        }

        var start = (page.PageIndex - 1) * page.PageSize;
        var max = page.PageSize;

        var orderby = page.GetOrderBy();
        if (!orderby.IsNullOrEmpty()) sql += " order by " + orderby;

        return Query<T>(sql, param, start, max);
    }

    /// <summary>查询Sql并返回单个结果</summary>
    /// <typeparam name="T">实体类</typeparam>
    /// <param name="sql">Sql语句</param>
    /// <param name="param">参数对象</param>
    /// <returns></returns>
    public T QuerySingle<T>(String sql, Object? param = null) => Query<T>(sql, param).FirstOrDefault();

    /// <summary>查询Sql并映射为结果集</summary>
    /// <typeparam name="T">实体类</typeparam>
    /// <param name="sql">Sql语句</param>
    /// <param name="param">参数对象</param>
    /// <returns></returns>
    public async Task<IEnumerable<T>> QueryAsync<T>(String sql, Object? param = null)
    {
        if (IsValueTuple(typeof(T))) throw new InvalidOperationException($"不支持ValueTuple类型[{typeof(T).FullName}]");

        var dt = await QueryAsyncWrap(sql, param, "", (ss, s, p, k3) => ss.QueryAsync(s, Db.CreateParameters(p)), nameof(QueryAsync)).ConfigureAwait(false);

        // 优先特殊处理基础类型，选择第一字段
        var type = typeof(T);
        var utype = Nullable.GetUnderlyingType(type);
        if (utype != null) type = utype;
        if (type.GetTypeCode() != TypeCode.Object) return dt.Rows.Select(e => e[0].ChangeType<T>()!);

        return dt.ReadModels<T>();
    }

    /// <summary>查询Sql并返回单个结果</summary>
    /// <typeparam name="T">实体类</typeparam>
    /// <param name="sql">Sql语句</param>
    /// <param name="param">参数对象</param>
    /// <returns></returns>
    public async Task<T> QuerySingleAsync<T>(String sql, Object? param = null) => (await QueryAsync<T>(sql, param).ConfigureAwait(false)).FirstOrDefault();

    private static Boolean IsValueTuple(Type type)
    {
        if (type is not null && type.IsValueType)
        {
            return type.FullName.StartsWith("System.ValueTuple`", StringComparison.Ordinal);
        }
        return false;
    }

    /// <summary>执行Sql</summary>
    /// <param name="sql">Sql语句</param>
    /// <param name="param">参数对象</param>
    /// <returns></returns>
    public Int32 Execute(String sql, Object? param = null) =>
        ExecuteWrap(sql, "", param, (ss, s, t, p) => ss.Execute(s, CommandType.Text, Db.CreateParameters(p)), nameof(Execute));

    /// <summary>执行Sql并返回数据读取器</summary>
    /// <param name="sql"></param>
    /// <param name="param"></param>
    /// <returns></returns>
    public IDataReader ExecuteReader(String sql, Object? param = null)
    {
        var traceName = $"db:{ConnName}:ExecuteReader";
        if (Tracer != null)
        {
            var tables = GetTables(sql, true);
            if (tables.Length > 0) traceName += ":" + tables.Join("-");
        }
        using var span = Tracer?.NewSpan(traceName, sql);
        try
        {
            //var ps = param?.ToDictionary();
            var cmd = Session.CreateCommand(sql, CommandType.Text, Db.CreateParameters(param));
            cmd.Connection = Db.OpenConnection();

            return cmd.ExecuteReader(CommandBehavior.CloseConnection);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>执行SQL语句，返回结果中的第一行第一列</summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="sql">SQL语句</param>
    /// <param name="param">参数对象</param>
    /// <returns></returns>
    public T? ExecuteScalar<T>(String sql, Object? param = null) =>
        QueryWrap(sql, param, "", (ss, s, p, k3) => ss.ExecuteScalar<T>(s, CommandType.Text, Db.CreateParameters(p)), nameof(ExecuteScalar));

    /// <summary>执行Sql</summary>
    /// <param name="sql">Sql语句</param>
    /// <param name="param">参数对象</param>
    /// <returns></returns>
    public Task<Int32> ExecuteAsync(String sql, Object? param = null) =>
        ExecuteAsyncWrap(sql, "", param, (ss, s, t, p) => ss.ExecuteAsync(s, CommandType.Text, Db.CreateParameters(p)), nameof(ExecuteAsync));

    /// <summary>执行Sql并返回数据读取器</summary>
    /// <param name="sql"></param>
    /// <param name="param"></param>
    /// <returns></returns>
    public Task<DbDataReader> ExecuteReaderAsync(String sql, Object? param = null)
    {
        var traceName = $"db:{ConnName}:ExecuteReaderAsync";
        if (Tracer != null)
        {
            var tables = GetTables(sql, true);
            if (tables.Length > 0) traceName += ":" + tables.Join("-");
        }
        using var span = Tracer?.NewSpan(traceName, sql);
        try
        {
            var cmd = (AsyncSession as IDbSession)!.CreateCommand(sql, CommandType.Text, Db.CreateParameters(param));
            cmd.Connection = Db.OpenConnection();

            return cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>执行SQL语句，返回结果中的第一行第一列</summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="sql">SQL语句</param>
    /// <param name="param">参数对象</param>
    /// <returns></returns>
    public Task<T?> ExecuteScalarAsync<T>(String sql, Object? param = null) =>
        QueryAsyncWrap(sql, param, "", (ss, s, p, k3) => ss.ExecuteScalarAsync<T>(s, CommandType.Text, Db.CreateParameters(p)), nameof(ExecuteScalarAsync));

    private ConcurrentDictionary<Type, String> _tableMaps = new();
    private String? OnGetTableName(Type type)
    {
        if (GetTableName == null) return null;

        return _tableMaps.GetOrAdd(type, t => GetTableName(t));
    }

    private ConcurrentDictionary<Type, String> _keyMaps = new();
    private String? OnGetKeyName(Type type)
    {
        if (GetKeyName == null) return null;

        return _keyMaps.GetOrAdd(type, t => GetKeyName(t));
    }

    /// <summary>插入数据</summary>
    /// <param name="data">实体对象</param>
    /// <param name="tableName">表名</param>
    /// <returns></returns>
    public Int32 Insert(Object data, String? tableName = null)
    {
        if (tableName.IsNullOrEmpty() && GetTableName != null) tableName = OnGetTableName(data.GetType());
        if (tableName.IsNullOrEmpty()) tableName = data.GetType().Name;

        var pis = data.ToDictionary();
        var dps = Db.CreateParameters(data);
        var ns = pis.Join(",", e => e.Key);
        var vs = dps.Join(",", e => e.ParameterName);
        var sql = $"Insert Into {tableName}({ns}) Values({vs})";

        return ExecuteWrap(sql, "", dps, (ss, s, t, p) => ss.Execute(s, CommandType.Text, p), nameof(Insert));
    }

    /// <summary>插入数据表。多行数据循环插入，非批量</summary>
    /// <param name="table">表定义</param>
    /// <param name="columns">字段列表，为空表示所有字段</param>
    /// <param name="data">数据对象</param>
    /// <param name="mode">保存模式，默认Insert</param>
    /// <returns></returns>
    public Int32 Insert(DbTable data, IDataTable table, IDataColumn[]? columns = null, SaveModes mode = SaveModes.Insert)
    {
        var rs = 0;
        foreach (var row in data)
        {
            rs += Insert(row, table, columns, mode);
        }
        return rs;
    }

    /// <summary>插入数据行</summary>
    /// <param name="table">表定义</param>
    /// <param name="columns">字段列表，为空表示所有字段</param>
    /// <param name="data">数据对象</param>
    /// <param name="mode">保存模式，默认Insert</param>
    /// <returns></returns>
    public Int32 Insert(IModel data, IDataTable table, IDataColumn[]? columns = null, SaveModes mode = SaveModes.Insert)
    {
        var builder = new InsertBuilder
        {
            Mode = mode,
            UseParameter = true
        };
        var sql = builder.GetSql(Db, table, columns, data);
        if (sql.IsNullOrEmpty()) return 0;

        return ExecuteWrap(sql, "", builder.Parameters, (ss, s, t, p) => ss.Execute(s, CommandType.Text, p), nameof(Insert));
    }

    /// <summary>更新数据。不支持自动识别主键</summary>
    /// <param name="data">实体对象</param>
    /// <param name="where">查询条件。默认使用Id字段</param>
    /// <param name="tableName">表名</param>
    /// <returns></returns>
    public Int32 Update(Object data, Object where, String? tableName = null)
    {
        if (tableName.IsNullOrEmpty() && GetTableName != null) tableName = OnGetTableName(data.GetType());
        if (tableName.IsNullOrEmpty()) tableName = data.GetType().Name;

        var sb = Pool.StringBuilder.Get();
        sb.Append("Update ");
        sb.Append(tableName);

        var dic = data as IDictionary<String, Object>;
        var dps = new List<IDataParameter>();
        // Set参数
        if (dic != null)
        {
            sb.Append(" Set ");
            var i = 0;
            foreach (var item in dic)
            {
                if (i++ > 0) sb.Append(',');

                var p = Db.CreateParameter(item.Key, item.Value, item.Value?.GetType());
                dps.Add(p);
                sb.AppendFormat("{0}={1}", item.Key, p.ParameterName);
            }
        }
        else
        {
            sb.Append(" Set ");
            var i = 0;
            foreach (var pi in data.GetType().GetProperties(true))
            {
                if (i++ > 0) sb.Append(',');

                var p = Db.CreateParameter(pi.Name, pi.GetValue(data, null), pi.PropertyType);
                dps.Add(p);
                sb.AppendFormat("{0}={1}", pi.Name, p.ParameterName);
            }
        }
        // Where条件
        if (where != null)
        {
            sb.Append(" Where ");
            var i = 0;
            foreach (var pi in where.GetType().GetProperties(true))
            {
                if (i++ > 0) sb.Append(" And ");

                var p = Db.CreateParameter(pi.Name, pi.GetValue(where, null), pi.PropertyType);
                dps.Add(p);
                sb.AppendFormat("{0}={1}", pi.Name, p.ParameterName);
            }
        }
        else
        {
            var name = OnGetKeyName(data.GetType());
            if (name.IsNullOrEmpty()) name = "Id";

            if (dic != null)
            {
                if (!dic.TryGetValue(name, out var val)) throw new XCodeException($"更新实体对象时未标记主键且未设置where");

                sb.Append(" Where ");

                var p = Db.CreateParameter(name, val, val?.GetType());
                dps.Add(p);
                sb.AppendFormat("{0}={1}", name, p.ParameterName);
            }
            else
            {
                var pi = data.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (pi == null) throw new XCodeException($"更新实体对象时未标记主键且未设置where");

                sb.Append(" Where ");

                var p = Db.CreateParameter(pi.Name, pi.GetValue(data, null), pi.PropertyType);
                dps.Add(p);
                sb.AppendFormat("{0}={1}", pi.Name, p.ParameterName);
            }
        }

        var sql = sb.Return(true);

        return ExecuteWrap(sql, "", dps.ToArray(), (ss, s, t, p) => ss.Execute(s, CommandType.Text, p), nameof(Update));
    }

    /// <summary>更细数据。无实体</summary>
    /// <param name="data">实体对象</param>
    /// <param name="table">表定义</param>
    /// <param name="columns">字段列表，为空表示所有字段</param>
    /// <param name="updateColumns">更新字段列表</param>
    /// <param name="addColumns">生成+=的字段</param>
    /// <returns></returns>
    public Int32 Update(IModel data, IDataTable table, IDataColumn[] columns, ICollection<String> updateColumns, ICollection<String> addColumns)
    {
        var ps = new HashSet<String>();
        var dps = new List<IDataParameter>();

        var sql = GetUpdateSql(table, columns, updateColumns, addColumns, ps);//builder.GetSql(Db, table, columns, data);
        if (sql.IsNullOrEmpty()) return 0;

        foreach (var dc in columns)
        {

            if (dc.Identity || dc.PrimaryKey)
            {
                //更新时添加主键做为查询条件
                dps.Add(Db.CreateParameter(dc.Name, data[dc.Name], dc));
                continue;
            }

            if (!ps.Contains(dc.Name)) continue;

            // 用于参数化的字符串不能为null
            var val = data[dc.Name];
            if (dc.DataType == typeof(String))
                val += "";
            else if (dc.DataType == typeof(DateTime))
            {
                var dt = val.ToDateTime();
                if (dt.Year < 1970) val = new DateTime(1970, 1, 1);
            }
            //byte[]类型查询时候参数化异常
            else if (dc.DataType == typeof(Byte[]))
            {
                val ??= new Byte[0];
            }
            if (dc.DataType == typeof(Guid))
            {
                val ??= Guid.Empty;
            }

            // 逐列创建参数对象
            dps.Add(Db.CreateParameter(dc.Name, val, dc));
        }

        return ExecuteWrap(sql, "", dps.ToArray(), (ss, s, t, p) => ss.Execute(s, CommandType.Text, p), nameof(Update));
    }

    private String GetUpdateSql(IDataTable table, IDataColumn[] columns, ICollection<String> updateColumns, ICollection<String> addColumns, ICollection<String> ps)
    {
        var sb = Pool.StringBuilder.Get();
        //var db = Database as DbBase;

        // 字段列表
        sb.AppendFormat("Update {0} Set ", Db.FormatName(table));
        foreach (var dc in columns)
        {
            if (dc.Identity || dc.PrimaryKey) continue;

            // 修复当columns看存在updateColumns不存在列时构造出来的Sql语句会出现连续逗号的问题
            if (updateColumns != null && updateColumns.Contains(dc.Name) && (addColumns == null || !addColumns.Contains(dc.Name)))
            {
                sb.AppendFormat("{0}={1},", Db.FormatName(dc), Db.FormatParameterName(dc.Name));

                if (!ps.Contains(dc.Name)) ps.Add(dc.Name);
            }
            else if (addColumns != null && addColumns.Contains(dc.Name))
            {
                sb.AppendFormat("{0}={0}+{1},", Db.FormatName(dc), Db.FormatParameterName(dc.Name));

                if (!ps.Contains(dc.Name)) ps.Add(dc.Name);
            }
            //sb.Append(",");
        }
        sb.Length--;
        //sb.Append(")");

        // 条件
        var pks = columns.Where(e => e.PrimaryKey).ToArray();
        if (pks == null || pks.Length == 0) throw new InvalidOperationException("未指定用于更新的主键");

        sb.Append(" Where ");
        foreach (var dc in columns)
        {
            if (!dc.PrimaryKey) continue;

            sb.AppendFormat("{0}={1}", Db.FormatName(dc), Db.FormatParameterName(dc.Name));
            sb.Append(" And ");

            if (!ps.Contains(dc.Name)) ps.Add(dc.Name);
        }
        sb.Length -= " And ".Length;

        return sb.Return(true);
    }

    /// <summary>删除数据</summary>
    /// <param name="tableName">表名</param>
    /// <param name="where">查询条件</param>
    /// <returns></returns>
    public Int32 Delete(String tableName, Object where)
    {
        if (tableName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(tableName));

        var (sql, dps) = GetDeleteSql(tableName, where);

        return ExecuteWrap(sql, "", dps.ToArray(), (ss, s, t, p) => ss.Execute(s, CommandType.Text, p), nameof(Delete));
    }

    private (String, IList<IDataParameter>) GetDeleteSql(String tableName, Object where)
    {
        var sb = Pool.StringBuilder.Get();
        sb.Append("Delete From ");
        sb.Append(tableName);

        // 带上参数化的Where条件
        var dps = new List<IDataParameter>();
        if (where != null)
        {
            sb.Append(" Where ");
            var i = 0;
            foreach (var pi in where.GetType().GetProperties(true))
            {
                if (i++ > 0) sb.Append(" And ");

                var p = Db.CreateParameter(pi.Name, pi.GetValue(where, null), pi.PropertyType);
                dps.Add(p);
                sb.AppendFormat("{0}={1}", pi.Name, p.ParameterName);
            }
        }

        return (sb.Return(true), dps);
    }

    /// <summary>插入数据</summary>
    /// <param name="data">实体对象</param>
    /// <param name="tableName">表名</param>
    /// <returns></returns>
    public Task<Int32> InsertAsync(Object data, String? tableName = null)
    {
        if (tableName.IsNullOrEmpty() && GetTableName != null) tableName = OnGetTableName(data.GetType());
        if (tableName.IsNullOrEmpty()) tableName = data.GetType().Name;

        var pis = data.ToDictionary();
        var dps = Db.CreateParameters(data);
        var ns = pis.Join(",", e => e.Key);
        var vs = dps.Join(",", e => e.ParameterName);
        var sql = $"Insert Into {tableName}({ns}) Values({vs})";

        return ExecuteAsyncWrap(sql, "", dps, (ss, s, t, p) => ss.ExecuteAsync(s, CommandType.Text, p), nameof(InsertAsync));
    }

    /// <summary>更新数据。不支持自动识别主键</summary>
    /// <param name="data">实体对象</param>
    /// <param name="where">查询条件。默认使用Id字段</param>
    /// <param name="tableName">表名</param>
    /// <returns></returns>
    public Task<Int32> UpdateAsync(Object data, Object where, String? tableName = null)
    {
        if (tableName.IsNullOrEmpty() && GetTableName != null) tableName = OnGetTableName(data.GetType());
        if (tableName.IsNullOrEmpty()) tableName = data.GetType().Name;

        var sb = Pool.StringBuilder.Get();
        sb.Append("Update ");
        sb.Append(tableName);

        var dps = new List<IDataParameter>();
        // Set参数
        {
            sb.Append(" Set ");
            var i = 0;
            foreach (var pi in data.GetType().GetProperties(true))
            {
                if (i++ > 0) sb.Append(',');

                var p = Db.CreateParameter(pi.Name, pi.GetValue(data, null), pi.PropertyType);
                dps.Add(p);
                sb.AppendFormat("{0}={1}", pi.Name, p.ParameterName);
            }
        }
        // Where条件
        if (where != null)
        {
            sb.Append(" Where ");
            var i = 0;
            foreach (var pi in where.GetType().GetProperties(true))
            {
                if (i++ > 0) sb.Append(" And ");

                var p = Db.CreateParameter(pi.Name, pi.GetValue(where, null), pi.PropertyType);
                dps.Add(p);
                sb.AppendFormat("{0}={1}", pi.Name, p.ParameterName);
            }
        }
        else
        {
            var name = OnGetKeyName(data.GetType());
            if (name.IsNullOrEmpty()) name = "Id";

            var pi = data.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (pi == null) throw new XCodeException($"更新实体对象时未标记主键且未设置where");

            sb.Append(" Where ");

            var p = Db.CreateParameter(pi.Name, pi.GetValue(data, null), pi.PropertyType);
            dps.Add(p);
            sb.AppendFormat("{0}={1}", pi.Name, p.ParameterName);
        }

        var sql = sb.Return(true);

        return ExecuteAsyncWrap(sql, "", dps.ToArray(), (ss, s, t, p) => ss.ExecuteAsync(s, CommandType.Text, p), nameof(UpdateAsync));
    }

    /// <summary>删除数据</summary>
    /// <param name="tableName">表名</param>
    /// <param name="where">查询条件</param>
    /// <returns></returns>
    public Task<Int32> DeleteAsync(String tableName, Object where)
    {
        if (tableName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(tableName));

        var (sql, dps) = GetDeleteSql(tableName, where);

        return ExecuteAsyncWrap(sql, "", dps.ToArray(), (ss, s, t, p) => ss.ExecuteAsync(s, CommandType.Text, p), nameof(DeleteAsync));
    }

    /// <summary>插入数据</summary>
    /// <param name="tableName"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    [Obsolete]
    public Int32 Insert(String tableName, Object data) => Insert(data, tableName);

    /// <summary>更新数据</summary>
    /// <param name="tableName"></param>
    /// <param name="data"></param>
    /// <param name="where"></param>
    /// <returns></returns>
    [Obsolete]
    public Int32 Update(String tableName, Object data, Object where) => Update(data, where, tableName);
    #endregion
}