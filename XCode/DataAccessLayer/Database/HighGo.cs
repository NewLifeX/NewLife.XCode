using System.Data;
using System.Data.Common;
using NewLife;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;
using NewLife.Web;

namespace XCode.DataAccessLayer;

/// <summary>瀚高(HighGo)</summary>
internal class HighGo : RemoteDb
{
    #region 属性
    /// <summary>返回数据库类型。外部DAL数据库类请使用Other</summary>
    public override DatabaseType Type => DatabaseType.HighGo;
    /// <summary>模式</summary>
    public string? TableSchema { get; set; }
    /// <summary>系统数据库名</summary>
    public override String SystemDatabaseName => string.Empty;
    #endregion

    #region 数据库特性
    protected override String ReservedWordsStr =>
        "ABORT,ABSOLUTE,ACCESS,ACTION,ADD,AFTER,ALL,ALLOCATE,ALTER,AND,ANY,ARE,ARRAY,AS,ASC,ASSERTION,AT,AUTHORIZATION,AVG,BEFORE,BEGIN,BETWEEN,BIT,BIT_LENGTH,BOTH,BY,CASCADE,CASCADED,CASE,CAST,CATALOG,CHAR,CHAR_LENGTH,CHARACTER,CHARACTER_LENGTH,CHECK,CLOSE,COALESCE,COLLATE,COLLATION,COLUMN,COMMIT,CONNECT,CONNECTION,CONSTRAINT,CONSTRAINTS,CONTINUE,CONVERT,CORRESPONDING,COUNT,CREATE,CROSS,CURRENT,CURRENT_DATE,CURRENT_TIME,CURRENT_TIMESTAMP,CURRENT_USER,CURSOR,DATE,DAY,DEALLOCATE,DEC,DECIMAL,DECLARE,DEFAULT,DEFERRABLE,DEFERRED,DELETE,DESC,DESCRIBE,DESCRIPTOR,DIAGNOSTICS,DISCONNECT,DISTINCT,DOMAIN,DOUBLE,DROP,ELSE,END,END-EXEC,ESCAPE,EXCEPT,EXCEPTION,EXEC,EXECUTE,EXISTS,EXTERNAL,EXTRACT,FALSE,FETCH,FIRST,FLOAT,FOR,FOREIGN,FOUND,FROM,FULL,GET,GLOBAL,GO,GOTO,GRANT,GROUP,HAVING,HOUR,IDENTITY,IMMEDIATE,IN,INDICATOR,INITIALLY,INNER,INPUT,INSENSITIVE,INSERT,INT,INTEGER,INTERSECT,INTERVAL,INTO,IS,ISOLATION,JOIN,KEY,LANGUAGE,LAST,LEADING,LEFT,LEVEL,LIKE,LOCAL,LOWER,MATCH,MAX,MIN,MINUTE,MODULE,MONTH,NAMES,NATIONAL,NATURAL,NCHAR,NEXT,NO,NOT,NULL,NULLIF,NUMERIC,OCTET_LENGTH,OF,ON,ONLY,OPEN,OPTION,OR,ORDER,OUTER,OUTPUT,OVERLAPS,PAD,PARTIAL,POSITION,PRECISION,PREPARE,PRESERVE,PRIMARY,PRIOR,PRIVILEGES,PROCEDURE,PUBLIC,READ,REAL,REFERENCES,RELATIVE,RESTRICT,REVOKE,RIGHT,ROLLBACK,ROWS,SCHEMA,SCROLL,SECOND,SECTION,SELECT,SESSION,SESSION_USER,SET,SIZE,SMALLINT,SOME,SPACE,SQL,SQLCODE,SQLERROR,SQLSTATE,SUBSTRING,SUM,SYSTEM_USER,TABLE,TEMPORARY,THEN,TIME,TIMESTAMP,TIMEZONE_HOUR,TIMEZONE_MINUTE,TO,TRAILING,TRANSACTION,TRANSLATE,TRANSLATION,TRIM,TRUE,UNION,UNIQUE,UNKNOWN,UPDATE,UPPER,USAGE,USER,USING,VALUE,VALUES,VARYING,VIEW,WHEN,WHENEVER,WHERE,WITH,WORK,WRITE,YEAR,ZONE";

    public override String FormatName(String name)
    {
        if (name.IsNullOrWhiteSpace()) { return name; }
        if (name[0] == '"' && name[name.Length - 1] == '"') { return name; }
        return $"\"{name}\"";
    }
    public override String FormatValue(IDataColumn column, Object? value)
    {
        if (column.DataType == typeof(Boolean)) { return value.ToBoolean() ? "true" : "false"; }
        return base.FormatValue(column, value);
    }
    public override String FormatLike(IDataColumn column, String format)
    {
        format = format.Replace("'%{", "'%' || {").Replace("}%'", "} || '%'").Replace("'{", "{").Replace("}'", "}");
        return base.FormatLike(column, format);
    }
    public override String? BuildDeleteSql(String tableName, String where, Int32 batchSize)
    {
        if (batchSize <= 0) return base.BuildDeleteSql(tableName, where, 0);
        var sb = Pool.StringBuilder.Get();
        var xWhere = string.Empty;
        var xTable = this.FormatName(tableName);
        if (!string.IsNullOrWhiteSpace(where)) xWhere = " Where " + where;
        var sql = $"WITH to_delete AS (SELECT \"ctid\" FROM {xTable} {xWhere} LIMIT {batchSize}) ";
        sql += $"DELETE FROM {xTable} where \"ctid\" in (SELECT \"ctid\" from to_delete)";
        return sql;
    }
    #endregion

    protected override void OnSetConnectionString(ConnectionStringBuilder builder) => base.OnSetConnectionString(builder);
    protected override DbProviderFactory? CreateFactory()
    {
        if (!Provider.IsNullOrEmpty() && Provider.Contains("HighGo"))
        {
            var type = PluginHelper.LoadPlugin("Nhgdb.NhgdbFactory", null, "Nhgdb.dll", null);
            var factory = GetProviderFactory(type);
            if (factory != null) return factory;
        }
        // 找不到驱动时，再到线上下载
        {
            var factory = GetProviderFactory(null, "Nhgdb.dll", "Nhgdb.NhgdbFactory");
            return factory;
        }
    }
    /// <summary>创建元数据对象</summary>
    protected override IMetaData OnCreateMetaData() => new HighGoMetaData();
    /// <summary>创建数据库会话</summary>
    protected override IDbSession OnCreateSession() => new HighGoSession(this);
}
/// <summary>瀚高(HighGo)数据库</summary>
internal class HighGoSession : RemoteDbSession
{
    #region 构造函数
    public HighGoSession(IDatabase db) : base(db) { }
    #endregion

    #region 快速查询单表记录数量
    public override Int64 QueryCountFast(String tableName)
    {
        var db = Database as HighGo;
        var sql = $"SELECT \"n_live_tup\" FROM \"pg_stat_user_tables\" WHERE \"schemaname\"='{db.TableSchema}' AND \"relname\" = '{tableName.Replace("\"", string.Empty)}'";
        return ExecuteScalar<Int64>(sql);
    }
    public override Task<Int64> QueryCountFastAsync(String tableName)
    {
        var db = Database as HighGo;
        var sql = $"SELECT \"n_live_tup\" FROM \"pg_stat_user_tables\" WHERE \"schemaname\"='{db.TableSchema}' AND \"relname\" = '{tableName.Replace("\"", string.Empty)}'";
        return ExecuteScalarAsync<Int64>(sql);
    }
    #endregion

    #region 批量操作
    string GetBatchSql(String action, IDataTable table, IDataColumn[] columns, ICollection<String> updateColumns, ICollection<String> addColumns, IEnumerable<IModel> list)
    {
        var sb = Pool.StringBuilder.Get();
        var db = Database as DbBase;

        // 字段列表
        columns ??= table.Columns.ToArray();
        BuildInsert(sb, db, action, table, columns);
        DefaultSpan.Current?.AppendTag(sb.ToString());

        // 值列表
        sb.Append(" Values");
        BuildBatchValues(sb, db, action, table, columns, list);
        // 重复键执行update
        if (updateColumns != null || addColumns != null)
        {
            sb.Append(" On Conflict");

            // 先找唯一索引，再用主键
            //var table = columns.FirstOrDefault()?.Table;
            var di = table.Indexes?.FirstOrDefault(e => e.Unique);
            if (di != null && di.Columns != null && di.Columns.Length > 0)
            {
                var dcs = table.GetColumns(di.Columns);
                sb.AppendFormat("({0})", dcs.Join(",", e => db.FormatName(e)));
            }
            else
            {
                var pks = table.PrimaryKeys;
                if (pks != null && pks.Length > 0)
                    sb.AppendFormat("({0})", pks.Join(",", e => db.FormatName(e)));
            }

            sb.Append(" Do Update Set ");
            if (updateColumns != null)
            {
                foreach (var dc in columns)
                {
                    if (dc.Identity || dc.PrimaryKey) continue;

                    if (updateColumns.Contains(dc.Name) && (addColumns == null || !addColumns.Contains(dc.Name)))
                        sb.AppendFormat("{0}=excluded.{0},", db.FormatName(dc));
                }
                sb.Length--;
            }
            if (addColumns != null)
            {
                sb.Append(',');
                foreach (var dc in columns)
                {
                    if (dc.Identity || dc.PrimaryKey) continue;

                    if (addColumns.Contains(dc.Name))
                        sb.AppendFormat("{0}={0}+excluded.{0},", db.FormatName(dc));
                }
                sb.Length--;
            }
        }
        return sb.Put(true);
    }
    public override Int32 Insert(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list)
    {
        var sql = GetBatchSql("Insert Into", table, columns, null, null, list);
        return Execute(sql);
    }
    public override Int32 Upsert(IDataTable table, IDataColumn[] columns, ICollection<String> updateColumns, ICollection<String> addColumns, IEnumerable<IModel> list)
    {
        var sql = GetBatchSql("Insert Into", table, columns, updateColumns, addColumns, list);
        return Execute(sql);
    }
    #endregion

    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        sql = sql + $" RETURNING *";
        return base.InsertAndGetIdentity(sql, type, ps);
    }
    public override Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        sql = sql + $" RETURNING *";
        return base.InsertAndGetIdentityAsync(sql, type, ps);
    }
}
/// <summary>瀚高(HighGo)元数据</summary>
internal class HighGoMetaData : RemoteDbMetaData
{
    #region 属性
    HighGo _HighGo => Database as HighGo;
    string GetTablesSql
    {
        get
        {
            return $@"select cast(relname as varchar) as TableName,
                        cast(obj_description(c.oid,'pg_class') as varchar) as Description from pg_class c 
                         inner join 
						 pg_namespace n on n.oid = c.relnamespace and nspname='{_HighGo.TableSchema}'
                         inner join 
                         pg_tables z on z.tablename=c.relname
                        where  relkind in('p', 'r') and relname not like 'hg_%' and relname not like 'sql_%' and schemaname='{_HighGo.TableSchema}' order by relname";
        }
    }
    string GetColumnsSql
    {
        get
        {
            return $@"select cast (pclass.oid as int4) as TableId,cast(ptables.tablename as varchar) as TableName,
                                pcolumn.column_name as ColumnName,pcolumn.udt_name as DataType,
                                CASE WHEN pcolumn.numeric_scale >0 THEN pcolumn.numeric_precision ELSE pcolumn.character_maximum_length END as Length,
                                pcolumn.column_default as DefaultValue,
                                pcolumn.numeric_scale as DecimalDigits,
                                pcolumn.numeric_scale as Scale,
                                col_description(pclass.oid, pcolumn.ordinal_position) as ColumnDescription,
                                case when pkey.colname = pcolumn.column_name
                                then true else false end as IsPrimaryKey,
                                case when pcolumn.column_default like 'nextval%'
                                then true else false end as IsIdentity,
                                case when pcolumn.is_nullable = 'YES'
                                then true else false end as IsNullable
                                 from (select * from pg_tables where schemaname='{_HighGo.TableSchema}' {{0}}) ptables inner join pg_class pclass
                                on ptables.tablename = pclass.relname inner join (SELECT *
                                FROM information_schema.columns
                                ) pcolumn on pcolumn.table_name = ptables.tablename
                                left join (
	                                select  pg_class.relname,pg_attribute.attname as colname from 
	                                pg_constraint  inner join pg_class 
	                                on pg_constraint.conrelid = pg_class.oid 
	                                inner join pg_attribute on pg_attribute.attrelid = pg_class.oid 
	                                and  pg_attribute.attnum = pg_constraint.conkey[1]
	                                inner join pg_type on pg_type.oid = pg_attribute.atttypid
	                                where pg_constraint.contype='p'
                                ) pkey on pcolumn.table_name = pkey.relname
                                order by table_catalog, table_schema, ordinal_position";
        }
    }
    string GetIndexsSql
    {
        get
        {
            return $"SELECT * FROM pg_indexes WHERE schemaname = '{_HighGo.TableSchema}'";
        }
    }
    #endregion

    #region 构造函数
    public HighGoMetaData() => Types = _DataTypes;
    #endregion

    #region 数据类型
    /// <summary>数据类型映射</summary>
    static readonly Dictionary<Type, String[]> _DataTypes = new()
    {
        { typeof(Byte[]), new String[] { "bytea" } },
        { typeof(Byte), new String[] { "bit", "int1" } },
        { typeof(Int16), new String[] { "smallint", "smallserial", "int2" } },
        { typeof(Int32), new String[] { "integer", "serial","int4" } },
        { typeof(Int64), new String[] { "bigint", "bigserial", "int8" } },
        { typeof(Single), new String[] { "real", "float","float4" } },
        { typeof(Double), new String[] { "double precision", "float8" } },
        { typeof(Decimal), new String[] { "numeric({0}, {1})", "decimal({0}, {1})", "money" } },
        { typeof(DateTime), new String[] { "timestamp", "date", "timestamp without time zone", "time without time zone", "timestamp with time zone", "time with time zone" } },
        { typeof(Boolean), new String[] { "bool", "boolean" } },
        { typeof(Guid), new String[] { "uuid" } },
        { typeof(String), new String[] { "varchar({0})", "text", "character varying({0})", "character({0})", "char{0})" } },
    };
    #endregion
    protected override String? GetDefault(IDataColumn field, Boolean onlyDefine)
    {
        if (field.DataType == typeof(Boolean)) { return $" DEFAULT {(field.DefaultValue.ToBoolean() ? true : false)}"; }
        return base.GetDefault(field, onlyDefine);
    }

    #region 架构
    protected override String? GetFieldType(IDataColumn field)
    {
        if (field.Identity)
        {
            if (field.DataType == typeof(Int16)) { return "smallserial"; }
            if (field.DataType == typeof(Int32)) { return "serial"; }
            if (field.DataType == typeof(Int64)) { return "bigserial"; }
        }
        return base.GetFieldType(field);
    }
    void CurrentSchema()
    {
        if (_HighGo.TableSchema.IsNullOrWhiteSpace())
        {
            var sql = "SELECT current_schema()";
            var ss = Database.CreateSession();
            _HighGo.TableSchema = ss.ExecuteScalar<String>(sql);
        }
    }
    protected override List<IDataTable> OnGetTables(String[]? names)
    {
        CurrentSchema();
        var ss = Database.CreateSession();
        var db = Database.DatabaseName;
        var list = new List<IDataTable>();
        var old = ss.ShowSQL;
        ss.ShowSQL = false;
        try
        {
            //表
            var sql = GetTablesSql;
            var dt = ss.Query(GetTablesSql, null);
            if (dt.Rows == null || dt.Rows.Count == 0) return list;
            //字段
            sql = string.Format(GetColumnsSql, names != null && names.Length > 0 ? " and tablename in ('" + names.Join("','") + "')" : string.Empty);
            var columns = ss.Query(sql, null);
            var hs = new HashSet<String>(names ?? [], StringComparer.OrdinalIgnoreCase);
            //索引
            sql = GetIndexsSql;
            if (names != null && names.Length > 0) sql += " AND tablename in ('" + names.Join("','") + "')";
            var indexes = ss.Query(sql, null);

            foreach (var dr in dt)
            {
                var name = $"{dr["TableName"]}";
                if (name.IsNullOrEmpty() || hs.Count > 0 && !hs.Contains(name)) continue;
                var table = DAL.CreateTable();
                table.TableName = name;
                table.Description = $"{dr["Description"]}";
                table.DbType = Database.Type;

                #region 字段
                if (columns.Rows != null && columns.Rows.Count > 0)
                {
                    var cols = columns.Where(o => $"{o["TableName"]}" == table.TableName);
                    if (cols is null || 0 >= cols.Count()) { continue; }
                    foreach (var dc in cols)
                    {
                        var field = table.CreateColumn();

                        field.ColumnName = $"{dc["ColumnName"]}";
                        field.RawType = $"{dc["DataType"]}";
                        field.Description = $"{dc["ColumnDescription"]}";

                        field.Identity = dc["IsIdentity"].ToBoolean();
                        field.PrimaryKey = dc["IsPrimaryKey"].ToBoolean();
                        field.Nullable = dc["IsNullable"].ToBoolean();

                        field.Length = dc["Length"].ToInt();

                        var type = GetDataType(field);
                        field.DataType = type;
                        field.Fix();
                        table.Columns.Add(field);
                    }
                }
                #endregion 字段

                #region 索引
                if (indexes.Rows != null && indexes.Rows.Count > 0)
                {
                    var ins = indexes.Where(o => $"{o["tablename"]}" == table.TableName);
                    if (ins is null || 0 >= ins.Count()) { continue; }
                    foreach (var dr2 in ins)
                    {
                        var dname = $"{dr2["indexname"]}";
                        var di = table.Indexes.FirstOrDefault(e => e.Name == dname) ?? table.CreateIndex();
                        var indexdef = $"{dr2["indexdef"]}";
                        di.Unique = indexdef.Contains("UNIQUE");
                        var startIndex = indexdef.IndexOf("(") + 1;
                        var endIndex = indexdef.LastIndexOf(")");
                        var cname = indexdef?.Substring(startIndex, endIndex - startIndex).Replace("\"", string.Empty).Split(",").Select(o => o.Trim()).ToArray();
                        if (cname is null || 0 >= cname.Length) continue;

                        var cs = new List<String>();
                        if (di.Columns != null && di.Columns.Length > 0) cs.AddRange(di.Columns);
                        cs.AddRange(cname);
                        di.Columns = cs.ToArray();

                        if (di.Name.IsNullOrWhiteSpace())
                        {
                            di.Name = dname;
                            table.Indexes.Add(di);
                        }
                    }
                }
                #endregion 索引

                // 修正关系数据
                table.Fix();
                list.Add(table);
            }
        }
        finally { ss.ShowSQL = old; }
        return list;
    }
    /// <summary>快速取得所有表名</summary>
    public override IList<String> GetTableNames()
    {
        var list = new List<String>();
        var db = Database.DatabaseName;
        var dt = base.Database.CreateSession().Query(GetTablesSql, null);
        if (dt.Rows == null || dt.Rows.Count == 0) return list;

        // 所有表
        foreach (var dr in dt)
        {
            var name = $"{dr["TableName"]}";
            if (!name.IsNullOrEmpty()) list.Add(name);
        }
        return list;
    }
    #endregion

    #region 反向工程
    protected override Boolean DatabaseExist(String databaseName)
    {
        var dt = GetSchema(_.Databases, [databaseName]);
        return dt != null && dt.Rows != null && dt.Rows.Count > 0;
    }
    public override String AddTableDescriptionSQL(IDataTable table)
    {
        if (String.IsNullOrEmpty(table.Description)) { return null; }
        return $"COMMENT ON TABLE {FormatName(table)} IS '{table.Description}'";
    }
    public override String DropTableDescriptionSQL(IDataTable table) => $"COMMENT ON TABLE {FormatName(table)} IS NULL";
    public override String AddColumnDescriptionSQL(IDataColumn field)
    {
        if (String.IsNullOrEmpty(field.Description)) { return null; };
        return $"COMMENT ON COLUMN {FormatName(field.Table)}.{FormatName(field)} IS '{field.Description}'";
    }
    public override String DropColumnDescriptionSQL(IDataColumn field) => $"COMMENT ON COLUMN {FormatName(field.Table)}.{FormatName(field)} IS NULL";
    public override String CreateIndexSQL(IDataIndex index)
    {
        var sb = Pool.StringBuilder.Get();
        if (index.Unique) { sb.Append("Create Unique Index "); }
        else { sb.Append("Create Index "); }
        sb.Append("If Not Exists ");
        sb.Append(index.Name);
        var dcs = index.Table.GetColumns(index.Columns);
        sb.AppendFormat(" On {0} USING btree ({1})", FormatName(index.Table), dcs.Join(",", FormatName));

        return sb.Put(true);
    }
    #endregion
}