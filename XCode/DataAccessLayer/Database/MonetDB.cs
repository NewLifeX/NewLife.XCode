using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using NewLife.Log;

namespace XCode.DataAccessLayer;

/// <summary>MonetDB数据库</summary>
/// <remarks>
/// MonetDB 是列式关系型数据库，SQL 方言接近 PostgreSQL。
/// 连接字符串示例：Host=localhost;Port=50000;Database=test;User=monetdb;Password=monetdb
/// 使用 MonetDb.Mapi 驱动；该驱动没有提供 DbProviderFactory，这里用反射工厂适配。
/// 未加引号的标识符折叠为小写，自增列无法回读，建议使用业务主键。
/// </remarks>
internal class MonetDB : RemoteDb
{
    #region 属性
    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.MonetDB;

    /// <summary>系统数据库名。MonetDB 没有可切换的系统库</summary>
    public override String SystemDatabaseName => String.Empty;

    /// <summary>默认名称格式。MonetDB 未加引号的标识符折叠为小写</summary>
    public override NameFormats DefaultNameFormat => NameFormats.Lower;

    /// <summary>模式名</summary>
    public String? SchemaName { get; set; }

    #endregion

    #region 方法
    /// <summary>创建工厂</summary>
    /// <remarks>MonetDb.Mapi 没有提供 DbProviderFactory，这里用反射工厂适配</remarks>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory()
    {
        var connType = DriverLoader.Load("MonetDb.Mapi.MonetDbConnection", null, "MonetDb.Mapi.dll", null);
        if (connType == null) return null;

        var cmdType = DriverLoader.Load("MonetDb.Mapi.MonetDbCommand", null, "MonetDb.Mapi.dll", null);
        var prmType = DriverLoader.Load("MonetDb.Mapi.MonetDbParameter", null, "MonetDb.Mapi.dll", null);
        if (cmdType == null || prmType == null) return null;

        return new MonetDbFactory(connType, cmdType, prmType);
    }

    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new MonetDBSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new MonetDBMetaData();

    /// <summary>是否支持该提供者所描述的数据库</summary>
    /// <param name="providerName">提供者</param>
    /// <returns></returns>
    public override Boolean Support(String providerName) => providerName.ToLower().Contains("monetdb");

    /// <summary>设置连接字符串</summary>
    /// <param name="builder"></param>
    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        if (builder["Port"].IsNullOrEmpty()) builder["Port"] = "50000";

        SchemaName = builder["Schema"];
    }
    #endregion

    #region 数据库特性
    /// <summary>关键字列表</summary>
    protected override String ReservedWordsStr =>
        "ALL,AND,ANY,AS,ASC,BETWEEN,BOTH,BY,CASE,CAST,CHECK,COLUMN,CONSTRAINT,CREATE,CROSS,CURRENT_DATE,CURRENT_TIME,CURRENT_TIMESTAMP,CURRENT_USER,DEFAULT,DELETE,DESC,DISTINCT,DROP,ELSE,END,EXCEPT,EXISTS,FALSE,FOR,FOREIGN,FROM,FULL,GROUP,HAVING,IN,INNER,INSERT,INTERSECT,INTO,IS,JOIN,LEFT,LIKE,LIMIT,NOT,NULL,OFFSET,ON,OR,ORDER,OUTER,PRIMARY,REFERENCES,RIGHT,SELECT,SET,SOME,TABLE,THEN,TRUE,UNION,UNIQUE,UPDATE,USING,VALUES,WHEN,WHERE,WITH," +
        "LOG,User,Role,Admin,Rank,Member,Groups,Error,MaxValue,MinValue,Signal";

    /// <summary>格式化标识符。MonetDB 使用双引号</summary>
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
    public override String FormatDateTime(IDataColumn column, DateTime dateTime) => FormatDateTimeLiteral(dateTime);

    /// <summary>长文本长度。MonetDB 超过该长度使用 CLOB</summary>
    public override Int32 LongTextLength => 4000;

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => (!left.IsNullOrEmpty() ? left : "''") + "||" + (!right.IsNullOrEmpty() ? right : "''");
    #endregion

    #region 分页
    /// <summary>已重写。MonetDB 使用 LIMIT/OFFSET 分页</summary>
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

/// <summary>MonetDB 反射工厂。驱动未提供 DbProviderFactory</summary>
internal sealed class MonetDbFactory(Type connType, Type cmdType, Type prmType) : DbProviderFactory
{
    /// <summary>创建连接</summary>
    /// <returns></returns>
    [UnconditionalSuppressMessage("AOT", "IL2077", Justification = "MonetDB 驱动类型由 DriverLoader 在运行时解析，AOT 下不支持该动态驱动路径")]
    public override DbConnection CreateConnection() => (DbConnection)Activator.CreateInstance(connType)!;

    /// <summary>创建命令</summary>
    /// <returns></returns>
    [UnconditionalSuppressMessage("AOT", "IL2077", Justification = "MonetDB 驱动类型由 DriverLoader 在运行时解析，AOT 下不支持该动态驱动路径")]
    public override DbCommand CreateCommand() => (DbCommand)Activator.CreateInstance(cmdType)!;

    /// <summary>创建参数</summary>
    /// <returns></returns>
    [UnconditionalSuppressMessage("AOT", "IL2077", Justification = "MonetDB 驱动类型由 DriverLoader 在运行时解析，AOT 下不支持该动态驱动路径")]
    public override DbParameter CreateParameter() => (DbParameter)Activator.CreateInstance(prmType)!;

    /// <summary>创建连接字符串生成器</summary>
    /// <returns></returns>
    public override DbConnectionStringBuilder CreateConnectionStringBuilder() => new();
}

/// <summary>MonetDB数据库会话</summary>
internal class MonetDBSession(IDatabase db) : RemoteDbSession(db)
{
    #region 基本方法 查询/执行
    /// <summary>MonetDB 无法回读自增列，进入自增路径时给出明确异常</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    /// <exception cref="NotSupportedException">MonetDB 不支持回读自增列</exception>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
        => throw IdentityNotSupported();

    /// <summary>MonetDB 无法回读自增列，进入自增路径时给出明确异常</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public override Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
        => FromException(IdentityNotSupported());
    #endregion
}

/// <summary>MonetDB数据库元数据</summary>
internal class MonetDBMetaData : RemoteDbMetaData
{
    #region 构造函数
    public MonetDBMetaData() => Types = _DataTypes;
    #endregion

    #region 数据类型
    /// <summary>数据类型映射</summary>
    private static readonly Dictionary<Type, String[]> _DataTypes = new()
    {
        { typeof(Byte[]), ["BLOB"] },
        { typeof(Boolean), ["BOOLEAN"] },
        { typeof(Byte), ["TINYINT"] },
        { typeof(Int16), ["SMALLINT"] },
        { typeof(Int32), ["INTEGER", "INT"] },
        { typeof(Int64), ["BIGINT"] },
        { typeof(Single), ["REAL"] },
        { typeof(Double), ["DOUBLE PRECISION", "DOUBLE"] },
        { typeof(Decimal), ["DECIMAL({0},{1})", "NUMERIC({0},{1})", "DECIMAL", "NUMERIC"] },
        { typeof(DateTime), ["TIMESTAMP", "DATE"] },
        { typeof(String), ["VARCHAR({0})", "CLOB"] },
    };

    /// <summary>取字段类型。VARCHAR 按长度格式化，超长使用 CLOB</summary>
    /// <param name="field">字段</param>
    /// <returns></returns>
    protected override String? GetFieldType(IDataColumn field)
    {
        if (field.DataType == typeof(String)) return SqlMetaDataHelper.GetStringFieldType(field, Database.LongTextLength, "CLOB");

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
        var tableFilter = "";
        var columnFilter = "";
        var list = SqlMetaDataHelper.EscapeNames(names);
        if (!list.IsNullOrEmpty())
        {
            tableFilter = $" and t.name in ({list})";
            columnFilter = $" and t.name in ({list})";
        }

        var tables = "select s.name as table_schema, t.name as table_name, case when t.type = 1 then 'VIEW' else 'TABLE' end as table_type " +
            $"from sys.tables t join sys.schemas s on t.schema_id = s.id where 1 = 1{tableFilter} order by s.name, t.name";
        var columns = "select s.name as table_schema, t.name as table_name, c.name as column_name, c.number as ordinal_position, c.type as data_type, " +
            "case when c.\"null\" then 'YES' else 'NO' end as is_nullable, " +
            "c.type_digits as character_maximum_length, c.type_digits as numeric_precision, c.type_scale as numeric_scale " +
            $"from sys.columns c join sys.tables t on c.table_id = t.id join sys.schemas s on t.schema_id = s.id where 1 = 1{columnFilter} order by s.name, t.name, c.number";

        return SqlMetaDataHelper.GetTables(Database, tables, columns, GetDataType);
    }
    #endregion
}
