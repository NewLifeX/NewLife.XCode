using System.Collections;
using System.Data;
using System.Data.Common;
using System.Net;
using System.Text;

using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;
using NewLife.Reflection;

namespace XCode.DataAccessLayer;

internal class VastBase : RemoteDb
{
    #region 属性

    /// <summary>返回数据库类型。</summary>
    public override DatabaseType Type => DatabaseType.VastBase;

    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory() => GetProviderFactory(null, "Npgsql.dll", "Npgsql.NpgsqlFactory");

    private const String Server_Key = "Server";

    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        var key = builder[Server_Key];
        if (key.EqualIgnoreCase(".", "localhost"))
        {
            //builder[Server_Key] = "127.0.0.1";
            builder[Server_Key] = IPAddress.Loopback.ToString();
        }

        //if (builder.TryGetValue("Database", out var db) && db != db.ToLower()) builder["Database"] = db.ToLower();
    }

    #endregion 属性

    #region 方法

    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new VastBaseSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new VastBaseMetaData();

    public override Boolean Support(String providerName)
    {
        providerName = providerName.ToLower();
        if (providerName.Contains("vastbase.data.vastbaseclient")) return true;
        if (providerName.Contains("vastbase")) return true;

        return false;
    }

    #endregion 方法

    #region 数据库特性

    private static Boolean IsArrayField(Type? type) => type?.IsArray == true && type != typeof(Byte[]);
    protected override String ReservedWordsStr
    {
        get
        {
            return "ACCESSIBLE,ADD,ALL,ALTER,ANALYZE,AND,AS,ASC,ASENSITIVE,BEFORE,BETWEEN,BIGINT,BINARY,BLOB,BOTH,BY,CALL,CASCADE,CASE,CHANGE,CHAR,CHARACTER,CHECK,COLLATE,COLUMN,CONDITION,CONNECTION,CONSTRAINT,CONTINUE,CONTRIBUTORS,CONVERT,CREATE,CROSS,CURRENT_DATE,CURRENT_TIME,CURRENT_TIMESTAMP,CURRENT_USER,CURSOR,DATABASE,DATABASES,DAY_HOUR,DAY_MICROSECOND,DAY_MINUTE,DAY_SECOND,DEC,DECIMAL,DECLARE,DEFAULT,DELAYED,DELETE,DESC,DESCRIBE,DETERMINISTIC,DISTINCT,DISTINCTROW,DIV,DOUBLE,DROP,DUAL,EACH,ELSE,ELSEIF,ENCLOSED,ESCAPED,EXISTS,EXIT,EXPLAIN,FALSE,FETCH,FLOAT,FLOAT4,FLOAT8,FOR,FORCE,FOREIGN,FROM,FULLTEXT,GRANT,GROUP,HAVING,HIGH_PRIORITY,HOUR_MICROSECOND,HOUR_MINUTE,HOUR_SECOND,IF,IGNORE,IN,INDEX,INFILE,INNER,INOUT,INSENSITIVE,INSERT,INT,INT1,INT2,INT3,INT4,INT8,INTEGER,INTERVAL,INTO,IS,ITERATE,JOIN,KEY,KEYS,KILL,LEADING,LEAVE,LEFT,LIKE,LIMIT,LINEAR,LINES,LOAD,LOCALTIME,LOCALTIMESTAMP,LOCK,LONG,LONGBLOB,LONGTEXT,LOOP,LOW_PRIORITY,MATCH,MEDIUMBLOB,MEDIUMINT,MEDIUMTEXT,MIDDLEINT,MINUTE_MICROSECOND,MINUTE_SECOND,MOD,MODIFIES,NATURAL,NOT,NO_WRITE_TO_BINLOG,NULL,NUMERIC,ON,OPTIMIZE,OPTION,OPTIONALLY,OR,ORDER,OUT,OUTER,OUTFILE,PRECISION,PRIMARY,PROCEDURE,PURGE,RANGE,READ,READS,READ_ONLY,READ_WRITE,REAL,REFERENCES,REGEXP,RELEASE,RENAME,REPEAT,REPLACE,REQUIRE,RESTRICT,RETURN,REVOKE,RIGHT,RLIKE,SCHEMA,SCHEMAS,SECOND_MICROSECOND,SELECT,SENSITIVE,SEPARATOR,SET,SHOW,SMALLINT,SPATIAL,SPECIFIC,SQL,SQLEXCEPTION,SQLSTATE,SQLWARNING,SQL_BIG_RESULT,SQL_CALC_FOUND_ROWS,SQL_SMALL_RESULT,SSL,STARTING,STRAIGHT_JOIN,TABLE,TERMINATED,THEN,TINYBLOB,TINYINT,TINYTEXT,TO,TRAILING,TRIGGER,TRUE,UNDO,UNION,UNIQUE,UNLOCK,UNSIGNED,UPDATE,UPGRADE,USAGE,USE,USING,UTC_DATE,UTC_TIME,UTC_TIMESTAMP,VALUES,VARBINARY,VARCHAR,VARCHARACTER,VARYING,WHEN,WHERE,WHILE,WITH,WRITE,X509,XOR,YEAR_MONTH,ZEROFILL,Offset" +
                "LOG,User,Role,Admin,Rank,Member";
        }
    }

    /// <summary>格式化关键字</summary>
    /// <param name="keyWord">关键字</param>
    /// <returns></returns>
    public override String FormatKeyWord(String keyWord)
    {
        //if (String.IsNullOrEmpty(keyWord)) throw new ArgumentNullException("keyWord");
        if (String.IsNullOrEmpty(keyWord)) return keyWord;

        if (keyWord.StartsWith("\"") && keyWord.EndsWith("\"")) return keyWord;

        return $"\"{keyWord}\"";
    }

    /// <summary>格式化数据为SQL数据</summary>
    /// <param name="column">字段</param>
    /// <param name="value">数值</param>
    /// <returns></returns>
    public override String FormatValue(IDataColumn? column, Object? value)
    {
        var isNullable = true;
        var isArrayField = false;
        Type? type = null;
        if (column != null)
        {
            type = column.DataType;
            isNullable = column.Nullable;
        }
        else if (value != null)
        {
            type = value.GetType();
        }
        var fieldType = String.Empty;
        // 如果类型是Nullable的，则获取对应的类型
        type = Nullable.GetUnderlyingType(type) ?? type;
        //如果是数组，就取数组的元素类型
        if (IsArrayField(type))
        {
            isArrayField = true;
            type = type!.GetElementType();
        }
        if (isArrayField)
        {
            if (value is null) return isNullable ? "NULL" : "'{}'";
            var count = 0;
            var builder = Pool.StringBuilder.Get();
            builder.Append("ARRAY[");
            foreach (var v in (IEnumerable)value)
            {
                builder.Append(ValueToSQL(type, isNullable, v, ref fieldType));
                builder.Append(',');
                count++;
            }
            if (count != 0)
            {
                builder.Length--;
                builder.Append("]");
                //在进行数组运算时，因字符串可能会被映射为多种类型造成方法签名不匹配，所以需要指定类型
                //这里仅处理了字符串，如果其他类型也出现了类似情况，仅需在 ValueToSQL 中添加对应的类型即可
                if (!String.IsNullOrWhiteSpace(fieldType))
                {
                    builder.Append("::").Append(fieldType).Append("[]");
                }
            }
            else
            {
                builder.Clear();
                builder.Append("'{}'");
            }
            return builder.Return();
        }
        else
        {
            return ValueToSQL(type, isNullable, value, ref fieldType);
        }
    }


    private String ValueToSQL(Type? type, Boolean isNullable, Object? value, ref String fieldType)
    {
        if (type == typeof(String))
        {
            if (String.IsNullOrWhiteSpace(fieldType)) fieldType = "varchar";
            if (value is null) return isNullable ? "null" : "''";
            return "'" + value.ToString().Replace("'", "''") + "'";
        }
        if (type == typeof(DateTime))
        {
            if (value == null) return isNullable ? "null" : "''";
            var dt = Convert.ToDateTime(value);

            if (isNullable && (dt <= DateTime.MinValue || dt >= DateTime.MaxValue)) return "null";

            return FormatDateTime(dt);
        }
        if (type == typeof(Boolean))
        {
            if (value == null) return isNullable ? "null" : "";
            return Convert.ToBoolean(value) ? "true" : "false";
        }
        if (type == typeof(Byte[]))
        {
            if (value is not Byte[] bts || bts.Length <= 0) return isNullable ? "null" : "0x0";

            return "0x" + BitConverter.ToString(bts).Replace("-", null);
        }
        if (type == typeof(Guid))
        {
            if (value == null) return isNullable ? "null" : "''";

            return $"'{value}'";
        }

        if (value == null) return isNullable ? "null" : "";
        // 枚举
        if (type != null && type.IsEnum) type = typeof(Int32);

        // 转为目标类型，比如枚举转为数字
        if (type != null) value = value.ChangeType(type);
        if (value == null) return isNullable ? "null" : "";

        return value.ToString();
    }

    /// <summary>长文本长度</summary>
    public override Int32 LongTextLength => 4000;

    protected internal override String ParamPrefix => "@";

    /// <summary>系统数据库名</summary>
    public override String SystemDatabaseName => "vastbase";

    /// <inheritdoc/>
    public override NameFormats DefaultNameFormat => NameFormats.Underline;

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => (!String.IsNullOrEmpty(left) ? left : "''") + "||" + (!String.IsNullOrEmpty(right) ? right : "''");

    /// <inheritdoc/>
    public override String FormatName(String name)
    {
        name = base.FormatName(name);

        if (name.StartsWith("\"") || name.EndsWith("\"")) return name;

        ////如果包含大写字符，就加上引号
        //if (name.Any(Char.IsUpper)) return $"\"{name}\"";

        return name;
    }

    /// <inheritdoc/>
    public override String? BuildDeleteSql(String tableName, String where, Int32 batchSize)
    {
        if (batchSize <= 0) return base.BuildDeleteSql(tableName, where, 0);

        var xWhere = String.Empty;
        var xTable = FormatName(tableName);
        if (!String.IsNullOrWhiteSpace(where)) xWhere = " Where " + where;
        var sql = $"WITH to_delete AS (SELECT ctid FROM {xTable} {xWhere} LIMIT {batchSize}) ";
        sql += $"DELETE FROM {xTable} where ctid in (SELECT ctid from to_delete)";
        return sql;
    }
    #endregion 数据库特性

    #region 分页

    /// <summary>已重写。获取分页</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <param name="keyColumn">主键列。用于not in分页</param>
    /// <returns></returns>
    public override String PageSplit(String sql, Int64 startRowIndex, Int64 maximumRows, String keyColumn) => PageSplitByOffsetLimit(sql, startRowIndex, maximumRows);

    /// <summary>构造分页SQL</summary>
    /// <param name="builder">查询生成器</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns>分页SQL</returns>
    public override SelectBuilder PageSplit(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows) => PageSplitByOffsetLimit(builder, startRowIndex, maximumRows);

    /// <summary>已重写。获取分页</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns></returns>
    public static String PageSplitByOffsetLimit(String sql, Int64 startRowIndex, Int64 maximumRows)
    {
        // 从第一行开始，不需要分页
        if (startRowIndex <= 0)
        {
            if (maximumRows < 1) return sql;

            return $"{sql} limit {maximumRows}";
        }
        if (maximumRows < 1) throw new NotSupportedException("不支持取第几条数据之后的所有数据！");

        return $"{sql} offset {startRowIndex} limit {maximumRows}";
    }

    /// <summary>构造分页SQL</summary>
    /// <param name="builder">查询生成器</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns>分页SQL</returns>
    public static SelectBuilder PageSplitByOffsetLimit(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows)
    {
        // 从第一行开始，不需要分页
        if (startRowIndex <= 0)
        {
            if (maximumRows > 0) builder.Limit = $"limit {maximumRows}";
            return builder;
        }
        if (maximumRows < 1) throw new NotSupportedException("不支持取第几条数据之后的所有数据！");

        builder.Limit = $"offset {startRowIndex} limit {maximumRows}";
        return builder;
    }

    #endregion 分页
}

/// <summary>VastBase数据库</summary>
internal class VastBaseSession : RemoteDbSession
{
    #region 构造函数

    public VastBaseSession(IDatabase db) : base(db)
    {
    }

    #endregion 构造函数

    #region 基本方法 查询/执行

    /// <summary>执行SQL查询，返回记录集</summary>
    /// <param name="builder">查询生成器</param>
    /// <returns>总记录数</returns>
    public override DbTable Query(SelectBuilder builder)
    {
        if (Transaction != null)
        {
            builder = builder.Clone();
            builder.Limit += " For Update ";
        }
        var sql = builder.ToString();

        return Query(sql, builder.Parameters.ToArray());
    }

    /// <summary>执行插入语句并返回新增行的自动编号</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型，默认SQL文本</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[] ps)
    {
        sql += $" RETURNING *";
        return base.InsertAndGetIdentity(sql, type, ps);
    }

    public override Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[] ps)
    {
        sql += $" RETURNING *";
        return base.InsertAndGetIdentityAsync(sql, type, ps);
    }

    #endregion 基本方法 查询/执行

    #region 批量操作
    public override Int32 Insert(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list)
    {
        const String action = "Insert Into";

        var sb = Pool.StringBuilder.Get();
        var db = Database as DbBase;

        // 字段列表
        columns ??= table.Columns.ToArray();
        BuildInsert(sb, db, action, table, columns);
        DefaultSpan.Current?.AppendTag(sb.ToString());

        // 值列表
        sb.Append(" Values");
        BuildBatchValues(sb, db, action, table, columns, list);

        var sql = sb.Return(true);
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
        /*
         * INSERT INTO table_name (列1, 列2, 列3, ...)
         * VALUES (值1, 值2, 值3, ...),(值1, 值2, 值3, ...),(值1, 值2, 值3, ...)...
         * ON conflict ( 索引列，主键或者唯一索引 ) 
         * DO UPDATE
         * SET 列1 = EXCLUDED.列1, ...
         *     列2 = EXCLUDED.列1 + table_name.列2 ...
         *  ;
         */
        const String action = "Insert Into";

        var sb = Pool.StringBuilder.Get();
        var db = Database as DbBase;

        // 字段列表
        columns ??= table.Columns.ToArray();
        BuildInsert(sb, db, action, table, columns);
        DefaultSpan.Current?.AppendTag(sb.ToString());

        // 值列表
        sb.Append(" Values");
        BuildBatchValues(sb, db, action, table, columns, list);

        if (updateColumns is { Count: > 0 } || addColumns is { Count: > 0 })
        {
            //取唯一索引或主键
            var keys = table.PrimaryKeys.Select(f => f.ColumnName).ToArray();
            foreach (var idx in table.Indexes)
            {
                if (idx.Unique && idx.Columns is { Length: > 0 })
                {
                    if (idx.Columns.All(c => columns.Any(f => f.ColumnName == c)))
                    {
                        keys = idx.Columns;
                        break;
                    }
                }
            }

            if (keys is { Length: > 0 })
            {
                var conflict = String.Join(",", keys.Select(f => db.FormatName(f)));
                var tb = db.FormatName(table);
                var setters = new List<String>(columns.Length);

                if (updateColumns is { Count: > 0 })
                {
                    foreach (var dc in columns)
                    {
                        if (dc.Identity || dc.PrimaryKey) continue;

                        if (updateColumns.Contains(dc.Name) && (addColumns?.Contains(dc.Name) != true))
                        {
                            if (dc.Nullable)
                            {
                                setters.Add(String.Format("{0} = EXCLUDED.{0}", db.FormatName(dc)));
                            }
                            else
                            {
                                setters.Add(String.Format("{0} = COALESCE(EXCLUDED.{0},{1}.{0})", db.FormatName(dc), tb));
                            }
                        }
                    }
                }

                if (addColumns is { Count: > 0 })
                {
                    foreach (var dc in columns)
                    {
                        if (dc.Identity || dc.PrimaryKey) continue;

                        if (addColumns.Contains(dc.Name))
                        {
                            setters.Add(String.Format("{0} = EXCLUDED.{0} + {1}.{0}", db.FormatName(dc), tb));
                        }
                    }
                }

                if (setters.Count != 0)
                {
                    sb.Append($" ON conflict ({conflict}) DO UPDATE SET ");
                    sb.Append(String.Join(",", setters));
                }
            }
        }

        var sql = sb.Return(true);
        return Execute(sql);
    }

    #endregion 批量操作
}

/// <summary>VastBase元数据</summary>
internal class VastBaseMetaData : RemoteDbMetaData
{
    public VastBaseMetaData() => Types = _DataTypes;

    #region 数据类型

    //protected override List<KeyValuePair<Type, Type>> FieldTypeMaps
    //{
    //    get
    //    {
    //        if (_FieldTypeMaps == null)
    //        {
    //            var list = base.FieldTypeMaps;
    //            if (!list.Any(e => e.Key == typeof(Byte) && e.Value == typeof(Boolean)))
    //                list.Add(new(typeof(Byte), typeof(Boolean)));
    //        }
    //        return base.FieldTypeMaps;
    //    }
    //}

    /// <summary>数据类型映射</summary>
    private static readonly Dictionary<Type, String[]> _DataTypes = new()
    {
        { typeof(Byte[]), new String[] { "bytea" } },
        { typeof(Boolean), new String[] { "boolean" } },
        { typeof(Int16), new String[] { "smallint" } },
        { typeof(Int32), new String[] { "integer" } },
        { typeof(Int64), new String[] { "bigint" } },
        { typeof(Single), new String[] { "float" } },
        { typeof(Double), new String[] { "float8", "double precision" } },
        { typeof(Decimal), new String[] { "decimal" } },
        { typeof(DateTime), new String[] { "timestamp", "timestamp without time zone", "date" } },
        { typeof(String), new String[] { "varchar({0})", "character varying", "text" } },
    };

    protected override String? ArrayTypePostfix => "[]";

    protected override Type? GetDataType(IDataColumn field)
    {
        var postfix = ArrayTypePostfix!;
        if (field.RawType?.EndsWith(postfix) == true)
        {
            var clone = field.Clone(field.Table);
            clone.RawType = field.RawType.Substring(0, field.RawType.Length - postfix.Length);

            var type = base.GetDataType(clone);
            if (type != null) return type.MakeArrayType();
        }
        return base.GetDataType(field);
    }
    #endregion 数据类型

    protected override void FixTable(IDataTable table, DataRow dr, IDictionary<String, DataTable?>? data)
    {
        // 注释
        if (TryGetDataRowValue(dr, "TABLE_COMMENT", out String? comment)) table.Description = comment;

        base.FixTable(table, dr, data);
    }

    protected override void FixField(IDataColumn field, DataRow dr)
    {
        // 修正原始类型
        if (TryGetDataRowValue(dr, "COLUMN_TYPE", out String? rawType)) field.RawType = rawType;

        // 修正自增字段
        if (TryGetDataRowValue(dr, "EXTRA", out String? extra) && extra == "auto_increment") field.Identity = true;

        // 修正主键
        if (TryGetDataRowValue(dr, "COLUMN_KEY", out String? key)) field.PrimaryKey = key == "PRI";

        // 注释
        if (TryGetDataRowValue(dr, "COLUMN_COMMENT", out String? comment)) field.Description = comment;

        // 布尔类型
        if (field.RawType == "enum")
        {
            // VastBase中没有布尔型，这里处理YN枚举作为布尔型
            if (field.RawType is "enum('N','Y')" or "enum('Y','N')")
            {
                field.DataType = typeof(Boolean);
                //// 处理默认值
                //if (!String.IsNullOrEmpty(field.Default))
                //{
                //    if (field.Default == "Y")
                //        field.Default = "true";
                //    else if (field.Default == "N")
                //        field.Default = "false";
                //}
                return;
            }
        }

        base.FixField(field, dr);
    }

    public override String FieldClause(IDataColumn field, Boolean onlyDefine)
    {
        if (field.Identity)
        {
            if (field.DataType == typeof(Int64))
            {
                return $"{FormatName(field)} serial8 NOT NULL";
            }
            return $"{FormatName(field)} serial NOT NULL";
        }

        var sql = base.FieldClause(field, onlyDefine);

        //// 加上注释
        //if (!String.IsNullOrEmpty(field.Description)) sql = $"{sql} COMMENT '{field.Description}'";

        return sql;
    }

    protected override String? GetFieldConstraints(IDataColumn field, Boolean onlyDefine)
    {
        String? str = null;
        if (!field.Nullable) str = " NOT NULL";

        if (field.Identity) str = " serial NOT NULL";

        // 默认值
        if (!field.Nullable && !field.Identity)
        {
            str += GetDefault(field, onlyDefine);
        }

        return str;
    }

    /// <summary>默认值</summary>
    /// <param name="field"></param>
    /// <param name="onlyDefine"></param>
    /// <returns></returns>
    protected override String? GetDefault(IDataColumn field, Boolean onlyDefine)
    {
        if (field.DataType == typeof(Boolean))
            return $" DEFAULT {(field.DefaultValue.ToBoolean())}";

        return base.GetDefault(field, onlyDefine);
    }

    #region 架构定义

    //public override object SetSchema(DDLSchema schema, params object[] values)
    //{
    //    if (schema == DDLSchema.DatabaseExist)
    //    {
    //        IDbSession session = Database.CreateSession();

    //        DataTable dt = GetSchema(_.Databases, new String[] { values != null && values.Length > 0 ? (String)values[0] : session.DatabaseName });
    //        if (dt == null || dt.Rows == null || dt.Rows.Count <= 0) return false;
    //        return true;
    //    }

    //    return base.SetSchema(schema, values);
    //}

    protected override Boolean DatabaseExist(String databaseName)
    {
        //return base.DatabaseExist(databaseName);

        var session = Database.CreateSession();
        //var dt = GetSchema(_.Databases, new String[] { databaseName.ToLower() });
        var dt = GetSchema(_.Databases, [databaseName]);
        return dt != null && dt.Rows != null && dt.Rows.Count > 0;
    }

    /// <summary>
    /// 创建数据库的 SQL 语句，强制带上双引号。
    /// </summary>
    public override String CreateDatabaseSQL(String dbname, String? file)
    {
        return String.Format("Create Database \"{0}\"", dbname.Replace("\"", "\"\""));
    }

    public override String DropDatabaseSQL(String dbname) => $"Drop Database If Exists {Database.FormatName(dbname)}";

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
        if (table.PrimaryKeys.Length > 0) sb.AppendFormat(",\r\n\tPrimary Key ({0})", table.PrimaryKeys.Join(",", FormatName));
        sb.AppendLine();
        sb.Append(')');

        return sb.ToString();
    }

    public override String AddTableDescriptionSQL(IDataTable table) => $"Comment On Table {FormatName(table)} is '{table.Description}'";

    public override String DropTableDescriptionSQL(IDataTable table) => $"Comment On Table {FormatName(table)} is ''";

    public override String AddColumnDescriptionSQL(IDataColumn field) => $"Comment On Column {FormatName(field.Table)}.{FormatName(field)} is '{field.Description}'";

    public override String DropColumnDescriptionSQL(IDataColumn field) => $"Comment On Column {FormatName(field.Table)}.{FormatName(field)} is ''";

    #endregion 架构定义

    #region 表构架
    protected override List<IDataTable> OnGetTables(String[]? names)
    {
        var tables = base.OnGetTables(names);
        var session = Database.CreateSession();
        using var _ = session.SetShowSql(false);
        const String sql = @"with tables as (
  select c
    .oid,
    ns.nspname as schema_name,
    c.relname as table_name,
    d.description as table_description,
    pg_get_userbyid ( c.relowner ) as table_owner 
  from
    pg_catalog.pg_class
    as c join pg_catalog.pg_namespace as ns on c.relnamespace = ns.
    oid left join pg_catalog.pg_description d on c.oid = d.objoid 
    and d.objsubid = 0 
  where
    ns.nspname not in ( 'pg_catalog' ) 
)  select 
c.table_name as table_name,
t.table_description,
c.column_name as column_name,
c.ordinal_position,
d.description as column_description
from
  tables
  t join information_schema.columns c on c.table_schema = t.schema_name 
  and c.table_name = t.
  table_name left join pg_catalog.pg_description d on d.objoid = t.oid 
  and d.objsubid = c.ordinal_position 
  and d.objsubid > 0 
where
  c.table_schema = 'public' 
order by
  t.schema_name,
  t.table_name,
  c.ordinal_position";
        var ds = session.Query(sql);
        if (ds.Tables.Count != 0)
        {
            var dt = ds.Tables[0]!;
            foreach (var table in tables)
            {
                var rows = dt.Select($"table_name = '{table.TableName}'");
                if (rows is { Length: > 0 })
                {
                    if (String.IsNullOrWhiteSpace(table.Description))
                    {
                        foreach (var row in rows)
                        {
                            table.Description = Convert.ToString(row["table_description"]);
                            break;
                        }
                    }
                    foreach (var row in rows)
                    {
                        var columnName = Convert.ToString(row["column_name"]);
                        if (String.IsNullOrWhiteSpace(columnName)) continue;
                        var col = table.GetColumn(columnName);
                        if (col == null) continue;
                        if (String.IsNullOrWhiteSpace(col.Description)) col.Description = Convert.ToString(row["column_description"]);
                    }
                }
            }
        }

        var idxs = tables.SelectMany(f => f.Indexes.Select(f => f.Name)).ToArray();
        if (idxs.Length > 0)
        {
            var idx_sql = $"SELECT conname FROM pg_constraint WHERE contype = 'p' AND conname IN (" +
                $"{String.Join(",", idxs.Select(f => $"'{f}'"))})";
            ds = session.Query(idx_sql);
            if (ds.Tables.Count != 0)
            {
                var dt = ds.Tables[0]!;
                var set = new HashSet<String>();
                foreach (DataRow dr in dt.Rows)
                {
                    set.Add(Convert.ToString(dr[0]));
                }
                if (set.Count > 0)
                {
                    foreach (var tbl in tables)
                    {
                        foreach (var idx in tbl.Indexes)
                        {
                            if (!String.IsNullOrWhiteSpace(idx.Name) && set.Contains(idx.Name!)) idx.PrimaryKey = true;
                        }
                    }
                }
            }
        }
        return tables;
    }
    #endregion
}