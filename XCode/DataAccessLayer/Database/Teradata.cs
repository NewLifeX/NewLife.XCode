using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using NewLife.Log;

namespace XCode.DataAccessLayer;

/// <summary>Teradata数据库</summary>
/// <remarks>
/// Teradata 是企业级数据仓库，SQL 方言接近 ANSI SQL。
/// 连接字符串示例：Data Source=localhost;User ID=xxx;Password=xxx;Database=test
/// 使用 Teradata.Client.Provider 官方 ADO.NET 驱动。
/// 首页分页使用 TOP n，后续页使用 ROW_NUMBER() 包装；无法回读自增列，建议使用业务主键。
/// </remarks>
internal class Teradata : RemoteDb
{
    #region 属性
    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.Teradata;

    /// <summary>系统数据库名。Teradata 没有可切换的系统库</summary>
    public override String SystemDatabaseName => String.Empty;

    /// <summary>默认名称格式。Teradata 保留创建时的大小写</summary>
    public override NameFormats DefaultNameFormat => NameFormats.None;

    /// <summary>批量操作能力。Teradata 驱动无可靠的批量参数绑定</summary>
    public override BatchCapability BatchCapability => BatchCapability.None;
    #endregion

    #region 方法
    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory()
    {
        var type = DriverLoader.Load("Teradata.Client.Provider.TdFactory", null, "Teradata.Client.Provider.dll", null);
        var factory = GetProviderFactory(type);
        if (factory != null) return factory;

        return GetProviderFactory(null, "Teradata.Client.Provider.dll", "Teradata.Client.Provider.TdFactory", true, true);
    }

    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new TeradataSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new TeradataMetaData();

    /// <summary>是否支持该提供者所描述的数据库</summary>
    /// <param name="providerName">提供者</param>
    /// <returns></returns>
    public override Boolean Support(String providerName)
    {
        providerName = providerName.ToLower();
        if (providerName.Contains("teradata")) return true;

        return false;
    }
    #endregion

    #region 数据库特性
    /// <summary>关键字列表</summary>
    protected override String ReservedWordsStr =>
        "ABORT,ABORTSESSION,ACCESS,ACCOUNT,ADD,ALL,ALTER,AND,ANY,AS,ASC,AT,AVG,BETWEEN,BY,CASE,CAST,CHECK,COLLECT,COLUMN,COMMIT,CONSTRAINT,CREATE,CROSS,CURRENT,CURRENT_DATE,CURRENT_TIME,CURRENT_TIMESTAMP,CURRENT_USER,DATABASE,DATE,DELETE,DESC,DISTINCT,DROP,ELSE,END,EXCEPT,EXEC,EXECUTE,EXISTS,EXPLAIN,FALSE,FETCH,FIRST,FOR,FOREIGN,FROM,FULL,GRANT,GROUP,HAVING,IN,INDEX,INNER,INSERT,INTERSECT,INTO,IS,JOIN,KEY,LEFT,LIKE,LIMIT,LOAD,MERGE,MODIFY,NATURAL,NOT,NULL,OF,ON,ONLY,OR,ORDER,OUTER,PRIMARY,QUALIFY,REFERENCES,RENAME,REPLACE,RESET,RIGHT,ROLLBACK,SELECT,SEL,SET,SOME,TABLE,THEN,TO,TOP,TRAILING,TRUE,UNION,UNIQUE,UPDATE,USER,USING,VALUES,VIEW,WHEN,WHERE,WITH," +
        "LOG,Role,Admin,Rank,Member,Groups,Error,MaxValue,MinValue,Signal,Offset";

    /// <summary>格式化标识符。Teradata 使用双引号</summary>
    /// <param name="keyWord">关键字</param>
    /// <returns></returns>
    public override String FormatKeyWord(String keyWord)
    {
        if (keyWord.IsNullOrEmpty()) return keyWord;

        if (keyWord.StartsWith("\"") && keyWord.EndsWith("\"")) return keyWord;

        return "\"" + keyWord.Replace("\"", "\"\"") + "\"";
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

    /// <summary>长文本长度。Teradata VARCHAR 上限 32000</summary>
    public override Int32 LongTextLength => 32000;

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => (!left.IsNullOrEmpty() ? left : "''") + "||" + (!right.IsNullOrEmpty() ? right : "''");
    #endregion

    #region 分页
    private static readonly Regex _orderByRegex = new(@"\s+order\s+by\s+[^()]*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>已重写。首页使用 TOP，后续页使用 ROW_NUMBER() 包装</summary>
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

            return InsertTop(sql, $"Top {maximumRows}");
        }
        if (maximumRows < 1) throw new NotSupportedException(L.T("Fetching all rows after a given row is not supported!"));

        return BuildOffsetSql(sql, startRowIndex, maximumRows, keyColumn);
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
            if (maximumRows < 1) return builder;

            return builder.Clone().Top(maximumRows);
        }
        if (maximumRows < 1) throw new NotSupportedException(L.T("Fetching all rows after a given row is not supported!"));

        return new SelectBuilder(BuildOffsetSql(builder.ToString(), startRowIndex, maximumRows, builder.Key));
    }

    private static String InsertTop(String sql, String top)
    {
        var p = sql.IndexOf("select", StringComparison.OrdinalIgnoreCase);
        if (p < 0) return sql;

        var head = sql.Substring(0, p + 6);
        var rest = sql.Substring(p + 6);
        var trim = rest.TrimStart();
        var space = rest.Substring(0, rest.Length - trim.Length);

        return head + space + top + " " + trim;
    }

    private static String BuildOffsetSql(String sql, Int64 startRowIndex, Int64 maximumRows, String? keyColumn)
    {
        // 派生表内不允许 ORDER BY（除非带 TOP），先移除尾部排序
        var inner = _orderByRegex.Replace(sql, "");
        var key = keyColumn.IsNullOrEmpty() ? "1" : keyColumn;
        var end = startRowIndex + maximumRows;

        return $"SELECT * FROM (SELECT T0.*, ROW_NUMBER() OVER (ORDER BY {key}) AS XCodeRowNumber FROM ({inner}) T0) T1 WHERE XCodeRowNumber BETWEEN {startRowIndex + 1} AND {end} ORDER BY XCodeRowNumber";
    }
    #endregion
}

/// <summary>Teradata数据库会话</summary>
internal class TeradataSession : RemoteDbSession
{
    #region 构造函数
    public TeradataSession(IDatabase db) : base(db) { }
    #endregion

    #region 基本方法 查询/执行
    /// <summary>Teradata 无法回读自增列，进入自增路径时给出明确异常</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    /// <exception cref="NotSupportedException">Teradata 不支持回读自增列</exception>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
        => throw new NotSupportedException(L.T("Teradata does not support returning generated identity values; use a business key (Guid/snowflake) or set AllowInsertIdentity."));

    /// <summary>Teradata 无法回读自增列，进入自增路径时给出明确异常</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public override Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        var tcs = new TaskCompletionSource<Int64>();
        tcs.SetException(new NotSupportedException(L.T("Teradata does not support returning generated identity values; use a business key (Guid/snowflake) or set AllowInsertIdentity.")));

        return tcs.Task;
    }
    #endregion
}

/// <summary>Teradata数据库元数据</summary>
internal class TeradataMetaData : RemoteDbMetaData
{
    #region 构造函数
    public TeradataMetaData() => Types = _DataTypes;
    #endregion

    #region 数据类型
    /// <summary>数据类型映射</summary>
    private static readonly Dictionary<Type, String[]> _DataTypes = new()
    {
        { typeof(Byte[]), ["VARBYTE({0})", "BYTE({0})", "BLOB"] },
        { typeof(Byte), ["BYTEINT"] },
        { typeof(Boolean), ["BYTEINT"] },
        { typeof(Int16), ["SMALLINT"] },
        { typeof(Int32), ["INTEGER", "INT"] },
        { typeof(Int64), ["BIGINT"] },
        { typeof(Single), ["REAL", "FLOAT"] },
        { typeof(Double), ["DOUBLE PRECISION", "DOUBLE"] },
        { typeof(Decimal), ["DECIMAL({0},{1})", "NUMERIC({0},{1})", "DECIMAL", "NUMERIC"] },
        { typeof(DateTime), ["TIMESTAMP", "DATE"] },
        { typeof(String), ["VARCHAR({0})", "CHAR({0})", "CLOB"] },
    };

    /// <summary>取字段类型。VARCHAR 按长度格式化</summary>
    /// <param name="field">字段</param>
    /// <returns></returns>
    protected override String? GetFieldType(IDataColumn field)
    {
        if (field.DataType == typeof(String))
            return field.Length <= 0 || field.Length >= Database.LongTextLength ? "CLOB" : $"VARCHAR({field.Length})";

        return base.GetFieldType(field);
    }

    /// <summary>获取Net数据类型。Teradata 的 dbc.ColumnsV 返回类型代码，这里做常见代码映射</summary>
    /// <param name="field"></param>
    /// <returns></returns>
    protected override Type? GetDataType(IDataColumn field)
    {
        var type = base.GetDataType(field);
        if (type != null) return type;

        return (field.RawType + "").Trim().ToUpper() switch
        {
            "I" => typeof(Int32),
            "SI" => typeof(Int16),
            "BY" => typeof(Byte),
            "BI" => typeof(Int64),
            "F" => typeof(Double),
            "D" or "N" => typeof(Decimal),
            "CV" or "CF" or "CO" => typeof(String),
            "BO" or "BF" => typeof(Byte[]),
            "DA" or "TS" or "TZ" => typeof(DateTime),
            _ => null,
        };
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
        var tableFilters = new List<String> { "TableKind in ('T','V','O')" };
        var columnFilters = new List<String>();
        if (names != null && names.Length > 0)
        {
            var ns = names.Where(e => !e.IsNullOrEmpty()).Select(e => "'" + e.Replace("'", "''") + "'").ToArray();
            if (ns.Length > 0)
            {
                tableFilters.Add("TableName in (" + String.Join(",", ns) + ")");
                columnFilters.Add("TableName in (" + String.Join(",", ns) + ")");
            }
        }

        var columnWhere = columnFilters.Count > 0 ? " where " + String.Join(" and ", columnFilters) : "";
        var tables = $"select DatabaseName as table_schema, TableName as table_name, case TableKind when 'V' then 'VIEW' else 'TABLE' end as table_type from dbc.TablesV where {String.Join(" and ", tableFilters)} order by DatabaseName, TableName";
        var columns = "select DatabaseName as table_schema, TableName as table_name, ColumnName as column_name, ColumnId as ordinal_position, " +
            "ColumnType as data_type, case Nullable when 'Y' then 'YES' else 'NO' end as is_nullable, " +
            "ColumnLength as character_maximum_length, DecimalTotalDigits as numeric_precision, DecimalFractionalDigits as numeric_scale " +
            $"from dbc.ColumnsV{columnWhere} order by DatabaseName, TableName, ColumnId";

        return SqlMetaDataHelper.GetTables(Database, tables, columns, GetDataType);
    }
    #endregion
}
