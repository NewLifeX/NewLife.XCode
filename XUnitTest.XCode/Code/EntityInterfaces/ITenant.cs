using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace XCode.Membership666;

/// <summary>租户。多租户SAAS平台，用于隔离业务数据</summary>
public partial interface ITenant
{
    #region 属性
    /// <summary>编号</summary>
    Int32 Id { get; set; }

    /// <summary>编码。唯一编码</summary>
    String? Code { get; set; }

    /// <summary>名称。显示名称</summary>
    String? Name { get; set; }

    /// <summary>启用</summary>
    Boolean Enable { get; set; }

    /// <summary>管理者</summary>
    Int32 ManagerId { get; set; }

    /// <summary>角色组。租户可选的角色集合，不同级别的租户所拥有的角色不一样，高级功能也会不同</summary>
    String? RoleIds { get; set; }

    /// <summary>图标。附件路径</summary>
    String? Logo { get; set; }

    /// <summary>数据库。分库用的数据库名</summary>
    String? DatabaseName { get; set; }

    /// <summary>数据表。分表用的数据表前缀</summary>
    String? TableName { get; set; }

    /// <summary>过期时间。达到该时间后，自动禁用租户，空表示永不过期</summary>
    DateTime Expired { get; set; }

    /// <summary>描述</summary>
    String? Remark { get; set; }
    #endregion
}
