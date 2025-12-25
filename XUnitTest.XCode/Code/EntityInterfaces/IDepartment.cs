using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace XCode.Membership666;

/// <summary>部门。组织机构，多级树状结构，支持多租户</summary>
public partial interface IDepartment
{
    #region 属性
    /// <summary>编号</summary>
    Int32 ID { get; set; }

    /// <summary>租户</summary>
    Int32 TenantId { get; set; }

    /// <summary>代码</summary>
    String? Code { get; set; }

    /// <summary>名称</summary>
    String Name { get; set; }

    /// <summary>全名</summary>
    String? FullName { get; set; }

    /// <summary>父级</summary>
    Int32 ParentID { get; set; }

    /// <summary>层级。树状结构的层级</summary>
    Int32 Level { get; set; }

    /// <summary>排序。同级内排序</summary>
    Int32 Sort { get; set; }

    /// <summary>启用</summary>
    Boolean Enable { get; set; }

    /// <summary>可见</summary>
    Boolean Visible { get; set; }

    /// <summary>管理者</summary>
    Int32 ManagerId { get; set; }

    /// <summary>扩展1</summary>
    Int32 Ex1 { get; set; }

    /// <summary>扩展2</summary>
    Int32 Ex2 { get; set; }

    /// <summary>扩展3</summary>
    Double Ex3 { get; set; }

    /// <summary>扩展4</summary>
    String? Ex4 { get; set; }

    /// <summary>扩展5</summary>
    String? Ex5 { get; set; }

    /// <summary>扩展6</summary>
    String? Ex6 { get; set; }

    /// <summary>创建者</summary>
    String? CreateUser { get; set; }

    /// <summary>创建用户</summary>
    Int32 CreateUserID { get; set; }

    /// <summary>创建地址</summary>
    String? CreateIP { get; set; }

    /// <summary>创建时间</summary>
    DateTime CreateTime { get; set; }

    /// <summary>更新者</summary>
    String? UpdateUser { get; set; }

    /// <summary>更新用户</summary>
    Int32 UpdateUserID { get; set; }

    /// <summary>更新地址</summary>
    String? UpdateIP { get; set; }

    /// <summary>更新时间</summary>
    DateTime UpdateTime { get; set; }

    /// <summary>备注</summary>
    String? Remark { get; set; }
    #endregion
}
