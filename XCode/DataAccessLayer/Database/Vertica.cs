using System.Data;
using System.Data.Common;
using NewLife.Log;

namespace XCode.DataAccessLayer;

/// <summary>Vertica数据库</summary>
/// <remarks>
/// Vertica 是列式分析型数据仓库，SQL 方言接近 PostgreSQL。
/// 连接字符串示例：Host=localhost;Port=5433;Database=test;User=xxx;Password=xxx;SearchPath=public
/// 使用 Vertica.Data 官方 ADO.NET 驱动。
/// 未加引号的标识符折叠为小写，自增列无法回读，建议使用业务主键。
/// </remarks>
internal class Vertica : RemoteDb
{
    #region 属性
    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.Vertica;

    /// <summary>系统数据库名。Vertica 没有系统库概念</summary>
    public override String SystemDatabaseName => String.Empty;

    /// <summary>默认名称格式。Vertica 未加引号的标识符折叠为小写</summary>
    public override NameFormats DefaultNameFormat => NameFormats.Lower;

    /// <summary>模式名</summary>
    public String? SchemaName { get; set; }

    /// <summary>批量操作能力。Vertica 无可靠的批量参数绑定</summary>
    public override BatchCapability BatchCapability => BatchCapability.None;
    #endregion

    #region 方法
    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory()
    {
        var type = DriverLoader.Load("Vertica.Data.VerticaClient.VerticaObjectFactory", null, "Vertica.Data.dll", null);
        var factory = GetProviderFactory(type);
        if (factory != null) return factory;

        return GetProviderFactory(null, "Vertica.Data.dll", "Vertica.Data.VerticaClient.VerticaObjectFactory", true, true);
    }

    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new VerticaSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new VerticaMetaData();

    /// <summary>是否支持该提供者所描述的数据库</summary>
    /// <param name="providerName">提供者</param>
    /// <returns></returns>
    public override Boolean Support(String providerName)
    {
        providerName = providerName.ToLower();
        if (providerName.Contains("vertica")) return true;

        return false;
    }

    /// <summary>设置连接字符串</summary>
    /// <param name="builder"></param>
    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        // XCode 的 Owner 对应 Vertica 的 SearchPath
        if (builder["SearchPath"].IsNullOrEmpty() && !Owner.IsNullOrEmpty()) builder["SearchPath"] = Owner;

        SchemaName = builder["SearchPath"];
    }
    #endregion

    #region 数据库特性
    /// <summary>关键字列表</summary>
    protected override String ReservedWordsStr =>
        "ALL,AND,ANY,ARRAY,AS,ASC,BETWEEN,BOTH,BY,CASE,CAST,CHECK,COLLATE,COLUMN,CONSTRAINT,CREATE,CROSS,CURRENT_DATE,CURRENT_TIME,CURRENT_TIMESTAMP,CURRENT_USER,DEFAULT,DELETE,DESC,DISTINCT,DROP,ELSE,END,EXCEPT,EXISTS,FALSE,FOR,FOREIGN,FROM,FULL,GRANT,GROUP,HAVING,ILIKE,IN,INNER,INSERT,INTERSECT,INTO,IS,JOIN,LEFT,LIKE,LIMIT,LOCALTIME,LOCALTIMESTAMP,NATURAL,NOT,NULL,OFFSET,ON,ONLY,OR,ORDER,OUTER,PRIMARY,REFERENCES,RIGHT,SELECT,SESSION_USER,SOME,SYMMETRIC,TABLE,THEN,TO,TRAILING,TRUE,UNION,UNIQUE,USER,USING,VALUES,WHEN,WHERE,WITH," +
        "LOG,Role,Admin,Rank,Member,Groups,Error,MaxValue,MinValue,Signal";

    /// <summary>格式化标识符。Vertica 使用双引号</summary>
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
        if (value != null && field?.DataType == typeof(Boolean)) return value.ToBoolean() ? "TRUE" : "FALSE";

        return base.FormatValue(field!, value);
    }

    /// <summary>格式化时间为SQL字符串</summary>
    /// <param name="column">字段</param>
    /// <param name="dateTime">时间值</param>
    /// <returns></returns>
    public override String FormatDateTime(IDataColumn column, DateTime dateTime)
    {
        if (dateTime.Ticks % 10_000_000 == 0)
            return $"TIMESTAMP '{dateTime:yyyy-MM-dd HH:mm:ss}'";

        return $"TIMESTAMP '{dateTime:yyyy-MM-dd HH:mm:ss.fffffff}'";
    }

    /// <summary>长文本长度。Vertica VARCHAR 上限 65000</summary>
    public override Int32 LongTextLength => 65000;

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => (!left.IsNullOrEmpty() ? left : "''") + "||" + (!right.IsNullOrEmpty() ? right : "''");
    #endregion

    #region 分页
    /// <summary>已重写。Vertica 使用 LIMIT/OFFSET 分页</summary>
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

        return $"{sql} limit {maximumRows} offset {startRowIndex}";
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

        builder.Limit = $"limit {maximumRows} offset {startRowIndex}";
        return builder;
    }
    #endregion
}

/// <summary>Vertica数据库会话</summary>
internal class VerticaSession : RemoteDbSession
{
    #region 构造函数
    public VerticaSession(IDatabase db) : base(db) { }
    #endregion

    #region 基本方法 查询/执行
    /// <summary>Vertica 无法回读自增列，进入自增路径时给出明确异常</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    /// <exception cref="NotSupportedException">Vertica 不支持回读自增列</exception>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
        => throw new NotSupportedException(L.T("Vertica does not support returning generated identity values; use a business key (Guid/snowflake) or set AllowInsertIdentity."));

    /// <summary>Vertica 无法回读自增列，进入自增路径时给出明确异常</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public override Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        var tcs = new TaskCompletionSource<Int64>();
        tcs.SetException(new NotSupportedException(L.T("Vertica does not support returning generated identity values; use a business key (Guid/snowflake) or set AllowInsertIdentity.")));

        return tcs.Task;
    }
    #endregion
}

/// <summary>Vertica数据库元数据</summary>
internal class VerticaMetaData : RemoteDbMetaData
{
    #region 构造函数
    public VerticaMetaData() => Types = _DataTypes;
    #endregion

    #region 数据类型
    /// <summary>数据类型映射</summary>
    private static readonly Dictionary<Type, String[]> _DataTypes = new()
    {
        { typeof(Byte[]), ["VARBINARY({0})", "LONG VARBINARY", "VARBINARY"] },
        { typeof(Boolean), ["BOOLEAN"] },
        { typeof(Byte), ["TINYINT"] },
        { typeof(Int16), ["SMALLINT"] },
        { typeof(Int32), ["INTEGER", "INT"] },
        { typeof(Int64), ["BIGINT"] },
        { typeof(Single), ["REAL", "FLOAT"] },
        { typeof(Double), ["DOUBLE PRECISION", "DOUBLE"] },
        { typeof(Decimal), ["NUMERIC({0},{1})", "DECIMAL({0},{1})", "NUMERIC", "DECIMAL"] },
        { typeof(DateTime), ["TIMESTAMP", "TIMESTAMPTZ", "DATE"] },
        { typeof(String), ["VARCHAR", "LONG VARCHAR"] },
    };

    /// <summary>取字段类型。VARCHAR 按长度格式化，超长使用 LONG VARCHAR</summary>
    /// <param name="field">字段</param>
    /// <returns></returns>
    protected override String? GetFieldType(IDataColumn field)
    {
        if (field.DataType == typeof(String))
            return field.Length <= 0 || field.Length >= Database.LongTextLength ? "LONG VARCHAR" : $"VARCHAR({field.Length})";

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
        var where = BuildWhere(names);
        var tables = $"select table_schema, table_name, table_type from information_schema.tables{where} order by table_schema, table_name";
        var columns = $"select table_schema, table_name, column_name, ordinal_position, data_type, is_nullable, column_default, character_maximum_length, numeric_precision, numeric_scale from information_schema.columns{where} order by table_schema, table_name, ordinal_position";

        return SqlMetaDataHelper.GetTables(Database, tables, columns, GetDataType);
    }

    private String BuildWhere(String[]? names)
    {
        var filters = new List<String>();
        var schema = (Database as Vertica)?.SchemaName;
        if (!schema.IsNullOrEmpty()) filters.Add($"table_schema = '{schema.Replace("'", "''")}'");

        if (names != null && names.Length > 0)
        {
            var ns = names.Where(e => !e.IsNullOrEmpty()).Select(e => "'" + e.Replace("'", "''") + "'").ToArray();
            if (ns.Length > 0) filters.Add("table_name in (" + String.Join(",", ns) + ")");
        }

        return filters.Count > 0 ? " where " + String.Join(" and ", filters) : "";
    }
    #endregion
}
