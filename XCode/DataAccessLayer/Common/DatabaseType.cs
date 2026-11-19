using System.ComponentModel;

namespace XCode.DataAccessLayer;

/// <summary>数据库类型</summary>
public enum DatabaseType
{
    /// <summary>无效值</summary>
    [Description("无效值")]
    None = 0,

    /// <summary>MS的Access文件数据库</summary>
    [Description("Access文件数据库")]
    Access = 1,

    /// <summary>MS的SqlServer数据库</summary>
    [Description("SqlServer数据库")]
    SqlServer = 2,

    /// <summary>Oracle数据库</summary>
    [Description("Oracle数据库")]
    Oracle = 3,

    /// <summary>MySql数据库</summary>
    [Description("MySql数据库")]
    MySql = 4,

    /// <summary>SqlCe数据库</summary>
    [Description("SqlCe数据库")]
    SqlCe = 5,

    /// <summary>SQLite数据库</summary>
    [Description("SQLite数据库")]
    SQLite = 6,

    /// <summary>Firebird数据库</summary>
    [Description("Firebird数据库")]
    Firebird = 7,

    /// <summary>SqlCe数据库</summary>
    [Description("PostgreSQL数据库")]
    PostgreSQL = 8,

    /// <summary>达梦数据库</summary>
    [Description("达梦数据库")]
    DaMeng = 9,

    /// <summary>DB2数据库</summary>
    [Description("DB2数据库")]
    DB2 = 10,

    /// <summary>TDengine数据库</summary>
    [Description("TDengine数据库")]
    TDengine = 11,

    /// <summary>Hana数据库</summary>
    [Description("Hana数据库")]
    Hana = 12,

    /// <summary>人大金仓(KingBase)数据库</summary>
    [Description("人大金仓(KingBase)数据库")]
    KingBase = 13,

    /// <summary>瀚高(HighGo)数据库</summary>
    [Description("瀚高(HighGo)数据库")]
    HighGo = 14,

    /// <summary>InterSystems IRIS数据库</summary>
    [Description("IRIS数据库")]
    IRIS = 15,

    /// <summary>VastBase数据库</summary>
    [Description("VastBase数据库")]
    VastBase = 16,

    /// <summary>InfluxDB时序数据库</summary>
    [Description("InfluxDB时序数据库")]
    InfluxDB = 17,

    /// <summary>NovaDb数据库。支持嵌入模式和网络模式</summary>
    [Description("NovaDb数据库")]
    NovaDb = 18,

    /// <summary>ClickHouse列式分析数据库</summary>
    [Description("ClickHouse列式分析数据库")]
    ClickHouse = 19,

    /// <summary>DuckDB嵌入式OLAP数据库</summary>
    [Description("DuckDB嵌入式OLAP数据库")]
    DuckDB = 20,

    /// <summary>MongoDB文档数据库</summary>
    [Description("MongoDB文档数据库")]
    MongoDB = 21,

    /// <summary>SonnetDB数据库。支持嵌入式目录和远程连接</summary>
    [Description("SonnetDB数据库")]
    SonnetDB = 22,

    /// <summary>Presto数据库。联邦查询引擎，写操作取决于连接器</summary>
    [Description("Presto数据库")]
    Presto = 23,

    /// <summary>Trino数据库。PrestoSQL 更名而来，写操作取决于连接器</summary>
    [Description("Trino数据库")]
    Trino = 24,

    /// <summary>Snowflake数据库。云原生数据仓库</summary>
    [Description("Snowflake数据库")]
    Snowflake = 25,

    /// <summary>Vertica数据库。列式分析型数据仓库</summary>
    [Description("Vertica数据库")]
    Vertica = 26,

    /// <summary>SAP ASE(Sybase)数据库</summary>
    [Description("SAP ASE(Sybase)数据库")]
    Ase = 27,

    /// <summary>Teradata数据库。企业级数据仓库</summary>
    [Description("Teradata数据库")]
    Teradata = 28,

    /// <summary>YashanDB(崖山)数据库</summary>
    [Description("YashanDB(崖山)数据库")]
    YashanDB = 29,

    /// <summary>网络虚拟数据库。通过HTTP接口转发数据库操作到远端服务执行</summary>
    [Description("网络虚拟数据库")]
    Network = 100,

    ///// <summary>分布式数据库</summary>
    //[Description("分布式数据库")]
    //Distributed = 888,

    ///// <summary>外部数据库</summary>
    //[Description("外部数据库")]
    //Other = 999
}