using System.Data;
using System.Data.Common;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;

namespace XCode.DataAccessLayer;

/// <summary>ClickHouse列式分析数据库。基于HTTP协议，默认端口8123</summary>
/// <remarks>
/// ClickHouse是高性能列式分析型数据库，适用于OLAP场景。
/// 连接字符串示例：Host=localhost;Port=8123;Database=default;Username=default;Password=
/// 使用ClickHouse.Client作为ADO.NET驱动。
/// </remarks>
internal class ClickHouse : RemoteDb
{
    #region 属性

    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.ClickHouse;

    /// <summary>批量操作能力。ClickHouse支持批量Insert</summary>
    public override BatchCapability BatchCapability => BatchCapability.Insert;

    /// <summary>创建工厂</summary>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory()
    {
        var type = DriverLoader.Load("ClickHouse.Client.ADO.ClickHouseClientFactory", null, "ClickHouse.Client.dll", null);
        var factory = GetProviderFactory(type);
        if (factory != null) return factory;

        return GetProviderFactory(null, "ClickHouse.Client.dll", "ClickHouse.Client.ADO.ClickHouseClientFactory", true, true);
    }

    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        // ClickHouse默认端口8123
        if (builder["Port"].IsNullOrEmpty())
            builder["Port"] = "8123";

        // 默认数据库
        if (builder["Database"].IsNullOrEmpty())
            builder["Database"] = "default";
    }

    #endregion 属性

    #region 方法

    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new ClickHouseSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new ClickHouseMetaData();

    public override Boolean Support(String providerName)
    {
        providerName = providerName.ToLower();
        if (providerName.Contains("clickhouse")) return true;
        if (providerName.Contains("clickhouse.client")) return true;

        return false;
    }

    #endregion 方法

    #region 数据库特性

    protected override String ReservedWordsStr => "ADD,ALL,ALTER,AND,AS,ASC,BETWEEN,BY,CASE,CAST,CHECK,COLUMN,CREATE,CROSS,DATABASE,DEFAULT,DELETE,DESC,DISTINCT,DROP,ELSE,END,EXISTS,FINAL,FORMAT,FROM,FULL,GLOBAL,GRANT,GROUP,HAVING,IF,IN,INDEX,INNER,INSERT,INTO,IS,JOIN,KEY,LEFT,LIKE,LIMIT,LIVE,MATERIALIZED,NOT,NULL,OFFSET,ON,OPTION,OR,ORDER,OUTER,POPULATE,PREWHERE,PRIMARY,RENAME,REPLACE,RIGHT,SAMPLE,SELECT,SET,SETTINGS,SHOW,TABLE,THEN,TOTALS,UNION,UPDATE,USING,VALUES,VIEW,WHEN,WHERE,WITH";

    /// <summary>格式化关键字</summary>
    /// <param name="keyWord">关键字</param>
    /// <returns></returns>
    public override String FormatKeyWord(String keyWord)
    {
        if (keyWord.IsNullOrEmpty()) return keyWord;

        if (keyWord.StartsWith("`") && keyWord.EndsWith("`")) return keyWord;

        return $"`{keyWord}`";
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
            return value.ToBoolean() ? "1" : "0";
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
            return $"'{dateTime:yyyy-MM-dd HH:mm:ss}'";
        else
            return $"'{dateTime:yyyy-MM-dd HH:mm:ss.fffffff}'";
    }

    /// <summary>长文本长度</summary>
    public override Int32 LongTextLength => 4000;

    protected internal override String ParamPrefix => "@";

    /// <summary>系统数据库名</summary>
    public override String SystemDatabaseName => "system";

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => $"concat({(!String.IsNullOrEmpty(left) ? left : "\'\'")},{(!String.IsNullOrEmpty(right) ? right : "\'\'")})";

    /// <summary>生成批量删除SQL。ClickHouse不支持DELETE LIMIT，直接返回标准DELETE</summary>
    /// <param name="tableName"></param>
    /// <param name="where"></param>
    /// <param name="batchSize"></param>
    /// <returns></returns>
    public override String? BuildDeleteSql(String tableName, String where, Int32 batchSize)
    {
        // ClickHouse的DELETE是异步的，且不支持LIMIT，直接使用标准删除
        return base.BuildDeleteSql(tableName, where, 0);
    }

    #endregion 数据库特性

    #region 分页

    /// <summary>已重写。ClickHouse使用OFFSET/LIMIT分页</summary>
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
        if (maximumRows < 1) throw new NotSupportedException("不支持取第几条数据之后的所有数据！");

        return $"{sql} limit {maximumRows} offset {startRowIndex}";
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
        if (maximumRows < 1) throw new NotSupportedException("不支持取第几条数据之后的所有数据！");

        builder.Limit = $"limit {maximumRows} offset {startRowIndex}";
        return builder;
    }

    #endregion 分页
}

/// <summary>ClickHouse数据库会话</summary>
internal class ClickHouseSession : RemoteDbSession
{
    #region 构造函数

    public ClickHouseSession(IDatabase db) : base(db) { }

    #endregion 构造函数

    #region 基本方法 查询/执行

    /// <summary>ClickHouse不支持自增主键，InsertAndGetIdentity不需要特殊处理</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        // ClickHouse不支持自增ID和RETURNING，直接执行插入返回0
        Execute(sql, type, ps);
        return 0;
    }

    public override Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        Execute(sql, type, ps);
        return Task.FromResult((Int64)0);
    }

    #endregion 基本方法 查询/执行

    #region 快速查询单表记录数

    /// <summary>快速查询单表记录数。ClickHouse通过system.parts估算</summary>
    /// <param name="tableName"></param>
    /// <returns></returns>
    public override Int64 QueryCountFast(String tableName)
    {
        tableName = tableName.Trim().Trim('`', '`').Trim();

        var db = Database.DatabaseName;
        var sql = $"select sum(rows) from system.parts where database='{db}' and table='{tableName}'";
        return ExecuteScalar<Int64>(sql);
    }

    public override Task<Int64> QueryCountFastAsync(String tableName)
    {
        tableName = tableName.Trim().Trim('`', '`').Trim();

        var db = Database.DatabaseName;
        var sql = $"select sum(rows) from system.parts where database='{db}' and table='{tableName}'";
        return ExecuteScalarAsync<Int64>(sql);
    }

    #endregion 快速查询单表记录数
}

/// <summary>ClickHouse数据库元数据</summary>
internal class ClickHouseMetaData : RemoteDbMetaData
{
    #region 构造函数

    public ClickHouseMetaData() { }

    #endregion 构造函数

    #region 表信息

    /// <summary>获取所有表</summary>
    /// <returns></returns>
    protected override List<IDataTable> OnGetTables(String[]? names)
    {
        var ss = Database.CreateSession();
        var list = new List<IDataTable>();

        var old = ss.ShowSQL;
        ss.ShowSQL = false;
        try
        {
            // ClickHouse查询表信息
            var sql = "select database, name, engine, total_rows, total_bytes, metadata_modification_time from system.tables";
            if (names != null && names.Length > 0)
            {
                var dbName = names[0];
                if (!dbName.IsNullOrEmpty())
                    sql += $" where database='{dbName}'";
            }
            sql += " order by database, name";

            var dt = ss.Query(sql, null);
            if (dt.Rows.Count == 0) return list;

            var hs = new HashSet<String>(names ?? [], StringComparer.OrdinalIgnoreCase);

            foreach (var dr in dt)
            {
                var tableName = dr["name"] + "";
                if (tableName.IsNullOrEmpty() || hs.Count > 0 && !hs.Contains(tableName)) continue;

                var table = DAL.CreateTable();
                table.TableName = tableName;
                table.Description = dr["engine"] + "";
                table.DbType = DatabaseType.ClickHouse;

                table.Fix();

                list.Add(table);
            }
        }
        finally
        {
            ss.ShowSQL = old;
        }

        return list;
    }

    #endregion 表信息
}
