using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace XCode.Membership666;

/// <summary>成员日志</summary>
public partial interface IMemberLog
{
    #region 属性
    /// <summary>编号</summary>
    Int64 ID { get; set; }

    /// <summary>数据分区</summary>
    String? Ds { get; set; }

    /// <summary>类别</summary>
    String? Category { get; set; }

    /// <summary>操作</summary>
    String? Action { get; set; }

    /// <summary>链接</summary>
    Int32 LinkID { get; set; }

    /// <summary>成功</summary>
    Boolean Success { get; set; }

    /// <summary>用户名</summary>
    String? UserName { get; set; }

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

    /// <summary>性能追踪。用于APM性能追踪定位，还原该事件的调用链</summary>
    String? TraceId { get; set; }

    /// <summary>创建者</summary>
    String? CreateUser { get; set; }

    /// <summary>创建用户</summary>
    Int32 CreateUserID { get; set; }

    /// <summary>创建地址</summary>
    String? CreateIP { get; set; }

    /// <summary>时间</summary>
    DateTime CreateTime { get; set; }

    /// <summary>详细信息</summary>
    String? Remark { get; set; }
    #endregion
}
