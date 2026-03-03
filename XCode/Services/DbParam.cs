namespace XCode.Services;

/// <summary>数据库请求参数模型。用于HTTP接口的SQL执行请求</summary>
public class DbRequest
{
    /// <summary>SQL语句</summary>
    public String? Sql { get; set; }

    /// <summary>SQL参数字典</summary>
    public IDictionary<String, Object?>? Parameters { get; set; }
}