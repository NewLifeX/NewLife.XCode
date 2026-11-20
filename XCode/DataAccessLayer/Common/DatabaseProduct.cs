using System.Collections.ObjectModel;

namespace XCode.DataAccessLayer;

/// <summary>数据库产品。表示 XCode 支持的一种实际数据库</summary>
/// <remarks>
/// 多个数据库产品可以共用同一种通信协议提供者。
/// 例如 MariaDB、TiDB、StarRocks 均复用 <see cref="DatabaseType.MySql"/> 提供者，
/// <see cref="ProtocolAlias"/> 为连接字符串中可识别的协议别名。
/// </remarks>
public sealed class DatabaseProduct
{
    /// <summary>实例化数据库产品</summary>
    /// <param name="name">数据库名称</param>
    /// <param name="provider">通信协议提供者</param>
    /// <param name="protocolAlias">协议别名。专属提供者为空</param>
    public DatabaseProduct(String name, DatabaseType provider, String? protocolAlias = null)
    {
        if (name.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(name));

        Name = name;
        Provider = provider;
        ProtocolAlias = protocolAlias;
    }

    /// <summary>数据库名称</summary>
    public String Name { get; }

    /// <summary>通信协议提供者</summary>
    public DatabaseType Provider { get; }

    /// <summary>协议别名。连接字符串 Provider 可使用该名称；专属提供者为空</summary>
    public String? ProtocolAlias { get; }

    /// <summary>连接字符串中使用的提供者名称</summary>
    public String ProviderName => ProtocolAlias ?? Provider.ToString();

    /// <summary>是否复用其它数据库的通信协议</summary>
    public Boolean IsProtocolCompatible => !ProtocolAlias.IsNullOrEmpty();

    /// <summary>已重载。返回数据库名称</summary>
    /// <returns></returns>
    public override String ToString() => Name;
}

/// <summary>数据库产品目录。列举全部已支持的数据库，包括复用 MySQL/PostgreSQL 协议的兼容数据库</summary>
/// <remarks>
/// 一个提供者（通信协议）可以对应多个数据库产品。目录只统计当前启用并注册的内置提供者，
/// Access/SqlCe 等已停用提供者不计入。通过 <see cref="All"/> 可遍历全部数据库产品。
/// </remarks>
/// <example>
/// 遍历全部支持的数据库：
/// <code>
/// foreach (var product in DatabaseProducts.All)
///     Console.WriteLine($"{product.Name} =&gt; {product.ProviderName}");
///
/// Console.WriteLine($"协议：{DatabaseProducts.ProtocolCount}，数据库：{DatabaseProducts.Count}");
/// </code>
/// </example>
public static class DatabaseProducts
{
    private static readonly DatabaseProduct[] _items =
    [
        // 专属提供者
        new("SQL Server", DatabaseType.SqlServer),
        new("MySQL", DatabaseType.MySql),
        new("PostgreSQL", DatabaseType.PostgreSQL),
        new("Oracle", DatabaseType.Oracle),
        new("SQLite", DatabaseType.SQLite),
        new("Firebird", DatabaseType.Firebird),
        new("DB2", DatabaseType.DB2),
        new("SAP ASE (Sybase)", DatabaseType.Ase),
        new("DaMeng", DatabaseType.DaMeng),
        new("Kingbase", DatabaseType.KingBase),
        new("HighGo", DatabaseType.HighGo),
        new("VastBase", DatabaseType.VastBase),
        new("YashanDB", DatabaseType.YashanDB),
        new("SAP HANA", DatabaseType.Hana),
        new("InterSystems IRIS", DatabaseType.IRIS),
        new("ClickHouse", DatabaseType.ClickHouse),
        new("Vertica", DatabaseType.Vertica),
        new("MonetDB", DatabaseType.MonetDB),
        new("DuckDB", DatabaseType.DuckDB),
        new("Snowflake", DatabaseType.Snowflake),
        new("Teradata", DatabaseType.Teradata),
        new("Google Cloud Spanner", DatabaseType.Spanner),
        new("TDengine", DatabaseType.TDengine),
        new("InfluxDB", DatabaseType.InfluxDB),
        new("OpenTSDB", DatabaseType.OpenTSDB),
        new("Apache IoTDB", DatabaseType.IoTDB),
        new("Prometheus", DatabaseType.Prometheus),
        new("MongoDB", DatabaseType.MongoDB),
        new("Presto", DatabaseType.Presto),
        new("Trino", DatabaseType.Trino),
        new("NovaDb", DatabaseType.NovaDb),
        new("SonnetDB", DatabaseType.SonnetDB),
        new("Network", DatabaseType.Network),

        // MySQL 协议
        new("MariaDB", DatabaseType.MySql, "mariadb"),
        new("TiDB", DatabaseType.MySql, "tidb"),
        new("StarRocks", DatabaseType.MySql, "starrocks"),
        new("Apache Doris", DatabaseType.MySql, "doris"),
        new("OceanBase", DatabaseType.MySql, "oceanbase"),
        new("SingleStore", DatabaseType.MySql, "singlestore"),
        new("PolarDB", DatabaseType.MySql, "polardb"),
        new("TDSQL", DatabaseType.MySql, "tdsql"),
        new("GoldenDB", DatabaseType.MySql, "goldendb"),
        new("GreatDB", DatabaseType.MySql, "greatdb"),
        new("Databend", DatabaseType.MySql, "databend"),

        // PostgreSQL 协议
        new("openGauss", DatabaseType.PostgreSQL, "opengauss"),
        new("GaussDB", DatabaseType.PostgreSQL, "gaussdb"),
        new("Greenplum", DatabaseType.PostgreSQL, "greenplum"),
        new("Amazon Redshift", DatabaseType.PostgreSQL, "redshift"),
        new("CockroachDB", DatabaseType.PostgreSQL, "cockroach"),
        new("YugabyteDB", DatabaseType.PostgreSQL, "yugabyte"),
        new("TimescaleDB", DatabaseType.PostgreSQL, "timescale"),
        new("QuestDB", DatabaseType.PostgreSQL, "questdb"),
        new("GreptimeDB", DatabaseType.PostgreSQL, "greptimedb"),
        new("CrateDB", DatabaseType.PostgreSQL, "cratedb"),

        // Prometheus 协议
        new("VictoriaMetrics", DatabaseType.Prometheus, "victoriametrics"),
    ];

    private static readonly ReadOnlyCollection<DatabaseProduct> _all = Array.AsReadOnly(_items);

    /// <summary>全部数据库产品</summary>
    public static ReadOnlyCollection<DatabaseProduct> All => _all;

    /// <summary>通信协议数量。即已注册的内置提供者数量</summary>
    public static Int32 ProtocolCount { get; } = _items.Select(e => e.Provider).Distinct().Count();

    /// <summary>实际支持的数据库数量。包含复用通信协议的兼容数据库</summary>
    public static Int32 Count => _items.Length;

    /// <summary>按通信协议提供者获取数据库产品</summary>
    /// <param name="provider">通信协议提供者</param>
    /// <returns>数据库产品列表</returns>
    public static IReadOnlyList<DatabaseProduct> GetByProvider(DatabaseType provider)
    {
        var list = new List<DatabaseProduct>();
        foreach (var item in _items)
        {
            if (item.Provider == provider) list.Add(item);
        }

        return list;
    }

    /// <summary>按名称查找数据库产品。支持名称、协议别名和提供者名称</summary>
    /// <param name="name">数据库名称、协议别名或提供者名称</param>
    /// <returns>数据库产品，未找到返回空</returns>
    public static DatabaseProduct? FindByName(String? name)
    {
        if (name.IsNullOrWhiteSpace()) return null;

        name = name.Trim();
        foreach (var item in _items)
        {
            if (String.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) return item;
            if (String.Equals(item.ProtocolAlias, name, StringComparison.OrdinalIgnoreCase)) return item;
            if (String.Equals(item.Provider.ToString(), name, StringComparison.OrdinalIgnoreCase)) return item;
        }

        return null;
    }
}
