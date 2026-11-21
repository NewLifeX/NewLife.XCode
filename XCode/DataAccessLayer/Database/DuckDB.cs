using System.Data;
using System.Data.Common;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;

namespace XCode.DataAccessLayer;

/// <summary>DuckDB嵌入式OLAP数据库。类似SQLite的嵌入式架构，支持列式分析查询</summary>
/// <remarks>
/// DuckDB是高性能嵌入式OLAP数据库，适用于数据分析场景。
/// 连接字符串示例：Data Source=/path/to/db.duckdb 或 Data Source=:memory:
/// 使用DuckDB.NET.Data作为ADO.NET驱动。
/// DuckDB SQL方言兼容PostgreSQL。
/// </remarks>
internal class DuckDB : FileDbBase
{
    #region 属性

    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.DuckDB;

    /// <summary>获取默认能力。DuckDB 支持 RETURNING 回读自增</summary>
    /// <returns>默认能力标志</returns>
    protected override DatabaseCapability GetDefaultCapabilities() => base.GetDefaultCapabilities()
        | DatabaseCapability.Identity
        | DatabaseCapability.WindowFunctions
        | DatabaseCapability.WindowFrame;

    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory()
    {
        var type = DriverLoader.Load("DuckDB.NET.Data.DuckDBClientFactory", null, "DuckDB.NET.Data.dll", null);
        var factory = GetProviderFactory(type);
        if (factory != null) return factory;

        return GetProviderFactory(null, "DuckDB.NET.Data.dll", "DuckDB.NET.Data.DuckDBClientFactory", true, true);
    }

    /// <summary>是否内存数据库</summary>
    public Boolean IsMemoryDatabase => DatabaseName.EqualIgnoreCase(MemoryDatabase);

    private static readonly String MemoryDatabase = ":memory:";

    protected override String OnResolveFile(String file)
    {
        if (String.IsNullOrEmpty(file) || file.EqualIgnoreCase(MemoryDatabase)) return MemoryDatabase;

        return base.OnResolveFile(file);
    }

    protected override void OnGetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnGetConnectionString(builder);

        // DuckDB连接字符串配置
        if (!Readonly)
        {
            // 允许访问外部文件（用于导入导出），对有安全需求的场景可以禁用
            // builder.TryAdd("access_mode", "automatic");
        }

        DAL.WriteLog(builder.ToString());
    }

    #endregion 属性

    #region 方法

    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new DuckDBSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new DuckDBMetaData();

    public override Boolean Support(String providerName)
    {
        providerName = providerName.ToLower();
        if (providerName.Contains("duckdb")) return true;

        return false;
    }

    #endregion 方法

    #region 数据库特性

    protected override String ReservedWordsStr => "ALL,ALTER,ANALYZE,AND,AS,ASC,ATTACH,BETWEEN,BY,CASE,CAST,CHECK,COLUMN,COMMIT,CONSTRAINT,COPY,CREATE,CROSS,DATABASE,DEFAULT,DELETE,DESC,DETACH,DISTINCT,DROP,ELSE,END,EXCEPT,EXISTS,EXPLAIN,EXPORT,FROM,FULL,FUNCTION,GRANT,GROUP,HAVING,IF,IMPORT,IN,INDEX,INNER,INSERT,INSTALL,INTERSECT,INTO,IS,JOIN,LEFT,LIMIT,LOAD,NOT,NULL,OFFSET,ON,OR,ORDER,OUTER,OVER,PRAGMA,PRIMARY,REFERENCES,RENAME,REPLACE,RIGHT,ROLLBACK,SCHEMA,SELECT,SET,SHOW,TABLE,THEN,TO,TRANSACTION,UNION,UNIQUE,UPDATE,USE,USING,VALUES,VIEW,WHEN,WHERE,WITH";

    /// <summary>格式化关键字</summary>
    /// <param name="keyWord">关键字</param>
    /// <returns></returns>
    public override String FormatKeyWord(String keyWord)
    {
        if (keyWord.IsNullOrEmpty()) return keyWord;

        if (keyWord.StartsWith("\"") && keyWord.EndsWith("\"")) return keyWord;

        return $"\"{keyWord}\"";
    }

    /// <summary>格式化数据为SQL数据</summary>
    /// <param name="field">字段</param>
    /// <param name="value">数值</param>
    /// <returns></returns>
    public override String FormatValue(IDataColumn field, Object? value)
    {
        var code = System.Type.GetTypeCode(field.DataType);
        if (code == TypeCode.Boolean)
        {
            return value.ToBoolean() ? "true" : "false";
        }

        return base.FormatValue(field, value);
    }

    /// <summary>格式化时间为SQL字符串</summary>
    /// <param name="column">字段</param>
    /// <param name="dateTime">时间值</param>
    /// <returns></returns>
    public override String FormatDateTime(IDataColumn column, DateTime dateTime)
    {
        if (dateTime.Ticks % 10_000_000 == 0)
            return $"'{dateTime:yyyy-MM-dd HH:mm:ss}'::TIMESTAMP";
        else
            return $"'{dateTime:yyyy-MM-dd HH:mm:ss.fffffff}'::TIMESTAMP";
    }

    /// <summary>长文本长度</summary>
    public override Int32 LongTextLength => 4000;

    protected internal override String ParamPrefix => "@";

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => (!String.IsNullOrEmpty(left) ? left : "''") + "||" + (!String.IsNullOrEmpty(right) ? right : "''");

    #endregion 数据库特性

    #region 分页

    /// <summary>已重写。DuckDB使用OFFSET/LIMIT分页（与PostgreSQL一致）</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <param name="keyColumn">主键列</param>
    /// <returns></returns>
    public override String PageSplit(String sql, Int64 startRowIndex, Int64 maximumRows, String? keyColumn)
    {
        // 从第一行开始，不需要分页
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
        // 从第一行开始，不需要分页
        if (startRowIndex <= 0)
        {
            if (maximumRows > 0) builder.Limit = $"limit {maximumRows}";
            return builder;
        }
        if (maximumRows < 1) throw new NotSupportedException(L.T("Fetching all rows after a given row is not supported!"));

        builder.Limit = $"offset {startRowIndex} limit {maximumRows}";
        return builder;
    }

    #endregion 分页
}

/// <summary>DuckDB数据库会话</summary>
internal class DuckDBSession : FileDbSession
{
    #region 构造函数

    public DuckDBSession(IDatabase db) : base(db) { }

    #endregion 构造函数

    #region 基本方法 查询/执行

    /// <summary>执行插入语句并返回新增行的自动编号。DuckDB使用RETURNING语法</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        sql += " RETURNING *";
        return base.InsertAndGetIdentity(sql, type, ps);
    }

    public override Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        sql += " RETURNING *";
        return base.InsertAndGetIdentityAsync(sql, type, ps);
    }

    #endregion 基本方法 查询/执行
}

/// <summary>DuckDB数据库元数据</summary>
internal class DuckDBMetaData : FileDbMetaData
{
    #region 构造函数

    public DuckDBMetaData() => Types = _DataTypes;

    #endregion 构造函数

    #region 数据类型

    /// <summary>数据类型映射。DuckDB 语法接近 PostgreSQL</summary>
    private static readonly Dictionary<Type, String[]> _DataTypes = new()
    {
        { typeof(Boolean), new String[] { "BOOLEAN" } },
        { typeof(SByte), new String[] { "TINYINT" } },
        { typeof(Byte), new String[] { "UTINYINT" } },
        { typeof(Int16), new String[] { "SMALLINT" } },
        { typeof(UInt16), new String[] { "USMALLINT" } },
        { typeof(Int32), new String[] { "INTEGER" } },
        { typeof(UInt32), new String[] { "UINTEGER" } },
        { typeof(Int64), new String[] { "BIGINT" } },
        { typeof(UInt64), new String[] { "UBIGINT" } },
        { typeof(Single), new String[] { "FLOAT" } },
        { typeof(Double), new String[] { "DOUBLE" } },
        { typeof(Decimal), new String[] { "DECIMAL" } },
        { typeof(DateTime), new String[] { "TIMESTAMP" } },
        { typeof(String), new String[] { "VARCHAR", "BLOB" } },
        { typeof(Byte[]), new String[] { "BLOB" } },
        { typeof(Guid), new String[] { "UUID" } },
    };

    #endregion

    /// <summary>建立数据库。DuckDB 会在首次连接时自行创建库文件，提前创建空文件反而导致文件无效</summary>
    /// <param name="databaseName">数据库名，忽略</param>
    /// <param name="file">数据文件路径，忽略</param>
    /// <returns>是否成功</returns>
    public override Boolean CreateDatabase(String databaseName, String? file = null)
    {
        if (String.IsNullOrEmpty(FileName) || FileName.EqualIgnoreCase(":memory:")) return false;

        // DuckDB 会在首次连接时创建库文件，这里只需保证目录存在
        var dir = Path.GetDirectoryName(FileName);
        if (!dir.IsNullOrEmpty()) Directory.CreateDirectory(dir);

        return true;
    }
}
