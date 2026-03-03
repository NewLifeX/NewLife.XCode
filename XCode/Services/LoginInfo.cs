using XCode.DataAccessLayer;

namespace XCode.Services;

/// <summary>登录信息。服务端返回给客户端的数据库信息</summary>
public class LoginInfo
{
    /// <summary>数据库类型</summary>
    public DatabaseType DbType { get; set; }

    /// <summary>服务端数据库版本</summary>
    public String? Version { get; set; }
}