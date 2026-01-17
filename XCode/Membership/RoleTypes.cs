namespace XCode.Membership;

/// <summary>角色类型</summary>
/// <remarks>
/// 角色类型用于区分不同来源和用途的角色：
/// <list type="bullet">
/// <item><term>系统</term><description>系统内置角色，拥有最高权限，不受数据权限约束，禁止删除或修改名称</description></item>
/// <item><term>普通</term><description>常规业务角色，受数据权限约束，由管理员创建和分配</description></item>
/// <item><term>租户</term><description>租户专属角色，仅在该租户范围内有效，用于租户内部权限管理</description></item>
/// </list>
/// </remarks>
public enum RoleTypes
{
    /// <summary>系统。系统内置角色，如"管理员"、"系统"角色，拥有最高权限，不受数据权限约束，禁止删除或修改名称</summary>
    系统 = 1,

    /// <summary>普通。常规业务角色，如"编辑"、"审核员"、"访客"等，受数据权限约束，由管理员创建和分配，用于日常业务操作</summary>
    普通 = 2,

    /// <summary>租户。租户专属角色，在多租户场景下每个租户可创建自己的角色，仅在该租户范围内有效，用于租户内部权限管理</summary>
    租户 = 3
}
