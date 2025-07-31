using System.Data;
using System.Data.Common;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;
using NewLife.Web;

namespace XCode.DataAccessLayer;

/// <summary>人大金仓</summary>
internal class KingBase : RemoteDb
{
    #region 属性
    /// <summary>返回数据库类型。外部DAL数据库类请使用Other</summary>
    public override DatabaseType Type => DatabaseType.KingBase;

    /// <summary>数据库版本</summary>
    public Version Version { get; set; }

    /// <summary>模式</summary>
    public String? TableSchema { get; set; }

    /// <summary>目前只支持兼容 Oracle、PostgreSQL、MySql</summary>
    public DatabaseType DataBaseMode { get; set; } = DatabaseType.None;

    /// <summary>系统数据库名</summary>
    public override String SystemDatabaseName => "security";
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

    public override String? BuildDeleteSql(String tableName, String where, Int32 batchSize)
    {
        if (batchSize <= 0) return base.BuildDeleteSql(tableName, where, 0);

        var xWhere = String.Empty;
        var xTable = FormatName(tableName);
        if (!String.IsNullOrWhiteSpace(where)) xWhere = " Where " + where;
        var sql = $"WITH to_delete AS (SELECT \"ctid\" FROM {xTable} {xWhere} LIMIT {batchSize}) ";
        sql += $"DELETE FROM {xTable} where \"ctid\" in (SELECT \"ctid\" from to_delete)";

        return sql;
    }

    public override String FormatLike(IDataColumn column, String format)
    {
        format = format.Replace("'%{", "'%' || {").Replace("}%'", "} || '%'").Replace("'{", "{").Replace("}'", "}");
        return base.FormatLike(column, format);
    }
    #endregion

    protected override void OnSetConnectionString(ConnectionStringBuilder builder) => base.OnSetConnectionString(builder);

    protected override DbProviderFactory? CreateFactory()
    {
        if (!Provider.IsNullOrEmpty() && Provider.Contains("KingBase"))
        {
            var type = PluginHelper.LoadPlugin("Kdbndp.KdbndpFactory", null, "Kdbndp.dll", null);
            var factory = GetProviderFactory(type);
            if (factory != null) return factory;
        }
        // 找不到驱动时，再到线上下载
        {
            var factory = GetProviderFactory(null, "Kdbndp.dll", "Kdbndp.KdbndpFactory");

            return factory;
        }
    }

    /// <summary>创建元数据对象</summary>
    protected override IMetaData OnCreateMetaData() => new KingBaseMetaData();

    /// <summary>创建数据库会话</summary>
    protected override IDbSession OnCreateSession() => new KingBaseSession(this);

    #region 分页
    public override SelectBuilder PageSplit(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows) => PostgreSQL.PageSplitByOffsetLimit(builder, startRowIndex, maximumRows);
    public override String PageSplit(String sql, Int64 startRowIndex, Int64 maximumRows, String keyColumn) => PostgreSQL.PageSplitByOffsetLimit(sql, startRowIndex, maximumRows);
    #endregion
}

/// <summary>人大金仓(KingBase)数据库</summary>
internal class KingBaseSession : RemoteDbSession
{
    #region 构造函数
    public KingBaseSession(IDatabase db) : base(db) { }
    #endregion

    #region 快速查询单表记录数量
    public override Int64 QueryCountFast(String tableName)
    {
        var db = Database as KingBase;
        var sql = $"SELECT \"n_live_tup\" FROM \"sys_stat_user_tables\" WHERE \"schemaname\"='{db.TableSchema}' AND \"relname\" = '{tableName.Replace("\"", String.Empty)}'";
        return ExecuteScalar<Int64>(sql);
    }
    public override Task<Int64> QueryCountFastAsync(String tableName)
    {
        var db = Database as KingBase;
        var sql = $"SELECT \"n_live_tup\" FROM \"sys_stat_user_tables\" WHERE \"schemaname\"='{db.TableSchema}' AND \"relname\" = '{tableName.Replace("\"", String.Empty)}'";
        return ExecuteScalarAsync<Int64>(sql);
    }
    #endregion

    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        sql += $" RETURNING *";
        return base.InsertAndGetIdentity(sql, type, ps);
    }

    public override Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        sql += $" RETURNING *";
        return base.InsertAndGetIdentityAsync(sql, type, ps);
    }

    #region 批量操作
    String GetBatchSql(String action, IDataTable table, IDataColumn[] columns, ICollection<String>? updateColumns, ICollection<String>? addColumns, IEnumerable<IModel> list)
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
        BuildDuplicateKey(sb, db, columns, updateColumns, addColumns);

        return sb.Return(true);
    }

    public override Int32 Insert(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list)
    {
        var sql = GetBatchSql("Insert Into", table, columns, null, null, list);
        return Execute(sql);
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
        var sql = GetBatchSql("Insert Into", table, columns, updateColumns, addColumns, list);
        return Execute(sql);
    }
    #endregion
}

/// <summary>人大金仓(KingBase)元数据</summary>
internal class KingBaseMetaData : RemoteDbMetaData
{
    #region 属性
    KingBase _KingBase => Database as KingBase;
    String GetTablesSql
    {
        get
        {
            return _KingBase.DataBaseMode switch
            {
                DatabaseType.PostgreSQL => @"select relname as TableName, obj_description(relfilenode,'pg_class') as Description from pg_class c 
                        where  relkind = 'r' and c.oid > 16384 and c.relnamespace != 99 and c.relname not like '%pl_profiler_saved%'",
                _ => @"select relname as TableName, obj_description(relfilenode,'pg_class') as Description from sys_class c 
                        where  relkind = 'r' and c.oid > 16384 and c.relnamespace != 99 and c.relname not like '%pl_profiler_saved%'"
            };
        }
    }
    String GetColumnsSql
    {
        get
        {
            var sql = $@"select cast (pclass.oid as int4) as TableId,cast(ptables.tablename as varchar) as TableName,
                                pcolumn.column_name as ColumnName,pcolumn.udt_name as DataType,
                                pcolumn.character_maximum_length as Length,
                                pcolumn.column_default as DefaultValue,
                                col_description(pclass.oid, pcolumn.ordinal_position) as ColumnDescription,
                                case when pkey.colname = pcolumn.column_name
                                then true else false end as IsPrimaryKey,
                                case when UPPER(pcolumn.column_default) like 'NEXTVAL%'
                                then true else false end as IsIdentity,
                                case when UPPER(pcolumn.is_nullable) = 'YES'
                                then true else false end as IsNullable
                                from (select * from sys_tables where schemaname='{_KingBase.TableSchema}' {{0}}) ptables inner join sys_class pclass
                                on ptables.tablename = pclass.relname inner join (SELECT *
                                FROM information_schema.columns
                                ) pcolumn on pcolumn.table_name = ptables.tablename
                                left join (
	                                select  sys_class.relname,sys_attribute.attname as colname from 
	                                sys_constraint  inner join sys_class 
	                                on sys_constraint.conrelid = sys_class.oid 
	                                inner join sys_attribute on sys_attribute.attrelid = sys_class.oid 
	                                and  sys_attribute.attnum = sys_constraint.conkey[1]
	                                inner join sys_type on sys_type.oid = sys_attribute.atttypid
	                                where sys_constraint.contype='p'
                                ) pkey on pcolumn.table_name = pkey.relname
                                order by ptables.tablename";
            return _KingBase.DataBaseMode switch
            {
                DatabaseType.PostgreSQL => sql.Replace("sys_", "pg_"),
                DatabaseType.SqlServer => sql.Replace("sys_constraint.conkey[1]", "sys_constraint.conkey{{1}}"),
                _ => sql
            };
        }
    }
    String GetIndexsSql
    {
        get
        {
            var sql = $"SELECT * FROM sys_indexes WHERE schemaname = '{_KingBase.TableSchema}'";
            return _KingBase.DataBaseMode switch
            {
                DatabaseType.PostgreSQL => sql.Replace("sys_", "pg_"),
                _ => sql
            };
        }
    }
    //        string GetSequencesSql
    //        {
    //            get
    //            {
    //                var sql = $@"SELECT 
    //    tab.relname AS TableName,
    //    seq.relname AS SequenceName, 
    //    seq_info.last_value as LastValue
    //FROM 
    //    pg_class AS seq 
    //JOIN 
    //    pg_depend AS dep ON seq.oid = dep.objid
    //JOIN 
    //    pg_class AS tab ON dep.refobjid = tab.oid
    //JOIN 
    //    pg_sequences AS seq_info ON seq.relname = seq_info.sequencename AND seq_info.schemaname = '{_KingBase.TableSchema}'
    //WHERE 
    //    seq.relkind = 'S';";
    //                return _KingBase.DataBaseMode switch
    //                {
    //                    DatabaseType.PostgreSQL => sql.Replace("sys_", "pg_"),
    //                    _ => sql
    //                };
    //            }
    //        }
    #endregion

    #region 构造函数
    public KingBaseMetaData() => Types = _DataTypes;
    #endregion

    #region 数据类型
    protected override List<KeyValuePair<Type, Type>> FieldTypeMaps
    {
        get
        {
            if (_FieldTypeMaps == null)
            {
                var list = base.FieldTypeMaps;
                if (!list.Any(e => e.Key == typeof(Byte) && e.Value == typeof(Boolean)))
                    list.Add(new(typeof(Byte), typeof(Boolean)));
            }
            return base.FieldTypeMaps;
        }
    }
    /// <summary>数据类型映射</summary>
    static readonly Dictionary<Type, String[]> _DataTypes = new()
    {
        { typeof(Byte[]), new String[] { "bytea", "blob", "clob", "nclob", "bit({0})", "bit varying({0})" } },
        { typeof(Byte), new String[] { "tinyint", "int1" } },
        { typeof(Int16), new String[] { "smallint", "int2", "smallserial" } },
        { typeof(Int32), new String[] { "integer", "int4", "tinyint", "year", "mediumint", "middleint", "int3" } },
        { typeof(Int64), new String[] { "bigint", "bigserial", "int8" } },
        { typeof(Single), new String[] { "real", "float", "float4" } },
        { typeof(Double), new String[] { "double precision", "float8" } },
        { typeof(Decimal), new String[] { "numeric({0}, {1})", "decimal({0}, {1})", "number({0}, {1})", "fixed({0}, {1})" } },
        { typeof(DateTime), new String[] { "datetime", "date", "time", "timestamp", "timestamp with time zone", "timestamptz", "timestamp without time zone", "time with time zone" , "timetz", "time without time zone" } },
        { typeof(Boolean), new String[] { "boolean", "bool" } },
        { typeof(Guid), new String[] { "uuid" } },
        { typeof(String), new String[] { "varchar({0})","text", "nvarchar({0})", "character({0})", "character varying({0})", "char({0})", "name", "longtext", "mediumtext", "tinytext", "xml", "json", "rowid" } },
    };
    #endregion

    #region 架构
    void DataBaseModel()
    {
        if (_KingBase.DataBaseMode == DatabaseType.None)
        {
            var sql = "show database_mode;";
            var ss = Database.CreateSession();
            var mode = ss.ExecuteScalar<String>(sql);
            _KingBase.DataBaseMode = mode switch
            {
                "mysql" => DatabaseType.MySql,
                "oracle" => DatabaseType.Oracle,
                "pg" => DatabaseType.PostgreSQL,
                _ => DatabaseType.None
            };
        }
    }
    void CurrentSchema()
    {
        if (_KingBase.TableSchema.IsNullOrWhiteSpace())
        {
            var sql = "SELECT current_schema()";
            var ss = Database.CreateSession();
            _KingBase.TableSchema = ss.ExecuteScalar<String>(sql);
        }
    }
    protected override List<IDataTable> OnGetTables(String[]? names)
    {
        DataBaseModel();
        CurrentSchema();
        var ss = Database.CreateSession();
        var db = Database.DatabaseName;
        var list = new List<IDataTable>();

        var old = ss.ShowSQL;
        ss.ShowSQL = false;
        try
        {
            var sql = GetTablesSql;
            var dt = ss.Query(GetTablesSql, null);
            if (dt.Rows == null || dt.Rows.Count == 0) return list;
            //字段
            sql = String.Format(GetColumnsSql, names != null && names.Length > 0 ? " and tablename in ('" + names.Join("','") + "')" : String.Empty);
            var columns = ss.Query(sql, null);
            //索引
            sql = GetIndexsSql;
            if (names != null && names.Length > 0) sql += " AND tablename in ('" + names.Join("','") + "')";
            var indexes = ss.Query(sql, null);
            //序列
            //sql = GetSequencesSql;
            //if (names != null && names.Length > 0) sql += " AND tablename in ('" + names.Join("','") + "')";
            //var sequences = ss.Query(sql, null);

            var hs = new HashSet<String>(names ?? [], StringComparer.OrdinalIgnoreCase);

            // 所有表
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
                    if (cols is null || !cols.Any()) { continue; }
                    foreach (var dc in cols)
                    {
                        var field = table.CreateColumn();

                        field.ColumnName = $"{dc["ColumnName"]}";
                        field.RawType = $"{dc["DataType"]}";
                        field.Description = $"{dc["ColumnDescription"]}";
                        //field.DefaultValue = dc["Default"] as String;

                        field.Identity = dc["IsIdentity"].ToBoolean();
                        field.PrimaryKey = dc["IsPrimaryKey"].ToBoolean();
                        field.Nullable = dc["IsNullable"].ToBoolean();

                        //field.Precision = dc["Precision"].ToInt();
                        //field.Scale = dc["Scale"].ToInt();
                        field.Length = dc["Length"].ToInt();

                        field.DataType = GetDataType(field)!;

                        field.Fix();

                        table.Columns.Add(field);
                    }
                }
                #endregion 字段

                #region 索引
                if (indexes.Rows != null && indexes.Rows.Count > 0)
                {
                    var ins = indexes.Where(o => $"{o["tablename"]}" == table.TableName);
                    if (ins is null || !ins.Any()) { continue; }
                    foreach (var dr2 in ins)
                    {
                        var dname = $"{dr2["indexname"]}";
                        var di = table.Indexes.FirstOrDefault(e => e.Name == dname) ?? table.CreateIndex();
                        var indexdef = $"{dr2["indexdef"]}";
                        di.Unique = indexdef.Contains("UNIQUE");
                        var startIndex = indexdef.IndexOf("(") + 1;
                        var endIndex = indexdef.LastIndexOf(")");
                        var cname = indexdef?.Substring(startIndex, endIndex - startIndex).Replace("\"", String.Empty).Split(",").Select(o => o.Trim()).ToArray();
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

                #region 序列
                //if (sequences != null && sequences.Rows.Count > 0) { 
                //    var seqs = sequences.Where(o => $"{o["TableName"]}" == table.TableName);
                //    if (seqs is null || 0 >= seqs.Count()) { continue; }

                //    foreach (var seq in seqs)
                //    {
                //        table.Properties["SequenceName"] = $"{seq["SequenceName"]}";
                //        table.Properties["LastValue"] = $"{seq["LastValue"]}";
                //    }
                //}
                #endregion
                // 修正关系数据
                table.Fix();

                list.Add(table);
            }
        }
        finally
        {
            ss.ShowSQL = old;
        }
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
            var name = $"{dr["Name"]}";
            if (!name.IsNullOrEmpty()) list.Add(name);
        }
        return list;
    }
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
    public override String FieldClause(IDataColumn field, Boolean onlyDefine)
    {
        var sql = base.FieldClause(field, onlyDefine);
        return sql;
    }
    protected override String? GetFieldConstraints(IDataColumn field, Boolean onlyDefine)
    {
        String? str = null;
        if (!field.Nullable) { str = " NOT NULL"; }
        // 默认值
        if (!field.Nullable && !field.Identity) { str += GetDefault(field, onlyDefine); }
        return str;
    }
    #endregion 架构

    #region 反向工程
    protected override Boolean DatabaseExist(String databaseName)
    {
        var dt = GetSchema(_.Databases, [databaseName]);
        return dt != null && dt.Rows != null && dt.Rows.Count > 0;
    }
    public override String DropDatabaseSQL(String dbname) => $"Drop Database If Exists {Database.FormatName(dbname)}";
    public override String CreateTableSQL(IDataTable table)
    {
        var fs = new List<IDataColumn>(table.Columns);
        var sb = Pool.StringBuilder.Get();
        sb.AppendFormat("Create Table If Not Exists {0}(", FormatName(table));
        for (var i = 0; i < fs.Count; i++)
        {
            sb.AppendLine();
            sb.Append('\t');
            sb.Append(FieldClause(fs[i], true));
            if (i < fs.Count - 1) sb.Append(',');
        }
        if (table.PrimaryKeys.Length > 0) sb.AppendFormat(",\r\n\tPrimary Key ({0})", table.PrimaryKeys.Join(",", FormatName));
        sb.AppendLine();
        sb.Append(");");
        //if (!string.IsNullOrWhiteSpace(table.Description)) { sb.Append($" COMMENT '{table.Description}'"); } 非Mysql兼容模式下，不支持，所以直接注释掉
        return sb.Return(true);
    }
    public override String DropTableSQL(IDataTable table) => $"Drop Table If Exists {FormatName(table)}";
    public override String? AddTableDescriptionSQL(IDataTable table)
    {
        if (String.IsNullOrEmpty(table.Description)) { return null; }
        return $"COMMENT ON TABLE {FormatName(table)} IS '{table.Description}'";
    }
    public override String? DropTableDescriptionSQL(IDataTable table) => $"COMMENT ON TABLE {FormatName(table)} IS NULL";
    public override String? AddColumnSQL(IDataColumn field) => $"ALTER TABLE {FormatName(field.Table)} ADD COLUMN {FormatName(field)} {GetFieldType(field)}";
    public override String? AlterColumnSQL(IDataColumn field, IDataColumn? oldfield) => $"ALTER TABLE {FormatName(field.Table)} ALTER COLUMN {FormatName(field)} TYPE {GetFieldType(field)}";
    public override String? AddColumnDescriptionSQL(IDataColumn field)
    {
        if (String.IsNullOrEmpty(field.Description)) return null;

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

        return sb.Return(true);
    }
    public override String DropIndexSQL(IDataIndex index) => $"DROP INDEX IF EXISTS \"{index.Name}\"";
    //public virtual String AlertSequenceSQL(IDataTable table)
    //{
    //    var col = table.Columns.FirstOrDefault(f => f.Identity);
    //    if (col is null) { return string.Empty; }
    //    var seq = table.Properties["SequenceName"];
    //    var ss = Database.CreateSession();
    //    var maxId = ss.ExecuteScalar<int>($"select Max(\"{col.ColumnName}\") from \"{table.TableName}\"");
    //    return $"ALTER SEQUENCE \"{table.Properties["SequenceName"]}\" RESTART WITH {(maxId == 0 ? maxId : maxId + 1)}";
    //}
    #endregion 反向工程
}
