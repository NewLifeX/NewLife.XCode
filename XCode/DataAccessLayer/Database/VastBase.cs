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

    internal String? _searchPath;

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

        // VastBase 必须指定 Search Path
        if (!builder.TryGetValue("Search Path", out var searchPath) && !builder.TryGetValue("SearchPath", out searchPath))
        {
            throw new ArgumentException("VastBase 连接字符串中必须包含 Search Path 参数,例如: Search Path=public");
        }

        // 保存 Search Path,用于后续查询表结构
        _searchPath = searchPath?.Split(',')[0].Trim();

        // VastBase 不支持 DISCARD 语句,禁用连接重置
        // 避免报错: DISCARD statement is not yet supported
        builder.TryAdd("No Reset On Close", "true");

        // 打印 Search Path 信息,便于调试
        if (!String.IsNullOrEmpty(_searchPath))
        {
            DAL.WriteLog("[{0}]VastBase Search Path(Schema): {1}", ConnName, _searchPath);
        }
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

        // VastBase/PostgreSQL 中未加引号的标识符会自动转为小写
        // 为了与数据库实际存储的名称一致,这里也转为小写
        return name.ToLower();
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
    public override String PageSplit(String sql, Int64 startRowIndex, Int64 maximumRows, String? keyColumn) => PageSplitByOffsetLimit(sql, startRowIndex, maximumRows);

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

    #endregion 基本方法 查询/执行

    #region 批量操作
    public override Int32 Insert(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list)
    {
        const String action = "Insert Into";

        var sb = Pool.StringBuilder.Get();
        var db = (Database as DbBase)!;

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
        var db = (Database as DbBase)!;

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
        // Byte 类型映射移除 - PostgreSQL/VastBase 的 smallint 是有符号 16 位整数,应映射到 Int16
        // 如果需要存储 0-255 的 Byte 值,应使用 Int16 类型
        { typeof(Boolean), new String[] { "boolean", "bool" } },
        { typeof(Int16), new String[] { "smallint", "int2" } },
        { typeof(Int32), new String[] { "integer", "int", "int4" } },
        { typeof(Int64), new String[] { "bigint", "int8" } },
        { typeof(Single), new String[] { "real", "float4" } },
        { typeof(Double), new String[] { "double precision", "float8" } },
        { typeof(Decimal), new String[] { "numeric", "decimal" } },
        { typeof(DateTime), new String[] { "timestamp", "timestamp without time zone", "timestamp with time zone", "timestamptz", "date", "time" } },
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

        // 强制修正:smallint/int2 必须映射为 Int16,不能是 Byte
        // 这样可以确保从数据库反向工程读取的类型与实体定义一致
        var rawType = field.RawType?.ToLower();
        if (rawType == "smallint" || rawType == "int2")
        {
            return typeof(Int16);
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

    protected override String? GetFieldType(IDataColumn field)
    {
        if (field.Identity)
        {
            if (field.DataType == typeof(Int16)) return "smallserial";
            if (field.DataType == typeof(Int32)) return "serial";
            if (field.DataType == typeof(Int64)) return "bigserial";
        }

        // 处理 Byte 类型:PostgreSQL/VastBase 没有无符号 8 位整数类型
        // Byte (0-255) 使用 smallint (Int16) 存储
        if (field.DataType == typeof(Byte))
        {
            WriteLog("字段[{0}]类型为Byte,VastBase不支持该类型,自动使用smallint(Int16)替代", field.Name);
            return "smallint";
        }

        // VastBase/PostgreSQL 不支持 tinyint 或 int1,需要转换
        // 当 RawType 是 tinyint 或 int1 时,根据 DataType 选择合适的类型
        if (!field.RawType.IsNullOrEmpty())
        {
            var rawType = field.RawType.ToLower();
            if (rawType.StartsWith("tinyint") || rawType.StartsWith("int1"))
            {
                // Boolean 使用 boolean,其他使用 smallint
                if (field.DataType == typeof(Boolean)) return "boolean";
                return "smallint";
            }
        }

        return base.GetFieldType(field);
    }

    public override String FieldClause(IDataColumn field, Boolean onlyDefine)
    {
        var sql = base.FieldClause(field, onlyDefine);

        //// 加上注释
        //if (!String.IsNullOrEmpty(field.Description)) sql = $"{sql} COMMENT '{field.Description}'";

        return sql;
    }

    protected override String? GetFieldConstraints(IDataColumn field, Boolean onlyDefine)
    {
        String? str = null;
        // serial 类型已隐含 NOT NULL,不需要再添加
        if (!field.Nullable && !field.Identity) str = " NOT NULL";

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

    protected override Boolean IsColumnTypeChanged(IDataColumn entityColumn, IDataColumn dbColumn)
    {
        // 特殊兼容:Byte 和 Int16 在 VastBase 中都映射到 smallint
        // 实体可能是 Byte,数据库反向工程返回 Int16,只要 RawType 都是 smallint 就视为兼容
        var entityType = entityColumn.DataType;
        var dbType = dbColumn.DataType;

        if ((entityType == typeof(Byte) && dbType == typeof(Int16)) ||
            (entityType == typeof(Int16) && dbType == typeof(Byte)))
        {
            var entityRaw = entityColumn.RawType;
            var dbRaw = dbColumn.RawType;

            // 如果都是 smallint 或 int2,则视为兼容（不区分大小写）
            if (!String.IsNullOrEmpty(entityRaw) && !String.IsNullOrEmpty(dbRaw))
            {
                if ((entityRaw.EqualIgnoreCase("smallint") || entityRaw.EqualIgnoreCase("int2")) &&
                    (dbRaw.EqualIgnoreCase("smallint") || dbRaw.EqualIgnoreCase("int2")))
                    return false;
            }
        }

        // 如果实体字段的 DataType 为 null,尝试从 RawType 反推
        // (ModelHelper.FixDefaultByType 会将 DataType 设为 null,以便写入 XML 时不重复)
        if (entityType == null && !String.IsNullOrEmpty(entityColumn.RawType))
        {
            var tempField = entityColumn.Table?.CreateColumn();
            if (tempField != null)
            {
                tempField.RawType = entityColumn.RawType;
                var dataType = GetDataType(tempField);
                if (dataType != null && dataType == dbType)
                    return false;
            }
        }

        // 标准化 RawType:将 MySQL 的 tinyint/int1 转换为 VastBase 的标准类型
        var entityRawType = NormalizeRawType(entityColumn.RawType, entityType);
        var dbRawType = NormalizeRawType(dbColumn.RawType, dbType);

        // 如果标准化后的 RawType 相同,则认为类型未改变
        if (!entityRawType.IsNullOrEmpty() && !dbRawType.IsNullOrEmpty())
        {
            if (entityRawType.EqualIgnoreCase(dbRawType))
                return false;

            // 处理 PostgreSQL/VastBase 类型别名兼容(忽略长度)
            var entityBaseType = entityRawType.Split('(')[0].Trim();
            var dbBaseType = dbRawType.Split('(')[0].Trim();

            // varchar ↔ character varying
            if ((entityBaseType.EqualIgnoreCase("varchar") && dbBaseType.EqualIgnoreCase("character varying")) ||
                (entityBaseType.EqualIgnoreCase("character varying") && dbBaseType.EqualIgnoreCase("varchar")))
                return false;
        }

        return base.IsColumnTypeChanged(entityColumn, dbColumn);
    }

    /// <summary>标准化 RawType,将不支持的类型转换为 VastBase 支持的类型</summary>
    private String? NormalizeRawType(String? rawType, Type? dataType)
    {
        if (rawType.IsNullOrEmpty()) return rawType;

        // 优化:避免重复 ToLower,使用 OrdinalIgnoreCase 比较
        if (rawType.StartsWith("tinyint", StringComparison.OrdinalIgnoreCase) ||
            rawType.StartsWith("int1", StringComparison.OrdinalIgnoreCase))
        {
            if (dataType == typeof(Boolean)) return "boolean";
            if (dataType == typeof(Byte)) return "smallint";
        }

        return rawType;
    }

    protected override Boolean IsColumnLengthChanged(IDataColumn entityColumn, IDataColumn dbColumn, IDatabase? entityDb)
        => base.IsColumnLengthChanged(entityColumn, dbColumn, entityDb);

    protected override Boolean IsColumnChanged(IDataColumn entityColumn, IDataColumn dbColumn, IDatabase? entityDb)
        => base.IsColumnChanged(entityColumn, dbColumn, entityDb);

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
        // 不使用 GetSchema,直接查询 pg_database 避免 pg_user 权限问题
        var session = Database.CreateSession();

        var sql = $"SELECT 1 FROM pg_catalog.pg_database WHERE datname = '{databaseName}'";
        var ds = session.Query(sql);

        return ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0;
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

    public override String? AddColumnSQL(IDataColumn field)
    {
        // 使用 FieldClause 获取完整的字段定义(包含类型和约束)
        var fieldDef = FieldClause(field, true);
        // 从字段定义中移除字段名(FieldClause 返回格式: "字段名 类型 约束")
        var parts = fieldDef.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        var typeAndConstraints = parts.Length > 1 ? parts[1] : fieldDef;

        return $"ALTER TABLE {FormatName(field.Table)} ADD COLUMN {FormatName(field)} {typeAndConstraints}";
    }

    public override String? AlterColumnSQL(IDataColumn field, IDataColumn? oldfield)
    {
        return $"ALTER TABLE {FormatName(field.Table)} ALTER COLUMN {FormatName(field)} TYPE {GetFieldType(field)}";
    }

    public override String? AddColumnDescriptionSQL(IDataColumn field) => $"Comment On Column {FormatName(field.Table)}.{FormatName(field)} is '{field.Description}'";

    public override String? DropColumnDescriptionSQL(IDataColumn field) => $"Comment On Column {FormatName(field.Table)}.{FormatName(field)} is ''";

    public override String? CreateIndexSQL(IDataIndex index)
    {
        // VastBase/PostgreSQL 中索引名不区分大小写
        // 检查数据库中是否已存在同名索引(忽略大小写)
        var table = index.Table;
        if (table?.Indexes != null)
        {
            foreach (var existingIndex in table.Indexes)
            {
                // 不区分大小写比对索引名和列
                if (existingIndex.Name.EqualIgnoreCase(index.Name) &&
                    existingIndex.Columns != null && index.Columns != null &&
                    existingIndex.Columns.Length == index.Columns.Length)
                {
                    var allMatch = true;
                    for (var i = 0; i < existingIndex.Columns.Length; i++)
                    {
                        if (!existingIndex.Columns[i].EqualIgnoreCase(index.Columns[i]))
                        {
                            allMatch = false;
                            break;
                        }
                    }

                    if (allMatch)
                        return String.Empty; // 索引已存在,跳过创建
                }
            }
        }

        return base.CreateIndexSQL(index);
    }

    #endregion 架构定义

    #region 表构架
    protected override List<IDataTable> OnGetTables(String[]? names)
    {
        // VastBase 不使用 GetSchema,直接查询系统表避免 pg_user 权限问题
        var list = new List<IDataTable>();
        var session = Database.CreateSession();
        using var _ = session.SetShowSql(false);

        // 获取 Search Path (当前 schema)
        var searchPath = "public";
        if (Database is VastBase vb && !String.IsNullOrEmpty(vb._searchPath))
        {
            searchPath = vb._searchPath;
        }

        DAL.WriteLog("[{0}]VastBase 查询表结构,使用 Schema: {1}", Database.ConnName, searchPath);

        // 查询表列表和列信息
        var sql = $@"
SELECT 
    t.tablename as table_name,
    obj_description((quote_ident(t.schemaname)||'.'||quote_ident(t.tablename))::regclass) as table_description,
    c.column_name,
    c.ordinal_position,
    c.data_type,
    c.character_maximum_length,
    c.numeric_precision,
    c.numeric_scale,
    c.is_nullable,
    c.column_default,
    col_description((quote_ident(t.schemaname)||'.'||quote_ident(t.tablename))::regclass, c.ordinal_position) as column_description
FROM 
    pg_catalog.pg_tables t
    LEFT JOIN information_schema.columns c ON c.table_schema = t.schemaname AND c.table_name = t.tablename
WHERE 
    t.schemaname = '{searchPath}'
ORDER BY 
    t.tablename, c.ordinal_position";

        var ds = session.Query(sql);
        if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
        {
            DAL.WriteLog("[{0}]VastBase 在 Schema '{1}' 中未找到任何表", Database.ConnName, searchPath);
            return list;
        }

        var dt = ds.Tables[0];

        // 按表名分组
        var tableDict = new Dictionary<String, List<DataRow>>(StringComparer.OrdinalIgnoreCase);
        foreach (DataRow row in dt.Rows)
        {
            var tableName = row["table_name"]?.ToString();
            if (String.IsNullOrEmpty(tableName)) continue;

            if (!tableDict.ContainsKey(tableName!))
                tableDict[tableName!] = [];

            tableDict[tableName!].Add(row);
        }

        foreach (var kvp in tableDict)
        {
            var tableName = kvp.Key;
            var tableRows = kvp.Value;
            if (String.IsNullOrEmpty(tableName)) continue;

            // 表名过滤
            if (names != null && names.Length > 0 && !names.Any(n => n.EqualIgnoreCase(tableName))) continue;

            // 从数据库获取实际的表名(小写)
            var dbTableName = tableRows[0]["table_name"]?.ToString();
            if (String.IsNullOrEmpty(dbTableName)) continue;

            var table = DAL.CreateTable();
            // 使用数据库实际存储的表名(小写),这样生成的索引名也会是小写,与数据库匹配
            table.TableName = dbTableName!;
            table.DbType = Database.Type;

            // 表描述(只取第一行)
            if (tableRows.Count > 0)
            {
                table.Description = tableRows[0]["table_description"]?.ToString();
            }

            // 添加列
            foreach (var row in tableRows)
            {
                var columnName = row["column_name"]?.ToString();
                if (String.IsNullOrEmpty(columnName)) continue;

                var column = table.CreateColumn();
                column.ColumnName = columnName!;
                column.Description = row["column_description"]?.ToString();
                column.RawType = row["data_type"]?.ToString();
                column.Nullable = row["is_nullable"]?.ToString() == "YES";

                var defaultValue = row["column_default"]?.ToString();
                column.DefaultValue = defaultValue;

                // 识别自增字段 (serial/bigserial 或 nextval)
                var rawType = column.RawType ?? "";
                if (defaultValue != null &&
                    (defaultValue.Contains("nextval") || rawType == "serial" || rawType == "bigserial"))
                {
                    column.Identity = true;
                }

                // 解析数据类型
                if (Int32.TryParse(row["character_maximum_length"]?.ToString(), out var len))
                    column.Length = len;
                if (Int32.TryParse(row["numeric_precision"]?.ToString(), out var precision))
                    column.Precision = precision;
                if (Int32.TryParse(row["numeric_scale"]?.ToString(), out var scale))
                    column.Scale = scale;

                table.Columns.Add(column);
            }

            // 让基类处理数据类型映射
            foreach (var col in table.Columns)
            {
                // 调用 GetDataType 来设置正确的 C# 类型
                var dataType = GetDataType(col);
                if (dataType != null)
                    col.DataType = dataType;
            }

            // 查询索引和主键(使用数据库实际表名)
            var idxSql = $@"
SELECT 
    i.relname as index_name,
    a.attname as column_name,
    ix.indisprimary as is_primary,
    ix.indisunique as is_unique
FROM 
    pg_catalog.pg_class t
    JOIN pg_catalog.pg_namespace n ON n.oid = t.relnamespace
    JOIN pg_catalog.pg_index ix ON ix.indrelid = t.oid
    JOIN pg_catalog.pg_class i ON i.oid = ix.indexrelid
    JOIN pg_catalog.pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY(ix.indkey)
WHERE 
    n.nspname = '{searchPath}' AND t.relname = '{dbTableName}'
ORDER BY 
    i.relname, a.attnum";

            var idxDs = session.Query(idxSql);
            if (idxDs.Tables.Count > 0 && idxDs.Tables[0].Rows.Count > 0)
            {
                var idxDt = idxDs.Tables[0];

                // 按索引名分组
                var idxDict = new Dictionary<String, List<DataRow>>(StringComparer.OrdinalIgnoreCase);
                foreach (DataRow row in idxDt.Rows)
                {
                    var indexName = row["index_name"]?.ToString();
                    if (String.IsNullOrEmpty(indexName)) continue;

                    if (!idxDict.ContainsKey(indexName!))
                        idxDict[indexName!] = [];

                    idxDict[indexName!].Add(row);
                }

                foreach (var idxKvp in idxDict)
                {
                    var dbIndexName = idxKvp.Key;  // 数据库实际索引名(小写)
                    var idxRows = idxKvp.Value;
                    if (String.IsNullOrEmpty(dbIndexName) || idxRows.Count == 0) continue;

                    var isPrimary = idxRows[0]["is_primary"]?.ToString() == "True";
                    var isUnique = idxRows[0]["is_unique"]?.ToString() == "True";

                    var index = table.CreateIndex();
                    // 使用数据库实际的索引名(小写),便于在 CreateIndexSQL 中进行不区分大小写的比对
                    index.Name = dbIndexName;
                    index.PrimaryKey = isPrimary;
                    index.Unique = isUnique;

                    var colNames = new List<String>();
                    foreach (var idxRow in idxRows)
                    {
                        var colName = idxRow["column_name"]?.ToString();
                        if (!String.IsNullOrEmpty(colName))
                            colNames.Add(colName!);
                    }
                    index.Columns = colNames.ToArray();

                    table.Indexes.Add(index);

                    // 标记主键列
                    if (isPrimary)
                    {
                        foreach (var colName in index.Columns)
                        {
                            var col = table.GetColumn(colName);
                            if (col != null) col.PrimaryKey = true;
                        }
                    }
                }
            }

            // 修正关系数据
            table.Fix();
            list.Add(table);
        }

        DAL.WriteLog("[{0}]VastBase 在 Schema '{1}' 中找到 {2} 个表", Database.ConnName, searchPath, list.Count);
        return list;
    }
    #endregion
}