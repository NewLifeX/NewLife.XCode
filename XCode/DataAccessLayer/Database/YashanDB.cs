using System.Data;
using System.Data.Common;
using NewLife.Log;

namespace XCode.DataAccessLayer;

/// <summary>YashanDB(崖山)数据库</summary>
/// <remarks>
/// YashanDB 是国产关系型数据库，SQL 方言与 Oracle 高度兼容。
/// 连接字符串示例：Data Source=localhost:1688;User ID=xxx;Password=xxx
/// 使用 Yashandb.Data.YashandbClient 官方 ADO.NET 驱动。
/// 未加引号的标识符折叠为大写；自增依赖序列/IDENTITY，插入后无法回读，建议使用业务主键。
/// </remarks>
internal class YashanDB : RemoteDb
{
    #region 属性
    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.YashanDB;

    /// <summary>系统数据库名。YashanDB 以用户模式组织，没有可切换的系统库</summary>
    public override String SystemDatabaseName => String.Empty;

    /// <summary>默认名称格式。Oracle 体系未加引号的标识符折叠为大写</summary>
    public override NameFormats DefaultNameFormat => NameFormats.Upper;

    /// <summary>批量操作能力。驱动无可靠的批量参数绑定</summary>
    public override BatchCapability BatchCapability => BatchCapability.None;
    #endregion

    #region 方法
    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory()
    {
        var type = DriverLoader.Load("Yashandb.Data.YashandbClient.YasdbClientFactory", null, "Yashandb.Data.YashandbClient.dll", null);
        var factory = GetProviderFactory(type);
        if (factory != null) return factory;

        return GetProviderFactory(null, "Yashandb.Data.YashandbClient.dll", "Yashandb.Data.YashandbClient.YasdbClientFactory", true, true);
    }

    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new YashanDBSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new YashanDBMetaData();

    /// <summary>是否支持该提供者所描述的数据库</summary>
    /// <param name="providerName">提供者</param>
    /// <returns></returns>
    public override Boolean Support(String providerName)
    {
        providerName = providerName.ToLower();
        if (providerName.Contains("yashan")) return true;
        if (providerName.Contains("yasdb")) return true;

        return false;
    }
    #endregion

    #region 数据库特性
    /// <summary>关键字列表</summary>
    protected override String ReservedWordsStr =>
        "ACCESS,ADD,ALL,ALTER,AND,ANY,AS,ASC,AUDIT,BETWEEN,BY,CHAR,CHECK,CLUSTER,COLUMN,COMMENT,COMPRESS,CONNECT,CREATE,CURRENT,DATE,DECIMAL,DEFAULT,DELETE,DESC,DISTINCT,DROP,ELSE,END,EXCLUSIVE,EXISTS,FALSE,FILE,FLOAT,FOR,FROM,GRANT,GROUP,HAVING,IDENTIFIED,IMMEDIATE,IN,INCREMENT,INDEX,INITIAL,INSERT,INTEGER,INTERSECT,INTO,IS,LEVEL,LIKE,LOCK,LONG,MAXEXTENTS,MINUS,MODE,MODIFY,NOAUDIT,NOCOMPRESS,NOT,NOWAIT,NULL,NUMBER,OF,OFFLINE,ON,ONLINE,OPTION,OR,ORDER,PCTFREE,PRIOR,PRIVILEGES,PUBLIC,RAW,RENAME,RESOURCE,REVOKE,ROW,ROWID,ROWNUM,ROWS,SELECT,SESSION,SET,SHARE,SIZE,SMALLINT,START,SUCCESSFUL,SYNONYM,SYSDATE,TABLE,THEN,TO,TRIGGER,TRUE,UID,UNION,UNIQUE,UPDATE,USER,VALIDATE,VALUES,VARCHAR,VARCHAR2,VIEW,WHEN,WHENEVER,WHERE,WITH," +
        "LOG,Role,Admin,Rank,Member,Groups,Error,MaxValue,MinValue,Signal,Offset";

    /// <summary>格式化标识符。YashanDB 使用双引号</summary>
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

    /// <summary>长文本长度。VARCHAR2 上限 4000，超出使用 CLOB</summary>
    public override Int32 LongTextLength => 4000;

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => (!left.IsNullOrEmpty() ? left : "''") + "||" + (!right.IsNullOrEmpty() ? right : "''");
    #endregion

    #region 分页
    /// <summary>已重写。YashanDB 使用 Oracle 风格 ROWNUM 分页</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <param name="keyColumn">主键列</param>
    /// <returns></returns>
    public override String PageSplit(String sql, Int64 startRowIndex, Int64 maximumRows, String? keyColumn)
    {
        if (startRowIndex <= 0)
        {
            if (maximumRows <= 0) return sql;

            if (!sql.ToLower().Contains("order by")) return $"Select * From ({sql}) T0 Where rownum<={maximumRows}";
        }

        sql = $"Select * From (Select T0.*, rownum as rowNumber From ({sql}) T0) T1 Where rowNumber>{startRowIndex}";
        if (maximumRows > 0) sql += $" And rowNumber<={startRowIndex + maximumRows}";

        return sql;
    }

    /// <summary>构造分页SQL</summary>
    /// <param name="builder">查询生成器</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns>分页SQL</returns>
    public override SelectBuilder PageSplit(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows)
    {
        var sql = PageSplit(builder.ToString(), startRowIndex, maximumRows, builder.Key);

        return new SelectBuilder(sql);
    }
    #endregion
}

/// <summary>YashanDB数据库会话</summary>
internal class YashanDBSession : RemoteDbSession
{
    #region 构造函数
    public YashanDBSession(IDatabase db) : base(db) { }
    #endregion

    #region 基本方法 查询/执行
    /// <summary>YashanDB 无法回读自增列，进入自增路径时给出明确异常</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    /// <exception cref="NotSupportedException">YashanDB 不支持回读自增列</exception>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
        => throw new NotSupportedException(L.T("YashanDB does not support returning generated identity values; use a business key (Guid/snowflake) or set AllowInsertIdentity."));

    /// <summary>YashanDB 无法回读自增列，进入自增路径时给出明确异常</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public override Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        var tcs = new TaskCompletionSource<Int64>();
        tcs.SetException(new NotSupportedException(L.T("YashanDB does not support returning generated identity values; use a business key (Guid/snowflake) or set AllowInsertIdentity.")));

        return tcs.Task;
    }
    #endregion
}

/// <summary>YashanDB数据库元数据</summary>
internal class YashanDBMetaData : RemoteDbMetaData
{
    #region 构造函数
    public YashanDBMetaData() => Types = _DataTypes;
    #endregion

    #region 数据类型
    /// <summary>数据类型映射</summary>
    private static readonly Dictionary<Type, String[]> _DataTypes = new()
    {
        { typeof(Byte[]), ["BLOB", "RAW({0})"] },
        { typeof(Boolean), ["NUMBER(1)"] },
        { typeof(Byte), ["NUMBER(3)"] },
        { typeof(Int16), ["NUMBER(5)"] },
        { typeof(Int32), ["INTEGER", "NUMBER(10)"] },
        { typeof(Int64), ["BIGINT", "NUMBER(19)"] },
        { typeof(Single), ["BINARY_FLOAT", "FLOAT"] },
        { typeof(Double), ["BINARY_DOUBLE", "DOUBLE PRECISION"] },
        { typeof(Decimal), ["NUMBER({0},{1})", "DECIMAL({0},{1})", "NUMBER", "DECIMAL"] },
        { typeof(DateTime), ["TIMESTAMP", "DATE"] },
        { typeof(String), ["VARCHAR2({0})", "NVARCHAR2({0})", "CLOB", "CHAR({0})"] },
    };

    /// <summary>取字段类型。VARCHAR2 按长度格式化，超长使用 CLOB</summary>
    /// <param name="field">字段</param>
    /// <returns></returns>
    protected override String? GetFieldType(IDataColumn field)
    {
        if (field.DataType == typeof(String))
            return field.Length <= 0 || field.Length >= Database.LongTextLength ? "CLOB" : $"VARCHAR2({field.Length})";

        return base.GetFieldType(field);
    }

    /// <summary>获取Net数据类型。NUMBER 无精度时按整数处理，避免全部映射为 Decimal</summary>
    /// <param name="field"></param>
    /// <returns></returns>
    protected override Type? GetDataType(IDataColumn field)
    {
        var type = base.GetDataType(field);
        if (type != null) return type;

        var raw = (field.RawType + "").Trim();
        if (raw.StartsWithIgnoreCase("NUMBER") && field.Scale <= 0)
        {
            if (field.Precision > 0 && field.Precision <= 10) return typeof(Int32);
            if (field.Precision > 10) return typeof(Int64);
        }

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
        var nameFilterTables = "";
        var nameFilterViews = "";
        var nameFilterColumns = "";
        if (names != null && names.Length > 0)
        {
            var ns = names.Where(e => !e.IsNullOrEmpty()).Select(e => "'" + e.Replace("'", "''") + "'").ToArray();
            if (ns.Length > 0)
            {
                var list = String.Join(",", ns);
                nameFilterTables = $" where table_name in ({list})";
                nameFilterViews = $" where view_name in ({list})";
                nameFilterColumns = $" where table_name in ({list})";
            }
        }

        var tables = $"select user as table_schema, table_name, 'TABLE' as table_type from user_tables{nameFilterTables}" +
            $" union all select user as table_schema, view_name as table_name, 'VIEW' as table_type from user_views{nameFilterViews}";
        var columns = "select user as table_schema, table_name, column_name, column_id as ordinal_position, data_type, " +
            "case nullable when 'Y' then 'YES' else 'NO' end as is_nullable, " +
            "data_length as character_maximum_length, data_precision as numeric_precision, data_scale as numeric_scale " +
            $"from user_tab_columns{nameFilterColumns} order by table_name, column_id";

        return SqlMetaDataHelper.GetTables(Database, tables, columns, GetDataType);
    }
    #endregion
}
