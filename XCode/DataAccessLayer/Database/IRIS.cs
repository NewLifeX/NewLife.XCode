using System.Data;
using System.Data.Common;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;

namespace XCode.DataAccessLayer;

/// <summary>InterSystems IRIS 数据库</summary>
/// <remarks>
/// InterSystems IRIS 的核心是一个超高性能、多模型、事务分析型数据库引擎，其独特的架构使其能够在 大规模应用中具有极高性能 ，并具有卓越的资源效率。
/// 
/// https://www.intersystems.com/cn/products/intersystems-iris/database-management/
/// </remarks>
internal class IRIS : RemoteDb
{
    #region 属性

    /// <summary>返回数据库类型。</summary>
    public override DatabaseType Type => DatabaseType.IRIS;

    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory()
    {
        if (!Provider.IsNullOrEmpty() && Provider.Contains("IRIS"))
        {
            var type = DriverLoader.Load("InterSystems.Data.IRISClient.IRISFactory", null, "InterSystems.Data.IRISClient.dll", null);
            var factory = GetProviderFactory(type);
            if (factory != null) return factory;
        }
        // 找不到驱动时，再到线上下载
        {
            var factory = GetProviderFactory(null, "InterSystems.Data.IRISClient.dll", "InterSystems.Data.IRISClient.IRISFactory");
            return factory;
        }
    }

    private const String Server_Key = "Server";
    private const String CharSet = "CharSet";

    //const String AllowZeroDatetime = "Allow Zero Datetime";
    private const String MaxPoolSize = "MaxPoolSize";

    private const String Sslmode = "Sslmode";

    //protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    //{
    //    base.OnSetConnectionString(builder);

    //    var key = builder[Server_Key];
    //    if (key.EqualIgnoreCase(".", "localhost"))
    //    {
    //        //builder[Server_Key] = "127.0.0.1";
    //        builder[Server_Key] = IPAddress.Loopback.ToString();
    //    }

    //    // 默认设置为utf8mb4，支持表情符
    //    builder.TryAdd(CharSet, "utf8mb4");

    //    //if (!builder.ContainsKey(AllowZeroDatetime)) builder[AllowZeroDatetime] = "True";
    //    // 默认最大连接数1000
    //    if (builder["Pooling"].ToBoolean()) builder.TryAdd(MaxPoolSize, "1000");

    //    // 如未设置Sslmode,默认为Preferred(兼容新旧版本IRIS驱动)
    //    if (builder[Sslmode] == null) builder.TryAdd(Sslmode, "Preferred");
    //}

    #endregion 属性

    #region 方法

    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new IRISSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new IRISMetaData();

    public override Boolean Support(String providerName)
    {
        providerName = providerName.ToLower();
        if (providerName.Contains("IRIS.data.IRISclient")) return true;
        if (providerName.Contains("IRIS")) return true;

        return false;
    }

    #endregion 方法

    #region 数据库特性

    protected override String ReservedWordsStr => "ACCESSIBLE,ADD,ALL,ALTER,ANALYZE,AND,AS,ASC,ASENSITIVE,BEFORE,BETWEEN,BIGINT,BINARY,BLOB,BOTH,BY,CALL,CASCADE,CASE,CHANGE,CHAR,CHARACTER,CHECK,COLLATE,COLUMN,CONDITION,CONNECTION,CONSTRAINT,CONTINUE,CONTRIBUTORS,CONVERT,CREATE,CROSS,CURRENT_DATE,CURRENT_TIME,CURRENT_TIMESTAMP,CURRENT_USER,CURSOR,DATABASE,DATABASES,DAY_HOUR,DAY_MICROSECOND,DAY_MINUTE,DAY_SECOND,DEC,DECIMAL,DECLARE,DEFAULT,DELAYED,DELETE,DESC,DESCRIBE,DETERMINISTIC,DISTINCT,DISTINCTROW,DIV,DOUBLE,DROP,DUAL,EACH,ELSE,ELSEIF,ENCLOSED,ESCAPED,EXISTS,EXIT,EXPLAIN,FALSE,FETCH,FLOAT,FLOAT4,FLOAT8,FOR,FORCE,FOREIGN,FROM,FULLTEXT,GRANT,GROUP,HAVING,HIGH_PRIORITY,HOUR_MICROSECOND,HOUR_MINUTE,HOUR_SECOND,IF,IGNORE,IN,INDEX,INFILE,INNER,INOUT,INSENSITIVE,INSERT,INT,INT1,INT2,INT3,INT4,INT8,INTEGER,INTERVAL,INTO,IS,ITERATE,JOIN,KEY,KEYS,KILL,LEADING,LEAVE,LEFT,LIKE,LIMIT,LINEAR,LINES,LOAD,LOCALTIME,LOCALTIMESTAMP,LOCK,LONG,LONGBLOB,LONGTEXT,LOOP,LOW_PRIORITY,MATCH,MEDIUMBLOB,MEDIUMINT,MEDIUMTEXT,MIDDLEINT,MINUTE_MICROSECOND,MINUTE_SECOND,MOD,MODIFIES,NATURAL,NOT,NO_WRITE_TO_BINLOG,NULL,NUMERIC,ON,OPTIMIZE,OPTION,OPTIONALLY,OR,ORDER,OUT,OUTER,OUTFILE,PRECISION,PRIMARY,PROCEDURE,PURGE,RANGE,READ,READS,READ_ONLY,READ_WRITE,REAL,REFERENCES,REGEXP,RELEASE,RENAME,REPEAT,REPLACE,REQUIRE,RESTRICT,RETURN,REVOKE,RIGHT,RLIKE,SCHEMA,SCHEMAS,SECOND_MICROSECOND,SELECT,SENSITIVE,SEPARATOR,SET,SHOW,SMALLINT,SPATIAL,SPECIFIC,SQL,SQLEXCEPTION,SQLSTATE,SQLWARNING,SQL_BIG_RESULT,SQL_CALC_FOUND_ROWS,SQL_SMALL_RESULT,SSL,STARTING,STRAIGHT_JOIN,TABLE,TERMINATED,THEN,TINYBLOB,TINYINT,TINYTEXT,TO,TRAILING,TRIGGER,TRUE,UNDO,UNION,UNIQUE,UNLOCK,UNSIGNED,UPDATE,UPGRADE,USAGE,USE,USING,UTC_DATE,UTC_TIME,UTC_TIMESTAMP,VALUES,VARBINARY,VARCHAR,VARCHARACTER,VARYING,WHEN,WHERE,WHILE,WITH,WRITE,X509,XOR,YEAR_MONTH,ZEROFILL," +
                "LOG,User,Role,Admin,Rank,Member,Groups,Error,MaxValue,MinValue,Signal,Offset";

    /// <summary>格式化关键字</summary>
    /// <param name="keyWord">关键字</param>
    /// <returns></returns>
    public override String FormatKeyWord(String keyWord)
    {
        //if (String.IsNullOrEmpty(keyWord)) throw new ArgumentNullException("keyWord");
        if (keyWord.IsNullOrEmpty()) return keyWord;

        if (keyWord.StartsWith("`") && keyWord.EndsWith("`")) return keyWord;

        return $"`{keyWord}`";
    }

    /// <summary>格式化数据为SQL数据</summary>
    /// <param name="field">字段</param>
    /// <param name="value">数值</param>
    /// <returns></returns>
    public override String FormatValue(IDataColumn field, Object? value)
    {
        var code = System.Type.GetTypeCode(field.DataType);
        if (code == TypeCode.String)
        {
            if (value == null)
                return field.Nullable ? "null" : "''";

            return "'" + value.ToString()
                .Replace("\\", "\\\\")//反斜杠需要这样才能插入到数据库
                .Replace("'", @"\'") + "'";
        }
        else if (code == TypeCode.Boolean)
        {
            var v = value.ToBoolean();
            if (field.Table != null && EnumTables.Contains(field.Table.TableName))
                return v ? "'Y'" : "'N'";
            else
                return v ? "1" : "0";
        }

        return base.FormatValue(field, value);
    }

    private static readonly Char[] _likeKeys = ['\\', '\'', '\"', '%', '_'];

    /// <summary>格式化模糊搜索的字符串。处理转义字符</summary>
    /// <param name="column">字段</param>
    /// <param name="format">格式化字符串</param>
    /// <param name="value">数值</param>
    /// <returns></returns>
    public override String FormatLike(IDataColumn column, String format, String value)
    {
        if (value.IsNullOrEmpty()) return value;

        if (value.IndexOfAny(_likeKeys) >= 0)
            value = value
                .Replace("\\", "\\\\")
                .Replace("'", "''")
                .Replace("\"", "\\\"")
                .Replace("%", "\\%")
                .Replace("_", "\\_");

        return base.FormatLike(column, format, value);
    }

    /// <summary>长文本长度</summary>
    public override Int32 LongTextLength => 4000;

    protected internal override String ParamPrefix => "?";

    /// <summary>创建参数</summary>
    /// <param name="name">名称</param>
    /// <param name="value">值</param>
    /// <param name="type">类型</param>
    /// <returns></returns>
    public override IDataParameter CreateParameter(String name, Object? value, Type? type = null)
    {
        var dp = base.CreateParameter(name, value, type);

        //var type = field?.DataType;
        if (type == null) type = value?.GetType();

        // IRIS的枚举要用 DbType.String
        if (type == typeof(Boolean))
        {
            var v = value.ToBoolean();
            //if (field?.Table != null && EnumTables.Contains(field.Table.TableName))
            //{
            //    dp.DbType = DbType.String;
            //    dp.Value = value.ToBoolean() ? 'Y' : 'N';
            //}
            //else
            {
                dp.DbType = DbType.Int16;
                dp.Value = v ? 1 : 0;
            }
        }

        return dp;
    }

    /// <summary>系统数据库名</summary>
    public override String SystemDatabaseName => "IRIS";

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => $"concat({(!String.IsNullOrEmpty(left) ? left : "\'\'")},{(!String.IsNullOrEmpty(right) ? right : "\'\'")})";

    /// <summary>生成批量删除SQL。部分数据库支持分批删除</summary>
    /// <param name="tableName"></param>
    /// <param name="where"></param>
    /// <param name="batchSize"></param>
    /// <returns>不支持分批删除时返回null</returns>
    public override String? BuildDeleteSql(String tableName, String where, Int32 batchSize)
    {
        var sql = base.BuildDeleteSql(tableName, where, 0);

        if (batchSize <= 0) return sql;

        sql = $"{sql} limit {batchSize}";

        return sql;
    }
    #endregion 数据库特性

    #region 跨版本兼容

    /// <summary>采用枚举来表示布尔型的数据表。由正向工程赋值</summary>
    public ICollection<String> EnumTables { get; } = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

    #endregion 跨版本兼容
}

/// <summary>IRIS数据库</summary>
internal class IRISSession : RemoteDbSession
{
    #region 构造函数

    public IRISSession(IDatabase db) : base(db)
    {
    }

    #endregion 构造函数

    #region 快速查询单表记录数

    /// <summary>快速查询单表记录数，大数据量时，稍有偏差。</summary>
    /// <param name="tableName"></param>
    /// <returns></returns>
    public override Int64 QueryCountFast(String tableName)
    {
        tableName = tableName.Trim().Trim('`', '`').Trim();

        var db = Database.DatabaseName;
        var sql = $"select table_rows from information_schema.tables where table_schema='{db}' and table_name='{tableName}'";
        return ExecuteScalar<Int64>(sql);
    }

    public override Task<Int64> QueryCountFastAsync(String tableName)
    {
        tableName = tableName.Trim().Trim('`', '`').Trim();

        var db = Database.DatabaseName;
        var sql = $"select table_rows from information_schema.tables where table_schema='{db}' and table_name='{tableName}'";
        return ExecuteScalarAsync<Int64>(sql);
    }

    #endregion 快速查询单表记录数

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
        sql += ";Select LAST_INSERT_ID()";
        return base.InsertAndGetIdentity(sql, type, ps);
    }

    public override Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        sql += ";Select LAST_INSERT_ID()";
        return base.InsertAndGetIdentityAsync(sql, type, ps);
    }

    #endregion 基本方法 查询/执行

    #region 批量操作

    /*
    insert into stat (siteid,statdate,`count`,cost,createtime,updatetime) values
    (1,'2018-08-11 09:34:00',1,123,now(),now()),
    (2,'2018-08-11 09:34:00',1,456,now(),now()),
    (3,'2018-08-11 09:34:00',1,789,now(),now()),
    (2,'2018-08-11 09:34:00',1,456,now(),now())
    on duplicate key update
    `count`=`count`+values(`count`),cost=cost+values(cost),
    updatetime=values(updatetime);
     */

    private String GetBatchSql(String action, IDataTable table, IDataColumn[] columns, ICollection<String>? updateColumns, ICollection<String>? addColumns, IEnumerable<IModel> list)
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

    public override Int32 InsertIgnore(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list)
    {
        var sql = GetBatchSql("Insert Ignore Into", table, columns, null, null, list);
        return Execute(sql);
    }

    public override Int32 Replace(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list)
    {
        var sql = GetBatchSql("Replace Into", table, columns, null, null, list);
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

    #endregion 批量操作
}

/// <summary>IRIS元数据</summary>
internal class IRISMetaData : RemoteDbMetaData
{
    public IRISMetaData() => Types = _DataTypes;

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
    private static readonly Dictionary<Type, String[]> _DataTypes = new()
    {
        { typeof(Byte[]), new String[] { "BLOB", "TINYBLOB", "MEDIUMBLOB", "LONGBLOB", "binary({0})", "varbinary({0})" } },
        //{ typeof(TimeSpan), new String[] { "TIME" } },
        //{ typeof(SByte), new String[] { "TINYINT" } },
        { typeof(Byte), new String[] { "TINYINT", "TINYINT UNSIGNED" } },
        { typeof(Int16), new String[] { "SMALLINT", "SMALLINT UNSIGNED" } },
        //{ typeof(UInt16), new String[] { "SMALLINT UNSIGNED" } },
        { typeof(Int32), new String[] { "INT", "YEAR", "MEDIUMINT", "MEDIUMINT UNSIGNED", "INT UNSIGNED" } },
        //{ typeof(UInt32), new String[] { "MEDIUMINT UNSIGNED", "INT UNSIGNED" } },
        { typeof(Int64), new String[] { "BIGINT", "BIT", "BIGINT UNSIGNED" } },
        //{ typeof(UInt64), new String[] { "BIT", "BIGINT UNSIGNED" } },
        { typeof(Single), new String[] { "FLOAT" } },
        { typeof(Double), new String[] { "DOUBLE" } },
        { typeof(Decimal), new String[] { "DECIMAL({0}, {1})" } },
        { typeof(DateTime), new String[] { "DATETIME", "DATE", "TIMESTAMP", "TIME" } },
        // IRIS中nvarchar会变成utf8字符集的varchar，而不会取数据库的utf8mb4
        { typeof(String), new String[] { "VARCHAR({0})", "LONGTEXT", "TEXT", "CHAR({0})", "NCHAR({0})", "NVARCHAR({0})", "SET", "ENUM", "TINYTEXT", "TEXT", "MEDIUMTEXT" } },
        { typeof(Boolean), new String[] { "TINYINT" } },
        { typeof(Guid), new String[] { "CHAR(36)" } },
    };

    #endregion 数据类型

    #region 架构

    protected override List<IDataTable> OnGetTables(String[]? names)
    {
        var ss = Database.CreateSession();
        var db = Database.DatabaseName;
        var list = new List<IDataTable>();

        var old = ss.ShowSQL;
        ss.ShowSQL = false;
        try
        {
            var sql = $"SHOW TABLE STATUS FROM `{db}`";
            var dt = ss.Query(sql, null);
            if (dt.Rows == null || dt.Rows.Count == 0) return list;

            sql = $"select * from information_schema.columns where table_schema='{db}'";
            if (names != null && names.Length > 0) sql += " and table_name in ('" + names.Join("','") + "')";
            var columns = ss.Query(sql, null);

            sql = $"select * from information_schema.STATISTICS where table_schema='{db}'";
            if (names != null && names.Length > 0) sql += " and table_name in ('" + names.Join("','") + "')";
            var indexes = ss.Query(sql, null);

            var hs = new HashSet<String>(names ?? [], StringComparer.OrdinalIgnoreCase);

            // 所有表
            foreach (var dr in dt)
            {
                var name = dr["Name"] + "";
                if (name.IsNullOrEmpty() || hs.Count > 0 && !hs.Contains(name)) continue;

                var table = DAL.CreateTable();
                table.TableName = name;
                table.Description = dr["Comment"] + "";
                table.DbType = Database.Type;

                #region 字段
                if (columns.Rows != null && columns.Rows.Count > 0)
                {
                    foreach (var dc in columns)
                    {
                        if (dc["TABLE_NAME"] + "" != table.TableName) continue;

                        var field = table.CreateColumn();

                        field.ColumnName = dc["COLUMN_NAME"] + "";
                        field.RawType = dc["COLUMN_TYPE"] + "";
                        field.Description = dc["COLUMN_COMMENT"] + "";

                        if (dc["Extra"] + "" == "auto_increment") field.Identity = true;
                        if (dc["COLUMN_KEY"] + "" == "PRI") field.PrimaryKey = true;
                        if (dc["IS_NULLABLE"] + "" == "YES") field.Nullable = true;

                        // 精度 与 位数
                        field.Precision = dc["NUMERIC_PRECISION"].ToInt();
                        field.Scale = dc["NUMERIC_SCALE"].ToInt();
                        field.DefaultValue = dc["COLUMN_DEFAULT"] as String;

                        field.Length = field.RawType.Substring("(", ")").ToInt();

                        // 在IRIS8.0中，COLUMN_TYPE取得的类型，数字类型已经没有圆括号精度，需要额外处理
                        //if (field.Length == 0 && field.RawType.EqualIgnoreCase("int", "bigint", "tinyint", "datetime"))
                        //{
                        //    field.Precision = dc["NUMERIC_PRECISION"].ToInt();
                        //}

                        var type = GetDataType(field);
                        if (type == null)
                        {
                            if (field.RawType.StartsWithIgnoreCase("varchar", "nvarchar")) field.DataType = typeof(String);
                        }
                        else
                            field.DataType = type;

                        // IRIS中没有布尔型，这里处理YN枚举作为布尔型
                        if (field.RawType is "enum('N','Y')" or "enum('Y','N')") field.DataType = typeof(Boolean);

                        field.Fix();

                        table.Columns.Add(field);
                    }
                }
                else
                {
                    sql = $"SHOW FULL COLUMNS FROM `{db}`.`{name}`";
                    var dcs = ss.Query(sql, null);
                    foreach (var dc in dcs)
                    {
                        var field = table.CreateColumn();

                        field.ColumnName = dc["Field"] + "";
                        field.RawType = dc["Type"] + "";
                        field.Description = dc["Comment"] + "";
                        field.DefaultValue = dc["Default"] as String;

                        if (dc["Extra"] + "" == "auto_increment") field.Identity = true;
                        if (dc["Key"] + "" == "PRI") field.PrimaryKey = true;
                        if (dc["Null"] + "" == "YES") field.Nullable = true;

                        field.Length = field.RawType.Substring("(", ")").ToInt();
                        field.DataType = GetDataType(field)!;

                        if (field.DataType == null)
                        {
                            if (field.RawType.StartsWithIgnoreCase("varchar", "nvarchar")) field.DataType = typeof(String);
                        }

                        // IRIS中没有布尔型，这里处理YN枚举作为布尔型
                        if (field.RawType is "enum('N','Y')" or "enum('Y','N')") field.DataType = typeof(Boolean);

                        field.Fix();

                        table.Columns.Add(field);
                    }
                }
                #endregion 字段

                #region 索引
                if (indexes.Rows != null && indexes.Rows.Count > 0)
                {
                    foreach (var dr2 in indexes)
                    {
                        if (dr2["TABLE_NAME"] + "" != table.TableName) continue;

                        var dname = dr2["INDEX_NAME"] + "";
                        var di = table.Indexes.FirstOrDefault(e => e.Name == dname) ?? table.CreateIndex();
                        //di.Name = dname;
                        di.Unique = dr2.Get<Int32>("Non_unique") == 0;

                        var cname = dr2.Get<String>("Column_name");
                        if (cname.IsNullOrEmpty()) continue;

                        var cs = new List<String>();
                        if (di.Columns != null && di.Columns.Length > 0) cs.AddRange(di.Columns);
                        cs.Add(cname);
                        di.Columns = cs.ToArray();

                        if (di.Name == null)
                        {
                            di.Name = dname;
                            table.Indexes.Add(di);
                        }
                    }
                }
                else
                {
                    sql = $"SHOW INDEX FROM `{db}`.`{name}`";
                    var dis = ss.Query(sql, null);
                    foreach (var dr2 in dis)
                    {
                        var dname = dr2["Key_name"] + "";
                        var di = table.Indexes.FirstOrDefault(e => e.Name == dname) ?? table.CreateIndex();
                        //di.Name = dname;
                        di.Unique = dr2.Get<Int32>("Non_unique") == 0;

                        var cname = dr2.Get<String>("Column_name");
                        if (cname.IsNullOrEmpty()) continue;

                        var cs = new List<String>();
                        if (di.Columns != null && di.Columns.Length > 0) cs.AddRange(di.Columns);
                        cs.Add(cname);
                        di.Columns = cs.ToArray();

                        if (di.Name == null)
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
        finally
        {
            ss.ShowSQL = old;
        }

        // 找到使用枚举作为布尔型的旧表
        var es = (Database as IRIS)!.EnumTables;
        foreach (var table in list)
        {
            if (!es.Contains(table.TableName))
            {
                var dc = table.Columns.FirstOrDefault(c => c.DataType == typeof(Boolean)
                  && c.RawType.EqualIgnoreCase("enum('N','Y')", "enum('Y','N')"));
                if (dc != null)
                {
                    es.Add(table.TableName);

                    WriteLog("发现IRIS中旧格式的布尔型字段 {0} {1}", table.TableName, dc);
                }
            }
        }

        return list;
    }

    /// <summary>
    /// 快速取得所有表名
    /// </summary>
    /// <returns></returns>
    public override IList<String> GetTableNames()
    {
        var list = new List<String>();

        var sql = $"SHOW TABLE STATUS FROM `{Database.DatabaseName}`";
        var dt = base.Database.CreateSession().Query(sql, null);
        if (dt.Rows == null || dt.Rows.Count == 0) return list;

        // 所有表
        foreach (var dr in dt)
        {
            var name = dr["Name"] + "";
            if (!name.IsNullOrEmpty()) list.Add(name);
        }

        return list;
    }

    protected override Type? GetDataType(IDataColumn field)
    {
        // tinyint根据不同宽度判断所属类型
        if (!field.RawType.IsNullOrEmpty() && field.RawType.StartsWithIgnoreCase("tinyint("))
        {
            return field.Length switch
            {
                1 => typeof(Boolean),
                <= 2 => typeof(Byte),
                <= 4 => typeof(Int16),
                _ => typeof(Int32)
            };
        }

        return base.GetDataType(field);
    }

    protected override String? GetFieldType(IDataColumn field)
    {
        //field.Length = field.Length > 255 ? 255 : field.Length;
        if (field.DataType == null && field.RawType == "datetimeoffset")
        {
            field.DataType = typeof(DateTime);
        }
        if (field.DataType == typeof(Decimal) && field is XField fi)
        {
            // 精度 与 位数
            field.Precision = field.Length <= 0 ? field.Precision : field.Length > 255 ? 255 : field.Length;
            //field.Precision = field.Length > 255 ? 255 : field.Length <= 0 ? field.Precision : field.Length;
        }
        return base.GetFieldType(field);
    }

    public override String FieldClause(IDataColumn field, Boolean onlyDefine)
    {
        var sql = base.FieldClause(field, onlyDefine);

        // 加上注释
        if (!String.IsNullOrEmpty(field.Description)) sql = $"{sql} COMMENT '{field.Description}'";

        return sql;
    }

    protected override String? GetFieldConstraints(IDataColumn field, Boolean onlyDefine)
    {
        String? str = null;
        if (!field.Nullable) str = " NOT NULL";

        // 默认值
        if (!field.Nullable && !field.Identity)
        {
            str += GetDefault(field, onlyDefine);
        }

        if (field.Identity) str += " AUTO_INCREMENT";

        return str;
    }

    #endregion 架构

    #region 反向工程

    protected override Boolean DatabaseExist(String databaseName)
    {
        // IRISConnector 不支持获取单个数据库架构，需要整体获取后再过滤
        if (Database.Factory.GetType().Name.Contains("IRISConnector"))
        {
            var dbs = GetSchema(_.Databases, null);
            return dbs != null && dbs.Select($"schema_name='{databaseName}'").Length > 0;
        }

        var dt = GetSchema(_.Databases, [databaseName]);
        return dt != null && dt.Rows != null && dt.Rows.Count > 0;
    }

    public override String CreateDatabaseSQL(String dbname, String? file) => base.CreateDatabaseSQL(dbname, file) + " DEFAULT CHARACTER SET utf8mb4";

    public override String DropDatabaseSQL(String dbname) => $"Drop Database If Exists {Database.FormatName(dbname)}";

    public override String CreateTableSQL(IDataTable table)
    {
        var fs = new List<IDataColumn>(table.Columns);

        //var sb = new StringBuilder(32 + fs.Count * 20);
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
        sb.Append(')');

        // 引擎和编码
        //sb.Append(" ENGINE=InnoDB");
        if (table.Properties != null)
        {
            if (table.Properties.TryGetValue("Engine", out var str))
                sb.AppendFormat(" ENGINE={0}", str);
            if (table.Properties.TryGetValue("ROW_FORMAT", out str))
                sb.AppendFormat(" ROW_FORMAT={0}", str);
            if (table.Properties.TryGetValue("KEY_BLOCK_SIZE", out str))
                sb.AppendFormat(" KEY_BLOCK_SIZE={0}", str);
        }
        sb.Append(" DEFAULT CHARSET=utf8mb4");
        sb.Append(';');

        return sb.Return(true);
    }

    public override String AddTableDescriptionSQL(IDataTable table)
    {
        if (String.IsNullOrEmpty(table.Description)) return null;

        return $"Alter Table {FormatName(table)} Comment '{table.Description}'";
    }

    public override String AlterColumnSQL(IDataColumn field, IDataColumn? oldfield) => $"Alter Table {FormatName(field.Table)} Modify Column {FieldClause(field, false)}";

    public override String AddColumnDescriptionSQL(IDataColumn field) =>
        // 返回String.Empty表示已经在别的SQL中处理
        String.Empty;

    #endregion 反向工程
}