namespace XCode.Membership;

/// <summary>数据范围。角色的数据权限范围</summary>
public enum DataScopes
{
    /// <summary>全部</summary>
    全部 = 0,

    /// <summary>本部门及下级</summary>
    本部门及下级 = 1,

    /// <summary>本部门</summary>
    本部门 = 2,

    /// <summary>仅本人</summary>
    仅本人 = 3,

    /// <summary>自定义</summary>
    自定义 = 4
}
