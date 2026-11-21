namespace XCode.DataAccessLayer;

/// <summary>数据库驱动 NuGet 包索引</summary>
/// <remarks>驱动加载失败时，按数据库类型或程序集文件名给出应安装的 NuGet 包，避免只提示"找不到驱动"</remarks>
internal static class DriverPackages
{
    /// <summary>按数据库类型索引的推荐 NuGet 包</summary>
    private static readonly Dictionary<DatabaseType, String> _byType = new()
    {
        [DatabaseType.SqlServer] = "System.Data.SqlClient",
        [DatabaseType.Oracle] = "Oracle.ManagedDataAccess.Core",
        [DatabaseType.MySql] = "NewLife.MySql",
        [DatabaseType.PostgreSQL] = "Npgsql",
        [DatabaseType.SQLite] = "Microsoft.Data.Sqlite",
        [DatabaseType.Firebird] = "FirebirdSql.Data.FirebirdClient",
        [DatabaseType.DaMeng] = "dmdbms.DmProvider",
        [DatabaseType.DB2] = "IBM.Data.DB2.Core",
        [DatabaseType.Hana] = "Sap.Data.Hana",
        [DatabaseType.KingBase] = "Kdbndp",
        [DatabaseType.HighGo] = "Nhgdb",
        [DatabaseType.IRIS] = "InterSystems.Data.IRISClient",
        [DatabaseType.VastBase] = "Npgsql",
        [DatabaseType.NovaDb] = "NewLife.NovaDb",
        [DatabaseType.ClickHouse] = "ClickHouse.Client",
        [DatabaseType.DuckDB] = "DuckDB.NET.Data",
        [DatabaseType.MongoDB] = "MongoDB.Driver",
        [DatabaseType.SonnetDB] = "SonnetDB",
        [DatabaseType.TDengine] = "NewLife.TDengine",
        // 新驱动已支持优先加载；待 NewLife.Presto 首次发布后把包名切换为 NewLife.Presto
        [DatabaseType.Presto] = "NReco.PrestoAdo",
        [DatabaseType.Trino] = "Trino.Data.ADO",
        [DatabaseType.Snowflake] = "Snowflake.Data",
        [DatabaseType.Vertica] = "Vertica.Data",
        [DatabaseType.Ase] = "AdoNetCore.AseClient",
        [DatabaseType.Teradata] = "Teradata.Client.Provider",
        [DatabaseType.YashanDB] = "Yashandb.Data.YashandbClient",
        [DatabaseType.MonetDB] = "MonetDb.Mapi",
        [DatabaseType.Spanner] = "Google.Cloud.Spanner.Data",
    };

    /// <summary>按驱动程序集文件名索引的推荐 NuGet 包</summary>
    private static readonly Dictionary<String, String> _byAssembly = new(StringComparer.OrdinalIgnoreCase)
    {
        ["System.Data.SqlClient.dll"] = "System.Data.SqlClient",
        ["Oracle.ManagedDataAccess.dll"] = "Oracle.ManagedDataAccess.Core",
        ["NewLife.MySql.dll"] = "NewLife.MySql",
        ["MySql.Data.dll"] = "MySql.Data",
        ["Npgsql.dll"] = "Npgsql",
        ["Microsoft.Data.Sqlite.dll"] = "Microsoft.Data.Sqlite",
        ["System.Data.SQLite.dll"] = "System.Data.SQLite",
        ["FirebirdSql.Data.FirebirdClient.dll"] = "FirebirdSql.Data.FirebirdClient",
        ["DmProvider.dll"] = "dmdbms.DmProvider",
        ["IBM.Data.Db2.dll"] = "Net.IBM.Data.Db2",
        ["IBM.Data.DB2.Core.dll"] = "IBM.Data.DB2.Core",
        ["Sap.Data.Hana.Core.dll"] = "Sap.Data.Hana",
        ["Kdbndp.dll"] = "Kdbndp",
        ["Nhgdb.dll"] = "Nhgdb",
        ["InterSystems.Data.IRISClient.dll"] = "InterSystems.Data.IRISClient",
        ["NewLife.NovaDb.dll"] = "NewLife.NovaDb",
        ["ClickHouse.Client.dll"] = "ClickHouse.Client",
        ["DuckDB.NET.Data.dll"] = "DuckDB.NET.Data",
        ["MongoDB.Driver.dll"] = "MongoDB.Driver",
        ["SonnetDB.Data.dll"] = "SonnetDB",
        ["NewLife.TDengine.dll"] = "NewLife.TDengine",
        ["NewLife.Presto.dll"] = "NewLife.Presto",
        ["NReco.PrestoAdo.dll"] = "NReco.PrestoAdo",
        ["Trino.Data.ADO.dll"] = "Trino.Data.ADO",
        ["Snowflake.Data.dll"] = "Snowflake.Data",
        ["Vertica.Data.dll"] = "Vertica.Data",
        ["AdoNetCore.AseClient.dll"] = "AdoNetCore.AseClient",
        ["Teradata.Client.Provider.dll"] = "Teradata.Client.Provider",
        ["Yashandb.Data.YashandbClient.dll"] = "Yashandb.Data.YashandbClient",
        ["MonetDb.Mapi.dll"] = "MonetDb.Mapi",
        ["Google.Cloud.Spanner.Data.dll"] = "Google.Cloud.Spanner.Data",
    };

    /// <summary>根据数据库类型获取推荐 NuGet 包</summary>
    /// <param name="type">数据库类型</param>
    /// <returns>NuGet 包名，未知类型返回空</returns>
    public static String? Get(DatabaseType type) => _byType.TryGetValue(type, out var package) ? package : null;

    /// <summary>根据驱动程序集文件名获取推荐 NuGet 包</summary>
    /// <param name="assemblyFile">驱动程序集名称或路径</param>
    /// <returns>NuGet 包名，未知程序集返回空</returns>
    public static String? Get(String? assemblyFile)
    {
        if (assemblyFile.IsNullOrEmpty()) return null;

        // 驱动路径可能来自其它平台的配置，两种目录分隔符都当作分隔符
        var index = assemblyFile.LastIndexOfAny(_separators);
        var file = index >= 0 ? assemblyFile[(index + 1)..] : assemblyFile;
        return _byAssembly.TryGetValue(file, out var package) ? package : null;
    }

    private static readonly Char[] _separators = ['\\', '/'];
}
