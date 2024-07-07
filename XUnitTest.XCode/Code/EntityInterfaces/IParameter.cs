using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace XCode.Membership666;

/// <summary>字典参数</summary>
public partial interface IParameter
{
    #region 属性
    /// <summary>编号</summary>
    Int32 ID { get; set; }

    /// <summary>用户。按用户区分参数，用户0表示系统级</summary>
    Int32 UserID { get; set; }

    /// <summary>类别</summary>
    String? Category { get; set; }

    /// <summary>名称</summary>
    String? Name { get; set; }

    /// <summary>数值</summary>
    String? Value { get; set; }

    /// <summary>长数值</summary>
    String? LongValue { get; set; }

    /// <summary>种类。0普通，21列表，22名值</summary>
    XCode.Membership.ParameterKinds Kind { get; set; }

    /// <summary>启用</summary>
    Boolean Enable { get; set; }

    /// <summary>扩展1</summary>
    Int32 Ex1 { get; set; }

    /// <summary>扩展2</summary>
    Decimal Ex2 { get; set; }

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
