using System.Data;
using System.Data.Common;
using NewLife.Log;

namespace XCode.DataAccessLayer;

/// <summary>Snowflake数据库</summary>
/// <remarks>
/// Snowflake 是云原生数据仓库，SQL 方言接近 ANSI SQL。
/// 连接字符串示例：account=xxx;user=xxx;password=xxx;db=TESTDB;schema=PUBLIC;warehouse=WH
/// 使用 Snowflake.Data 官方 ADO.NET 驱动。
/// 未加引号的标识符折叠为大写，自增列无法回读，建议使用业务主键。
/// </remarks>
internal class Snowflake : RemoteDb
{
    #region 属性
    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.Snowflake;

    /// <summary>系统数据库名</summary>
    public override String SystemDatabaseName => "SNOWFLAKE";

    /// <summary>默认名称格式。Snowflake 未加引号的标识符折叠为大写</summary>
    public override NameFormats DefaultNameFormat => NameFormats.Upper;

    /// <summary>模式名</summary>
    public String? SchemaName { get; set; }

    #endregion

    #region 方法
    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory() => LoadFactory("Snowflake.Data.Client.SnowflakeDbFactory", "Snowflake.Data.dll");

    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new SnowflakeSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new SnowflakeMetaData();

    /// <summary>是否支持该提供者所描述的数据库</summary>
    /// <param name="providerName">提供者</param>
    /// <returns></returns>
    public override Boolean Support(String providerName) => providerName.ToLower().Contains("snowflake");

    /// <summary>设置连接字符串</summary>
    /// <param name="builder"></param>
    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        // XCode 的 Database 对应 Snowflake 的 db，Owner 对应 schema
        var dbName = builder["Database"];
        if (dbName.IsNullOrEmpty()) dbName = builder["Db"];
        if (dbName.IsNullOrEmpty()) dbName = builder["Initial Catalog"];
        if (dbName.IsNullOrEmpty()) dbName = DatabaseName;

        var schema = builder["schema"];
        if (schema.IsNullOrEmpty()) schema = Owner;

        // 先移除 XCode 的数据库名写法，再写入 db，避免 Db 与 db 在大小写不敏感的字典中互相覆盖
        builder.TryGetAndRemove("Database", out _);
        builder.TryGetAndRemove("Db", out _);
        builder.TryGetAndRemove("Initial Catalog", out _);

        if (!dbName.IsNullOrEmpty()) builder["db"] = dbName;
        if (!schema.IsNullOrEmpty()) builder["schema"] = schema;

        SchemaName = schema;
    }
    #endregion

    #region 数据库特性
    /// <summary>关键字列表</summary>
    protected override String ReservedWordsStr =>
        "ACCOUNT,ALL,ALTER,AND,ANY,AS,BETWEEN,BY,CASE,CAST,CHECK,COLUMN,CONNECT,CONNECTION,CONSTRAINT,CREATE,CROSS,CURRENT,CURRENT_DATE,CURRENT_TIME,CURRENT_TIMESTAMP,CURRENT_USER,DATABASE,DELETE,DISTINCT,DROP,ELSE,EXISTS,FALSE,FOLLOWING,FOR,FROM,FULL,GRANT,GROUP,HAVING,ILIKE,IN,INCREMENT,INNER,INSERT,INTERSECT,INTO,IS,JOIN,LATERAL,LEFT,LIKE,LOCALTIME,LOCALTIMESTAMP,MINUS,NATURAL,NOT,NULL,OF,ON,OR,ORDER,QUALIFY,REGEXP,REVOKE,RIGHT,RLIKE,ROW,ROWS,SAMPLE,SCHEMA,SELECT,SET,SOME,START,TABLE,TABLESAMPLE,THEN,TO,TRIGGER,TRUE,UNION,UNIQUE,UPDATE,USING,VALUES,VIEW,WHEN,WHENEVER,WHERE,WITH," +
        "LOG,User,Role,Admin,Rank,Member,Groups,Error,MaxValue,MinValue,Signal,Offset";

    /// <summary>格式化标识符。Snowflake 使用双引号</summary>
    /// <param name="keyWord">关键字</param>
    /// <returns></returns>
    public override String FormatKeyWord(String keyWord) => FormatKeyWord(keyWord, "\"");

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
    public override String FormatDateTime(IDataColumn column, DateTime dateTime) => $"TO_TIMESTAMP_NTZ({base.FormatDateTime(column, dateTime)})";

    /// <summary>长文本长度。Snowflake VARCHAR 上限 16MB</summary>
    public override Int32 LongTextLength => 16777216;

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => (!left.IsNullOrEmpty() ? left : "''") + "||" + (!right.IsNullOrEmpty() ? right : "''");
    #endregion

    #region 分页
    /// <summary>已重写。Snowflake 使用 LIMIT/OFFSET 分页</summary>
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

/// <summary>Snowflake数据库会话</summary>
internal class SnowflakeSession(IDatabase db) : RemoteDbSession(db)
{
    #region 基本方法 查询/执行
    /// <summary>Snowflake 无法回读自增列，进入自增路径时给出明确异常</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    /// <exception cref="NotSupportedException">Snowflake 不支持回读自增列</exception>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
        => throw IdentityNotSupported();

    /// <summary>Snowflake 无法回读自增列，进入自增路径时给出明确异常</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public override Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
        => FromException(IdentityNotSupported());
    #endregion
}

/// <summary>Snowflake数据库元数据</summary>
internal class SnowflakeMetaData : RemoteDbMetaData
{
    #region 构造函数
    public SnowflakeMetaData() => Types = _DataTypes;
    #endregion

    #region 数据类型
    /// <summary>数据类型映射</summary>
    private static readonly Dictionary<Type, String[]> _DataTypes = new()
    {
        { typeof(Byte[]), ["BINARY"] },
        { typeof(Boolean), ["BOOLEAN"] },
        { typeof(Byte), ["TINYINT"] },
        { typeof(Int16), ["SMALLINT"] },
        { typeof(Int32), ["INTEGER", "INT"] },
        { typeof(Int64), ["BIGINT"] },
        { typeof(Single), ["FLOAT"] },
        { typeof(Double), ["DOUBLE", "DOUBLE PRECISION"] },
        { typeof(Decimal), ["NUMBER({0},{1})", "NUMBER", "NUMERIC"] },
        { typeof(DateTime), ["TIMESTAMP_NTZ", "TIMESTAMP_LTZ", "TIMESTAMP_TZ", "TIMESTAMP"] },
        { typeof(String), ["VARCHAR", "TEXT"] },
    };

    /// <summary>取字段类型。VARCHAR 按长度格式化</summary>
    /// <param name="field">字段</param>
    /// <returns></returns>
    protected override String? GetFieldType(IDataColumn field)
    {
        if (field.DataType == typeof(String)) return SqlMetaDataHelper.GetStringFieldType(field, Int32.MaxValue, "VARCHAR");

        return base.GetFieldType(field);
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
        var where = SqlMetaDataHelper.BuildWhere((Database as Snowflake)?.SchemaName, names);
        var tables = $"select table_schema, table_name, table_type from information_schema.tables{where} order by table_schema, table_name";
        var columns = $"select table_schema, table_name, column_name, ordinal_position, data_type, is_nullable, column_default, character_maximum_length, numeric_precision, numeric_scale from information_schema.columns{where} order by table_schema, table_name, ordinal_position";

        return SqlMetaDataHelper.GetTables(Database, tables, columns, GetDataType);
    }
    #endregion
}
