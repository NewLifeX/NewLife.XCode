using System.Data;
using System.Data.Common;
using NewLife.Log;

namespace XCode.DataAccessLayer;

/// <summary>Google Cloud Spanner数据库</summary>
/// <remarks>
/// Cloud Spanner 是 Google 的全球分布式关系型数据库，SQL 方言为 GoogleSQL。
/// 连接字符串示例：Data Source=projects/p/instances/i/databases/d
/// 使用 Google.Cloud.Spanner.Data 官方 ADO.NET 驱动。
/// 类型体系与常规数据库差异较大（INT64/STRING(MAX)/ARRAY），不支持自动建表，强制 Migration=Off。
/// </remarks>
internal class Spanner : RemoteDb
{
    #region 属性
    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.Spanner;

    /// <summary>系统数据库名。Spanner 没有可切换的系统库</summary>
    public override String SystemDatabaseName => String.Empty;

    /// <summary>默认名称格式。Spanner 标识符按创建时大小写保存</summary>
    public override NameFormats DefaultNameFormat => NameFormats.None;

    /// <summary>创建能力配置。Spanner 支持事务与查询，但不支持 XCode 自动建表</summary>
    /// <returns>能力配置</returns>
    protected override DatabaseProfile CreateProfile() => new()
    {
        Model = DatabaseModel.Relational,
        QueryLanguage = QueryLanguage.Sql,
        Capabilities = DatabaseCapability.Sql
            | DatabaseCapability.Transactions
            | DatabaseCapability.Insert
            | DatabaseCapability.Update
            | DatabaseCapability.Delete
            | DatabaseCapability.Parameterization
            | DatabaseCapability.SchemaDiscovery
            | DatabaseCapability.Joins
            | DatabaseCapability.Subqueries
            | DatabaseCapability.GroupBy
            | DatabaseCapability.Having
            | DatabaseCapability.Union
            | DatabaseCapability.OffsetPaging
            | DatabaseCapability.LimitPaging
            | DatabaseCapability.WindowFunctions
            | DatabaseCapability.WindowFrame,
        MaxMigration = Migration.Off,
        LongTextLength = 4000,
        ParameterPrefix = "@",
    };
    #endregion

    #region 构造
    public Spanner()
    {
        // Spanner 的主键必须作为表级子句声明，通用建表 SQL 不可用
        Migration = DataAccessLayer.Migration.Off;
    }
    #endregion

    #region 方法
    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory() => LoadFactory("Google.Cloud.Spanner.Data.SpannerProviderFactory", "Google.Cloud.Spanner.Data.dll");

    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new SpannerSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new SpannerMetaData();

    /// <summary>是否支持该提供者所描述的数据库</summary>
    /// <param name="providerName">提供者</param>
    /// <returns></returns>
    public override Boolean Support(String providerName) => providerName.ToLower().Contains("spanner");

    /// <summary>设置连接字符串。Spanner 使用 Data Source 指定实例下的数据库</summary>
    /// <param name="builder"></param>
    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        // 驱动不认识 XCode 的数据库名写法，统一移除
        builder.TryGetAndRemove("Database", out _);
        builder.TryGetAndRemove("Db", out _);
        builder.TryGetAndRemove("Initial Catalog", out _);
    }
    #endregion

    #region 数据库特性
    /// <summary>关键字列表</summary>
    protected override String ReservedWordsStr =>
        "ALL,AND,ANY,ARRAY,AS,ASC,ASSERT_ROWS_MODIFIED,AT,BETWEEN,BY,CASE,CAST,COLLATE,CONTAINS,CREATE,CROSS,CUBE,CURRENT,DEFAULT,DEFINE,DESC,DISTINCT,ELSE,END,ENUM,ESCAPE,EXCEPT,EXCLUDE,EXISTS,EXTRACT,FALSE,FETCH,FOLLOWING,FOR,FROM,FULL,GROUP,GROUPING,GROUPS,HASH,HAVING,IF,IGNORE,IN,INNER,INTERSECT,INTERVAL,INTO,IS,JOIN,LATERAL,LEFT,LIKE,LIMIT,LOOKUP,MERGE,NATURAL,NEW,NO,NOT,NULL,NULLS,OF,ON,OR,ORDER,OUTER,OVER,PARTITION,PRECEDING,PROTO,QUALIFY,RANGE,RECURSIVE,RESPECT,RIGHT,ROLLUP,ROWS,SELECT,SET,SOME,STRUCT,TABLESAMPLE,THEN,TO,TREAT,TRUE,UNBOUNDED,UNION,UNNEST,USING,WHEN,WHERE,WINDOW,WITH,WITHIN," +
        "LOG,User,Role,Admin,Rank,Member,Groups,Error,MaxValue,MinValue,Signal";

    /// <summary>格式化标识符。Spanner 使用反引号</summary>
    /// <param name="keyWord">关键字</param>
    /// <returns></returns>
    public override String FormatKeyWord(String keyWord) => FormatKeyWord(keyWord, "`");

    /// <summary>格式化数据为SQL数据</summary>
    /// <param name="field">字段</param>
    /// <param name="value">数值</param>
    /// <returns></returns>
    public override String FormatValue(IDataColumn field, Object? value)
    {
        if (value != null && field?.DataType == typeof(Boolean)) return value.ToBoolean() ? "TRUE" : "FALSE";

        return base.FormatValue(field!, value);
    }

    /// <summary>格式化时间为SQL字符串</summary>
    /// <param name="column">字段</param>
    /// <param name="dateTime">时间值</param>
    /// <returns></returns>
    public override String FormatDateTime(IDataColumn column, DateTime dateTime) => FormatDateTimeLiteral(dateTime);

    /// <summary>长文本长度。Spanner STRING 上限 2621440 字符</summary>
    public override Int32 LongTextLength => 2621440;

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => (!left.IsNullOrEmpty() ? left : "''") + "||" + (!right.IsNullOrEmpty() ? right : "''");
    #endregion

    #region 分页
    /// <summary>已重写。Spanner 使用 LIMIT/OFFSET 分页</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <param name="keyColumn">主键列</param>
    /// <returns></returns>
    public override String PageSplit(String sql, Int64 startRowIndex, Int64 maximumRows, String? keyColumn) => PageSplitByLimitOffset(sql, startRowIndex, maximumRows);

    /// <summary>构造分页SQL</summary>
    /// <param name="builder">查询生成器</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns>分页SQL</returns>
    public override SelectBuilder PageSplit(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows) => PageSplitByLimitOffset(builder, startRowIndex, maximumRows);
    #endregion
}

/// <summary>Google Cloud Spanner数据库会话</summary>
internal class SpannerSession(IDatabase db) : RemoteDbSession(db)
{
    #region 基本方法 查询/执行
    /// <summary>Spanner 无法回读自增列，进入自增路径时给出明确异常</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    /// <exception cref="NotSupportedException">Spanner 不支持回读自增列</exception>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
        => throw IdentityNotSupported();

    /// <summary>Spanner 无法回读自增列，进入自增路径时给出明确异常</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public override Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
        => FromException(IdentityNotSupported());
    #endregion
}

/// <summary>Google Cloud Spanner数据库元数据</summary>
internal class SpannerMetaData : RemoteDbMetaData
{
    #region 构造函数
    public SpannerMetaData() => Types = _DataTypes;
    #endregion

    #region 数据类型
    /// <summary>数据类型映射</summary>
    private static readonly Dictionary<Type, String[]> _DataTypes = new()
    {
        { typeof(Byte[]), ["BYTES(MAX)"] },
        { typeof(Boolean), ["BOOL"] },
        { typeof(Int64), ["INT64"] },
        { typeof(Int32), ["INT64"] },
        { typeof(Int16), ["INT64"] },
        { typeof(Byte), ["INT64"] },
        { typeof(Single), ["FLOAT64"] },
        { typeof(Double), ["FLOAT64"] },
        { typeof(Decimal), ["NUMERIC"] },
        { typeof(DateTime), ["TIMESTAMP", "DATE"] },
        { typeof(String), ["STRING(MAX)", "STRING"] },
    };

    /// <summary>获取Net数据类型</summary>
    /// <param name="field"></param>
    /// <returns></returns>
    protected override Type? GetDataType(IDataColumn field)
    {
        var type = base.GetDataType(field);
        if (type != null) return type;

        var raw = (field.RawType + "").Trim();
        if (raw.StartsWithIgnoreCase("ARRAY", "JSON")) return typeof(String);

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
        var where = SqlMetaDataHelper.BuildWhere(null, names);
        var tables = $"select table_schema, table_name, table_type from information_schema.tables{where} order by table_schema, table_name";
        var columns = "select table_schema, table_name, column_name, ordinal_position, spanner_type as data_type, is_nullable, " +
            "null as column_default, null as character_maximum_length, null as numeric_precision, null as numeric_scale " +
            $"from information_schema.columns{where} order by table_schema, table_name, ordinal_position";

        return SqlMetaDataHelper.GetTables(Database, tables, columns, GetDataType);
    }
    #endregion

    #region 数据定义
    /// <summary>Spanner 不支持通用建表与结构迁移</summary>
    private const String NotSupportedMessage = "Spanner does not support automatic schema migration; declare primary keys in table options and create tables with DDL scripts.";

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

    /// <summary>建立表。Spanner 不支持</summary>
    /// <param name="table">表模型</param>
    /// <returns></returns>
    public override Boolean CreateTable(IDataTable table) => throw new NotSupportedException(NotSupportedMessage);

    /// <summary>删除表。Spanner 不支持</summary>
    /// <param name="table">表模型</param>
    /// <returns></returns>
    public override Boolean DropTable(IDataTable table) => throw new NotSupportedException(NotSupportedMessage);
    #endregion
}
