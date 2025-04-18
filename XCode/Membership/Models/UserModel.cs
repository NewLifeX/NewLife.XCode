﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Reflection;

namespace XCode.Membership;

/// <summary>用户。用户帐号信息，以身份验证为中心，拥有多种角色，可加入多个租户</summary>
public partial class UserModel : IModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int32 ID { get; set; }

    /// <summary>名称。登录用户名</summary>
    public String Name { get; set; } = null!;

    /// <summary>密码</summary>
    public String? Password { get; set; }

    /// <summary>昵称</summary>
    public String? DisplayName { get; set; }

    /// <summary>性别。未知、男、女</summary>
    public XCode.Membership.SexKinds Sex { get; set; }

    /// <summary>邮件。支持登录</summary>
    public String? Mail { get; set; }

    /// <summary>邮箱验证。邮箱是否已通过验证</summary>
    public Boolean MailVerified { get; set; }

    /// <summary>手机。支持登录</summary>
    public String? Mobile { get; set; }

    /// <summary>手机验证。手机是否已通过验证</summary>
    public Boolean MobileVerified { get; set; }

    /// <summary>代码。身份证、员工编码等，支持登录</summary>
    public String? Code { get; set; }

    /// <summary>地区。省市区</summary>
    public Int32 AreaId { get; set; }

    /// <summary>头像</summary>
    public String? Avatar { get; set; }

    /// <summary>角色。主要角色</summary>
    public Int32 RoleID { get; set; }

    /// <summary>角色组。次要角色集合</summary>
    public String? RoleIds { get; set; }

    /// <summary>部门。组织机构</summary>
    public Int32 DepartmentID { get; set; }

    /// <summary>在线</summary>
    public Boolean Online { get; set; }

    /// <summary>启用</summary>
    public Boolean Enable { get; set; }

    /// <summary>年龄。周岁</summary>
    public Int32 Age { get; set; }

    /// <summary>生日。公历年月日</summary>
    public DateTime Birthday { get; set; }

    /// <summary>登录次数</summary>
    public Int32 Logins { get; set; }

    /// <summary>最后登录</summary>
    public DateTime LastLogin { get; set; }

    /// <summary>最后登录IP</summary>
    public String? LastLoginIP { get; set; }

    /// <summary>注册时间</summary>
    public DateTime RegisterTime { get; set; }

    /// <summary>注册IP</summary>
    public String? RegisterIP { get; set; }

    /// <summary>在线时间。累计在线总时间，单位秒</summary>
    public Int32 OnlineTime { get; set; }

    /// <summary>扩展1</summary>
    public Int32 Ex1 { get; set; }

    /// <summary>扩展2</summary>
    public Int32 Ex2 { get; set; }

    /// <summary>扩展3</summary>
    public Double Ex3 { get; set; }

    /// <summary>扩展4</summary>
    public String? Ex4 { get; set; }

    /// <summary>扩展5</summary>
    public String? Ex5 { get; set; }

    /// <summary>扩展6</summary>
    public String? Ex6 { get; set; }

    /// <summary>备注</summary>
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
                "ID" => ID,
                "Name" => Name,
                "Password" => Password,
                "DisplayName" => DisplayName,
                "Sex" => Sex,
                "Mail" => Mail,
                "MailVerified" => MailVerified,
                "Mobile" => Mobile,
                "MobileVerified" => MobileVerified,
                "Code" => Code,
                "AreaId" => AreaId,
                "Avatar" => Avatar,
                "RoleID" => RoleID,
                "RoleIds" => RoleIds,
                "DepartmentID" => DepartmentID,
                "Online" => Online,
                "Enable" => Enable,
                "Age" => Age,
                "Birthday" => Birthday,
                "Logins" => Logins,
                "LastLogin" => LastLogin,
                "LastLoginIP" => LastLoginIP,
                "RegisterTime" => RegisterTime,
                "RegisterIP" => RegisterIP,
                "OnlineTime" => OnlineTime,
                "Ex1" => Ex1,
                "Ex2" => Ex2,
                "Ex3" => Ex3,
                "Ex4" => Ex4,
                "Ex5" => Ex5,
                "Ex6" => Ex6,
                "Remark" => Remark,
                _ => this.GetValue(name, false),
            };
        }
        set
        {
            switch (name)
            {
                case "ID": ID = value.ToInt(); break;
                case "Name": Name = Convert.ToString(value); break;
                case "Password": Password = Convert.ToString(value); break;
                case "DisplayName": DisplayName = Convert.ToString(value); break;
                case "Sex": Sex = (XCode.Membership.SexKinds)value.ToInt(); break;
                case "Mail": Mail = Convert.ToString(value); break;
                case "MailVerified": MailVerified = value.ToBoolean(); break;
                case "Mobile": Mobile = Convert.ToString(value); break;
                case "MobileVerified": MobileVerified = value.ToBoolean(); break;
                case "Code": Code = Convert.ToString(value); break;
                case "AreaId": AreaId = value.ToInt(); break;
                case "Avatar": Avatar = Convert.ToString(value); break;
                case "RoleID": RoleID = value.ToInt(); break;
                case "RoleIds": RoleIds = Convert.ToString(value); break;
                case "DepartmentID": DepartmentID = value.ToInt(); break;
                case "Online": Online = value.ToBoolean(); break;
                case "Enable": Enable = value.ToBoolean(); break;
                case "Age": Age = value.ToInt(); break;
                case "Birthday": Birthday = value.ToDateTime(); break;
                case "Logins": Logins = value.ToInt(); break;
                case "LastLogin": LastLogin = value.ToDateTime(); break;
                case "LastLoginIP": LastLoginIP = Convert.ToString(value); break;
                case "RegisterTime": RegisterTime = value.ToDateTime(); break;
                case "RegisterIP": RegisterIP = Convert.ToString(value); break;
                case "OnlineTime": OnlineTime = value.ToInt(); break;
                case "Ex1": Ex1 = value.ToInt(); break;
                case "Ex2": Ex2 = value.ToInt(); break;
                case "Ex3": Ex3 = value.ToDouble(); break;
                case "Ex4": Ex4 = Convert.ToString(value); break;
                case "Ex5": Ex5 = Convert.ToString(value); break;
                case "Ex6": Ex6 = Convert.ToString(value); break;
                case "Remark": Remark = Convert.ToString(value); break;
                default: this.SetValue(name, value); break;
            }
        }
    }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IUser model)
    {
        ID = model.ID;
        Name = model.Name;
        Password = model.Password;
        DisplayName = model.DisplayName;
        Sex = model.Sex;
        Mail = model.Mail;
        MailVerified = model.MailVerified;
        Mobile = model.Mobile;
        MobileVerified = model.MobileVerified;
        Code = model.Code;
        AreaId = model.AreaId;
        Avatar = model.Avatar;
        RoleID = model.RoleID;
        RoleIds = model.RoleIds;
        DepartmentID = model.DepartmentID;
        Online = model.Online;
        Enable = model.Enable;
        Age = model.Age;
        Birthday = model.Birthday;
        Logins = model.Logins;
        LastLogin = model.LastLogin;
        LastLoginIP = model.LastLoginIP;
        RegisterTime = model.RegisterTime;
        RegisterIP = model.RegisterIP;
        OnlineTime = model.OnlineTime;
        Ex1 = model.Ex1;
        Ex2 = model.Ex2;
        Ex3 = model.Ex3;
        Ex4 = model.Ex4;
        Ex5 = model.Ex5;
        Ex6 = model.Ex6;
        Remark = model.Remark;
    }
    #endregion
}
