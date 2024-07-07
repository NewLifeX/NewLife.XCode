using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace XCode.Membership666;

/// <summary>租户关系。用户选择租户进入系统后，以租户关系角色组替代自有角色组来进行鉴权</summary>
public partial class TenantUserModel : ITenantUser
{
    #region 属性
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>租户</summary>
    public Int32 TenantId { get; set; }

    /// <summary>用户</summary>
    public Int32 UserId { get; set; }

    /// <summary>启用</summary>
    public Boolean Enable { get; set; }

    /// <summary>角色。用户在该租户所对应的主要角色，替换用户自身的角色组</summary>
    public Int32 RoleId { get; set; }

    /// <summary>角色组。次要角色集合</summary>
    public String? RoleIds { get; set; }

    /// <summary>描述</summary>
    public String? Remark { get; set; }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(ITenantUser model)
    {
        Id = model.Id;
        TenantId = model.TenantId;
        UserId = model.UserId;
        Enable = model.Enable;
        RoleId = model.RoleId;
        RoleIds = model.RoleIds;
        Remark = model.Remark;
    }
    #endregion
}
