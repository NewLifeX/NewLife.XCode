﻿using System.Collections;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;
using NewLife;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;
using NewLife.Reflection;
using XCode.Common;

namespace XCode.DataAccessLayer;

class DaMeng : RemoteDb
{
    #region 属性
    /// <summary>返回数据库类型。外部DAL数据库类请使用Other</summary>
    public override DatabaseType Type => DatabaseType.DaMeng;

    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory() => GetProviderFactory(null, "DmProvider.dll", "Dm.DmClientFactory");

    const String Server_Key = "Server";
    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        var key = builder[Server_Key];
        if (key.EqualIgnoreCase(".", "localhost"))
        {
            builder[Server_Key] = "localhost";
        }
    }
    #endregion

    #region 方法
    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new DaMengSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new DaMengMeta();

    public override Boolean Support(String providerName)
    {
        providerName = providerName.ToLower();
        if (providerName.Contains("dameng")) return true;
        if (providerName == "dm") return true;

        return false;
    }
    #endregion

    #region 数据库特性
    ///// <summary>已重载。格式化时间</summary>
    ///// <param name="dt"></param>
    ///// <returns></returns>
    //public override String FormatDateTime(DateTime dt)
    //{
    //    if (dt.Hour == 0 && dt.Minute == 0 && dt.Second == 0) return "To_Date('{0:yyyy-MM-dd}', 'YYYY-MM-DD')".F(dt);

    //    return "To_Date('{0:yyyy-MM-dd HH:mm:ss}', 'YYYY-MM-DD HH24:MI:SS')".F(dt);
    //}

    public override String FormatValue(IDataColumn field, Object? value)
    {
        var code = System.Type.GetTypeCode(field.DataType);
        var isNullable = field.Nullable;

        if (code == TypeCode.String)
        {
            if (value == null) return isNullable ? "null" : "''";

            if (field.RawType.StartsWithIgnoreCase("n"))
                return "N'" + value.ToString().Replace("'", "''") + "'";
            else
                return "'" + value.ToString().Replace("'", "''") + "'";
        }

        return base.FormatValue(field, value);
    }

    ///// <summary>格式化标识列，返回插入数据时所用的表达式，如果字段本身支持自增，则返回空</summary>
    ///// <param name="field">字段</param>
    ///// <param name="value">数值</param>
    ///// <returns></returns>
    //public override String FormatIdentity(IDataColumn field, Object value) => String.Format("SEQ_{0}.nextval", field.Table.TableName);

    internal protected override String ParamPrefix => ":";

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => (!String.IsNullOrEmpty(left) ? left : "\'\'") + "||" + (!String.IsNullOrEmpty(right) ? right : "\'\'");

    /// <summary>创建参数</summary>
    /// <param name="name">名称</param>
    /// <param name="value">值</param>
    /// <param name="type">类型</param>
    /// <returns></returns>
    public override IDataParameter CreateParameter(String name, Object? value, Type? type = null)
    {
        //var type = field?.DataType;
        if (type == null)
        {
            type = value?.GetType();
            // 参数可能是数组
            if (type != null && type != typeof(Byte[]) && type.IsArray) type = type.GetElementTypeEx();
        }

        if (type == typeof(Boolean))
        {
            if (value is IEnumerable<Object> list)
                value = list.Select(e => e.ToBoolean() ? 1 : 0).ToArray();
            else if (value is IEnumerable<Boolean> list2)
                value = list2.Select(e => e.ToBoolean() ? 1 : 0).ToArray();
            else
                value = value.ToBoolean() ? 1 : 0;

            //type = typeof(Int32);
            var dp2 = Factory.CreateParameter();
            dp2.ParameterName = FormatParameterName(name);
            dp2.Direction = ParameterDirection.Input;
            dp2.DbType = DbType.Int32;
            dp2.Value = value;
            return dp2;
        }

        var dp = base.CreateParameter(name, value, type);

        // 修正时间映射
        if (type == typeof(DateTime)) dp.DbType = DbType.Date;

        return dp;
    }
    #endregion

    #region 关键字
    protected override String ReservedWordsStr
    {
        get
        {
            return "ALL,ALTER,AND,ANY,AS,ASC,BETWEEN,BY,CHAR,CHECK,CLUSTER,COMPRESS,CONNECT,CREATE,DATE,DECIMAL,DEFAULT,DELETE,DESC,DISTINCT,DROP,ELSE,EXCLUSIVE,EXISTS,FLOAT,FOR,FROM,GRANT,GROUP,HAVING,IDENTIFIED,IN,INDEX,INSERT,INTEGER,INTERSECT,INTO,IS,LIKE,LOCK,LONG,MINUS,MODE,NOCOMPRESS,NOT,NOWAIT,NULL,NUMBER,OF,ON,OPTION,OR,ORDER,PCTFREE,PRIOR,PUBLIC,RAW,RENAME,RESOURCE,REVOKE,SELECT,SET,SHARE,SIZE,SMALLINT,START,SYNONYM,TABLE,THEN,TO,TRIGGER,UNION,UNIQUE,UPDATE,VALUES,VARCHAR,VARCHAR2,VIEW,WHERE,WITH," +
              "Sort,Level,User,Online,Path,Rows,Columns";
        }
    }

    /// <summary>格式化关键字</summary>
    /// <param name="keyWord">表名</param>
    /// <returns></returns>
    public override String FormatKeyWord(String keyWord)
    {
        //return String.Format("\"{0}\"", keyWord);

        //if (String.IsNullOrEmpty(keyWord)) throw new ArgumentNullException("keyWord");
        if (String.IsNullOrEmpty(keyWord)) return keyWord;

        var pos = keyWord.LastIndexOf(".");

        if (pos < 0) return "\"" + keyWord + "\"";

        var tn = keyWord[(pos + 1)..];
        if (tn.StartsWith("\"")) return keyWord;

        return keyWord[..(pos + 1)] + "\"" + tn + "\"";
    }
    #endregion
}

/// <summary>DaMeng数据库</summary>
internal class DaMengSession : RemoteDbSession
{
    #region 构造函数
    public DaMengSession(IDatabase db) : base(db) { }
    #endregion

    #region 基本方法 查询/执行
    /// <summary>快速查询单表记录数，稍有偏差</summary>
    /// <param name="tableName"></param>
    /// <returns></returns>
    public override Int64 QueryCountFast(String tableName)
    {
        if (String.IsNullOrEmpty(tableName)) return 0;

        var p = tableName.LastIndexOf(".");
        if (p >= 0 && p < tableName.Length - 1) tableName = tableName[(p + 1)..];
        tableName = tableName.ToUpper();

        var owner = (Database as DaMeng)!.Owner;
        if (owner.IsNullOrEmpty()) owner = (Database as DaMeng)!.User;
        //var owner = (Database as DaMeng).Owner.ToUpper();
        owner = owner.ToUpper();

        //var sql = String.Format("select NUM_ROWS from sys.all_indexes where TABLE_OWNER='{0}' and TABLE_NAME='{1}' and UNIQUENESS='UNIQUE'", owner, tableName);
        // 某些表没有聚集索引，导致查出来的函数为零
        var sql = $"select NUM_ROWS from all_tables where OWNER='{owner}' and TABLE_NAME='{tableName}'";
        return ExecuteScalar<Int64>(sql);
    }

    static readonly Regex reg_SEQ = new(@"\b(\w+)\.nextval\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    /// <summary>执行插入语句并返回新增行的自动编号</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        BeginTransaction(IsolationLevel.Serializable);
        try
        {
            Int64 rs = Execute(sql, type, ps);
            if (rs > 0)
            {
                var m = reg_SEQ.Match(sql);
                if (m != null && m.Success && m.Groups != null && m.Groups.Count > 0)
                    rs = ExecuteScalar<Int64>($"Select {m.Groups[1].Value}.currval From dual");
                else
                    rs = ExecuteScalar<Int64>("Select @@Identity");
            }
            Commit();
            return rs;
        }
        catch { Rollback(true); throw; }
    }

    public override Task<Int64> QueryCountFastAsync(String tableName)
    {
        if (String.IsNullOrEmpty(tableName)) return Task.FromResult(0L);

        var p = tableName.LastIndexOf(".");
        if (p >= 0 && p < tableName.Length - 1) tableName = tableName[(p + 1)..];
        tableName = tableName.ToUpper();

        var owner = (Database as DaMeng)!.Owner;
        if (owner.IsNullOrEmpty()) owner = (Database as DaMeng)!.User;
        owner = owner.ToUpper();

        // 某些表没有聚集索引，导致查出来的函数为零
        var sql = $"select NUM_ROWS from all_tables where OWNER='{owner}' and TABLE_NAME='{tableName}'";
        return ExecuteScalarAsync<Int64>(sql);
    }

    public override async Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        BeginTransaction(IsolationLevel.Serializable);
        try
        {
            Int64 rs = await ExecuteAsync(sql, type, ps).ConfigureAwait(false);
            if (rs > 0)
            {
                var m = reg_SEQ.Match(sql);
                if (m != null && m.Success && m.Groups != null && m.Groups.Count > 0)
                    rs = await ExecuteScalarAsync<Int64>($"Select {m.Groups[1].Value}.currval From dual").ConfigureAwait(false);
                else
                    rs = await ExecuteScalarAsync<Int64>("Select @@Identity").ConfigureAwait(false);
            }
            Commit();
            return rs;
        }
        catch { Rollback(true); throw; }
    }

    /// <summary>重载支持批量操作</summary>
    /// <param name="sql"></param>
    /// <param name="type"></param>
    /// <param name="ps"></param>
    /// <returns></returns>
    protected override DbCommand OnCreateCommand(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        var cmd = base.OnCreateCommand(sql, type, ps);
        //if (cmd == null) return null;

        // 如果参数Value都是数组，那么就是批量操作
        if (ps != null && ps.Length > 0 && ps.All(p => p.Value is IList))
        {
            if (ps.First().Value is IList arr)
                cmd.SetValue("ArrayBindCount", arr.Count);
            cmd.SetValue("BindByName", true);

            // 超时时间放大10倍
            if (cmd.CommandTimeout > 0)
                cmd.CommandTimeout *= 10;
            else
                cmd.CommandTimeout = 120;
        }

        return cmd;
    }
    #endregion

    #region 批量操作
    public override Int32 Insert(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list)
    {
        var ps = new HashSet<String>();
        var sql = GetInsertSql(table, columns, ps);
        var dps = GetParameters(columns, ps, list);
        DefaultSpan.Current?.AppendTag(sql);

        return Execute(sql, CommandType.Text, dps);
    }

    private String GetInsertSql(IDataTable table, IDataColumn[] columns, ICollection<String> ps)
    {
        var sb = Pool.StringBuilder.Get();
        var db = Database as DbBase;

        // 字段列表
        sb.AppendFormat("Insert Into {0}(", db!.FormatName(table));
        foreach (var dc in columns)
        {
            //if (dc.Identity) continue;

            sb.Append(db.FormatName(dc));
            sb.Append(',');
        }
        sb.Length--;
        sb.Append(')');

        // 值列表
        sb.Append(" Values(");
        foreach (var dc in columns)
        {
            //if (dc.Identity) continue;

            sb.Append(db.FormatParameterName(dc.Name));
            sb.Append(',');

            if (!ps.Contains(dc.Name)) ps.Add(dc.Name);
        }
        sb.Length--;
        sb.Append(')');

        return sb.Return(true);
    }

    private IDataParameter[] GetParameters(IDataColumn[] columns, ICollection<String> ps, IEnumerable<IModel> list)
    {
        var db = Database;
        var dps = new List<IDataParameter>();
        foreach (var dc in columns)
        {
            if (!ps.Contains(dc.Name)) continue;

            var type = dc.DataType ?? throw new ArgumentNullException(nameof(dc.DataType));
            if (type.IsEnum) type = typeof(Int32);

            var arr = Array.CreateInstance(type, list.Count());
            var k = 0;
            foreach (var entity in list)
            {
                //vs.Add(entity[dc.Name]);
                arr.SetValue(entity[dc.Name], k++);
            }
            var dp = db.CreateParameter(dc.Name, arr, dc);

            dps.Add(dp);
        }

        return dps.ToArray();
    }

    /// <summary>批量插入或更新</summary>
    /// <param name="table">数据表</param>
    /// <param name="columns">要插入的字段，默认所有字段</param>
    /// <param name="updateColumns">主键已存在时，要更新的字段。属性名，不是字段名</param>
    /// <param name="addColumns">主键已存在时，要累加更新的字段。属性名，不是字段名</param>
    /// <param name="list">实体列表</param>
    /// <returns></returns>
    public override Int32 Upsert(IDataTable table, IDataColumn[] columns, ICollection<String>? updateColumns, ICollection<String>? addColumns, IEnumerable<IModel> list)
    {
        var ps = new HashSet<String>();
        var insert = GetInsertSql(table, columns, ps);
        var update = GetUpdateSql(table, columns, updateColumns, addColumns, ps);

        var sb = Pool.StringBuilder.Get();
        sb.AppendLine("BEGIN");
        sb.AppendLine(insert + ";");
        sb.AppendLine("EXCEPTION");
        // 没有更新时，直接返回，可用于批量插入且其中部分有冲突需要忽略的场景
        if (!update.IsNullOrEmpty())
        {
            sb.AppendLine("WHEN DUP_VAL_ON_INDEX THEN");
            sb.AppendLine(update + ";");
        }
        else
        {
            //sb.AppendLine("WHEN OTHERS THEN");
            sb.AppendLine("WHEN DUP_VAL_ON_INDEX THEN");
            sb.AppendLine("RETURN;");
        }
        sb.AppendLine("END;");

        var sql = sb.Return(true);
        DefaultSpan.Current?.AppendTag(sql);

        var dps = GetParameters(columns, ps, list);

        return Execute(sql, CommandType.Text, dps);
    }

    private String? GetUpdateSql(IDataTable table, IDataColumn[] columns, ICollection<String>? updateColumns, ICollection<String>? addColumns, ICollection<String> ps)
    {
        if ((updateColumns == null || updateColumns.Count == 0)
            && (addColumns == null || addColumns.Count == 0)) return null;

        var sb = Pool.StringBuilder.Get();
        var db = Database as DbBase;

        // 字段列表
        sb.AppendFormat("Update {0} Set ", db!.FormatName(table));
        foreach (var dc in columns)
        {
            if (dc.Identity || dc.PrimaryKey) continue;

            if (addColumns != null && addColumns.Contains(dc.Name))
            {
                sb.AppendFormat("{0}={0}+{1},", db.FormatName(dc), db.FormatParameterName(dc.Name));

                if (!ps.Contains(dc.Name)) ps.Add(dc.Name);
            }
            else if (updateColumns != null && updateColumns.Contains(dc.Name))
            {
                sb.AppendFormat("{0}={1},", db.FormatName(dc), db.FormatParameterName(dc.Name));

                if (!ps.Contains(dc.Name)) ps.Add(dc.Name);
            }
        }
        sb.Length--;

        // 条件
        sb.Append(" Where ");
        foreach (var dc in columns)
        {
            if (!dc.PrimaryKey) continue;

            sb.AppendFormat("{0}={1}", db.FormatName(dc), db.FormatParameterName(dc.Name));
            sb.Append(" And ");

            if (!ps.Contains(dc.Name)) ps.Add(dc.Name);
        }
        sb.Length -= " And ".Length;

        return sb.Return(true);
    }

    public override Int32 Update(IDataTable table, IDataColumn[] columns, ICollection<String>? updateColumns, ICollection<String>? addColumns, IEnumerable<IModel> list)
    {
        var ps = new HashSet<String>();
        var sql = GetUpdateSql(table, columns, updateColumns, addColumns, ps);
        if (sql.IsNullOrEmpty()) return 0;
        DefaultSpan.Current?.AppendTag(sql);

        var dps = GetParameters(columns, ps, list);

        return Execute(sql, CommandType.Text, dps);
    }
    #endregion
}

/// <summary>DaMeng元数据</summary>
class DaMengMeta : RemoteDbMetaData
{
    public DaMengMeta() => Types = _DataTypes;

    /// <summary>拥有者</summary>
    public String Owner
    {
        get
        {
            var owner = Database.Owner;
            if (owner.IsNullOrEmpty()) owner = (Database as DaMeng)!.User;

            return owner;
        }
    }

    /// <summary>用户名</summary>
    public String UserID => (Database as DaMeng)!.User.ToUpper();

    /// <summary>取得所有表构架</summary>
    /// <returns></returns>
    protected override List<IDataTable> OnGetTables(String[]? names)
    {
        DataTable? dt = null;

        // 不缺分大小写，并且不是保留字，才转大写
        if (names != null)
        {
            var db = Database as DaMeng;
            /*if (db.IgnoreCase)*/
            names = names.Select(e => db!.IsReservedWord(e) ? e : e.ToUpper()).ToArray();
        }

        // 采用集合过滤，提高效率
        String? tableName = null;
        if (names != null && names.Length == 1) tableName = names.FirstOrDefault();
        if (tableName.IsNullOrEmpty()) tableName = null;

        var owner = Owner;
        //if (owner.IsNullOrEmpty()) owner = UserID;

        //dt = Get("all_tables", owner, tableName);
        dt = GetSchema(_.Tables, [owner, tableName]);
        if (dt == null) return [];

        if (!dt.Columns.Contains("TABLE_TYPE"))
        {
            dt.Columns.Add("TABLE_TYPE", typeof(String));
            foreach (var dr in dt.Rows.ToArray())
            {
                dr["TABLE_TYPE"] = "Table";
            }
        }
        var dtView = GetSchema(_.Views, [owner, tableName]);
        if (dtView != null && dtView.Rows.Count != 0)
        {
            foreach (var dr in dtView.Rows.ToArray())
            {
                var drNew = dt.NewRow();
                drNew["OWNER"] = dr["OWNER"];
                drNew["TABLE_NAME"] = dr["VIEW_NAME"];
                drNew["TABLE_TYPE"] = "View";
                dt.Rows.Add(drNew);
            }
        }

        var data = new NullableDictionary<String, DataTable?>(StringComparer.OrdinalIgnoreCase);

        // 如果表太多，则只要目标表数据
        var mulTable = "";
        if (dt.Rows.Count > 10 && names != null && names.Length > 0)
        {
            //var tablenames = dt.Rows.ToArray().Select(e => "'{0}'".F(e["TABLE_NAME"]));
            //mulTable = " And TABLE_NAME in ({0})".F(tablenames.Join(","));
            mulTable = $" And TABLE_NAME in ({names.Select(e => $"'{e}'").Join(",")})";
        }

        // 列和索引
        data["Columns"] = Get("all_tab_columns", owner, tableName, mulTable);
        data["Indexes"] = Get("all_indexes", owner, tableName, mulTable);
        data["IndexColumns"] = Get("all_ind_columns", owner, tableName, mulTable, "Table_Owner");

        // 主键
        if (MetaDataCollections.Contains(_.PrimaryKeys) && !tableName.IsNullOrEmpty())
            data["PrimaryKeys"] = GetSchema(_.PrimaryKeys, [owner, tableName, null]);

        // 序列
        data["Sequences"] = Get("all_sequences", owner, null, null, "Sequence_Owner");

        // 表注释
        data["TableComment"] = Get("all_tab_comments", owner, tableName, mulTable);

        // 列注释
        data["ColumnComment"] = Get("all_col_comments", owner, tableName, mulTable, "SCHEMA_NAME");

        var list = GetTables(dt.Rows.ToArray(), names, data);

        return list;
    }

    /// <summary>
    /// 快速取得所有表名
    /// </summary>
    /// <returns></returns>
    public override IList<String> GetTableNames()
    {
        var list = new List<String>();

        var dt = GetSchema(_.Tables, [Owner, null]);
        if (dt?.Rows == null || dt.Rows.Count <= 0) return list;

        foreach (DataRow dr in dt.Rows)
        {
            var tn = GetDataRowValue<String>(dr, _.TalbeName);
            if (!tn.IsNullOrEmpty()) list.Add(tn);
        }

        return list;
    }

    private DataTable Get(String name, String owner, String? tableName, String? mulTable = null, String? ownerName = null)
    {
        if (ownerName.IsNullOrEmpty()) ownerName = "Owner";
        var sql = $"Select * From {name} Where {ownerName}='{owner}'";
        if (!tableName.IsNullOrEmpty())
            sql += $" And TABLE_NAME='{tableName}'";
        else if (!mulTable.IsNullOrEmpty())
            sql += mulTable;

        return Database.CreateSession().Query(sql).Tables[0];
    }

    protected override void FixTable(IDataTable table, DataRow dr, IDictionary<String, DataTable?>? data)
    {
        base.FixTable(table, dr, data);

        // 主键
        var dt = data?["PrimaryKeys"];
        if (dt != null && dt.Rows.Count > 0)
        {
            var drs = dt.Select($"{_.TalbeName}='{table.TableName}'");
            if (drs != null && drs.Length > 0)
            {
                // 找到主键所在索引，这个索引的列才是主键
                if (TryGetDataRowValue(drs[0], _.IndexName, out String? name) && !String.IsNullOrEmpty(name))
                {
                    var di = table.Indexes.FirstOrDefault(i => i.Name == name);
                    if (di != null)
                    {
                        di.PrimaryKey = true;
                        foreach (var dc in table.Columns)
                        {
                            dc.PrimaryKey = di.Columns.Contains(dc.ColumnName);
                        }
                    }
                }
            }
        }

        // 表注释 USER_TAB_COMMENTS
        table.Description = GetTableComment(table.TableName, data);

        if (table?.Columns == null || table.Columns.Count == 0) return;
    }

    String? GetTableComment(String name, IDictionary<String, DataTable?>? data)
    {
        var dt = data?["TableComment"];
        if (dt?.Rows == null || dt.Rows.Count <= 0) return null;

        var where = $"TABLE_NAME='{name}'";
        var drs = dt.Select(where);
        if (drs != null && drs.Length > 0) return Convert.ToString(drs[0]["COMMENTS"]);

        return null;
    }

    /// <summary>取得指定表的所有列构架</summary>
    /// <param name="table"></param>
    /// <param name="columns">列</param>
    /// <param name="data"></param>
    /// <returns></returns>
    protected override List<IDataColumn> GetFields(IDataTable table, DataTable? columns, IDictionary<String, DataTable?>? data)
    {
        var list = base.GetFields(table, columns, data);
        if (list == null || list.Count <= 0) return [];

        // 字段注释
        if (list.Count > 0)
        {
            foreach (var field in list)
            {
                field.Description = GetColumnComment(table.TableName, field.ColumnName, data);
            }
        }

        return list;
    }

    const String KEY_OWNER = "OWNER";

    protected override List<IDataColumn> GetFields(IDataTable table, DataRow[] rows)
    {
        if (rows == null || rows.Length <= 0) return [];

        var owner = Owner;
        if (owner.IsNullOrEmpty() || !rows[0].Table.Columns.Contains(KEY_OWNER)) return base.GetFields(table, rows);

        var list = new List<DataRow>();
        foreach (var dr in rows)
        {
            if (TryGetDataRowValue(dr, KEY_OWNER, out String? str) && owner.EqualIgnoreCase(str)) list.Add(dr);
        }

        return base.GetFields(table, list.ToArray());
    }

    String? GetColumnComment(String tableName, String columnName, IDictionary<String, DataTable?>? data)
    {
        var dt = data?["ColumnComment"];
        if (dt?.Rows == null || dt.Rows.Count <= 0) return null;

        var where = $"{_.TalbeName}='{tableName}' AND {_.ColumnName}='{columnName}'";
        var drs = dt.Select(where);
        if (drs != null && drs.Length > 0) return Convert.ToString(drs[0]["COMMENTS"]);

        return null;
    }

    protected override void FixField(IDataColumn field, DataRow drColumn)
    {
        var dr = drColumn;

        // 长度
        //field.Length = GetDataRowValue<Int32>(dr, "CHAR_LENGTH", "DATA_LENGTH");
        field.Length = GetDataRowValue<Int32>(dr, "DATA_LENGTH");

        if (field is XField fi)
        {
            // 精度 与 位数
            fi.Precision = GetDataRowValue<Int32>(dr, "DATA_PRECISION");
            fi.Scale = GetDataRowValue<Int32>(dr, "DATA_SCALE");
            if (field.Length == 0) field.Length = fi.Precision;
        }

        // 长度
        if (TryGetDataRowValue(drColumn, "LENGTHINCHARS", out Int32 len) && len > 0) field.Length = len;

        base.FixField(field, drColumn);
    }

    protected override void FixIndex(IDataIndex index, DataRow dr)
    {
        if (TryGetDataRowValue(dr, "UNIQUENESS", out String? str))
            index.Unique = str == "UNIQUE";

        base.FixIndex(index, dr);
    }

    /// <summary>数据类型映射</summary>
    private static readonly Dictionary<Type, String[]> _DataTypes = new()
    {
        { typeof(Byte[]), new String[] { "BLOB", "BINARY", "VARBINARY" } },
        { typeof(Boolean), new String[] { "BIT" } },
        { typeof(Byte), new String[] { "TINYINT" } },
        { typeof(Int16), new String[] { "SMALLINT" } },
        { typeof(Int32), new String[] { "INT" } },
        { typeof(Int64), new String[] { "BIGINT" } },
        { typeof(Single), new String[] { "REAL" } },
        { typeof(Double), new String[] { "DOUBLE" } },
        { typeof(Decimal), new String[] { "DEC" } },
        { typeof(DateTime), new String[] { "DATETIME", "TIME", "DATE", "TIMESTAMP" } },
        { typeof(String), new String[] { "VARCHAR({0})", "TEXT", "CHAR", "CLOB" } }
    };

    #region 架构定义
    //public override Object SetSchema(DDLSchema schema, params Object[] values)
    //{
    //    var session = Database.CreateSession();

    //    var dbname = String.Empty;
    //    var databaseName = String.Empty;
    //    switch (schema)
    //    {
    //        case DDLSchema.DatabaseExist:
    //            // DaMeng不支持判断数据库是否存在
    //            return true;

    //        default:
    //            break;
    //    }
    //    return base.SetSchema(schema, values);
    //}

    public override String DatabaseExistSQL(String dbname) => String.Empty;

    protected override String GetFieldConstraints(IDataColumn field, Boolean onlyDefine)
    {
        var str = field.Nullable ? " NULL" : " NOT NULL";

        if (field.Identity) str = " IDENTITY NOT NULL";

        // 默认值
        if (!field.Nullable && !field.Identity)
        {
            str += GetDefault(field, onlyDefine);
        }

        return str;
    }

    //protected override String GetDefault(IDataColumn field, Boolean onlyDefine)
    //{
    //    if (field.DataType == typeof(DateTime)) return " DEFAULT To_Date('0001-01-01','yyyy-mm-dd')";

    //    return base.GetDefault(field, onlyDefine);
    //}

    public override String CreateTableSQL(IDataTable table)
    {
        var fs = new List<IDataColumn>(table.Columns);

        var sb = new StringBuilder(32 + fs.Count * 20);

        sb.AppendFormat("Create Table {0}(", FormatName(table));
        for (var i = 0; i < fs.Count; i++)
        {
            sb.AppendLine();
            sb.Append('\t');
            sb.Append(FieldClause(fs[i], true));
            if (i < fs.Count - 1) sb.Append(',');
        }

        // 主键
        var pks = table.PrimaryKeys;
        if (pks != null && pks.Length > 0) sb.AppendFormat(",\r\n\tprimary key ({0})", pks.Join(",", FormatName));

        sb.AppendLine();
        sb.Append(')');

        var sql = sb.ToString();
        if (sql.IsNullOrEmpty()) return sql;

        // 去掉分号后的空格，DaMeng不支持同时执行多个语句
        return sb.ToString();
    }

    //public override String DropTableSQL(String tableName)
    //{
    //    var sql = base.DropTableSQL(tableName);
    //    if (String.IsNullOrEmpty(sql)) return sql;

    //    var sqlSeq = String.Format("Drop Sequence SEQ_{0}", tableName);
    //    return sql + "; " + Environment.NewLine + sqlSeq;
    //}

    public override String? AddColumnSQL(IDataColumn field) => $"Alter Table {FormatName(field.Table)} Add {FieldClause(field, true)}";

    public override String AlterColumnSQL(IDataColumn field, IDataColumn? oldfield) => $"Alter Table {FormatName(field.Table)} Modify {FieldClause(field, false)}";

    public override String DropColumnSQL(IDataColumn field) => $"Alter Table {FormatName(field.Table)} Drop Column {field}";

    public override String AddTableDescriptionSQL(IDataTable table) => $"Comment On Table {FormatName(table)} is '{table.Description}'";

    public override String DropTableDescriptionSQL(IDataTable table) => $"Comment On Table {FormatName(table)} is ''";

    public override String AddColumnDescriptionSQL(IDataColumn field) => $"Comment On Column {FormatName(field.Table)}.{FormatName(field)} is '{field.Description}'";

    public override String DropColumnDescriptionSQL(IDataColumn field) => $"Comment On Column {FormatName(field.Table)}.{FormatName(field)} is ''";
    #endregion
}