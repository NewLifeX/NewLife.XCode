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

    /// <summary>创建能力配置。Presto/Trino 为只读分析 + 显式写入，不支持事务与自动建表</summary>
    /// <returns>能力配置</returns>
    protected override DatabaseProfile CreateProfile() => new()
    {
        Model = DatabaseModel.Federated,
        QueryLanguage = QueryLanguage.Sql,
        Capabilities = DatabaseCapability.Sql
            | DatabaseCapability.Joins
            | DatabaseCapability.Subqueries
            | DatabaseCapability.GroupBy
            | DatabaseCapability.Having
            | DatabaseCapability.Union
            | DatabaseCapability.OffsetPaging
            | DatabaseCapability.LimitPaging
            | DatabaseCapability.SchemaDiscovery
            | DatabaseCapability.WindowFunctions
            | DatabaseCapability.WindowFrame,
        MaxMigration = Migration.Off,
        LongTextLength = 4000,
        ParameterPrefix = "@",
    };

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
    public override String FormatKeyWord(String keyWord) => FormatKeyWord(keyWord, "\"");

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
    public override String FormatDateTime(IDataColumn column, DateTime dateTime) => FormatDateTimeLiteral(dateTime);

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
    public override String PageSplit(String sql, Int64 startRowIndex, Int64 maximumRows, String? keyColumn) => PageSplitByLimitOffset(sql, startRowIndex, maximumRows, true);

    /// <summary>构造分页SQL</summary>
    /// <param name="builder">查询生成器</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns>分页SQL</returns>
    public override SelectBuilder PageSplit(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows) => PageSplitByLimitOffset(builder, startRowIndex, maximumRows, true);
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
    protected override DbProviderFactory? CreateFactory() => LoadFactory("NReco.PrestoAdo.PrestoDbFactory", "NReco.PrestoAdo.dll");

    /// <summary>是否支持该提供者所描述的数据库</summary>
    /// <param name="providerName">提供者</param>
    /// <returns></returns>
    public override Boolean Support(String providerName) => providerName.ToLower().Contains("presto");
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
    public override Boolean Support(String providerName) => providerName.ToLower().Contains("trino");
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
internal sealed class TrinoDbFactory(Type connType, Type cmdType, Type prmType) : DbProviderFactory
{
    /// <summary>创建连接</summary>
    /// <returns></returns>
    [UnconditionalSuppressMessage("AOT", "IL2067", Justification = "Trino 驱动类型由 DriverLoader 在运行时解析，AOT 下不支持该动态驱动路径")]
    [UnconditionalSuppressMessage("AOT", "IL2077", Justification = "Trino 驱动类型由 DriverLoader 在运行时解析，AOT 下不支持该动态驱动路径")]
    public override DbConnection CreateConnection() => (DbConnection)Activator.CreateInstance(connType)!;

    /// <summary>创建命令。TrinoCommand 没有无参构造，先绑定一个临时连接，随后由 XCode 重新赋值</summary>
    /// <returns></returns>
    [UnconditionalSuppressMessage("AOT", "IL2067", Justification = "Trino 驱动类型由 DriverLoader 在运行时解析，AOT 下不支持该动态驱动路径")]
    [UnconditionalSuppressMessage("AOT", "IL2077", Justification = "Trino 驱动类型由 DriverLoader 在运行时解析，AOT 下不支持该动态驱动路径")]
    public override DbCommand CreateCommand()
    {
        var conn = Activator.CreateInstance(connType);

        return (DbCommand)Activator.CreateInstance(cmdType, conn)!;
    }

    /// <summary>创建参数</summary>
    /// <returns></returns>
    [UnconditionalSuppressMessage("AOT", "IL2067", Justification = "Trino 驱动类型由 DriverLoader 在运行时解析，AOT 下不支持该动态驱动路径")]
    [UnconditionalSuppressMessage("AOT", "IL2077", Justification = "Trino 驱动类型由 DriverLoader 在运行时解析，AOT 下不支持该动态驱动路径")]
    public override DbParameter CreateParameter() => (DbParameter)Activator.CreateInstance(prmType)!;

    /// <summary>创建连接字符串生成器</summary>
    /// <returns></returns>
    public override DbConnectionStringBuilder CreateConnectionStringBuilder() => new();
}

/// <summary>Presto/Trino 数据库会话</summary>
internal class PrestoSession(IDatabase db) : RemoteDbSession(db)
{
    #region 基本方法 查询/执行
    /// <summary>执行查询并装配结果集</summary>
    /// <remarks>
    /// NReco 驱动对 varchar 字段返回 ColumnSize=DBNull，DataTable.Load 会据此把 MaxLength 置为 0，
    /// 导致任何非空字符串都被判为超长。这里先按字段类型建列再加载数据，保持 MaxLength 默认值。
    /// </remarks>
    /// <param name="cmd">命令</param>
    /// <returns></returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "驱动不提供DataAdapter，采用DataReader装配结果集；仅在裁剪下需要DataTable相关成员，数据库访问本身依赖运行时驱动加载，不承诺裁剪后可用")]
    public override DataSet Query(DbCommand cmd) => Execute(cmd, true, cmd2 =>
    {
        var ds = new DataSet();

        using var reader = cmd2.ExecuteReader();
        do
        {
            var table = ReadTable(reader);
            if (table.Columns.Count > 0) ds.Tables.Add(table);
        } while (!reader.IsClosed && reader.NextResult());

        return ds;
    });

    private static DataTable ReadTable(DbDataReader reader)
    {
        var table = new DataTable();

        var schema = reader.GetSchemaTable();
        if (schema != null)
        {
            foreach (DataRow row in schema.Rows)
            {
                var name = row["ColumnName"] + "";
                if (name.IsNullOrEmpty()) name = "Column" + table.Columns.Count;
                while (table.Columns.Contains(name)) name += "_";

                var type = row["DataType"] as Type ?? typeof(String);
                table.Columns.Add(name, type);
            }
        }

        table.Load(reader);

        return table;
    }

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
        => FromException(new NotSupportedException(L.T("Presto/Trino does not support auto-increment columns; use a business key (Guid/snowflake) as the primary key, or set AllowInsertIdentity.")));
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
    public override IList<String> GetTableNames()
    {
        var where = SqlMetaDataHelper.BuildWhere((Database as PrestoBase)?.SchemaName, null);
        using var ss = Database.CreateSession();
        var ds = ss.Query($"select table_name from information_schema.tables{where}", CommandType.Text);
        if (ds.Tables.Count == 0) return [];

        return ds.Tables[0].Rows.Cast<DataRow>()
            .Select(e => e["table_name"] + "")
            .Where(e => !e.IsNullOrEmpty())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>获取所有表</summary>
    /// <param name="names">指定表名</param>
    /// <returns></returns>
    protected override List<IDataTable> OnGetTables(String[]? names)
    {
        var schema = (Database as PrestoBase)?.SchemaName;
        var list = new List<IDataTable>();

        // 部分老版 Hive Metastore 不支持一次性列出整库字段（get_table_req），先取表清单，再按表名分批查字段
        var tableNames = names;
        if (tableNames == null || tableNames.Length == 0)
        {
            var where = SqlMetaDataHelper.BuildWhere(schema, null);
            using var ss = Database.CreateSession();
            var ds = ss.Query($"select table_name from information_schema.tables{where}", CommandType.Text);
            if (ds.Tables.Count == 0) return list;

            tableNames = ds.Tables[0].Rows.Cast<DataRow>()
                .Select(e => e["table_name"] + "")
                .Where(e => !e.IsNullOrEmpty())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        // 分批查询，避免单条 SQL 携带过多表名
        const Int32 batchSize = 100;
        for (var i = 0; i < tableNames.Length; i += batchSize)
        {
            var batch = tableNames.Skip(i).Take(batchSize).ToArray();
            if (!TryGetTables(schema, batch, list) && batch.Length > 1)
            {
                // 单个坏表（如无法解析的 Hive 视图）会让整批失败，退化为逐表查询并跳过坏表
                foreach (var name in batch)
                {
                    TryGetTables(schema, [name], list);
                }
            }
        }

        list.Sort((x, y) =>
        {
            var rs = String.Compare(x.Owner, y.Owner, StringComparison.OrdinalIgnoreCase);

            return rs != 0 ? rs : String.Compare(x.TableName, y.TableName, StringComparison.OrdinalIgnoreCase);
        });

        return list;
    }

    private Boolean TryGetTables(String? schema, String[] names, List<IDataTable> list)
    {
        try
        {
            var where = SqlMetaDataHelper.BuildWhere(schema, names);

            // 部分 Presto 开启 strict mode 后禁止无 limit 的排序，元数据查询不排序，取回后在内存里排
            var tables = $"select table_schema, table_name, table_type from information_schema.tables{where}";
            var columns = $"select table_schema, table_name, column_name, ordinal_position, data_type, is_nullable, column_default from information_schema.columns{where}";

            list.AddRange(SqlMetaDataHelper.GetTables(Database, tables, columns, GetDataType));

            return true;
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Presto metadata query failed for [{0}]: {1}", String.Join(",", names), ex.Message);

            return false;
        }
    }
    #endregion

    #region 数据定义
    /// <summary>Presto/Trino 不支持自动建表与结构迁移</summary>
    private const String NotSupportedMessage = "Presto/Trino does not support automatic schema migration. Keep Migration=Off and create tables with the connector's own tooling.";

    /// <summary>获取数据定义语句。DDL 全部不支持</summary>
    /// <param name="command">DDL 命令</param>
    /// <returns></returns>
    public override String? GetCommandSql(DDLCommand command) => command.Schema switch
    {
        DDLSchema.CreateTable or DDLSchema.DropTable or DDLSchema.AddColumn or DDLSchema.AlterColumn or
        DDLSchema.RenameColumn or DDLSchema.DropColumn or DDLSchema.CreateIndex or DDLSchema.DropIndex or
        DDLSchema.CreateDatabase or DDLSchema.DropDatabase => throw new NotSupportedException(NotSupportedMessage),
        DDLSchema.AddTableDescription or DDLSchema.DropTableDescription or DDLSchema.AddColumnDescription or DDLSchema.DropColumnDescription => null,
        _ => base.GetCommandSql(command),
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
