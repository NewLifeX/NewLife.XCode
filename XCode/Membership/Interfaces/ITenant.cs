using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace XCode.Membership;

/// <summary>租户。多租户SAAS平台，用于隔离业务数据</summary>
public partial interface ITenant
{
    #region 属性
    /// <summary>编号</summary>
    Int32 Id { get; set; }

    /// <summary>编码。唯一编码</summary>
    String? Code { get; set; }

    /// <summary>名称。显示名称</summary>
    String Name { get; set; }

    /// <summary>类型。1免费/2个人/3企业/4旗舰</summary>
    XCode.Membership.TenantTypes Type { get; set; }

    /// <summary>启用</summary>
    Boolean Enable { get; set; }

    /// <summary>等级。租户级别或套餐版本，数字越大功能越多</summary>
    Int32 Level { get; set; }

    /// <summary>管理者</summary>
    Int32 ManagerId { get; set; }

    /// <summary>角色组。租户可选的角色集合，不同级别的租户所拥有的角色不一样，高级功能也会不同</summary>
    String? RoleIds { get; set; }

    /// <summary>图标。附件路径</summary>
    String? Logo { get; set; }

    /// <summary>域名。绑定的独立域名</summary>
    String? Domain { get; set; }

    /// <summary>用户数上限。该租户最大允许用户数，0表示不限制</summary>
    Int32 MaxUsers { get; set; }

    /// <summary>存储上限。该租户最大允许存储字节数，0表示不限制</summary>
    Int64 MaxStorage { get; set; }

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
