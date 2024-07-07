using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace XCode.Membership666;

/// <summary>租户关系。用户选择租户进入系统后，以租户关系角色组替代自有角色组来进行鉴权</summary>
public partial interface ITenantUser
{
    #region 属性
    /// <summary>编号</summary>
    Int32 Id { get; set; }

    /// <summary>租户</summary>
    Int32 TenantId { get; set; }

    /// <summary>用户</summary>
    Int32 UserId { get; set; }

    /// <summary>启用</summary>
    Boolean Enable { get; set; }

    /// <summary>角色。用户在该租户所对应的主要角色，替换用户自身的角色组</summary>
    Int32 RoleId { get; set; }

    /// <summary>角色组。次要角色集合</summary>
    String? RoleIds { get; set; }

    /// <summary>描述</summary>
    String? Remark { get; set; }
    #endregion
}
