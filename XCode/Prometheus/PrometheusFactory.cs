using System.Data.Common;

namespace XCode.Prometheus;

/// <summary>Prometheus数据库工厂</summary>
public class PrometheusFactory : DbProviderFactory
{
    /// <summary>实例</summary>
    public static readonly PrometheusFactory Instance = new();

    /// <summary>创建连接</summary>
    /// <returns></returns>
    public override DbConnection CreateConnection() => new PrometheusConnection();

    /// <summary>创建命令</summary>
    /// <returns></returns>
    public override DbCommand CreateCommand() => new PrometheusCommand();

    /// <summary>创建参数</summary>
    /// <returns></returns>
    public override DbParameter CreateParameter() => new PrometheusParameter();

    /// <summary>创建数据适配器</summary>
    /// <returns></returns>
    public override DbDataAdapter CreateDataAdapter() => new PrometheusDataAdapter();

    /// <summary>创建连接字符串生成器</summary>
    /// <returns></returns>
    public override DbConnectionStringBuilder CreateConnectionStringBuilder() => new PrometheusConnectionStringBuilder();
}

/// <summary>Prometheus数据适配器</summary>
public class PrometheusDataAdapter : DbDataAdapter
{
}
