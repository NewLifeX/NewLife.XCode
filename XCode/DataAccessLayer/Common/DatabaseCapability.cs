using System;

namespace XCode.DataAccessLayer;

/// <summary>数据库数据模型</summary>
public enum DatabaseModel
{
    /// <summary>关系型</summary>
    Relational = 0,

    /// <summary>文档型</summary>
    Document,

    /// <summary>时序型</summary>
    TimeSeries,

    /// <summary>列式</summary>
    Columnar,

    /// <summary>联邦查询</summary>
    Federated,

    /// <summary>键值型</summary>
    KeyValue,

    /// <summary>图型</summary>
    Graph,
}

/// <summary>数据库查询语言</summary>
public enum QueryLanguage
{
    /// <summary>SQL</summary>
    Sql = 0,

    /// <summary>Flux</summary>
    Flux,

    /// <summary>InfluxQL</summary>
    InfluxQl,

    /// <summary>MongoDB 命令</summary>
    MongoCommand,

    /// <summary>原生 API</summary>
    Native,
}

/// <summary>数据库能力。Flags 枚举，描述 XCode 在当前连接上可以安全执行的操作</summary>
/// <remarks>
/// 能力 = 引擎能力 ∩ 当前连接配置 ∩ XCode 已实现范围。
/// 条件能力默认关闭，显式开启后再加入标志；原生 Execute 仍是逃生通道。
/// </remarks>
[Flags]
public enum DatabaseCapability : Int64
{
    /// <summary>无能力</summary>
    None = 0,

    #region 核心 DML/DDL/事务 0-15
    /// <summary>支持 SQL 文本</summary>
    Sql = 1L << 0,

    /// <summary>支持事务</summary>
    Transactions = 1L << 1,

    /// <summary>支持表级 DDL</summary>
    Ddl = 1L << 2,

    /// <summary>支持数据库级 DDL</summary>
    DatabaseDdl = 1L << 3,

    /// <summary>支持自增/回读自增</summary>
    Identity = 1L << 4,

    /// <summary>支持更新</summary>
    Update = 1L << 5,

    /// <summary>支持删除</summary>
    Delete = 1L << 6,

    /// <summary>支持清空表</summary>
    Truncate = 1L << 7,

    /// <summary>支持参数化</summary>
    Parameterization = 1L << 8,

    /// <summary>支持架构发现</summary>
    SchemaDiscovery = 1L << 9,

    /// <summary>支持插入</summary>
    Insert = 1L << 10,
    #endregion

    #region 查询能力 16-31
    /// <summary>支持连接</summary>
    Joins = 1L << 16,

    /// <summary>支持子查询</summary>
    Subqueries = 1L << 17,

    /// <summary>支持分组</summary>
    GroupBy = 1L << 18,

    /// <summary>支持分组条件</summary>
    Having = 1L << 19,

    /// <summary>支持窗口函数</summary>
    WindowFunctions = 1L << 20,

    /// <summary>支持窗口帧</summary>
    WindowFrame = 1L << 21,

    /// <summary>支持 CTE</summary>
    Cte = 1L << 22,

    /// <summary>支持 Union</summary>
    Union = 1L << 23,

    /// <summary>支持 Offset 分页</summary>
    OffsetPaging = 1L << 24,

    /// <summary>支持 Limit 分页</summary>
    LimitPaging = 1L << 25,
    #endregion

    #region 批量/架构/维护 32-47
    /// <summary>支持批量插入</summary>
    BulkInsert = 1L << 32,

    /// <summary>支持批量更新</summary>
    BulkUpdate = 1L << 33,

    /// <summary>支持批量 Upsert</summary>
    BulkUpsert = 1L << 34,

    /// <summary>支持批量替换</summary>
    BulkReplace = 1L << 35,

    /// <summary>支持批量忽略插入</summary>
    BulkInsertIgnore = 1L << 36,

    /// <summary>支持表说明</summary>
    TableDescription = 1L << 37,

    /// <summary>支持字段说明</summary>
    ColumnDescription = 1L << 38,

    /// <summary>支持备份</summary>
    Backup = 1L << 39,

    /// <summary>支持还原</summary>
    Restore = 1L << 40,

    /// <summary>支持收缩</summary>
    Compact = 1L << 41,
    #endregion

    #region 预留 48-63
    /// <summary>支持 JSON</summary>
    Json = 1L << 48,

    /// <summary>支持数组</summary>
    Array = 1L << 49,

    /// <summary>支持全文检索</summary>
    FullText = 1L << 50,
    #endregion
}

/// <summary>数据库能力配置。除布尔能力外，还描述查询语言、迁移上限和方言限制</summary>
public sealed class DatabaseProfile
{
    /// <summary>数据模型</summary>
    public DatabaseModel Model { get; set; } = DatabaseModel.Relational;

    /// <summary>查询语言</summary>
    public QueryLanguage QueryLanguage { get; set; } = QueryLanguage.Sql;

    /// <summary>能力集合</summary>
    public DatabaseCapability Capabilities { get; set; }

    /// <summary>最大迁移模式。有效迁移取用户设置与该值的较小者</summary>
    public Migration MaxMigration { get; set; } = Migration.Full;

    /// <summary>长文本长度</summary>
    public Int32 LongTextLength { get; set; } = 4000;

    /// <summary>参数名前缀</summary>
    public String ParameterPrefix { get; set; } = "@";

    /// <summary>最大标识符长度</summary>
    public Int32 MaxIdentifierLength { get; set; }

    /// <summary>最大参数个数</summary>
    public Int32 MaxParameters { get; set; }
}

/// <summary>数据库能力接口</summary>
public interface IDbCapabilities
{
    /// <summary>能力配置</summary>
    DatabaseProfile Profile { get; }

    /// <summary>能力集合</summary>
    DatabaseCapability Capabilities { get; }

    /// <summary>最大迁移模式</summary>
    Migration MaxMigration { get; }

    /// <summary>有效迁移模式。用户设置与最大迁移模式取较小者</summary>
    Migration EffectiveMigration { get; }

    /// <summary>判断是否支持指定能力</summary>
    /// <param name="capability">能力标志</param>
    /// <returns>是否全部支持</returns>
    Boolean Supports(DatabaseCapability capability);

    /// <summary>确保支持指定能力，不支持时抛出异常</summary>
    /// <param name="capability">能力标志</param>
    /// <param name="operation">操作名称</param>
    void EnsureSupported(DatabaseCapability capability, String operation);
}
