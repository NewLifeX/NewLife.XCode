using System.ComponentModel;
using System.Runtime.CompilerServices;
using NewLife.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;

[assembly: InternalsVisibleTo("XUnitTest.XCode, PublicKey=00240000048000001401000006020000002400005253413100080000010001000d41eb3bdab5c2150958b46c95632b7e4dcb0af77ed8637bd8543875bc2443d01273143bb46655a48a92efa76251adc63ccca6d0e9cef2e0ce93e32b5043bea179a6c710981be4a71703a03e10960643f7df091f499cf60183ef0e4e4e2eebf26e25cea0eebf87c8a6d7f8130c283fc3f747cb90623f0aaa619825e3fcd82f267a0f4bfd26c9f2a6b5a62a6b180b4f6d1d091fce6bd60a9aa9aa5b815b833b44e0f2e58b28a354cb20f52f31bb3b3a7c54f515426537e41f9c20c07e51f9cab8abc311daac19a41bd473a51c7386f014edf1863901a5c29addc89da2f2659c9c1e95affd6997396b9680e317c493e974a813186da277ff9c1d1b30e33cb5a2f6")]

namespace XCode;

/// <summary>XCode设置</summary>
[Obsolete("=>XCodeSetting")]
public class Setting : XCodeSetting { }

/// <summary>XCode设置</summary>
[DisplayName("XCode settings")]
[Config("XCode")]
public class XCodeSetting : Config<XCodeSetting>
{
    #region 日志
    /// <summary>调试模式。输出数据库调试日志，包括连接、迁移与结构检查等诊断信息，默认开启</summary>
    [Category("Logging")]
    [Description("Debug mode. Writes database debug logs such as connection, migration and schema diagnostics; enabled by default")]
    public Boolean Debug { get; set; } = true;

    /// <summary>SQL日志。把执行的SQL语句写入日志，默认开启；日志量过大时可在生产环境关闭</summary>
    [Category("Logging")]
    [Description("SQL logging. Writes executed SQL statements to the log; enabled by default. Turn it off in production when log volume is too high")]
    public Boolean ShowSQL { get; set; } = true;

    /// <summary>SQL日志目录。SQL日志输出到单独目录，默认空表示写入当前日志。生产环境建议放到站点外的SqlLog目录</summary>
    [Category("Logging")]
    [Description("SQL log directory. Writes SQL logs to a separate directory; empty by default so they go to the current log. In production, use a dedicated SqlLog directory outside the site")]
    public String SQLPath { get; set; } = "";

    /// <summary>慢SQL阈值（毫秒）。执行时间超过该值的SQL写入日志，0表示不记录慢SQL，默认1000</summary>
    [Category("Logging")]
    [Description("Slow SQL threshold (ms). SQL statements slower than this are logged; 0 disables slow SQL logging; default 1000")]
    public Int32 TraceSQLTime { get; set; } = 1000;

    /// <summary>SQL最大长度（字符）。日志中单条SQL超过该长度将被截断，0表示不截断，默认4096</summary>
    [Category("Logging")]
    [Description("Maximum SQL length (chars). SQL longer than this is truncated in logs; 0 disables truncation; default 4096")]
    public Int32 SQLMaxLength { get; set; } = 4096;

    /// <summary>日志脱敏模式。按严格程度递增：Off 原样记录凭据（仅限本地调试），PasswordOnly 只掩口令，Partial 口令全掩、密钥与令牌保留头尾，Full 全部凭据掩码（默认）</summary>
    [Category("Logging")]
    [Description("Log masking mode. Increasing strictness: Off (no masking, local debugging only), PasswordOnly (passwords only), Partial (passwords fully masked, keep the head and tail of secrets and tokens), Full (all credentials masked, default)")]
    public LogMaskModes LogMaskMode { get; set; } = LogMaskModes.Full;

    /// <summary>SQL日志脱敏。按脱敏模式掩码SQL日志与异常中的口令、密钥和令牌，默认开启</summary>
    [Category("Logging")]
    [Description("SQL log masking. Masks passwords, secrets and tokens in SQL logs and exceptions according to the masking mode; enabled by default")]
    public Boolean MaskSqlLog { get; set; } = true;

    ///// <summary>连接名映射#，表名映射@，表名映射@，把实体类中的Test2和Test3连接名映射到Test去</summary>
    //[Description("连接映射。连接名映射#，表名映射@，把实体类中的Test2和Test3连接名映射到Test去")]
    //public String ConnMaps { get; set; } = "";
    #endregion

    #region 数据库
    /// <summary>参数化增删改查。使用参数化SQL执行增删改，避免拼接SQL带来的注入风险，默认关闭</summary>
    [Category("Database")]
    [Description("Parameterized CRUD. Executes insert, update and delete with parameterized SQL to avoid injection risks; disabled by default")]
    public Boolean UseParameter { get; set; }

    /// <summary>命令超时（秒）。SQL命令执行超时时间，0表示不限制，默认0</summary>
    [Category("Database")]
    [Description("Command timeout (s). Timeout for SQL command execution; 0 means unlimited; default 0")]
    public Int32 CommandTimeout { get; set; }

    /// <summary>失败重试次数。命令超时后自动重试的次数，0表示不重试，默认0</summary>
    [Category("Database")]
    [Description("Failure retry count. Automatic retries after a command timeout; 0 means no retry; default 0")]
    public Int32 RetryOnFailure { get; set; }

    /// <summary>精确计数行数上限（行）。快速统计行数低于该值时才执行 count(*) 精确统计；每张表按实测耗时自动调整；0 表示不做精确统计；默认100万。修改后各表在下次访问时重置学习状态</summary>
    [Category("Database")]
    [Description("Exact count row limit (rows). Runs count(*) only when the fast row estimate is below this value; each table adapts it from measured duration; 0 disables exact counting; default 1000000. Changing it resets the learned state on next access")]
    public Int32 FullCountFloor { get; set; } = 1_000_000;

    /// <summary>批大小（行）。抽取、删除、备份、恢复等批量操作每批处理的行数，默认5000</summary>
    [Category("Database")]
    [Description("Batch size (rows). Rows per batch for extract, delete, backup and restore; default 5000")]
    public Int32 BatchSize { get; set; } = 5_000;

    /// <summary>批操作间隙（毫秒）。批量删除等操作在批次之间的暂停时间，默认100</summary>
    [Category("Database")]
    [Description("Batch interval (ms). Pause between batches during bulk operations such as batch delete; default 100")]
    public Int32 BatchInterval { get; set; } = 100;

    ///// <summary>SQLite数据库默认目录。没有设置连接字符串的连接默认创建SQLite连接，数据库放在该目录</summary>
    //[Description("SQLite默认目录。没有设置连接字符串的连接默认创建SQLite连接，数据库放在该目录")]
    //public String SQLiteDbPath { get; set; } = "";

    ///// <summary>备份目录。备份数据库时存放的目录</summary>
    //[Description("备份目录。备份数据库时存放的目录")]
    //public String BackupPath { get; set; } = "";
    #endregion

    #region 迁移
    /// <summary>反向工程模式。Off 关闭；ReadOnly 只检查不执行；On 创建缺失的表和字段；Full 还可以修改和删除；默认 On</summary>
    [Category("Schema migration")]
    [Description("Schema migration mode. Off: disabled; ReadOnly: check only; On: create missing tables and columns; Full: also alter and drop; default On")]
    public Migration Migration { get; set; } = Migration.On;

    /// <summary>检查注释。迁移时同步表与字段注释，默认开启</summary>
    [Category("Schema migration")]
    [Description("Check comments. Synchronizes table and column comments during migration; enabled by default")]
    public Boolean CheckComment { get; set; } = true;

    /// <summary>检查添加索引。迁移时创建模型中新增的索引，默认开启</summary>
    [Category("Schema migration")]
    [Description("Check added indexes. Creates indexes added to the model during migration; enabled by default")]
    public Boolean CheckAddIndex { get; set; } = true;

    /// <summary>检查删除索引。迁移时删除模型中已移除的索引，默认开启</summary>
    [Category("Schema migration")]
    [Description("Check removed indexes. Drops indexes removed from the model during migration; enabled by default")]
    public Boolean CheckDeleteIndex { get; set; } = true;

    /// <summary>检查索引重复。迁移时清理定义相同但名称不同的索引，默认开启</summary>
    [Category("Schema migration")]
    [Description("Check duplicate indexes. Removes indexes with the same definition but different names during migration; enabled by default")]
    public Boolean CheckDuplicateIndex { get; set; } = true;

    /// <summary>表名与字段名大小写。Default 由数据库决定；None 与模型保持一致；Upper 全大写；Lower 全小写；Underline 下划线分隔</summary>
    [Category("Schema migration")]
    [Description("Table and column name case. Default: database decides; None: keep model casing; Upper: uppercase; Lower: lowercase; Underline: underscore-separated")]
    public NameFormats NameFormat { get; set; } = NameFormats.Default;
    #endregion

    #region 缓存
    /// <summary>数据层缓存过期（秒）。按SQL缓存查询结果，0表示不缓存，默认0</summary>
    [Category("Cache")]
    [Description("Query cache expiration (s). Caches query results by SQL; 0 disables caching; default 0")]
    public Int32 DataCacheExpire { get; set; }

    /// <summary>实体缓存过期（秒）。整表缓存实体列表，默认10</summary>
    [Category("Cache")]
    [Description("Entity cache expiration (s). Caches the whole entity list; default 10")]
    public Int32 EntityCacheExpire { get; set; } = 10;

    /// <summary>单对象缓存过期（秒）。按主键缓存实体，默认10</summary>
    [Category("Cache")]
    [Description("Single object cache expiration (s). Caches entities by primary key; default 10")]
    public Int32 SingleCacheExpire { get; set; } = 10;

    /// <summary>扩展属性过期（秒）。缓存扩展属性Extends，默认10</summary>
    [Category("Cache")]
    [Description("Extended property expiration (s). Caches the Extends property; default 10")]
    public Int32 ExtendExpire { get; set; } = 10;

    /// <summary>字段缓存过期（秒）。缓存表中分类型字段的分组数据，默认3600</summary>
    [Category("Cache")]
    [Description("Field cache expiration (s). Caches grouped values of category fields; default 3600")]
    public Int32 FieldCacheExpire { get; set; } = 3600;

    /// <summary>缓存统计周期（秒）。定期在日志中输出各缓存的统计数据，用于分析性能问题，0表示不输出，默认3600</summary>
    [Category("Cache")]
    [Description("Cache statistics period (s). Periodically logs statistics of all caches for performance analysis; 0 disables; default 3600")]
    public Int32 CacheStatPeriod { get; set; } = 3600;
    #endregion

    #region 方法
    ///// <summary>加载后检查默认值</summary>
    //protected override void OnLoaded()
    //{
    //    if (SQLiteDbPath.IsNullOrEmpty()) SQLiteDbPath = Runtime.IsWeb ? "..\\Data" : "Data";
    //    if (BackupPath.IsNullOrEmpty()) BackupPath = Runtime.IsWeb ? "..\\Backup" : "Backup";

    //    base.OnLoaded();
    //}
    #endregion
}