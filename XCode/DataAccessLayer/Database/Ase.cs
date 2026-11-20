using System.Data;
using System.Data.Common;
using NewLife.Log;

namespace XCode.DataAccessLayer;

/// <summary>SAP ASE(Sybase)数据库</summary>
/// <remarks>
/// SAP ASE（原 Sybase ASE）是 T-SQL 体系的关系型数据库。
/// 连接字符串示例：Data Source=localhost;Port=5000;Database=test;User ID=sa;Password=xxx
/// 使用 AdoNetCore.AseClient 开源 ADO.NET 驱动。
/// 自增列使用 IDENTITY，插入后通过 @@IDENTITY 回读；分页使用 TOP n START AT m（ASE 16+）。
/// </remarks>
internal class Ase : RemoteDb
{
    #region 属性
    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.Ase;

    /// <summary>系统数据库名</summary>
    public override String SystemDatabaseName => "master";

    /// <summary>默认名称格式。ASE 保留创建时的大小写</summary>
    public override NameFormats DefaultNameFormat => NameFormats.None;

    /// <summary>获取默认能力。SAP ASE 支持 IDENTITY 回读</summary>
    /// <returns>默认能力标志</returns>
    protected override DatabaseCapability GetDefaultCapabilities() => base.GetDefaultCapabilities() | DatabaseCapability.Identity;
    #endregion

    #region 方法
    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory() => LoadFactory("AdoNetCore.AseClient.AseClientFactory", "AdoNetCore.AseClient.dll");

    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new AseSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new AseMetaData();

    /// <summary>是否支持该提供者所描述的数据库</summary>
    /// <param name="providerName">提供者</param>
    /// <returns></returns>
    public override Boolean Support(String providerName)
    {
        var name = providerName.ToLower();
        return name.Contains("aseclient") || name.Contains("sybase") || name is "ase" or "sapase" or "sap ase";
    }
    #endregion

    #region 数据库特性
    /// <summary>关键字列表</summary>
    protected override String ReservedWordsStr =>
        "ADD,ALL,ALTER,AND,ANY,AS,ASC,BEGIN,BETWEEN,BREAK,BY,CASE,CHECK,CLOSE,COLUMN,COMMIT,CONSTRAINT,CONTINUE,CREATE,CURRENT,CURSOR,DATABASE,DECLARE,DEFAULT,DELETE,DESC,DISTINCT,DROP,ELSE,END,ESCAPE,EXEC,EXECUTE,EXISTS,FETCH,FOR,FOREIGN,FROM,FULL,GRANT,GROUP,HAVING,IDENTITY,IF,IN,INDEX,INNER,INSERT,INTO,IS,JOIN,KEY,LEFT,LIKE,NOT,NULL,OFF,ON,OPEN,OPTION,OR,ORDER,OUTER,PRIMARY,PRINT,PROC,PROCEDURE,PUBLIC,RAISERROR,REFERENCES,RETURN,REVOKE,RIGHT,ROLLBACK,SELECT,SET,SOME,TABLE,THEN,TO,TRAN,TRANSACTION,TRIGGER,TRUNCATE,UNION,UNIQUE,UPDATE,USER,VALUES,VIEW,WHEN,WHERE,WHILE,WITH," +
        "LOG,Role,Admin,Rank,Member,Groups,Error,MaxValue,MinValue,Signal,Offset";

    /// <summary>格式化标识符。ASE 支持双引号定界标识符</summary>
    /// <param name="keyWord">关键字</param>
    /// <returns></returns>
    public override String FormatKeyWord(String keyWord) => FormatKeyWord(keyWord, "\"");

    /// <summary>长文本长度。ASE VARCHAR 上限 16384</summary>
    public override Int32 LongTextLength => 16384;

    /// <summary>字符串相加。ASE 使用加号</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => (!left.IsNullOrEmpty() ? left : "''") + "+" + (!right.IsNullOrEmpty() ? right : "''");
    #endregion

    #region 分页
    /// <summary>已重写。ASE 使用 TOP n START AT m 分页</summary>
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

        return InsertTop(sql, $"Top {maximumRows} Start At {startRowIndex + 1}");
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

        builder = builder.Clone();
        builder.Column = $"Top {maximumRows} Start At {startRowIndex + 1} {(builder.Column.IsNullOrEmpty() ? "*" : builder.Column)}";

        return builder;
    }
    #endregion
}

/// <summary>SAP ASE数据库会话</summary>
internal class AseSession(IDatabase db) : RemoteDbSession(db)
{
    #region 基本方法 查询/执行
    /// <summary>执行插入语句并返回新增行的自动编号。ASE 使用 @@IDENTITY</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        var rs = Execute(sql, type, ps);
        if (rs <= 0) return 0;

        return ExecuteScalar<Int64>("Select @@IDENTITY");
    }

    /// <summary>执行插入语句并返回新增行的自动编号。ASE 使用 @@IDENTITY</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public override async Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        var rs = await ExecuteAsync(sql, type, ps).ConfigureAwait(false);
        if (rs <= 0) return 0;

        return await ExecuteScalarAsync<Int64>("Select @@IDENTITY").ConfigureAwait(false);
    }
    #endregion
}

/// <summary>SAP ASE数据库元数据</summary>
internal class AseMetaData : RemoteDbMetaData
{
    #region 构造函数
    public AseMetaData() => Types = _DataTypes;
    #endregion

    #region 数据类型
    /// <summary>数据类型映射</summary>
    private static readonly Dictionary<Type, String[]> _DataTypes = new()
    {
        { typeof(Byte[]), ["IMAGE", "VARBINARY({0})", "BINARY({0})"] },
        { typeof(Boolean), ["BIT"] },
        { typeof(Byte), ["TINYINT"] },
        { typeof(Int16), ["SMALLINT"] },
        { typeof(Int32), ["INTEGER", "INT"] },
        { typeof(Int64), ["BIGINT"] },
        { typeof(Single), ["REAL"] },
        { typeof(Double), ["FLOAT", "DOUBLE PRECISION"] },
        { typeof(Decimal), ["DECIMAL({0},{1})", "NUMERIC({0},{1})", "DECIMAL", "NUMERIC"] },
        { typeof(DateTime), ["DATETIME", "BIGDATETIME", "SMALLDATETIME", "DATE", "TIME"] },
        { typeof(String), ["VARCHAR({0})", "NVARCHAR({0})", "CHAR({0})", "TEXT", "UNITEXT"] },
    };

    /// <summary>取字段类型。VARCHAR 按长度格式化</summary>
    /// <param name="field">字段</param>
    /// <returns></returns>
    protected override String? GetFieldType(IDataColumn field)
    {
        if (field.DataType == typeof(String)) return SqlMetaDataHelper.GetStringFieldType(field, Database.LongTextLength, "TEXT");

        return base.GetFieldType(field);
    }

    /// <summary>取得字段约束。自增列使用 IDENTITY</summary>
    /// <param name="field">字段</param>
    /// <param name="onlyDefine">仅仅定义</param>
    /// <returns></returns>
    protected override String? GetFieldConstraints(IDataColumn field, Boolean onlyDefine)
    {
        var str = base.GetFieldConstraints(field, onlyDefine);
        if (onlyDefine && field.Identity) str = " IDENTITY" + str;

        return str;
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
            tableFilter = $" and name in ({list})";
            columnFilter = $" and o.name in ({list})";
        }

        var tables = $"select 'dbo' as table_schema, name as table_name, case type when 'V' then 'VIEW' else 'TABLE' end as table_type from sysobjects where type in ('U','V'){tableFilter} order by name";
        var columns = "select 'dbo' as table_schema, o.name as table_name, c.name as column_name, c.colid as ordinal_position, t.name as data_type, " +
            "case when (c.status & 8) = 8 then 'YES' else 'NO' end as is_nullable, " +
            "case when (c.status & 1) = 1 then 'YES' else 'NO' end as is_identity, " +
            "c.length as character_maximum_length, c.prec as numeric_precision, c.scale as numeric_scale " +
            $"from syscolumns c join sysobjects o on c.id = o.id join systypes t on c.usertype = t.usertype where o.type in ('U','V'){columnFilter} order by o.name, c.colid";

        return SqlMetaDataHelper.GetTables(Database, tables, columns, GetDataType);
    }
    #endregion
}
