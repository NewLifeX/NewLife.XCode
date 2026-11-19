using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;
using NewLife.Log;

namespace XCode.DataAccessLayer;

/// <summary>Firebird数据库</summary>
/// <remarks>
/// Firebird 是开源关系型数据库，支持嵌入与远程两种部署方式。
/// 连接字符串示例：Database=../data/test.fdb;DataSource=localhost;User=SYSDBA;Password=masterkey
/// 使用 FirebirdSql.Data.FirebirdClient 作为 ADO.NET 驱动，自增主键使用 Firebird 3.0+ 的 IDENTITY 与 INSERT ... RETURNING。
/// </remarks>
internal class Firebird : DbBase
{
    #region 属性
    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.Firebird;

    /// <summary>默认名称格式。Firebird 未加引号的标识符折叠为大写</summary>
    public override NameFormats DefaultNameFormat => NameFormats.Upper;
    #endregion

    #region 方法
    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory()
    {
        var type = DriverLoader.Load("FirebirdSql.Data.FirebirdClient.FirebirdClientFactory", null, "FirebirdSql.Data.FirebirdClient.dll", null);
        var factory = GetProviderFactory(type);
        if (factory != null) return factory;

        return GetProviderFactory(null, "FirebirdSql.Data.FirebirdClient.dll", "FirebirdSql.Data.FirebirdClient.FirebirdClientFactory", true, true);
    }

    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new FirebirdSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new FirebirdMetaData();

    /// <summary>是否支持该提供者所描述的数据库</summary>
    /// <param name="providerName">提供者</param>
    /// <returns></returns>
    public override Boolean Support(String providerName)
    {
        providerName = providerName.ToLower();
        if (providerName.Contains("firebird")) return true;

        return false;
    }

    /// <summary>设置连接字符串。Firebird 的 Database 表示数据库文件，Data Source 表示服务器地址</summary>
    /// <param name="builder"></param>
    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        var file = builder["Database"];
        if (!file.IsNullOrEmpty())
        {
            file = ResolveFile(file);
            builder["Database"] = file;
            DatabaseName = file;
        }
    }
    #endregion

    #region 数据库特性
    /// <summary>关键字列表</summary>
    protected override String ReservedWordsStr =>
        "ACTION,ACTIVE,ADD,ADMIN,AFTER,ALL,ALTER,AND,ANY,AS,ASC,ASCENDING,AT,AUTO,AVG,BASE_NAME,BEFORE,BEGIN,BETWEEN,BIGINT,BLOB,BREAK,BY,CACHE,CASCADE,CASE,CAST,CHAR,CHARACTER,CHECK,CHECK_POINT_LENGTH,COALESCE,COLLATE,COLUMN,COMMIT,COMMITTED,COMPUTED,CONDITIONAL,CONNECTION_ID,CONSTRAINT,CONTAINING,COUNT,CREATE,CSTRING,CURRENT,CURRENT_DATE,CURRENT_ROLE,CURRENT_TIME,CURRENT_TIMESTAMP,CURRENT_USER,CURSOR,DATABASE,DATE,DAY,DEBUG,DEC,DECIMAL,DECLARE,DEFAULT,DELETE,DESC,DESCENDING,DESCRIPTOR,DISTINCT,DO,DOMAIN,DOUBLE,DROP,ELSE,END,ENTRY_POINT,ESCAPE,EXCEPTION,EXECUTE,EXISTS,EXIT,EXTERNAL,EXTRACT,FILE,FILTER,FIRST,FLOAT,FOR,FOREIGN,FREE_IT,FROM,FULL,FUNCTION,GDSCODE,GENERATOR,GEN_ID,GRANT,GROUP,GROUP_COMMIT_WAIT_TIME,HAVING,HOUR,IF,IN,INACTIVE,INDEX,INNER,INPUT_TYPE,INSERT,INT,INTEGER,INTO,IS,ISOLATION,JOIN,KEY,LAST,LEFT,LENGTH,LEVEL,LIKE,LOGFILE,LOG_BUFFER_SIZE,LONG,MANUAL,MAX,MAXIMUM_SEGMENT,MERGE,MESSAGE,MIN,MINUTE,MODULE_NAME,MONTH,NAMES,NATIONAL,NATURAL,NCHAR,NO,NOT,NULL,NULLIF,NULLS,LOCK,NUMERIC,NUM_LOG_BUFFERS,OF,ON,ONLY,OPTION,OR,ORDER,OUTER,OUTPUT_TYPE,OVERFLOW,PAGE,PAGES,PAGE_SIZE,PARAMETER,PASSWORD,PLAN,POSITION,POST_EVENT,PRECISION,PRIMARY,PRIVILEGES,PROCEDURE,PROTECTED,RAW_PARTITIONS,READ,REAL,RECORD_VERSION,RECREATE,REFERENCES,RESERV,RESERVING,RESTRICT,RETAIN,RETURNING_VALUES,RETURNS,REVOKE,RIGHT,ROLE,ROLLBACK,ROWS_AFFECTED,SAVEPOINT,SCHEMA,SECOND,SEGMENT,SELECT,SET,SHADOW,SHARED,SINGULAR,SIZE,SKIP,SMALLINT,SNAPSHOT,SOME,SORT,SQLCODE,STABILITY,STARTING,STARTS,STATISTICS,SUBSTRING,SUB_TYPE,SUM,SUSPEND,TABLE,THEN,TIME,TIMESTAMP,TO,TRANSACTION,TRANSACTION_ID,TRIGGER,TYPE,UNCOMMITTED,UNION,UNIQUE,UPDATE,UPPER,USER,USING,VALUE,VALUES,VARCHAR,VARIABLE,VARYING,VIEW,WAIT,WEEKDAY,WHEN,WHERE,WHILE,WITH,WORK,WRITE,YEAR,YEARDAY," +
        "LOG,Role,Admin,Rank,Member,Groups,Error,MaxValue,MinValue,Signal,Offset";

    /// <summary>格式化标识符。Firebird 使用双引号</summary>
    /// <param name="keyWord">关键字</param>
    /// <returns></returns>
    public override String FormatKeyWord(String keyWord)
    {
        if (keyWord.IsNullOrEmpty()) return keyWord;

        if (keyWord.StartsWith("\"") && keyWord.EndsWith("\"")) return keyWord;

        return "\"" + keyWord.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>长文本长度</summary>
    public override Int32 LongTextLength => 32767;

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => (!left.IsNullOrEmpty() ? left : "''") + "||" + (!right.IsNullOrEmpty() ? right : "''");
    #endregion

    #region 分页
    /// <summary>已重写。Firebird 使用 ROWS m TO n 分页</summary>
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

            return $"{sql} rows 1 to {maximumRows}";
        }
        if (maximumRows < 1) throw new NotSupportedException(L.T("Fetching all rows after a given row is not supported!"));

        return $"{sql} rows {startRowIndex + 1} to {startRowIndex + maximumRows}";
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
            if (maximumRows > 0) builder.Limit = $"rows 1 to {maximumRows}";
            return builder;
        }
        if (maximumRows < 1) throw new NotSupportedException(L.T("Fetching all rows after a given row is not supported!"));

        builder.Limit = $"rows {startRowIndex + 1} to {startRowIndex + maximumRows}";
        return builder;
    }
    #endregion
}

/// <summary>Firebird数据库会话</summary>
internal class FirebirdSession : DbSession
{
    #region 构造函数
    public FirebirdSession(IDatabase db) : base(db) { }
    #endregion

    #region 基本方法 查询/执行
    private static readonly ConcurrentDictionary<String, String> _identityCache = new();

    private static readonly Regex _tableRegex = new(@"^\s*Insert\s+Into\s+(?<name>""[^""]+""|[\w\$]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>执行插入语句并返回新增行的自动编号。Firebird 3+ 使用 INSERT ... RETURNING</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号。无法识别自增列时返回 0</returns>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        var column = GetIdentityColumn(sql);
        if (!column.IsNullOrEmpty()) return ExecuteScalar<Int64>($"{sql} Returning {column}", type, ps);

        Execute(sql, type, ps);
        return 0;
    }

    /// <summary>执行插入语句并返回新增行的自动编号。Firebird 3+ 使用 INSERT ... RETURNING</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号。无法识别自增列时返回 0</returns>
    public override async Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        var column = GetIdentityColumn(sql);
        if (!column.IsNullOrEmpty()) return await ExecuteScalarAsync<Int64>($"{sql} Returning {column}", type, ps).ConfigureAwait(false);

        await ExecuteAsync(sql, type, ps).ConfigureAwait(false);
        return 0;
    }

    /// <summary>从插入语句解析表名，并查找该表的自增列</summary>
    /// <param name="sql">插入语句</param>
    /// <returns>格式化后的自增列名，找不到时返回空</returns>
    private String? GetIdentityColumn(String sql)
    {
        var m = _tableRegex.Match(sql);
        if (!m.Success) return null;

        var tableName = m.Groups["name"].Value.Trim('"');
        var key = Database.ConnName + ":" + tableName;
        if (_identityCache.TryGetValue(key, out var cached)) return cached.IsNullOrEmpty() ? null : cached;

        String? name = null;
        try
        {
            var table = Database.CreateMetaData().GetTables().FirstOrDefault(e => e.TableName.EqualIgnoreCase(tableName));
            var column = table?.Columns.FirstOrDefault(e => e.Identity);
            if (column != null) name = Database.FormatName(column);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        _identityCache[key] = name ?? "";

        return name;
    }
    #endregion
}

/// <summary>Firebird数据库元数据</summary>
internal class FirebirdMetaData : DbMetaData
{
    #region 构造函数
    public FirebirdMetaData() => Types = _DataTypes;
    #endregion

    #region 数据类型
    /// <summary>数据类型映射。Int16 在前，smallint 反向映射时优先识别为 Int16</summary>
    private static readonly Dictionary<Type, String[]> _DataTypes = new()
    {
        { typeof(Byte[]), ["BLOB", "binary({0})", "varbinary({0})"] },
        { typeof(Int16), ["SMALLINT"] },
        { typeof(Int32), ["INTEGER", "INT"] },
        { typeof(Int64), ["BIGINT"] },
        { typeof(Single), ["FLOAT"] },
        { typeof(Double), ["DOUBLE PRECISION", "DOUBLE"] },
        { typeof(Decimal), ["DECIMAL({0},{1})", "DECIMAL", "NUMERIC"] },
        { typeof(DateTime), ["TIMESTAMP", "DATE"] },
        { typeof(String), ["VARCHAR({0})", "BLOB SUB_TYPE TEXT"] },
        { typeof(Boolean), ["SMALLINT"] },
    };

    /// <summary>取字段类型</summary>
    /// <param name="field">字段</param>
    /// <returns></returns>
    protected override String? GetFieldType(IDataColumn field)
    {
        if (field.DataType == typeof(Boolean)) return "smallint";

        return base.GetFieldType(field);
    }

    /// <summary>取得字段约束。Firebird 3+ 使用 GENERATED BY DEFAULT AS IDENTITY</summary>
    /// <param name="field">字段</param>
    /// <param name="onlyDefine">仅仅定义</param>
    /// <returns></returns>
    protected override String? GetFieldConstraints(IDataColumn field, Boolean onlyDefine)
    {
        if (onlyDefine && field.Identity)
        {
            var tail = field.PrimaryKey && field.Table.PrimaryKeys.Length <= 1 ? " Primary Key" : " NOT NULL";

            return " Generated By Default As Identity" + tail;
        }

        return base.GetFieldConstraints(field, onlyDefine);
    }
    #endregion

    #region DDL 执行方法
    /// <summary>建立数据库。Firebird 使用驱动静态方法创建数据库文件</summary>
    /// <param name="databaseName">数据库名，忽略</param>
    /// <param name="file">数据文件路径，忽略</param>
    /// <returns>是否成功</returns>
    [UnconditionalSuppressMessage("AOT", "IL2075", Justification = "Firebird 驱动的静态 CreateDatabase 方法在运行时按类型名解析，AOT 下不支持该动态驱动路径")]
    public override Boolean CreateDatabase(String databaseName, String? file = null)
    {
        var dbFile = Database.DatabaseName;
        if (dbFile.IsNullOrEmpty() || File.Exists(dbFile)) return true;

        var conn = Database.Factory.CreateConnection();
        if (conn == null) return false;

        var method = conn.GetType().GetMethod("CreateDatabase", BindingFlags.Public | BindingFlags.Static, null, [typeof(String)], null);
        if (method == null) return false;

        DAL.WriteLog("Creating database: {0}", dbFile);
        method.Invoke(null, [Database.ConnectionString]);

        return true;
    }
    #endregion
}
