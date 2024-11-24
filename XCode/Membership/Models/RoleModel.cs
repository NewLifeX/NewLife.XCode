﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife.Data;
using NewLife.Reflection;

namespace XCode.Membership;

/// <summary>角色。业务场景中的岗位，功能权限的集合。不管是用户还是租户，都以角色来管理权限</summary>
public partial class RoleModel : IModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int32 ID { get; set; }

    /// <summary>名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>启用</summary>
    public Boolean Enable { get; set; }

    /// <summary>系统。用于业务系统开发使用，不受数据权限约束，禁止修改名称或删除</summary>
    public Boolean IsSystem { get; set; }

    /// <summary>租户。角色所属组合，0表示全局角色</summary>
    public Int32 TenantId { get; set; }

    /// <summary>权限。对不同资源的权限，逗号分隔，每个资源的权限子项竖线分隔</summary>
    public String? Permission { get; set; }

    /// <summary>排序</summary>
    public Int32 Sort { get; set; }

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

    /// <summary>创建者</summary>
    public String? CreateUser { get; set; }

    /// <summary>创建用户</summary>
    public Int32 CreateUserID { get; set; }

    /// <summary>创建地址</summary>
    public String? CreateIP { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新者</summary>
    public String? UpdateUser { get; set; }

    /// <summary>更新用户</summary>
    public Int32 UpdateUserID { get; set; }

    /// <summary>更新地址</summary>
    public String? UpdateIP { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }

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
                "Enable" => Enable,
                "IsSystem" => IsSystem,
                "TenantId" => TenantId,
                "Permission" => Permission,
                "Sort" => Sort,
                "Ex1" => Ex1,
                "Ex2" => Ex2,
                "Ex3" => Ex3,
                "Ex4" => Ex4,
                "Ex5" => Ex5,
                "Ex6" => Ex6,
                "CreateUser" => CreateUser,
                "CreateUserID" => CreateUserID,
                "CreateIP" => CreateIP,
                "CreateTime" => CreateTime,
                "UpdateUser" => UpdateUser,
                "UpdateUserID" => UpdateUserID,
                "UpdateIP" => UpdateIP,
                "UpdateTime" => UpdateTime,
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
                case "Enable": Enable = value.ToBoolean(); break;
                case "IsSystem": IsSystem = value.ToBoolean(); break;
                case "TenantId": TenantId = value.ToInt(); break;
                case "Permission": Permission = Convert.ToString(value); break;
                case "Sort": Sort = value.ToInt(); break;
                case "Ex1": Ex1 = value.ToInt(); break;
                case "Ex2": Ex2 = value.ToInt(); break;
                case "Ex3": Ex3 = value.ToDouble(); break;
                case "Ex4": Ex4 = Convert.ToString(value); break;
                case "Ex5": Ex5 = Convert.ToString(value); break;
                case "Ex6": Ex6 = Convert.ToString(value); break;
                case "CreateUser": CreateUser = Convert.ToString(value); break;
                case "CreateUserID": CreateUserID = value.ToInt(); break;
                case "CreateIP": CreateIP = Convert.ToString(value); break;
                case "CreateTime": CreateTime = value.ToDateTime(); break;
                case "UpdateUser": UpdateUser = Convert.ToString(value); break;
                case "UpdateUserID": UpdateUserID = value.ToInt(); break;
                case "UpdateIP": UpdateIP = Convert.ToString(value); break;
                case "UpdateTime": UpdateTime = value.ToDateTime(); break;
                case "Remark": Remark = Convert.ToString(value); break;
                default: this.SetValue(name, value); break;
            }
        }
    }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IRole model)
    {
        ID = model.ID;
        Name = model.Name;
        Enable = model.Enable;
        IsSystem = model.IsSystem;
        TenantId = model.TenantId;
        Permission = model.Permission;
        Sort = model.Sort;
        Ex1 = model.Ex1;
        Ex2 = model.Ex2;
        Ex3 = model.Ex3;
        Ex4 = model.Ex4;
        Ex5 = model.Ex5;
        Ex6 = model.Ex6;
        CreateUser = model.CreateUser;
        CreateUserID = model.CreateUserID;
        CreateIP = model.CreateIP;
        CreateTime = model.CreateTime;
        UpdateUser = model.UpdateUser;
        UpdateUserID = model.UpdateUserID;
        UpdateIP = model.UpdateIP;
        UpdateTime = model.UpdateTime;
        Remark = model.Remark;
    }
    #endregion
}
