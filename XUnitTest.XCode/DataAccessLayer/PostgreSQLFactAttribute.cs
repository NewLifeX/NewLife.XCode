using System;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>PostgreSQL 真库用例特性。服务不可达时自动跳过，避免缺少数据库的环境（如本地开发机）出现红灯</summary>
/// <remarks>
/// xUnit 2.x 不支持运行时跳过，只能在用例发现阶段设置 Skip。
/// 公共探测逻辑见 <see cref="LiveDatabaseFactAttribute"/>。
/// CI 通过环境变量 XCode_pgsql 注入连接串即视为服务可用，不做探测，防止探测误差掩盖真实失败。
/// </remarks>
public sealed class PostgreSQLFactAttribute : LiveDatabaseFactAttribute
{
    public PostgreSQLFactAttribute() : base("pgsql", "pgsql", 5432) { }
}

