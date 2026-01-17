using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Reflection;

namespace XCode.Membership;

/// <summary>租户。多租户SAAS平台，用于隔离业务数据</summary>
public partial class TenantModel : IModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>编码。唯一编码</summary>
    public String? Code { get; set; }

    /// <summary>名称。显示名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>类型。1免费/2个人/3企业/4旗舰</summary>
    public XCode.Membership.TenantTypes Type { get; set; }

    /// <summary>启用</summary>
    public Boolean Enable { get; set; }

    /// <summary>等级。租户级别或套餐版本，数字越大功能越多</summary>
    public Int32 Level { get; set; }

    /// <summary>管理者</summary>
    public Int32 ManagerId { get; set; }

    /// <summary>角色组。租户可选的角色集合，不同级别的租户所拥有的角色不一样，高级功能也会不同</summary>
    public String? RoleIds { get; set; }

    /// <summary>图标。附件路径</summary>
    public String? Logo { get; set; }

    /// <summary>域名。绑定的独立域名</summary>
    public String? Domain { get; set; }

    /// <summary>用户数上限。该租户最大允许用户数，0表示不限制</summary>
    public Int32 MaxUsers { get; set; }

    /// <summary>存储上限。该租户最大允许存储字节数，0表示不限制</summary>
    public Int64 MaxStorage { get; set; }

    /// <summary>数据库。分库用的数据库名</summary>
    public String? DatabaseName { get; set; }

    /// <summary>数据表。分表用的数据表前缀</summary>
    public String? TableName { get; set; }

    /// <summary>过期时间。达到该时间后，自动禁用租户，空表示永不过期</summary>
    public DateTime Expired { get; set; }

    /// <summary>描述</summary>
    public String? Remark { get; set; }
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public virtual Object? this[String name]
    {
        get
        {
            return name switch
            {
                "Id" => Id,
                "Code" => Code,
                "Name" => Name,
                "Type" => Type,
                "Enable" => Enable,
                "Level" => Level,
                "ManagerId" => ManagerId,
                "RoleIds" => RoleIds,
                "Logo" => Logo,
                "Domain" => Domain,
                "MaxUsers" => MaxUsers,
                "MaxStorage" => MaxStorage,
                "DatabaseName" => DatabaseName,
                "TableName" => TableName,
                "Expired" => Expired,
                "Remark" => Remark,
                _ => this.GetValue(name, false),
            };
        }
        set
        {
            switch (name)
            {
                case "Id": Id = value.ToInt(); break;
                case "Code": Code = Convert.ToString(value); break;
                case "Name": Name = Convert.ToString(value); break;
                case "Type": Type = (XCode.Membership.TenantTypes)value; break;
                case "Enable": Enable = value.ToBoolean(); break;
                case "Level": Level = value.ToInt(); break;
                case "ManagerId": ManagerId = value.ToInt(); break;
                case "RoleIds": RoleIds = Convert.ToString(value); break;
                case "Logo": Logo = Convert.ToString(value); break;
                case "Domain": Domain = Convert.ToString(value); break;
                case "MaxUsers": MaxUsers = value.ToInt(); break;
                case "MaxStorage": MaxStorage = value.ToLong(); break;
                case "DatabaseName": DatabaseName = Convert.ToString(value); break;
                case "TableName": TableName = Convert.ToString(value); break;
                case "Expired": Expired = value.ToDateTime(); break;
                case "Remark": Remark = Convert.ToString(value); break;
                default: this.SetValue(name, value); break;
            }
        }
    }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(ITenant model)
    {
        Id = model.Id;
        Code = model.Code;
        Name = model.Name;
        Type = model.Type;
        Enable = model.Enable;
        Level = model.Level;
        ManagerId = model.ManagerId;
        RoleIds = model.RoleIds;
        Logo = model.Logo;
        Domain = model.Domain;
        MaxUsers = model.MaxUsers;
        MaxStorage = model.MaxStorage;
        DatabaseName = model.DatabaseName;
        TableName = model.TableName;
        Expired = model.Expired;
        Remark = model.Remark;
    }
    #endregion
}
