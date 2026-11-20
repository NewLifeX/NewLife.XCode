using System.Data.Common;

namespace XCode.IoTDB;

/// <summary>IoTDB数据库工厂</summary>
public class IoTDBFactory : DbProviderFactory
{
    /// <summary>实例</summary>
    public static readonly IoTDBFactory Instance = new();

    /// <summary>创建连接</summary>
    /// <returns></returns>
    public override DbConnection CreateConnection() => new IoTDBConnection();

    /// <summary>创建命令</summary>
    /// <returns></returns>
    public override DbCommand CreateCommand() => new IoTDBCommand();

    /// <summary>创建参数</summary>
    /// <returns></returns>
    public override DbParameter CreateParameter() => new IoTDBParameter();

    /// <summary>创建数据适配器</summary>
    /// <returns></returns>
    public override DbDataAdapter CreateDataAdapter() => new IoTDBDataAdapter();

    /// <summary>创建连接字符串生成器</summary>
    /// <returns></returns>
    public override DbConnectionStringBuilder CreateConnectionStringBuilder() => new IoTDBConnectionStringBuilder();
}

/// <summary>IoTDB数据适配器</summary>
public class IoTDBDataAdapter : DbDataAdapter
{
}
