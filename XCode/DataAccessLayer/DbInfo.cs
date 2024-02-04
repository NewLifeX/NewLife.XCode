namespace XCode.DataAccessLayer;

/// <summary>数据库连接信息</summary>
internal class DbInfo
{
    /// <summary>连接名</summary>
    public String? Name { get; set; }

    /// <summary>连接字符串</summary>
    public String? ConnectionString { get; set; }

    /// <summary>数据库提供者类型</summary>
    public Type? Type { get; set; }

    /// <summary>数据库提供者</summary>
    public String? Provider { get; set; }
}