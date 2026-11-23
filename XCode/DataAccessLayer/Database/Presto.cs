using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using NewLife;
using NewLife.Data;
using NewLife.Log;

namespace XCode.DataAccessLayer;

/// <summary>快速计数模式</summary>
internal enum FastCountMode
{
    /// <summary>关闭。始终使用精确计数</summary>
    Off,

    /// <summary>仅使用 SHOW STATS FOR</summary>
    Stats,

    /// <summary>仅使用 Iceberg 元数据表</summary>
    Iceberg,

    /// <summary>自动。优先 Iceberg，其次 SHOW STATS，最后精确计数</summary>
    Auto,
}

/// <summary>Presto/Trino 数据库基类</summary>
/// <remarks>
/// Presto 与 Trino 同源（PrestoSQL 于 2020 年更名 Trino），SQL 方言一致，仅 HTTP 协议头与驱动不同。
/// 两者均为联邦查询引擎：读操作所有连接器可用，写操作与事务取决于底层连接器，
/// 且没有自增主键、约束强制与索引，因此 XCode 将其定位为“只读分析 + 显式写入”，不提供自动建表与事务。
/// </remarks>
internal abstract class PrestoBase : RemoteDb
{
    #region 属性
    /// <summary>系统数据库名。Presto/Trino 提供 system 目录存放运行时元数据</summary>
    public override String SystemDatabaseName => "system";

    /// <summary>默认名称格式。Presto/Trino 未加引号的标识符折叠为小写</summary>
    public override NameFormats DefaultNameFormat => NameFormats.Lower;

    /// <summary>创建能力配置。Presto/Trino 为只读分析 + 显式写入，不支持事务与自动建表</summary>
    /// <returns>能力配置</returns>
    protected override DatabaseProfile CreateProfile() => new()
    {
        Model = DatabaseModel.Federated,
        QueryLanguage = QueryLanguage.Sql,
        Capabilities = DatabaseCapability.Sql
            | DatabaseCapability.Joins
            | DatabaseCapability.Subqueries
            | DatabaseCapability.GroupBy
            | DatabaseCapability.Having
            | DatabaseCapability.Union
            | DatabaseCapability.OffsetPaging
            | DatabaseCapability.LimitPaging
            | DatabaseCapability.SchemaDiscovery
            | DatabaseCapability.WindowFunctions
            | DatabaseCapability.WindowFrame,
        MaxMigration = Migration.Off,
        // Presto/Trino 的 varchar 不限定长度，长文本阈值取无穷
        LongTextLength = Int32.MaxValue,
        ParameterPrefix = "@",
    };

    /// <summary>模式名。对应连接字符串的 Schema</summary>
    public String? SchemaName { get; set; }

    /// <summary>快速计数模式。连接字符串 FastCount 控制，默认 Auto</summary>
    public FastCountMode FastCount { get; set; } = FastCountMode.Auto;
    #endregion

    #region 构造
    protected PrestoBase()
    {
        // 无自增主键与索引，DDL 依赖连接器属性，不能自动建表
        Migration = DataAccessLayer.Migration.Off;

        // NewLife.Presto 在客户端把 @name 安全替换为字面量，兼容 XCode 的参数前缀
        UseParameter = true;
    }
    #endregion

    #region 方法
    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    protected override IDbSession OnCreateSession() => new PrestoSession(this);

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData OnCreateMetaData() => new PrestoMetaData();

    /// <summary>设置连接字符串</summary>
    /// <param name="builder"></param>
    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        // 兼容 XCode 的习惯写法：Database 表示 Catalog，Owner 表示 Schema
        if (builder["Catalog"].IsNullOrEmpty() && !DatabaseName.IsNullOrEmpty()) builder["Catalog"] = DatabaseName;
        if (builder["Schema"].IsNullOrEmpty() && !Owner.IsNullOrEmpty()) builder["Schema"] = Owner;

        // 兼容 Server / Data Source 写法
        var host = builder["Host"];
        if (host.IsNullOrEmpty()) host = builder["Server"];
        if (host.IsNullOrEmpty()) host = builder["Data Source"];
        if (host.IsNullOrEmpty()) host = builder["DataSource"];
        if (!host.IsNullOrEmpty())
        {
            builder["Host"] = host;
            builder.TryGetAndRemove("Server", out _);
            builder.TryGetAndRemove("Data Source", out _);
            builder.TryGetAndRemove("DataSource", out _);
        }

        // 驱动不认识 XCode 的数据库名写法，统一移除
        builder.TryGetAndRemove("Database", out _);
        builder.TryGetAndRemove("Db", out _);
        builder.TryGetAndRemove("Initial Catalog", out _);

        SchemaName = builder["Schema"];

        // FastCount 是 XCode 行为开关，必须从连接字符串移除，避免旧驱动因未知参数报错
        FastCount = FastCountMode.Auto;
        if (builder.TryGetAndRemove(nameof(FastCount), out var fastCount) && !fastCount.IsNullOrEmpty())
            FastCount = ParseFastCount(fastCount);
    }

    private static FastCountMode ParseFastCount(String value) => value.Trim().ToLowerInvariant() switch
    {
        "off" or "none" or "false" or "0" => FastCountMode.Off,
        "stats" or "statistics" => FastCountMode.Stats,
        "iceberg" => FastCountMode.Iceberg,
        _ => FastCountMode.Auto,
    };

    /// <summary>加载 NewLife.Presto 驱动工厂。只做本地加载，未安装时提示包名，避免运行时联网下载</summary>
    /// <returns></returns>
    internal static DbProviderFactory? LoadPrestoFactory()
    {
        var type = DriverLoader.Load("NewLife.Presto.PrestoClientFactory", null, "NewLife.Presto.dll", null);
        var factory = GetProviderFactory(type);
        if (factory == null)
        {
            var package = DriverPackages.Get(DatabaseType.Presto);
            throw new XCodeException(L.T("Unable to load driver [{0}]; add the NuGet package [{1}] and try again.", "NewLife.Presto.dll", package));
        }

        return factory;
    }
    #endregion

    #region 数据库特性
    /// <summary>关键字列表。Trino 保留字</summary>
    protected override String ReservedWordsStr =>
        "ALTER,AND,AS,BETWEEN,BY,CASE,CAST,CONSTRAINT,CREATE,CROSS,CUBE,CURRENT_CATALOG,CURRENT_DATE,CURRENT_PATH,CURRENT_ROLE,CURRENT_SCHEMA,CURRENT_TIME,CURRENT_TIMESTAMP,CURRENT_USER,DEALLOCATE,DELETE,DESCRIBE,DISTINCT,DROP,ELSE,END,ESCAPE,EXCEPT,EXECUTE,EXISTS,EXTRACT,FALSE,FOR,FROM,FULL,GROUP,GROUPING,HAVING,IN,INNER,INSERT,INTERSECT,INTERVAL,INTO,IS,JOIN,JSON_ARRAY,JSON_EXISTS,JSON_OBJECT,JSON_QUERY,JSON_TABLE,JSON_VALUE,LEFT,LIKE,LISTAGG,LOCALTIME,LOCALTIMESTAMP,NATURAL,NORMALIZE,NOT,NULL,OFFSET,ON,OR,ORDER,OUTER,PREPARE,RECURSIVE,RIGHT,ROLLUP,SELECT,SKIP,TABLE,THEN,TRUE,UESCAPE,UNION,UNNEST,USING,VALUES,WHEN,WHERE,WITH," +
        "LOG,User,Role,Admin,Rank,Member,Groups,Error,MaxValue,MinValue,Signal,Offset";

    /// <summary>格式化关键字。Presto/Trino 使用双引号，内部双引号成对转义</summary>
    /// <param name="keyWord">关键字</param>
    /// <returns></returns>
    public override String FormatKeyWord(String keyWord) => FormatKeyWord(keyWord, "\"");

    /// <summary>格式化数据为SQL数据</summary>
    /// <param name="field">字段</param>
    /// <param name="value">数值</param>
    /// <returns></returns>
    public override String FormatValue(IDataColumn field, Object? value)
    {
        if (value != null)
        {
            if (field?.DataType == typeof(Boolean)) return value.ToBoolean() ? "true" : "false";

            if (field?.DataType == typeof(Byte[]) || value is Byte[])
            {
                var bts = value as Byte[];
                if (bts == null || bts.Length == 0) return field?.Nullable == false ? "X''" : "null";

                return "X'" + BitConverter.ToString(bts).Replace("-", null) + "'";
            }
        }

        return base.FormatValue(field!, value);
    }

    /// <summary>格式化时间为SQL字符串。使用 TIMESTAMP 类型化字面量，避免字符串与时间字段比较时的隐式转换差异</summary>
    /// <param name="column">字段</param>
    /// <param name="dateTime">时间值</param>
    /// <returns></returns>
    public override String FormatDateTime(IDataColumn column, DateTime dateTime) => FormatDateTimeLiteral(dateTime);

    /// <summary>格式化模糊搜索。Presto/Trino 默认无转义字符，含通配符时需要显式 ESCAPE</summary>
    /// <param name="column">字段</param>
    /// <param name="format">格式化字符串</param>
    /// <param name="value">数值</param>
    /// <returns></returns>
    public override String FormatLike(IDataColumn column, String format, String value)
    {
        var line = base.FormatLike(column, format, value);
        if (line.IsNullOrEmpty() || value.IsNullOrEmpty()) return line;

        if (value.IndexOfAny(['%', '_']) < 0) return line;

        return format.Contains("{1}") ? line + " Escape '\\'" : line;
    }

    /// <summary>长文本长度。Presto/Trino 的 varchar 不限定长度</summary>
    public override Int32 LongTextLength => Int32.MaxValue;

    /// <summary>参数前缀。未启用参数化，仅保持接口一致</summary>
    protected internal override String ParamPrefix => "@";

    /// <summary>字符串相加</summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public override String StringConcat(String left, String right) => $"concat({(!left.IsNullOrEmpty() ? left : "''")},{(!right.IsNullOrEmpty() ? right : "''")})";
    #endregion

    #region 分页
    /// <summary>已重写。Presto/Trino 使用 OFFSET/LIMIT 分页</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <param name="keyColumn">主键列</param>
    /// <returns></returns>
    public override String PageSplit(String sql, Int64 startRowIndex, Int64 maximumRows, String? keyColumn) => PageSplitByLimitOffset(sql, startRowIndex, maximumRows, true);

    /// <summary>构造分页SQL</summary>
    /// <param name="builder">查询生成器</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns>分页SQL</returns>
    public override SelectBuilder PageSplit(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows) => PageSplitByLimitOffset(builder, startRowIndex, maximumRows, true);
    #endregion
}

/// <summary>Presto数据库。使用 NewLife.Presto 纯托管驱动</summary>
internal class Presto : PrestoBase
{
    #region 属性
    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.Presto;
    #endregion

    #region 方法
    /// <summary>创建工厂。驱动独立发布为 NewLife.Presto 包，按类型名反射加载</summary>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory() => LoadPrestoFactory();

    /// <summary>是否支持该提供者所描述的数据库</summary>
    /// <param name="providerName">提供者</param>
    /// <returns></returns>
    public override Boolean Support(String providerName) => providerName.ToLower().Contains("presto");
    #endregion

    #region 数据库特性
    /// <summary>设置连接字符串。使用 Host/Port/Catalog/Schema/User/Password/UseSsl</summary>
    /// <param name="builder"></param>
    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        // 旧驱动使用 TrinoHeaders 开关，新驱动统一用 Protocol
        if (builder.TryGetAndRemove("TrinoHeaders", out var trinoHeaders) && !trinoHeaders.IsNullOrEmpty() && trinoHeaders.ToBoolean())
            builder["Protocol"] = "Trino";

        if (builder["UseSsl"].IsNullOrEmpty()) builder["UseSsl"] = "false";
        if (builder["Port"].IsNullOrEmpty()) builder["Port"] = "8080";

        // 固定使用 Presto 协议头，Trino 请使用 DatabaseType.Trino
        if (builder["Protocol"].IsNullOrEmpty()) builder["Protocol"] = "Presto";
    }
    #endregion
}

/// <summary>Trino数据库。使用 NewLife.Presto 驱动，切换为 Trino 协议头</summary>
internal class Trino : PrestoBase
{
    #region 属性
    /// <summary>返回数据库类型</summary>
    public override DatabaseType Type => DatabaseType.Trino;
    #endregion

    #region 方法
    /// <summary>创建工厂。Presto 与 Trino 共用 NewLife.Presto 驱动</summary>
    /// <returns></returns>
    protected override DbProviderFactory? CreateFactory() => LoadPrestoFactory();

    /// <summary>是否支持该提供者所描述的数据库</summary>
    /// <param name="providerName">提供者</param>
    /// <returns></returns>
    public override Boolean Support(String providerName) => providerName.ToLower().Contains("trino");
    #endregion

    #region 数据库特性
    /// <summary>设置连接字符串。新驱动统一使用 UseSsl/Protocol</summary>
    /// <param name="builder"></param>
    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);

        // 旧 Trino 驱动使用 EnableSSL，新驱动统一为 UseSsl
        if (builder["UseSsl"].IsNullOrEmpty() && builder.TryGetAndRemove("EnableSSL", out var enableSsl) && !enableSsl.IsNullOrEmpty())
            builder["UseSsl"] = enableSsl;

        if (builder["UseSsl"].IsNullOrEmpty()) builder["UseSsl"] = "false";
        if (builder["Port"].IsNullOrEmpty() && !builder["UseSsl"].ToBoolean()) builder["Port"] = "8080";

        // 切换为 Trino 协议头（X-Trino-*）
        builder.TryGetAndRemove("TrinoHeaders", out _);
        builder["Protocol"] = "Trino";

        // 打开连接时不做额外探测，避免每次建连都多一次 HTTP 往返
        if (builder["TestConnection"].IsNullOrEmpty()) builder["TestConnection"] = "false";
    }
    #endregion
}

/// <summary>Presto/Trino 数据库会话</summary>
internal class PrestoSession(IDatabase db) : RemoteDbSession(db)
{
    #region 快速计数
    /// <summary>Iceberg 元数据不可靠（存在 delete 文件），需要精确计数</summary>
    private const Int64 IcebergUnreliable = -2;

    /// <summary>快速查询单表记录数。按 FastCount 模式选择 Iceberg 元数据或 SHOW STATS，失败返回 -1</summary>
    /// <param name="tableName">已格式化的表名</param>
    /// <returns>表行数；无法快速获取时返回 -1，由上层回退精确计数</returns>
    public override Int64 QueryCountFast(String tableName)
    {
        if (String.IsNullOrEmpty(tableName)) return 0;

        var mode = (Database as PrestoBase)?.FastCount ?? FastCountMode.Auto;
        if (mode == FastCountMode.Off) return -1;

        if (mode is FastCountMode.Auto or FastCountMode.Iceberg)
        {
            var count = QueryIcebergCount(tableName);
            if (count >= 0) return count;
            if (count == IcebergUnreliable) return -1;
            if (mode == FastCountMode.Iceberg) return -1;
        }

        return QueryStatsCount(tableName);
    }

    /// <summary>快速查询单表记录数。按 FastCount 模式选择 Iceberg 元数据或 SHOW STATS，失败返回 -1</summary>
    /// <param name="tableName">已格式化的表名</param>
    /// <returns>表行数；无法快速获取时返回 -1，由上层回退精确计数</returns>
    public override async Task<Int64> QueryCountFastAsync(String tableName)
    {
        if (String.IsNullOrEmpty(tableName)) return 0;

        var mode = (Database as PrestoBase)?.FastCount ?? FastCountMode.Auto;
        if (mode == FastCountMode.Off) return -1;

        if (mode is FastCountMode.Auto or FastCountMode.Iceberg)
        {
            var count = await QueryIcebergCountAsync(tableName).ConfigureAwait(false);
            if (count >= 0) return count;
            if (count == IcebergUnreliable) return -1;
            if (mode == FastCountMode.Iceberg) return -1;
        }

        return await QueryStatsCountAsync(tableName).ConfigureAwait(false);
    }

    /// <summary>SHOW STATS FOR 取表级 row_count。连接器没有统计时该值为 NULL</summary>
    private Int64 QueryStatsCount(String tableName)
    {
        try
        {
            var ds = Query($"SHOW STATS FOR {tableName}", CommandType.Text);
            if (ds.Tables.Count == 0) return -1;

            return GetRowCount(ds.Tables[0]);
        }
        catch (Exception ex)
        {
            if (DAL.Debug) XTrace.Log.Debug("Failed to get fast row count for [{0}] by stats: {1}", tableName, ex.Message);
            return -1;
        }
    }

    private async Task<Int64> QueryStatsCountAsync(String tableName)
    {
        try
        {
            var dt = await QueryAsync($"SHOW STATS FOR {tableName}", null).ConfigureAwait(false);

            return GetRowCount(dt);
        }
        catch (Exception ex)
        {
            if (DAL.Debug) XTrace.Log.Debug("Failed to get fast row count for [{0}] by stats: {1}", tableName, ex.Message);
            return -1;
        }
    }

    /// <summary>Iceberg 元数据取当前 main 分支快照的 total-records</summary>
    private Int64 QueryIcebergCount(String tableName)
    {
        var snapshots = GetMetadataTableName(tableName, "$snapshots");
        var refs = GetMetadataTableName(tableName, "$refs");

        // 有 delete 文件时 total-records 不是最终行数，退回精确计数
        if (TryQueryIcebergSummary($"SELECT CAST(element_at(s.summary, 'total-records') AS BIGINT), CAST(element_at(s.summary, 'total-delete-files') AS BIGINT) FROM {snapshots} s JOIN {refs} r ON s.snapshot_id = r.snapshot_id WHERE r.name = 'main'", out var count, out var deletes))
            return deletes > 0 ? IcebergUnreliable : count;

        // 兼容没有 $refs 的旧版本，取最近提交的快照
        if (TryQueryIcebergSummary($"SELECT CAST(element_at(summary, 'total-records') AS BIGINT), CAST(element_at(summary, 'total-delete-files') AS BIGINT) FROM {snapshots} ORDER BY committed_at DESC LIMIT 1", out count, out deletes))
            return deletes > 0 ? IcebergUnreliable : count;

        return -1;
    }

    private async Task<Int64> QueryIcebergCountAsync(String tableName)
    {
        var snapshots = GetMetadataTableName(tableName, "$snapshots");
        var refs = GetMetadataTableName(tableName, "$refs");

        var summary = await TryQueryIcebergSummaryAsync($"SELECT CAST(element_at(s.summary, 'total-records') AS BIGINT), CAST(element_at(s.summary, 'total-delete-files') AS BIGINT) FROM {snapshots} s JOIN {refs} r ON s.snapshot_id = r.snapshot_id WHERE r.name = 'main'").ConfigureAwait(false);
        if (summary != null) return summary.Deletes > 0 ? IcebergUnreliable : summary.Count;

        summary = await TryQueryIcebergSummaryAsync($"SELECT CAST(element_at(summary, 'total-records') AS BIGINT), CAST(element_at(summary, 'total-delete-files') AS BIGINT) FROM {snapshots} ORDER BY committed_at DESC LIMIT 1").ConfigureAwait(false);
        if (summary != null) return summary.Deletes > 0 ? IcebergUnreliable : summary.Count;

        return -1;
    }

    private Boolean TryQueryIcebergSummary(String sql, out Int64 count, out Int64 deletes)
    {
        count = -1;
        deletes = 0;
        try
        {
            var ds = Query(sql, CommandType.Text);
            if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0) return false;

            var row = ds.Tables[0].Rows[0];
            if (row[0] == null || row[0] == DBNull.Value) return false;

            count = row[0].ToLong();
            if (ds.Tables[0].Columns.Count > 1 && row[1] != null && row[1] != DBNull.Value) deletes = row[1].ToLong();

            return true;
        }
        catch (Exception ex)
        {
            if (DAL.Debug) XTrace.Log.Debug("Failed to get Iceberg row count: {0}", ex.Message);
            return false;
        }
    }

    private async Task<IcebergSummary?> TryQueryIcebergSummaryAsync(String sql)
    {
        try
        {
            var dt = await QueryAsync(sql, null).ConfigureAwait(false);
            if (dt.Rows.Count == 0) return null;

            var index = FindColumn(dt.Columns, "total_records");
            if (index < 0) index = 0;

            var value = dt.Rows[0][index];
            if (value == null || value == DBNull.Value) return null;

            var deletes = 0L;
            var deleteIndex = FindColumn(dt.Columns, "delete_files");
            if (deleteIndex < 0) deleteIndex = 1;
            if (dt.Rows[0].Length > deleteIndex && dt.Rows[0][deleteIndex] != null && dt.Rows[0][deleteIndex] != DBNull.Value)
                deletes = dt.Rows[0][deleteIndex].ToLong();

            return new IcebergSummary { Count = value.ToLong(), Deletes = deletes };
        }
        catch (Exception ex)
        {
            if (DAL.Debug) XTrace.Log.Debug("Failed to get Iceberg row count: {0}", ex.Message);
            return null;
        }
    }

    private static Int64 GetRowCount(DataTable table)
    {
        if (table.Rows.Count == 0) return -1;

        var index = FindColumn(table.Columns, "row_count");
        if (index < 0 && table.Columns.Count > 4) index = 4;
        if (index < 0) return -1;

        var value = table.Rows[table.Rows.Count - 1][index];
        return value == null || value == DBNull.Value ? -1 : value.ToLong();
    }

    private static Int64 GetRowCount(DbTable table)
    {
        if (table.Rows.Count == 0) return -1;

        var index = FindColumn(table.Columns, "row_count");
        if (index < 0 && table.Columns.Length > 4) index = 4;
        if (index < 0) return -1;

        var value = table.Rows[table.Rows.Count - 1][index];
        return value == null || value == DBNull.Value ? -1 : value.ToLong();
    }

    private static Int32 FindColumn(DataColumnCollection columns, String name)
    {
        for (var i = 0; i < columns.Count; i++)
            if (columns[i].ColumnName.EqualIgnoreCase(name)) return i;

        return -1;
    }

    private static Int32 FindColumn(String[] columns, String name)
    {
        for (var i = 0; i < columns.Length; i++)
            if (columns[i].EqualIgnoreCase(name)) return i;

        return -1;
    }

    private static String GetMetadataTableName(String tableName, String suffix)
    {
        var name = tableName.Trim();
        if (name.Length > 1 && name[0] == '"' && name[^1] == '"') name = name[1..^1].Replace("\"\"", "\"");
        return "\"" + name.Replace("\"", "\"\"") + suffix + "\"";
    }

    private sealed class IcebergSummary
    {
        public Int64 Count { get; set; }
        public Int64 Deletes { get; set; }
    }
    #endregion

    /// <summary>执行插入语句并返回新增行的自动编号。Presto/Trino 没有自增主键</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    /// <exception cref="NotSupportedException">Presto/Trino 不支持自增列</exception>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
        => throw new NotSupportedException(L.T("Presto/Trino does not support auto-increment columns; use a business key (Guid/snowflake) as the primary key, or set AllowInsertIdentity."));

    /// <summary>执行插入语句并返回新增行的自动编号。Presto/Trino 没有自增主键</summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="type">命令类型</param>
    /// <param name="ps">命令参数</param>
    /// <returns>新增行的自动编号</returns>
    /// <exception cref="NotSupportedException">Presto/Trino 不支持自增列</exception>
    public override Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
        => FromException(new NotSupportedException(L.T("Presto/Trino does not support auto-increment columns; use a business key (Guid/snowflake) as the primary key, or set AllowInsertIdentity.")));

    #region 事务
    /// <summary>开始事务。Presto/Trino 没有事务，直接抛出明确异常</summary>
    /// <param name="level">事务隔离级别</param>
    /// <returns></returns>
    public override Int32 BeginTransaction(IsolationLevel level) => throw new NotSupportedException(L.T("Presto/Trino does not support transactions; the underlying connector may provide limited write semantics only."));
    #endregion
}

/// <summary>Presto/Trino 数据库元数据</summary>
internal class PrestoMetaData : RemoteDbMetaData
{
    #region 构造函数
    public PrestoMetaData() => Types = _DataTypes;
    #endregion

    #region 数据类型
    /// <summary>数据类型映射</summary>
    private static readonly Dictionary<Type, String[]> _DataTypes = new()
    {
        { typeof(Byte[]), ["varbinary"] },
        { typeof(Boolean), ["boolean"] },
        { typeof(Byte), ["tinyint"] },
        { typeof(Int16), ["smallint"] },
        { typeof(Int32), ["integer"] },
        { typeof(Int64), ["bigint"] },
        { typeof(Single), ["real"] },
        { typeof(Double), ["double"] },
        { typeof(Decimal), ["decimal({0},{1})", "decimal"] },
        { typeof(DateTime), ["timestamp", "timestamp with time zone", "date"] },
        { typeof(TimeSpan), ["time", "time with time zone"] },
        { typeof(String), ["varchar"] },
    };

    /// <summary>获取Net数据类型。复杂类型（array/map/row/json）统一降级为字符串</summary>
    /// <param name="field"></param>
    /// <returns></returns>
    protected override Type? GetDataType(IDataColumn field)
    {
        var type = base.GetDataType(field);
        if (type != null) return type;

        var rawType = field.RawType;
        if (rawType.IsNullOrEmpty()) return null;

        var index = rawType.IndexOf('(');
        var name = (index >= 0 ? rawType[..index] : rawType).Trim();
        if (name.EqualIgnoreCase("array", "map", "row", "json", "char")) return typeof(String);

        return null;
    }

    /// <summary>修正字段的长度精度。Presto 的 information_schema.columns 不提供长度与精度列，需要从 data_type 解析</summary>
    /// <param name="column">字段</param>
    internal static void FixColumn(IDataColumn column)
    {
        if (column == null || column.RawType.IsNullOrEmpty()) return;

        var raw = column.RawType.Trim();
        var index = raw.IndexOf('(');
        var name = (index >= 0 ? raw[..index] : raw).Trim();
        var args = index >= 0 ? raw[(index + 1)..] : null;
        if (args != null)
        {
            var end = args.IndexOf(')');
            if (end >= 0) args = args[..end];
            args = args.Trim();
        }

        switch (name.ToLowerInvariant())
        {
            // Hive 的 string 映射为无长度 varchar，按长文本处理
            case "varchar":
                column.Length = Int32.TryParse(args, out var length) ? length : -1;
                break;
            // char 缺省长度为 1
            case "char":
                column.Length = Int32.TryParse(args, out length) ? length : 1;
                break;
            case "decimal":
            case "numeric":
                if (args.IsNullOrEmpty())
                {
                    column.Precision = 38;
                    column.Scale = 0;
                }
                else
                {
                    var ss = args.Split(',');
                    if (Int32.TryParse(ss[0].Trim(), out var precision)) column.Precision = precision;
                    if (ss.Length > 1 && Int32.TryParse(ss[1].Trim(), out var scale)) column.Scale = scale;
                }
                break;
            // 整数与浮点的精度取类型位宽对应的十进制位数，使模型与 XCode 默认值一致
            case "tinyint":
                column.Precision = 3;
                column.Scale = 0;
                break;
            case "smallint":
                column.Precision = 5;
                column.Scale = 0;
                break;
            case "integer":
                column.Precision = 10;
                column.Scale = 0;
                break;
            case "bigint":
                column.Precision = 19;
                column.Scale = 0;
                break;
            case "real":
                column.Precision = 12;
                column.Scale = 2;
                break;
            case "double":
                column.Precision = 22;
                column.Scale = 4;
                break;
            // 复杂类型统一映射为字符串，按长文本处理
            case "array":
            case "map":
            case "row":
            case "json":
                column.Length = -1;
                break;
        }
    }
    #endregion

    #region 表信息
    /// <summary>快速取得所有表名</summary>
    /// <returns></returns>
    public override IList<String> GetTableNames()
    {
        var where = SqlMetaDataHelper.BuildWhere((Database as PrestoBase)?.SchemaName, null);
        using var ss = Database.CreateSession();
        var ds = ss.Query($"select table_name from information_schema.tables{where}", CommandType.Text);
        if (ds.Tables.Count == 0) return [];

        return ds.Tables[0].Rows.Cast<DataRow>()
            .Select(e => e["table_name"] + "")
            .Where(e => !e.IsNullOrEmpty())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>获取所有表</summary>
    /// <param name="names">指定表名</param>
    /// <returns></returns>
    protected override List<IDataTable> OnGetTables(String[]? names)
    {
        var schema = (Database as PrestoBase)?.SchemaName;
        var list = new List<IDataTable>();

        // 部分老版 Hive Metastore 不支持一次性列出整库字段（get_table_req），先取表清单，再按表名分批查字段
        var tableNames = names;
        if (tableNames == null || tableNames.Length == 0)
        {
            var where = SqlMetaDataHelper.BuildWhere(schema, null);
            using var ss = Database.CreateSession();
            var ds = ss.Query($"select table_name from information_schema.tables{where}", CommandType.Text);
            if (ds.Tables.Count == 0) return list;

            tableNames = ds.Tables[0].Rows.Cast<DataRow>()
                .Select(e => e["table_name"] + "")
                .Where(e => !e.IsNullOrEmpty())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        // 分批查询，避免单条 SQL 携带过多表名
        const Int32 batchSize = 100;
        for (var i = 0; i < tableNames.Length; i += batchSize)
        {
            var batch = tableNames.Skip(i).Take(batchSize).ToArray();
            if (!TryGetTables(schema, batch, list) && batch.Length > 1)
            {
                // 单个坏表（如无法解析的 Hive 视图）会让整批失败，退化为逐表查询并跳过坏表
                foreach (var name in batch)
                {
                    TryGetTables(schema, [name], list);
                }
            }
        }

        list.Sort((x, y) =>
        {
            var rs = String.Compare(x.Owner, y.Owner, StringComparison.OrdinalIgnoreCase);

            return rs != 0 ? rs : String.Compare(x.TableName, y.TableName, StringComparison.OrdinalIgnoreCase);
        });

        return list;
    }

    private Boolean TryGetTables(String? schema, String[] names, List<IDataTable> list)
    {
        try
        {
            var where = SqlMetaDataHelper.BuildWhere(schema, names);

            // 部分 Presto 开启 strict mode 后禁止无 limit 的排序，元数据查询不排序，取回后在内存里排
            var tables = $"select table_schema, table_name, table_type from information_schema.tables{where}";
            var columns = $"select table_schema, table_name, column_name, ordinal_position, data_type, is_nullable, column_default from information_schema.columns{where}";

            var list2 = SqlMetaDataHelper.GetTables(Database, tables, columns, GetDataType);
            foreach (var table in list2)
            {
                foreach (var column in table.Columns) FixColumn(column);
            }
            list.AddRange(list2);

            return true;
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Presto metadata query failed for [{0}]: {1}", String.Join(",", names), ex.Message);

            return false;
        }
    }
    #endregion

    #region 数据定义
    /// <summary>Presto/Trino 不支持自动建表与结构迁移</summary>
    private const String NotSupportedMessage = "Presto/Trino does not support automatic schema migration. Keep Migration=Off and create tables with the connector's own tooling.";

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

    /// <summary>建立表。Presto/Trino 不支持</summary>
    /// <param name="table">表模型</param>
    /// <returns></returns>
    public override Boolean CreateTable(IDataTable table) => throw new NotSupportedException(NotSupportedMessage);

    /// <summary>删除表。Presto/Trino 不支持</summary>
    /// <param name="table">表模型</param>
    /// <returns></returns>
    public override Boolean DropTable(IDataTable table) => throw new NotSupportedException(NotSupportedMessage);
    #endregion
}
