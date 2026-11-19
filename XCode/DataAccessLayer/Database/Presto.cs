using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using NewLife.Log;

namespace XCode.DataAccessLayer;

/// <summary>Presto/Trino 数据库基类</summary>
/// <remarks>
/// Presto 与 Trino 同源（PrestoSQL 于 2020 年更名 Trino），SQL 方言一致，仅 HTTP 协议头与驱动不同。
/// 两者均为联邦查询引擎：读操作所有连接器可用，写操作与事务取决于底层连接器，
/// 且没有自增主键、约束强制与索引，因此 XCode 将其定位为“只读分析 + 显式写入”，不提供自动建表与事务。
/// </remarks>
internal abstract class PrestoBase : RemoteDb
{
    #region 属性
    /// <summary>系统数据库名。Presto/Trino 提供 system 目录存放运行时元数据</summary>
    public override String SystemDatabaseName => "system";

    /// <summary>默认名称格式。Presto/Trino 未加引号的标识符折叠为小写</summary>
    public override NameFormats DefaultNameFormat => NameFormats.Lower;

    /// <summary>批量操作能力。Presto/Trino 无可靠批量语义，逐行执行</summary>
    public override BatchCapability BatchCapability => BatchCapability.None;

    /// <summary>模式名。对应连接字符串的 Schema</summary>
    public String? SchemaName { get; set; }
    #endregion

    #region 构造
    protected PrestoBase()
    {
        // 无自增主键与索引，DDL 依赖连接器属性，不能自动建表
        Migration = DataAccessLayer.Migration.Off;

        // NReco 驱动使用 {name} 字符串替换，官方 Trino 驱动使用 ?，均与 XCode 的 @ 前缀不兼容
        UseParameter = false;
    }
    #endregion

    #region 方法
    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new PrestoSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new PrestoMetaData();

    /// <summary>设置连接字符串</summary>
    /// <param name="builder"></param>
    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        // 兼容 XCode 的习惯写法：Database 表示 Catalog，Owner 表示 Schema
        if (builder["Catalog"].IsNullOrEmpty() && !DatabaseName.IsNullOrEmpty()) builder["Catalog"] = DatabaseName;
        if (builder["Schema"].IsNullOrEmpty() && !Owner.IsNullOrEmpty()) builder["Schema"] = Owner;

        // 兼容 Server / Data Source 写法
        var host = builder["Host"];
        if (host.IsNullOrEmpty()) host = builder["Server"];
        if (host.IsNullOrEmpty()) host = builder["Data Source"];
        if (host.IsNullOrEmpty()) host = builder["DataSource"];
        if (!host.IsNullOrEmpty())
        {
            builder["Host"] = host;
            builder.TryGetAndRemove("Server", out _);
            builder.TryGetAndRemove("Data Source", out _);
            builder.TryGetAndRemove("DataSource", out _);
        }

        // 驱动不认识 XCode 的数据库名写法，统一移除
        builder.TryGetAndRemove("Database", out _);
        builder.TryGetAndRemove("Db", out _);
        builder.TryGetAndRemove("Initial Catalog", out _);

        SchemaName = builder["Schema"];
    }
    #endregion

    #region 数据库特性
    /// <summary>关键字列表。Trino 保留字</summary>
    protected override String ReservedWordsStr =>
        "ALTER,AND,AS,BETWEEN,BY,CASE,CAST,CONSTRAINT,CREATE,CROSS,CUBE,CURRENT_CATALOG,CURRENT_DATE,CURRENT_PATH,CURRENT_ROLE,CURRENT_SCHEMA,CURRENT_TIME,CURRENT_TIMESTAMP,CURRENT_USER,DEALLOCATE,DELETE,DESCRIBE,DISTINCT,DROP,ELSE,END,ESCAPE,EXCEPT,EXECUTE,EXISTS,EXTRACT,FALSE,FOR,FROM,FULL,GROUP,GROUPING,HAVING,IN,INNER,INSERT,INTERSECT,INTERVAL,INTO,IS,JOIN,JSON_ARRAY,JSON_EXISTS,JSON_OBJECT,JSON_QUERY,JSON_TABLE,JSON_VALUE,LEFT,LIKE,LISTAGG,LOCALTIME,LOCALTIMESTAMP,NATURAL,NORMALIZE,NOT,NULL,OFFSET,ON,OR,ORDER,OUTER,PREPARE,RECURSIVE,RIGHT,ROLLUP,SELECT,SKIP,TABLE,THEN,TRUE,UESCAPE,UNION,UNNEST,USING,VALUES,WHEN,WHERE,WITH," +
        "LOG,User,Role,Admin,Rank,Member,Groups,Error,MaxValue,MinValue,Signal,Offset";

    /// <summary>格式化关键字。Presto/Trino 使用双引号，内部双引号成对转义</summary>
    /// <param name="keyWord">关键字</param>
    /// <returns></returns>
    public override String FormatKeyWord(String keyWord)
    {
        if (keyWord.IsNullOrEmpty()) return keyWord;

        if (keyWord.StartsWith("\"") && keyWord.EndsWith("\"")) return keyWord;

        return "\"" + keyWord.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>格式化数据为SQL数据</summary>
    /// <param name="field">字段</param>
    /// <param name="value">数值</param>
    /// <returns></returns>
    public override String FormatValue(IDataColumn field, Object? value)
    {
        if (value != null)
        {
            if (field?.DataType == typeof(Boolean)) return value.ToBoolean() ? "true" : "false";

            if (field?.DataType == typeof(Byte[]) || value is Byte[])
            {
                var bts = value as Byte[];
                if (bts == null || bts.Length == 0) return field?.Nullable == false ? "X''" : "null";

                return "X'" + BitConverter.ToString(bts).Replace("-", null) + "'";
            }
        }

        return base.FormatValue(field!, value);
    }

    /// <summary>格式化时间为SQL字符串。使用 TIMESTAMP 类型化字面量，避免字符串与时间字段比较时的隐式转换差异</summary>
    /// <param name="column">字段</param>
    /// <param name="dateTime">时间值</param>
    /// <returns></returns>
    public override String FormatDateTime(IDataColumn column, DateTime dateTime)
    {
        if (dateTime.Ticks % 10_000_000 == 0)
            return $"TIMESTAMP '{dateTime:yyyy-MM-dd HH:mm:ss}'";

        return $"TIMESTAMP '{dateTime:yyyy-MM-dd HH:mm:ss.fffffff}'";
    }

    /// <summary>格式化模糊搜索。Presto/Trino 默认无转义字符，含通配符时需要显式 ESCAPE</summary>
    /// <param name="column">字段</param>
    /// <param name="format">格式化字符串</param>
    /// <param name="value">数值</param>
    /// <returns></returns>
    public override String FormatLike(IDataColumn column, String format, String value)
    {
        var line = base.FormatLike(column, format, value);
        if (line.IsNullOrEmpty() || value.IsNullOrEmpty()) return line;

        if (value.IndexOfAny(['%', '_']) < 0) return line;

        return format.Contains("{1}") ? line + " Escape '\\'" : line;
    }

    /// <summary>长文本长度。Presto/Trino 的 varchar 不限定长度</summary>
    public override Int32 LongTextLength => Int32.MaxValue;

    /// <summary>参数前缀。未启用参数化，仅保持接口一致</summary>
    protected internal override String ParamPrefix => "@";

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => $"concat({(!left.IsNullOrEmpty() ? left : "''")},{(!right.IsNullOrEmpty() ? right : "''")})";
    #endregion

    #region 分页
    /// <summary>已重写。Presto/Trino 使用 OFFSET/LIMIT 分页</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <param name="keyColumn">主键列</param>
    /// <returns></returns>
    public override String PageSplit(String sql, Int64 startRowIndex, Int64 maximumRows, String? keyColumn)
    {
        if (startRowIndex <= 0)
        {
            if (maximumRows < 1) return sql;

            return $"{sql} limit {maximumRows}";
        }
        if (maximumRows < 1) throw new NotSupportedException(L.T("Fetching all rows after a given row is not supported!"));

        return $"{sql} offset {startRowIndex} limit {maximumRows}";
    }

    /// <summary>构造分页SQL</summary>
    /// <param name="builder">查询生成器</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns>分页SQL</returns>
    public override SelectBuilder PageSplit(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows)
    {
        if (startRowIndex <= 0)
        {
            if (maximumRows > 0) builder.Limit = $"limit {maximumRows}";
            return builder;
        }
        if (maximumRows < 1) throw new NotSupportedException(L.T("Fetching all rows after a given row is not supported!"));

        builder.Limit = $"offset {startRowIndex} limit {maximumRows}";
        return builder;
    }
    #endregion
}

/// <summary>Presto数据库。使用 NReco.PrestoAdo 驱动</summary>
internal class Presto : PrestoBase
{
    #region 属性
    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.Presto;
    #endregion

    #region 方法
    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory()
    {
        var type = DriverLoader.Load("NReco.PrestoAdo.PrestoDbFactory", null, "NReco.PrestoAdo.dll", null);
        var factory = GetProviderFactory(type);
        if (factory != null) return factory;

        return GetProviderFactory(null, "NReco.PrestoAdo.dll", "NReco.PrestoAdo.PrestoDbFactory", true, true);
    }

    /// <summary>是否支持该提供者所描述的数据库</summary>
    /// <param name="providerName">提供者</param>
    /// <returns></returns>
    public override Boolean Support(String providerName)
    {
        providerName = providerName.ToLower();
        if (providerName.Contains("presto")) return true;

        return false;
    }
    #endregion

    #region 数据库特性
    /// <summary>设置连接字符串。NReco 驱动使用 Host/Port/Catalog/Schema/User/Password/UseSsl</summary>
    /// <param name="builder"></param>
    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        if (builder["UseSsl"].IsNullOrEmpty()) builder["UseSsl"] = "false";
        if (builder["Port"].IsNullOrEmpty()) builder["Port"] = "8080";

        // 固定使用 Presto 协议头，Trino 请使用 DatabaseType.Trino
        builder["TrinoHeaders"] = "false";
    }
    #endregion
}

/// <summary>Trino数据库。使用官方 Trino.Data.ADO 驱动</summary>
internal class Trino : PrestoBase
{
    #region 属性
    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.Trino;
    #endregion

    #region 方法
    /// <summary>创建工厂</summary>
    /// <remarks>Trino 官方 ADO.NET 客户端没有提供 DbProviderFactory，这里用反射工厂适配</remarks>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory()
    {
        var connType = DriverLoader.Load("Trino.Data.ADO.Server.TrinoConnection", null, "Trino.Data.ADO.dll", null);
        if (connType == null) return null;

        var cmdType = DriverLoader.Load("Trino.Data.ADO.Server.TrinoCommand", null, "Trino.Data.ADO.dll", null);
        var prmType = DriverLoader.Load("Trino.Data.ADO.TrinoParameter", null, "Trino.Data.ADO.dll", null);
        if (cmdType == null || prmType == null) return null;

        return new TrinoDbFactory(connType, cmdType, prmType);
    }

    /// <summary>是否支持该提供者所描述的数据库</summary>
    /// <param name="providerName">提供者</param>
    /// <returns></returns>
    public override Boolean Support(String providerName)
    {
        providerName = providerName.ToLower();
        if (providerName.Contains("trino")) return true;

        return false;
    }
    #endregion

    #region 数据库特性
    /// <summary>设置连接字符串。官方驱动使用 Host/Port/Catalog/Schema/User/EnableSSL/Auth</summary>
    /// <param name="builder"></param>
    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        if (builder["EnableSSL"].IsNullOrEmpty()) builder["EnableSSL"] = "false";

        // 官方驱动默认 443（SSL）；仅在非 SSL 且未指定端口时补 8080
        if (builder["Port"].IsNullOrEmpty() && !builder["EnableSSL"].ToBoolean()) builder["Port"] = "8080";

        // 打开连接时不做额外探测，避免每次建连都多一次 HTTP 往返
        if (builder["TestConnection"].IsNullOrEmpty()) builder["TestConnection"] = "false";
    }
    #endregion
}

/// <summary>Trino 反射工厂。官方驱动未提供 DbProviderFactory</summary>
internal sealed class TrinoDbFactory : DbProviderFactory
{
    private readonly Type _connType;
    private readonly Type _cmdType;
    private readonly Type _prmType;

    public TrinoDbFactory(Type connType, Type cmdType, Type prmType)
    {
        _connType = connType;
        _cmdType = cmdType;
        _prmType = prmType;
    }

    /// <summary>创建连接</summary>
    /// <returns></returns>
    [UnconditionalSuppressMessage("AOT", "IL2077", Justification = "Trino 驱动类型由 DriverLoader 在运行时解析，AOT 下不支持该动态驱动路径")]
    public override DbConnection CreateConnection() => (DbConnection)Activator.CreateInstance(_connType)!;

    /// <summary>创建命令。TrinoCommand 没有无参构造，先绑定一个临时连接，随后由 XCode 重新赋值</summary>
    /// <returns></returns>
    [UnconditionalSuppressMessage("AOT", "IL2077", Justification = "Trino 驱动类型由 DriverLoader 在运行时解析，AOT 下不支持该动态驱动路径")]
    public override DbCommand CreateCommand()
    {
        var conn = Activator.CreateInstance(_connType);

        return (DbCommand)Activator.CreateInstance(_cmdType, conn)!;
    }

    /// <summary>创建参数</summary>
    /// <returns></returns>
    [UnconditionalSuppressMessage("AOT", "IL2077", Justification = "Trino 驱动类型由 DriverLoader 在运行时解析，AOT 下不支持该动态驱动路径")]
    public override DbParameter CreateParameter() => (DbParameter)Activator.CreateInstance(_prmType)!;

    /// <summary>创建连接字符串生成器</summary>
    /// <returns></returns>
    public override DbConnectionStringBuilder CreateConnectionStringBuilder() => new();
}

/// <summary>Presto/Trino 数据库会话</summary>
internal class PrestoSession : RemoteDbSession
{
    #region 构造函数
    public PrestoSession(IDatabase db) : base(db) { }
    #endregion

    #region 基本方法 查询/执行
    /// <summary>执行插入语句并返回新增行的自动编号。Presto/Trino 没有自增主键</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    /// <exception cref="NotSupportedException">Presto/Trino 不支持自增列</exception>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
        => throw new NotSupportedException(L.T("Presto/Trino does not support auto-increment columns; use a business key (Guid/snowflake) as the primary key, or set AllowInsertIdentity."));

    /// <summary>执行插入语句并返回新增行的自动编号。Presto/Trino 没有自增主键</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    /// <exception cref="NotSupportedException">Presto/Trino 不支持自增列</exception>
    public override Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        var tcs = new TaskCompletionSource<Int64>();
        tcs.SetException(new NotSupportedException(L.T("Presto/Trino does not support auto-increment columns; use a business key (Guid/snowflake) as the primary key, or set AllowInsertIdentity.")));

        return tcs.Task;
    }
    #endregion

    #region 事务
    /// <summary>开始事务。Presto/Trino 没有事务，直接抛出明确异常</summary>
    /// <param name="level">事务隔离级别</param>
    /// <returns></returns>
    public override Int32 BeginTransaction(IsolationLevel level) => throw new NotSupportedException(L.T("Presto/Trino does not support transactions; the underlying connector may provide limited write semantics only."));
    #endregion
}

/// <summary>Presto/Trino 数据库元数据</summary>
internal class PrestoMetaData : RemoteDbMetaData
{
    #region 构造函数
    public PrestoMetaData() => Types = _DataTypes;
    #endregion

    #region 数据类型
    /// <summary>数据类型映射</summary>
    private static readonly Dictionary<Type, String[]> _DataTypes = new()
    {
        { typeof(Byte[]), ["varbinary"] },
        { typeof(Boolean), ["boolean"] },
        { typeof(Byte), ["tinyint"] },
        { typeof(Int16), ["smallint"] },
        { typeof(Int32), ["integer"] },
        { typeof(Int64), ["bigint"] },
        { typeof(Single), ["real"] },
        { typeof(Double), ["double"] },
        { typeof(Decimal), ["decimal({0},{1})", "decimal"] },
        { typeof(DateTime), ["timestamp(3)", "timestamp with time zone"] },
        { typeof(String), ["varchar"] },
    };

    /// <summary>获取Net数据类型。复杂类型（array/map/row/json）统一降级为字符串</summary>
    /// <param name="field"></param>
    /// <returns></returns>
    protected override Type? GetDataType(IDataColumn field)
    {
        var type = base.GetDataType(field);
        if (type != null) return type;

        var rawType = field.RawType;
        if (rawType.IsNullOrEmpty()) return null;

        if (rawType.StartsWithIgnoreCase("array(", "map(", "row(", "json")) return typeof(String);

        return null;
    }
    #endregion

    #region 表信息
    /// <summary>快速取得所有表名</summary>
    /// <returns></returns>
    public override IList<String> GetTableNames() => OnGetTables(null).Select(e => e.TableName).Where(e => !e.IsNullOrEmpty()).ToList();

    /// <summary>获取所有表</summary>
    /// <param name="names">指定表名</param>
    /// <returns></returns>
    protected override List<IDataTable> OnGetTables(String[]? names)
    {
        var list = new List<IDataTable>();

        var schema = (Database as PrestoBase)?.SchemaName;
        var where = "";
        if (!schema.IsNullOrEmpty()) where = $" where table_schema = '{schema.Replace("'", "''")}'";
        if (names != null && names.Length > 0)
        {
            var ns = names.Where(e => !e.IsNullOrEmpty()).Select(e => "'" + e.Replace("'", "''") + "'").ToArray();
            if (ns.Length > 0) where += where.IsNullOrEmpty() ? " where" : " and";
            if (ns.Length > 0) where += " table_name in (" + String.Join(",", ns) + ")";
        }

        var ss = Database.CreateSession();
        var old = ss.ShowSQL;
        ss.ShowSQL = false;
        try
        {
            var tables = ss.Query($"select table_schema, table_name, table_type from information_schema.tables{where} order by table_schema, table_name", CommandType.Text);
            if (tables.Tables.Count == 0) return list;

            var dt = tables.Tables[0];
            if (dt.Rows.Count == 0) return list;

            var columns = ss.Query($"select table_schema, table_name, column_name, ordinal_position, data_type, is_nullable, column_default, character_maximum_length, numeric_precision, numeric_scale from information_schema.columns{where} order by table_schema, table_name, ordinal_position", CommandType.Text);

            // 按 模式.表名 分组，避免同名表跨模式串数据
            var dic = new Dictionary<String, List<DataRow>>(StringComparer.OrdinalIgnoreCase);
            if (columns.Tables.Count > 0)
            {
                foreach (DataRow dr in columns.Tables[0].Rows)
                {
                    var key = $"{dr["table_schema"]}.{dr["table_name"]}";
                    if (!dic.TryGetValue(key, out var rows)) dic[key] = rows = [];
                    rows.Add(dr);
                }
            }

            foreach (DataRow dr in dt.Rows)
            {
                var table = DAL.CreateTable();
                table.TableName = dr["table_name"] + "";
                table.Owner = dr["table_schema"] + "";
                table.IsView = (dr["table_type"] + "").EqualIgnoreCase("VIEW");
                table.DbType = Database.Type;

                if (dic.TryGetValue($"{dr["table_schema"]}.{dr["table_name"]}", out var rows))
                {
                    foreach (var cr in rows)
                    {
                        var field = table.CreateColumn();
                        field.ColumnName = cr["column_name"] + "";
                        field.RawType = cr["data_type"] + "";
                        field.Length = GetInt32(cr, "character_maximum_length");
                        field.Precision = GetInt32(cr, "numeric_precision");
                        field.Scale = GetInt32(cr, "numeric_scale");
                        field.Nullable = (cr["is_nullable"] + "").EqualIgnoreCase("YES");
                        if (cr["column_default"] is not DBNull) field.DefaultValue = cr["column_default"] + "";

                        // 约束均为信息性，Presto/Trino 没有自增主键
                        field.Identity = false;
                        field.PrimaryKey = false;

                        field.DataType = GetDataType(field) ?? typeof(String);

                        field.Fix();
                        table.Columns.Add(field);
                    }
                }

                table.Fix();

                list.Add(table);
            }
        }
        finally
        {
            ss.ShowSQL = old;
            ss.Dispose();
        }

        return list;
    }

    private static Int32 GetInt32(DataRow dr, String name)
    {
        if (dr.Table.Columns.Contains(name) && !dr.IsNull(name) && Int32.TryParse(dr[name] + "", out var n)) return n;

        return 0;
    }
    #endregion

    #region 数据定义
    /// <summary>Presto/Trino 不支持自动建表与结构迁移</summary>
    private const String NotSupportedMessage = "Presto/Trino does not support automatic schema migration. Keep Migration=Off and create tables with the connector's own tooling.";

    /// <summary>获取数据定义语句。DDL 全部不支持</summary>
    /// <param name="schema">数据定义模式</param>
    /// <param name="values">其它信息</param>
    /// <returns></returns>
    public override String? GetSchemaSQL(DDLSchema schema, params Object?[] values) => schema switch
    {
        DDLSchema.CreateTable or DDLSchema.DropTable or DDLSchema.AddColumn or DDLSchema.AlterColumn or
        DDLSchema.RenameColumn or DDLSchema.DropColumn or DDLSchema.CreateIndex or DDLSchema.DropIndex or
        DDLSchema.CreateDatabase or DDLSchema.DropDatabase => throw new NotSupportedException(NotSupportedMessage),
        DDLSchema.AddTableDescription or DDLSchema.DropTableDescription or DDLSchema.AddColumnDescription or DDLSchema.DropColumnDescription => null,
        _ => base.GetSchemaSQL(schema, values),
    };

    /// <summary>建立表。Presto/Trino 不支持</summary>
    /// <param name="table">表模型</param>
    /// <returns></returns>
    public override Boolean CreateTable(IDataTable table) => throw new NotSupportedException(NotSupportedMessage);

    /// <summary>删除表。Presto/Trino 不支持</summary>
    /// <param name="table">表模型</param>
    /// <returns></returns>
    public override Boolean DropTable(IDataTable table) => throw new NotSupportedException(NotSupportedMessage);
    #endregion
}
