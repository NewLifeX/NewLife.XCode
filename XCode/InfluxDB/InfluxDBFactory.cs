using System.Data.Common;

namespace XCode.InfluxDB;

/// <summary>InfluxDB数据库工厂</summary>
public class InfluxDBFactory : DbProviderFactory
{
    /// <summary>实例</summary>
    public static readonly InfluxDBFactory Instance = new();

    /// <summary>创建连接</summary>
    /// <returns></returns>
    public override DbConnection CreateConnection() => new InfluxDBConnection();

    /// <summary>创建命令</summary>
    /// <returns></returns>
    public override DbCommand CreateCommand() => new InfluxDBCommand();

    /// <summary>创建参数</summary>
    /// <returns></returns>
    public override DbParameter CreateParameter() => new InfluxDBParameter();

    /// <summary>创建数据适配器</summary>
    /// <returns></returns>
    public override DbDataAdapter CreateDataAdapter() => new InfluxDBDataAdapter();

    /// <summary>创建连接字符串生成器</summary>
    /// <returns></returns>
    public override DbConnectionStringBuilder CreateConnectionStringBuilder() => new InfluxDBConnectionStringBuilder();
}
