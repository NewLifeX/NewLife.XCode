using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace XCode.Membership666;

/// <summary>租户。多租户SAAS平台，用于隔离业务数据</summary>
public partial class TenantModel : ITenant
{
    #region 属性
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>编码。唯一编码</summary>
    public String? Code { get; set; }

    /// <summary>名称。显示名称</summary>
    public String? Name { get; set; }

    /// <summary>启用</summary>
    public Boolean Enable { get; set; }

    /// <summary>管理者</summary>
    public Int32 ManagerId { get; set; }

    /// <summary>角色组。租户可选的角色集合，不同级别的租户所拥有的角色不一样，高级功能也会不同</summary>
    public String? RoleIds { get; set; }

    /// <summary>图标。附件路径</summary>
    public String? Logo { get; set; }

    /// <summary>数据库。分库用的数据库名</summary>
    public String? DatabaseName { get; set; }

    /// <summary>数据表。分表用的数据表前缀</summary>
    public String? TableName { get; set; }

    /// <summary>过期时间。达到该时间后，自动禁用租户，空表示永不过期</summary>
    public DateTime Expired { get; set; }

    /// <summary>描述</summary>
    public String? Remark { get; set; }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(ITenant model)
    {
        Id = model.Id;
        Code = model.Code;
        Name = model.Name;
        Enable = model.Enable;
        ManagerId = model.ManagerId;
        RoleIds = model.RoleIds;
        Logo = model.Logo;
        DatabaseName = model.DatabaseName;
        TableName = model.TableName;
        Expired = model.Expired;
        Remark = model.Remark;
    }
    #endregion
}
