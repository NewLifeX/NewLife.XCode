using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Remoting;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Company.MyName;

/// <summary>日志。应用系统审计日志，记录用户的各种操作，禁止修改和删除</summary>
[Serializable]
[DataObject]
[Description("日志。应用系统审计日志，记录用户的各种操作，禁止修改和删除")]
[BindIndex("IX_Log_Action_Category_ID", false, "Action,Category,ID")]
[BindIndex("IX_Log_Category_LinkID_ID", false, "Category,LinkID,ID")]
[BindIndex("IX_Log_CreateUserID_ID", false, "CreateUserID,ID")]
[BindTable("Log", Description = "日志。应用系统审计日志，记录用户的各种操作，禁止修改和删除", ConnName = "MyConn", DbType = DatabaseType.None)]
public partial class Log
{
    #region 属性
    private Int64 _ID;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, false, false, 0)]
    [BindColumn("ID", "编号", "", DataScale = "time")]
    public Int64 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

    private String? _Category;
    /// <summary>类别</summary>
    [DisplayName("类别")]
    [Description("类别")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Category", "类别", "")]
    public String? Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private String _Action = null!;
    /// <summary>操作</summary>
    [DisplayName("操作")]
    [Description("操作")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Action", "操作", "")]
    public String Action { get => _Action; set { if (OnPropertyChanging("Action", value)) { _Action = value; OnPropertyChanged("Action"); } } }

    private Int64 _LinkID;
    /// <summary>链接</summary>
    [DisplayName("链接")]
    [Description("链接")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("LinkID", "链接", "")]
    public Int64 LinkID { get => _LinkID; set { if (OnPropertyChanging("LinkID", value)) { _LinkID = value; OnPropertyChanged("LinkID"); } } }

    private Boolean _Success;
    /// <summary>成功</summary>
    [DisplayName("成功")]
    [Description("成功")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Success", "成功", "")]
    public Boolean Success { get => _Success; set { if (OnPropertyChanging("Success", value)) { _Success = value; OnPropertyChanged("Success"); } } }

    private String? _UserName;
    /// <summary>用户名</summary>
    [DisplayName("用户名")]
    [Description("用户名")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UserName", "用户名", "")]
    public String? UserName { get => _UserName; set { if (OnPropertyChanging("UserName", value)) { _UserName = value; OnPropertyChanged("UserName"); } } }

    private Int32 _Ex1;
    /// <summary>扩展1</summary>
    [Category("扩展")]
    [DisplayName("扩展1")]
    [Description("扩展1")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Ex1", "扩展1", "")]
    public Int32 Ex1 { get => _Ex1; set { if (OnPropertyChanging("Ex1", value)) { _Ex1 = value; OnPropertyChanged("Ex1"); } } }

    private Int32 _Ex2;
    /// <summary>扩展2</summary>
    [Category("扩展")]
    [DisplayName("扩展2")]
    [Description("扩展2")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Ex2", "扩展2", "")]
    public Int32 Ex2 { get => _Ex2; set { if (OnPropertyChanging("Ex2", value)) { _Ex2 = value; OnPropertyChanged("Ex2"); } } }

    private Double _Ex3;
    /// <summary>扩展3</summary>
    [Category("扩展")]
    [DisplayName("扩展3")]
    [Description("扩展3")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Ex3", "扩展3", "")]
    public Double Ex3 { get => _Ex3; set { if (OnPropertyChanging("Ex3", value)) { _Ex3 = value; OnPropertyChanged("Ex3"); } } }

    private String? _Ex4;
    /// <summary>扩展4</summary>
    [Category("扩展")]
    [DisplayName("扩展4")]
    [Description("扩展4")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Ex4", "扩展4", "")]
    public String? Ex4 { get => _Ex4; set { if (OnPropertyChanging("Ex4", value)) { _Ex4 = value; OnPropertyChanged("Ex4"); } } }

    private String? _Ex5;
    /// <summary>扩展5</summary>
    [Category("扩展")]
    [DisplayName("扩展5")]
    [Description("扩展5")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Ex5", "扩展5", "")]
    public String? Ex5 { get => _Ex5; set { if (OnPropertyChanging("Ex5", value)) { _Ex5 = value; OnPropertyChanged("Ex5"); } } }

    private String? _Ex6;
    /// <summary>扩展6</summary>
    [Category("扩展")]
    [DisplayName("扩展6")]
    [Description("扩展6")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Ex6", "扩展6", "")]
    public String? Ex6 { get => _Ex6; set { if (OnPropertyChanging("Ex6", value)) { _Ex6 = value; OnPropertyChanged("Ex6"); } } }

    private String? _TraceId;
    /// <summary>性能追踪。用于APM性能追踪定位，还原该事件的调用链</summary>
    [DisplayName("性能追踪")]
    [Description("性能追踪。用于APM性能追踪定位，还原该事件的调用链")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TraceId", "性能追踪。用于APM性能追踪定位，还原该事件的调用链", "")]
    public String? TraceId { get => _TraceId; set { if (OnPropertyChanging("TraceId", value)) { _TraceId = value; OnPropertyChanged("TraceId"); } } }

    private String? _CreateUser;
    /// <summary>创建者</summary>
    [Category("扩展")]
    [DisplayName("创建者")]
    [Description("创建者")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateUser", "创建者", "")]
    public String? CreateUser { get => _CreateUser; set { if (OnPropertyChanging("CreateUser", value)) { _CreateUser = value; OnPropertyChanged("CreateUser"); } } }

    private Int32 _CreateUserID;
    /// <summary>创建用户</summary>
    [Category("扩展")]
    [DisplayName("创建用户")]
    [Description("创建用户")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateUserID", "创建用户", "")]
    public Int32 CreateUserID { get => _CreateUserID; set { if (OnPropertyChanging("CreateUserID", value)) { _CreateUserID = value; OnPropertyChanged("CreateUserID"); } } }

    private String? _CreateIP;
    /// <summary>创建地址</summary>
    [Category("扩展")]
    [DisplayName("创建地址")]
    [Description("创建地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateIP", "创建地址", "")]
    public String? CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }

    private DateTime _CreateTime;
    /// <summary>时间</summary>
    [DisplayName("时间")]
    [Description("时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateTime", "时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private String? _Remark;
    /// <summary>详细信息</summary>
    [DisplayName("详细信息")]
    [Description("详细信息")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Remark", "详细信息", "")]
    public String? Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object? this[String name]
    {
        get => name switch
        {
            "ID" => _ID,
            "Category" => _Category,
            "Action" => _Action,
            "LinkID" => _LinkID,
            "Success" => _Success,
            "UserName" => _UserName,
            "Ex1" => _Ex1,
            "Ex2" => _Ex2,
            "Ex3" => _Ex3,
            "Ex4" => _Ex4,
            "Ex5" => _Ex5,
            "Ex6" => _Ex6,
            "TraceId" => _TraceId,
            "CreateUser" => _CreateUser,
            "CreateUserID" => _CreateUserID,
            "CreateIP" => _CreateIP,
            "CreateTime" => _CreateTime,
            "Remark" => _Remark,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "ID": _ID = ValidHelper.ToInt64(value); break;
                case "Category": _Category = ValidHelper.ToString(value); break;
                case "Action": _Action = ValidHelper.ToString(value); break;
                case "LinkID": _LinkID = ValidHelper.ToInt64(value); break;
                case "Success": _Success = ValidHelper.ToBoolean(value); break;
                case "UserName": _UserName = ValidHelper.ToString(value); break;
                case "Ex1": _Ex1 = ValidHelper.ToInt32(value); break;
                case "Ex2": _Ex2 = ValidHelper.ToInt32(value); break;
                case "Ex3": _Ex3 = ValidHelper.ToDouble(value); break;
                case "Ex4": _Ex4 = ValidHelper.ToString(value); break;
                case "Ex5": _Ex5 = ValidHelper.ToString(value); break;
                case "Ex6": _Ex6 = ValidHelper.ToString(value); break;
                case "TraceId": _TraceId = ValidHelper.ToString(value); break;
                case "CreateUser": _CreateUser = ValidHelper.ToString(value); break;
                case "CreateUserID": _CreateUserID = ValidHelper.ToInt32(value); break;
                case "CreateIP": _CreateIP = ValidHelper.ToString(value); break;
                case "CreateTime": _CreateTime = ValidHelper.ToDateTime(value); break;
                case "Remark": _Remark = ValidHelper.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    /// <summary>创建用户</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public User? MyCreateUser => Extends.Get(nameof(MyCreateUser), k => User.FindById(CreateUserID));

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static Log? FindByID(Int64 id)
    {
        if (id < 0) return null;

        return Find(_.ID == id);
    }

    /// <summary>根据操作、类别查找</summary>
    /// <param name="action">操作</param>
    /// <param name="category">类别</param>
    /// <returns>实体列表</returns>
    public static IList<Log> FindAllByActionAndCategory(String action, String? category)
    {
        if (action.IsNullOrEmpty()) return [];
        if (category == null) return [];

        return FindAll(_.Action == action & _.Category == category);
    }

    /// <summary>根据类别、链接查找</summary>
    /// <param name="category">类别</param>
    /// <param name="linkId">链接</param>
    /// <returns>实体列表</returns>
    public static IList<Log> FindAllByCategoryAndLinkID(String? category, Int64 linkId)
    {
        if (category == null) return [];
        if (linkId < 0) return [];

        return FindAll(_.Category == category & _.LinkID == linkId);
    }

    /// <summary>根据创建用户查找</summary>
    /// <param name="createUserId">创建用户</param>
    /// <returns>实体列表</returns>
    public static IList<Log> FindAllByCreateUserID(Int32 createUserId)
    {
        if (createUserId < 0) return [];

        return FindAll(_.CreateUserID == createUserId);
    }
    #endregion

    #region 数据清理
    /// <summary>清理指定时间段内的数据</summary>
    /// <param name="start">开始时间。未指定时清理小于指定时间的所有数据</param>
    /// <param name="end">结束时间</param>
    /// <returns>清理行数</returns>
    public static Int32 DeleteWith(DateTime start, DateTime end)
    {
        return Delete(_.ID.Between(start, end, Meta.Factory.Snow));
    }
    #endregion

    #region 字段名
    /// <summary>取得日志字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field ID = FindByName("ID");

        /// <summary>类别</summary>
        public static readonly Field Category = FindByName("Category");

        /// <summary>操作</summary>
        public static readonly Field Action = FindByName("Action");

        /// <summary>链接</summary>
        public static readonly Field LinkID = FindByName("LinkID");

        /// <summary>成功</summary>
        public static readonly Field Success = FindByName("Success");

        /// <summary>用户名</summary>
        public static readonly Field UserName = FindByName("UserName");

        /// <summary>扩展1</summary>
        public static readonly Field Ex1 = FindByName("Ex1");

        /// <summary>扩展2</summary>
        public static readonly Field Ex2 = FindByName("Ex2");

        /// <summary>扩展3</summary>
        public static readonly Field Ex3 = FindByName("Ex3");

        /// <summary>扩展4</summary>
        public static readonly Field Ex4 = FindByName("Ex4");

        /// <summary>扩展5</summary>
        public static readonly Field Ex5 = FindByName("Ex5");

        /// <summary>扩展6</summary>
        public static readonly Field Ex6 = FindByName("Ex6");

        /// <summary>性能追踪。用于APM性能追踪定位，还原该事件的调用链</summary>
        public static readonly Field TraceId = FindByName("TraceId");

        /// <summary>创建者</summary>
        public static readonly Field CreateUser = FindByName("CreateUser");

        /// <summary>创建用户</summary>
        public static readonly Field CreateUserID = FindByName("CreateUserID");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>详细信息</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得日志字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String ID = "ID";

        /// <summary>类别</summary>
        public const String Category = "Category";

        /// <summary>操作</summary>
        public const String Action = "Action";

        /// <summary>链接</summary>
        public const String LinkID = "LinkID";

        /// <summary>成功</summary>
        public const String Success = "Success";

        /// <summary>用户名</summary>
        public const String UserName = "UserName";

        /// <summary>扩展1</summary>
        public const String Ex1 = "Ex1";

        /// <summary>扩展2</summary>
        public const String Ex2 = "Ex2";

        /// <summary>扩展3</summary>
        public const String Ex3 = "Ex3";

        /// <summary>扩展4</summary>
        public const String Ex4 = "Ex4";

        /// <summary>扩展5</summary>
        public const String Ex5 = "Ex5";

        /// <summary>扩展6</summary>
        public const String Ex6 = "Ex6";

        /// <summary>性能追踪。用于APM性能追踪定位，还原该事件的调用链</summary>
        public const String TraceId = "TraceId";

        /// <summary>创建者</summary>
        public const String CreateUser = "CreateUser";

        /// <summary>创建用户</summary>
        public const String CreateUserID = "CreateUserID";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>详细信息</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
