using System.Data.Common;

namespace XCode.OpenTSDB;

/// <summary>OpenTSDB数据库工厂</summary>
public class OpenTSDBFactory : DbProviderFactory
{
    /// <summary>实例</summary>
    public static readonly OpenTSDBFactory Instance = new();

    /// <summary>创建连接</summary>
    /// <returns></returns>
    public override DbConnection CreateConnection() => new OpenTSDBConnection();

    /// <summary>创建命令</summary>
    /// <returns></returns>
    public override DbCommand CreateCommand() => new OpenTSDBCommand();

    /// <summary>创建参数</summary>
    /// <returns></returns>
    public override DbParameter CreateParameter() => new OpenTSDBParameter();

    /// <summary>创建数据适配器</summary>
    /// <returns></returns>
    public override DbDataAdapter CreateDataAdapter() => new OpenTSDBDataAdapter();

    /// <summary>创建连接字符串生成器</summary>
    /// <returns></returns>
    public override DbConnectionStringBuilder CreateConnectionStringBuilder() => new OpenTSDBConnectionStringBuilder();
}

/// <summary>OpenTSDB数据适配器</summary>
public class OpenTSDBDataAdapter : DbDataAdapter
{
}
