using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife.Data;

namespace XCode.Membership;

/// <summary>字典参数。管理用户或系统全局的名值对数据，常用于参数配置场合</summary>
public partial class ParameterModel : IModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int32 ID { get; set; }

    /// <summary>用户。按用户区分参数，用户0表示系统级</summary>
    public Int32 UserID { get; set; }

    /// <summary>类别</summary>
    public String Category { get; set; }

    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>数值</summary>
    public String Value { get; set; }

    /// <summary>长数值</summary>
    public String LongValue { get; set; }

    /// <summary>种类。0普通，21列表，22名值</summary>
    public XCode.Membership.ParameterKinds Kind { get; set; }

    /// <summary>启用</summary>
    public Boolean Enable { get; set; }

    /// <summary>扩展1</summary>
    public Int32 Ex1 { get; set; }

    /// <summary>扩展2</summary>
    public Decimal Ex2 { get; set; }

    /// <summary>扩展3</summary>
    public Double Ex3 { get; set; }

    /// <summary>扩展4</summary>
    public String Ex4 { get; set; }

    /// <summary>扩展5</summary>
    public String Ex5 { get; set; }

    /// <summary>扩展6</summary>
    public String Ex6 { get; set; }

    /// <summary>创建者</summary>
    public String CreateUser { get; set; }

    /// <summary>创建用户</summary>
    public Int32 CreateUserID { get; set; }

    /// <summary>创建地址</summary>
    public String CreateIP { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新者</summary>
    public String UpdateUser { get; set; }

    /// <summary>更新用户</summary>
    public Int32 UpdateUserID { get; set; }

    /// <summary>更新地址</summary>
    public String UpdateIP { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }

    /// <summary>备注</summary>
    public String Remark { get; set; }
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public virtual Object this[String name]
    {
        get
        {
            return name switch
            {
                "ID" => ID,
                "UserID" => UserID,
                "Category" => Category,
                "Name" => Name,
                "Value" => Value,
                "LongValue" => LongValue,
                "Kind" => Kind,
                "Enable" => Enable,
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
                _ => null
            };
        }
        set
        {
            switch (name)
            {
                case "ID": ID = value.ToInt(); break;
                case "UserID": UserID = value.ToInt(); break;
                case "Category": Category = Convert.ToString(value); break;
                case "Name": Name = Convert.ToString(value); break;
                case "Value": Value = Convert.ToString(value); break;
                case "LongValue": LongValue = Convert.ToString(value); break;
                case "Kind": Kind = (XCode.Membership.ParameterKinds)value.ToInt(); break;
                case "Enable": Enable = value.ToBoolean(); break;
                case "Ex1": Ex1 = value.ToInt(); break;
                case "Ex2": Ex2 = Convert.ToDecimal(value); break;
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
            }
        }
    }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IParameter model)
    {
        ID = model.ID;
        UserID = model.UserID;
        Category = model.Category;
        Name = model.Name;
        Value = model.Value;
        LongValue = model.LongValue;
        Kind = model.Kind;
        Enable = model.Enable;
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
