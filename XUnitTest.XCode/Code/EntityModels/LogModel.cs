using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace XCode.Membership666;

/// <summary>日志。应用系统审计日志，记录用户的各种操作，禁止修改和删除</summary>
public partial class LogModel : ILog
{
    #region 属性
    /// <summary>编号</summary>
    public Int64 ID { get; set; }

    /// <summary>类别</summary>
    public String? Category { get; set; }

    /// <summary>操作</summary>
    public String Action { get; set; } = null!;

    /// <summary>链接</summary>
    public Int64 LinkID { get; set; }

    /// <summary>成功</summary>
    public Boolean Success { get; set; }

    /// <summary>用户名</summary>
    public String? UserName { get; set; }

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

    /// <summary>性能追踪。用于APM性能追踪定位，还原该事件的调用链</summary>
    public String? TraceId { get; set; }

    /// <summary>创建者</summary>
    public String? CreateUser { get; set; }

    /// <summary>创建用户</summary>
    public Int32 CreateUserID { get; set; }

    /// <summary>创建地址</summary>
    public String? CreateIP { get; set; }

    /// <summary>时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>详细信息</summary>
    public String? Remark { get; set; }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(ILog model)
    {
        ID = model.ID;
        Category = model.Category;
        Action = model.Action;
        LinkID = model.LinkID;
        Success = model.Success;
        UserName = model.UserName;
        Ex1 = model.Ex1;
        Ex2 = model.Ex2;
        Ex3 = model.Ex3;
        Ex4 = model.Ex4;
        Ex5 = model.Ex5;
        Ex6 = model.Ex6;
        TraceId = model.TraceId;
        CreateUser = model.CreateUser;
        CreateUserID = model.CreateUserID;
        CreateIP = model.CreateIP;
        CreateTime = model.CreateTime;
        Remark = model.Remark;
    }
    #endregion
}
