using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Company.MyName;

/// <summary>居民信息</summary>
[Serializable]
[DataObject]
[Description("居民信息")]
[BindIndex("CreditNoPName", true, "Pname,CreditNo")]
[BindIndex("Build_IDIndex", false, "Build_ID")]
[BindIndex("BuildIDIndex", false, "BuildID")]
[BindTable("core_person", Description = "居民信息", ConnName = "MyConn", DbType = DatabaseType.MySql)]
public partial class CorePerson
{
    #region 属性
    private Int32 _PersonID;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("PersonID", "编号", "int(11)")]
    public Int32 PersonID { get => _PersonID; set { if (OnPropertyChanging("PersonID", value)) { _PersonID = value; OnPropertyChanged("PersonID"); } } }

    private String _Pname = null!;
    /// <summary>姓名</summary>
    [DisplayName("姓名")]
    [Description("姓名")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Pname", "姓名", "varchar(50)")]
    public String Pname { get => _Pname; set { if (OnPropertyChanging("Pname", value)) { _Pname = value; OnPropertyChanged("Pname"); } } }

    private Int32 _Psex;
    /// <summary>性别</summary>
    [DisplayName("性别")]
    [Description("性别")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("Psex", "性别", "int(11)")]
    public Int32 Psex { get => _Psex; set { if (OnPropertyChanging("Psex", value)) { _Psex = value; OnPropertyChanged("Psex"); } } }

    private String _CreditNo = null!;
    /// <summary>身份证号</summary>
    [DisplayName("身份证号")]
    [Description("身份证号")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("CreditNo", "身份证号", "char(50)")]
    public String CreditNo { get => _CreditNo; set { if (OnPropertyChanging("CreditNo", value)) { _CreditNo = value; OnPropertyChanged("CreditNo"); } } }

    private String? _Mobile;
    /// <summary>联系电话</summary>
    [DisplayName("联系电话")]
    [Description("联系电话")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Mobile", "联系电话", "char(50)")]
    public String? Mobile { get => _Mobile; set { if (OnPropertyChanging("Mobile", value)) { _Mobile = value; OnPropertyChanged("Mobile"); } } }

    private Int32 _BuildID;
    /// <summary>楼宇ID</summary>
    [DisplayName("楼宇ID")]
    [Description("楼宇ID")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("BuildID", "楼宇ID", "int(11)")]
    public Int32 BuildID { get => _BuildID; set { if (OnPropertyChanging("BuildID", value)) { _BuildID = value; OnPropertyChanged("BuildID"); } } }

    private Int32 _Build_ID;
    /// <summary>平台楼号</summary>
    [DisplayName("平台楼号")]
    [Description("平台楼号")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("Build_ID", "平台楼号", "int(11)")]
    public Int32 Build_ID { get => _Build_ID; set { if (OnPropertyChanging("Build_ID", value)) { _Build_ID = value; OnPropertyChanged("Build_ID"); } } }

    private String? _UnitNum;
    /// <summary>单元号</summary>
    [DisplayName("单元号")]
    [Description("单元号")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("UnitNum", "单元号", "varchar(20)")]
    public String? UnitNum { get => _UnitNum; set { if (OnPropertyChanging("UnitNum", value)) { _UnitNum = value; OnPropertyChanged("UnitNum"); } } }

    private String? _HouseNum;
    /// <summary>房屋号</summary>
    [DisplayName("房屋号")]
    [Description("房屋号")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("HouseNum", "房屋号", "varchar(20)")]
    public String? HouseNum { get => _HouseNum; set { if (OnPropertyChanging("HouseNum", value)) { _HouseNum = value; OnPropertyChanged("HouseNum"); } } }

    private String? _CreateUser;
    /// <summary>创建者</summary>
    [DisplayName("创建者")]
    [Description("创建者")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("CreateUser", "创建者", "varchar(100)")]
    public String? CreateUser { get => _CreateUser; set { if (OnPropertyChanging("CreateUser", value)) { _CreateUser = value; OnPropertyChanged("CreateUser"); } } }

    private Int32 _CreateUserId;
    /// <summary>创建者ID</summary>
    [DisplayName("创建者ID")]
    [Description("创建者ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateUserId", "创建者ID", "int(11)")]
    public Int32 CreateUserId { get => _CreateUserId; set { if (OnPropertyChanging("CreateUserId", value)) { _CreateUserId = value; OnPropertyChanged("CreateUserId"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private String? _CreateIP;
    /// <summary>创建IP</summary>
    [DisplayName("创建IP")]
    [Description("创建IP")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateIP", "创建IP", "varchar(50)")]
    public String? CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }

    private String? _UpdateUser;
    /// <summary>修改用户</summary>
    [DisplayName("修改用户")]
    [Description("修改用户")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("UpdateUser", "修改用户", "varchar(100)")]
    public String? UpdateUser { get => _UpdateUser; set { if (OnPropertyChanging("UpdateUser", value)) { _UpdateUser = value; OnPropertyChanged("UpdateUser"); } } }

    private Int32 _UpdateUserId;
    /// <summary>修改用户ID</summary>
    [DisplayName("修改用户ID")]
    [Description("修改用户ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdateUserId", "修改用户ID", "int(11)")]
    public Int32 UpdateUserId { get => _UpdateUserId; set { if (OnPropertyChanging("UpdateUserId", value)) { _UpdateUserId = value; OnPropertyChanged("UpdateUserId"); } } }

    private DateTime _UpdateTime;
    /// <summary>修改时间</summary>
    [DisplayName("修改时间")]
    [Description("修改时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdateTime", "修改时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }

    private String? _UpdateIP;
    /// <summary>修改IP</summary>
    [DisplayName("修改IP")]
    [Description("修改IP")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateIP", "修改IP", "varchar(50)")]
    public String? UpdateIP { get => _UpdateIP; set { if (OnPropertyChanging("UpdateIP", value)) { _UpdateIP = value; OnPropertyChanged("UpdateIP"); } } }

    private String? _Remark;
    /// <summary>备注</summary>
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Remark", "备注", "varchar(200)")]
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
            "PersonID" => _PersonID,
            "Pname" => _Pname,
            "Psex" => _Psex,
            "CreditNo" => _CreditNo,
            "Mobile" => _Mobile,
            "BuildID" => _BuildID,
            "Build_ID" => _Build_ID,
            "UnitNum" => _UnitNum,
            "HouseNum" => _HouseNum,
            "CreateUser" => _CreateUser,
            "CreateUserId" => _CreateUserId,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            "UpdateUser" => _UpdateUser,
            "UpdateUserId" => _UpdateUserId,
            "UpdateTime" => _UpdateTime,
            "UpdateIP" => _UpdateIP,
            "Remark" => _Remark,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "PersonID": _PersonID = value.ToInt(); break;
                case "Pname": _Pname = Convert.ToString(value); break;
                case "Psex": _Psex = value.ToInt(); break;
                case "CreditNo": _CreditNo = Convert.ToString(value); break;
                case "Mobile": _Mobile = Convert.ToString(value); break;
                case "BuildID": _BuildID = value.ToInt(); break;
                case "Build_ID": _Build_ID = value.ToInt(); break;
                case "UnitNum": _UnitNum = Convert.ToString(value); break;
                case "HouseNum": _HouseNum = Convert.ToString(value); break;
                case "CreateUser": _CreateUser = Convert.ToString(value); break;
                case "CreateUserId": _CreateUserId = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "UpdateUser": _UpdateUser = Convert.ToString(value); break;
                case "UpdateUserId": _UpdateUserId = value.ToInt(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                case "UpdateIP": _UpdateIP = Convert.ToString(value); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据姓名、身份证号查找</summary>
    /// <param name="pname">姓名</param>
    /// <param name="creditNo">身份证号</param>
    /// <returns>实体对象</returns>
    public static CorePerson? FindByPnameAndCreditNo(String pname, String creditNo)
    {
        if (pname.IsNullOrEmpty()) return null;
        if (creditNo.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Pname.EqualIgnoreCase(pname) && e.CreditNo.EqualIgnoreCase(creditNo));

        return Find(_.Pname == pname & _.CreditNo == creditNo);
    }

    /// <summary>根据姓名查找</summary>
    /// <param name="pname">姓名</param>
    /// <returns>实体列表</returns>
    public static IList<CorePerson> FindAllByPname(String pname)
    {
        if (pname.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Pname.EqualIgnoreCase(pname));

        return FindAll(_.Pname == pname);
    }

    /// <summary>根据楼宇ID查找</summary>
    /// <param name="buildId">楼宇ID</param>
    /// <returns>实体列表</returns>
    public static IList<CorePerson> FindAllByBuildID(Int32 buildId)
    {
        if (buildId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.BuildID == buildId);

        return FindAll(_.BuildID == buildId);
    }
    #endregion

    #region 字段名
    /// <summary>取得居民信息字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field PersonID = FindByName("PersonID");

        /// <summary>姓名</summary>
        public static readonly Field Pname = FindByName("Pname");

        /// <summary>性别</summary>
        public static readonly Field Psex = FindByName("Psex");

        /// <summary>身份证号</summary>
        public static readonly Field CreditNo = FindByName("CreditNo");

        /// <summary>联系电话</summary>
        public static readonly Field Mobile = FindByName("Mobile");

        /// <summary>楼宇ID</summary>
        public static readonly Field BuildID = FindByName("BuildID");

        /// <summary>平台楼号</summary>
        public static readonly Field Build_ID = FindByName("Build_ID");

        /// <summary>单元号</summary>
        public static readonly Field UnitNum = FindByName("UnitNum");

        /// <summary>房屋号</summary>
        public static readonly Field HouseNum = FindByName("HouseNum");

        /// <summary>创建者</summary>
        public static readonly Field CreateUser = FindByName("CreateUser");

        /// <summary>创建者ID</summary>
        public static readonly Field CreateUserId = FindByName("CreateUserId");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建IP</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>修改用户</summary>
        public static readonly Field UpdateUser = FindByName("UpdateUser");

        /// <summary>修改用户ID</summary>
        public static readonly Field UpdateUserId = FindByName("UpdateUserId");

        /// <summary>修改时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>修改IP</summary>
        public static readonly Field UpdateIP = FindByName("UpdateIP");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得居民信息字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String PersonID = "PersonID";

        /// <summary>姓名</summary>
        public const String Pname = "Pname";

        /// <summary>性别</summary>
        public const String Psex = "Psex";

        /// <summary>身份证号</summary>
        public const String CreditNo = "CreditNo";

        /// <summary>联系电话</summary>
        public const String Mobile = "Mobile";

        /// <summary>楼宇ID</summary>
        public const String BuildID = "BuildID";

        /// <summary>平台楼号</summary>
        public const String Build_ID = "Build_ID";

        /// <summary>单元号</summary>
        public const String UnitNum = "UnitNum";

        /// <summary>房屋号</summary>
        public const String HouseNum = "HouseNum";

        /// <summary>创建者</summary>
        public const String CreateUser = "CreateUser";

        /// <summary>创建者ID</summary>
        public const String CreateUserId = "CreateUserId";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建IP</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>修改用户</summary>
        public const String UpdateUser = "UpdateUser";

        /// <summary>修改用户ID</summary>
        public const String UpdateUserId = "UpdateUserId";

        /// <summary>修改时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>修改IP</summary>
        public const String UpdateIP = "UpdateIP";

        /// <summary>备注</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
