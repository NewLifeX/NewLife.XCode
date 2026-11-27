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

/// <summary>日志。应用系统审计日志，记录用户的各种操作，禁止修改和删除</summary>
public partial class LogModel : IModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int64 Id { get; set; }

    /// <summary>类别</summary>
    public String? Category { get; set; }

    /// <summary>操作</summary>
    public String Action { get; set; } = null!;

    /// <summary>链接</summary>
    public Int64 LinkId { get; set; }

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
    public Int32 CreateUserId { get; set; }

    /// <summary>创建地址</summary>
    public String? CreateIP { get; set; }

    /// <summary>时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>详细信息</summary>
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
                "Category" => Category,
                "Action" => Action,
                "LinkId" => LinkId,
                "Success" => Success,
                "UserName" => UserName,
                "Ex1" => Ex1,
                "Ex2" => Ex2,
                "Ex3" => Ex3,
                "Ex4" => Ex4,
                "Ex5" => Ex5,
                "Ex6" => Ex6,
                "TraceId" => TraceId,
                "CreateUser" => CreateUser,
                "CreateUserId" => CreateUserId,
                "CreateIP" => CreateIP,
                "CreateTime" => CreateTime,
                "Remark" => Remark,
                _ => this.GetValue(name, false),
            };
        }
        set
        {
            switch (name)
            {
                case "Id": Id = value.ToLong(); break;
                case "Category": Category = value?.ToString(); break;
                case "Action": Action = value?.ToString() ?? ""; break;
                case "LinkId": LinkId = value.ToLong(); break;
                case "Success": Success = value.ToBoolean(); break;
                case "UserName": UserName = value?.ToString(); break;
                case "Ex1": Ex1 = value.ToInt(); break;
                case "Ex2": Ex2 = value.ToInt(); break;
                case "Ex3": Ex3 = value.ToDouble(); break;
                case "Ex4": Ex4 = value?.ToString(); break;
                case "Ex5": Ex5 = value?.ToString(); break;
                case "Ex6": Ex6 = value?.ToString(); break;
                case "TraceId": TraceId = value?.ToString(); break;
                case "CreateUser": CreateUser = value?.ToString(); break;
                case "CreateUserId": CreateUserId = value.ToInt(); break;
                case "CreateIP": CreateIP = value?.ToString(); break;
                case "CreateTime": CreateTime = value.ToDateTime(); break;
                case "Remark": Remark = value?.ToString(); break;
                default: this.SetValue(name, value); break;
            }
        }
    }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(ILog model)
    {
        Id = model.Id;
        Category = model.Category;
        Action = model.Action;
        LinkId = model.LinkId;
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
        CreateUserId = model.CreateUserId;
        CreateIP = model.CreateIP;
        CreateTime = model.CreateTime;
        Remark = model.Remark;
    }
    #endregion
}
