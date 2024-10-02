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

namespace XCode.Membership666;

/// <summary>部门。组织机构，多级树状结构</summary>
[Serializable]
[DataObject]
[Description("部门。组织机构，多级树状结构")]
[BindIndex("IX_Department_Name", false, "Name")]
[BindIndex("IX_Department_ParentID_Name", false, "ParentID,Name")]
[BindIndex("IX_Department_Code", false, "Code")]
[BindIndex("IX_Department_UpdateTime", false, "UpdateTime")]
[BindIndex("IX_Department_TenantId", false, "TenantId")]
[BindTable("Department", Description = "部门。组织机构，多级树状结构", ConnName = "Membership666", DbType = DatabaseType.None)]
public partial class Department : IDepartment, IEntity<IDepartment>
{
    #region 属性
    private Int32 _ID;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("ID", "编号", "")]
    public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

    private Int32 _TenantId;
    /// <summary>租户</summary>
    [DisplayName("租户")]
    [Description("租户")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TenantId", "租户", "")]
    public Int32 TenantId { get => _TenantId; set { if (OnPropertyChanging("TenantId", value)) { _TenantId = value; OnPropertyChanged("TenantId"); } } }

    private String? _Code;
    /// <summary>代码</summary>
    [DisplayName("代码")]
    [Description("代码")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Code", "代码", "")]
    public String? Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private String _Name = null!;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String? _FullName;
    /// <summary>全名</summary>
    [DisplayName("全名")]
    [Description("全名")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("FullName", "全名", "")]
    public String? FullName { get => _FullName; set { if (OnPropertyChanging("FullName", value)) { _FullName = value; OnPropertyChanged("FullName"); } } }

    private Int32 _ParentID;
    /// <summary>父级</summary>
    [DisplayName("父级")]
    [Description("父级")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ParentID", "父级", "")]
    public Int32 ParentID { get => _ParentID; set { if (OnPropertyChanging("ParentID", value)) { _ParentID = value; OnPropertyChanged("ParentID"); } } }

    private Int32 _Level;
    /// <summary>层级。树状结构的层级</summary>
    [DisplayName("层级")]
    [Description("层级。树状结构的层级")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Level", "层级。树状结构的层级", "")]
    public Int32 Level { get => _Level; set { if (OnPropertyChanging("Level", value)) { _Level = value; OnPropertyChanged("Level"); } } }

    private Int32 _Sort;
    /// <summary>排序。同级内排序</summary>
    [DisplayName("排序")]
    [Description("排序。同级内排序")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Sort", "排序。同级内排序", "")]
    public Int32 Sort { get => _Sort; set { if (OnPropertyChanging("Sort", value)) { _Sort = value; OnPropertyChanged("Sort"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Boolean _Visible;
    /// <summary>可见</summary>
    [DisplayName("可见")]
    [Description("可见")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Visible", "可见", "")]
    public Boolean Visible { get => _Visible; set { if (OnPropertyChanging("Visible", value)) { _Visible = value; OnPropertyChanged("Visible"); } } }

    private Int32 _ManagerId;
    /// <summary>管理者</summary>
    [DisplayName("管理者")]
    [Description("管理者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ManagerId", "管理者", "")]
    public Int32 ManagerId { get => _ManagerId; set { if (OnPropertyChanging("ManagerId", value)) { _ManagerId = value; OnPropertyChanged("ManagerId"); } } }

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
    /// <summary>创建时间</summary>
    [Category("扩展")]
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private String? _UpdateUser;
    /// <summary>更新者</summary>
    [Category("扩展")]
    [DisplayName("更新者")]
    [Description("更新者")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateUser", "更新者", "")]
    public String? UpdateUser { get => _UpdateUser; set { if (OnPropertyChanging("UpdateUser", value)) { _UpdateUser = value; OnPropertyChanged("UpdateUser"); } } }

    private Int32 _UpdateUserID;
    /// <summary>更新用户</summary>
    [Category("扩展")]
    [DisplayName("更新用户")]
    [Description("更新用户")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdateUserID", "更新用户", "")]
    public Int32 UpdateUserID { get => _UpdateUserID; set { if (OnPropertyChanging("UpdateUserID", value)) { _UpdateUserID = value; OnPropertyChanged("UpdateUserID"); } } }

    private String? _UpdateIP;
    /// <summary>更新地址</summary>
    [Category("扩展")]
    [DisplayName("更新地址")]
    [Description("更新地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateIP", "更新地址", "")]
    public String? UpdateIP { get => _UpdateIP; set { if (OnPropertyChanging("UpdateIP", value)) { _UpdateIP = value; OnPropertyChanged("UpdateIP"); } } }

    private DateTime _UpdateTime;
    /// <summary>更新时间</summary>
    [Category("扩展")]
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdateTime", "更新时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }

    private String? _Remark;
    /// <summary>备注</summary>
    [Category("扩展")]
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Remark", "备注", "")]
    public String? Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IDepartment model)
    {
        ID = model.ID;
        TenantId = model.TenantId;
        Code = model.Code;
        Name = model.Name;
        FullName = model.FullName;
        ParentID = model.ParentID;
        Level = model.Level;
        Sort = model.Sort;
        Enable = model.Enable;
        Visible = model.Visible;
        ManagerId = model.ManagerId;
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

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object? this[String name]
    {
        get => name switch
        {
            "ID" => _ID,
            "TenantId" => _TenantId,
            "Code" => _Code,
            "Name" => _Name,
            "FullName" => _FullName,
            "ParentID" => _ParentID,
            "Level" => _Level,
            "Sort" => _Sort,
            "Enable" => _Enable,
            "Visible" => _Visible,
            "ManagerId" => _ManagerId,
            "Ex1" => _Ex1,
            "Ex2" => _Ex2,
            "Ex3" => _Ex3,
            "Ex4" => _Ex4,
            "Ex5" => _Ex5,
            "Ex6" => _Ex6,
            "CreateUser" => _CreateUser,
            "CreateUserID" => _CreateUserID,
            "CreateIP" => _CreateIP,
            "CreateTime" => _CreateTime,
            "UpdateUser" => _UpdateUser,
            "UpdateUserID" => _UpdateUserID,
            "UpdateIP" => _UpdateIP,
            "UpdateTime" => _UpdateTime,
            "Remark" => _Remark,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "ID": _ID = ValidHelper.ToInt32(value); break;
                case "TenantId": _TenantId = ValidHelper.ToInt32(value); break;
                case "Code": _Code = ValidHelper.ToString(value); break;
                case "Name": _Name = ValidHelper.ToString(value); break;
                case "FullName": _FullName = ValidHelper.ToString(value); break;
                case "ParentID": _ParentID = ValidHelper.ToInt32(value); break;
                case "Level": _Level = ValidHelper.ToInt32(value); break;
                case "Sort": _Sort = ValidHelper.ToInt32(value); break;
                case "Enable": _Enable = ValidHelper.ToBoolean(value); break;
                case "Visible": _Visible = ValidHelper.ToBoolean(value); break;
                case "ManagerId": _ManagerId = ValidHelper.ToInt32(value); break;
                case "Ex1": _Ex1 = ValidHelper.ToInt32(value); break;
                case "Ex2": _Ex2 = ValidHelper.ToInt32(value); break;
                case "Ex3": _Ex3 = ValidHelper.ToDouble(value); break;
                case "Ex4": _Ex4 = ValidHelper.ToString(value); break;
                case "Ex5": _Ex5 = ValidHelper.ToString(value); break;
                case "Ex6": _Ex6 = ValidHelper.ToString(value); break;
                case "CreateUser": _CreateUser = ValidHelper.ToString(value); break;
                case "CreateUserID": _CreateUserID = ValidHelper.ToInt32(value); break;
                case "CreateIP": _CreateIP = ValidHelper.ToString(value); break;
                case "CreateTime": _CreateTime = ValidHelper.ToDateTime(value); break;
                case "UpdateUser": _UpdateUser = ValidHelper.ToString(value); break;
                case "UpdateUserID": _UpdateUserID = ValidHelper.ToInt32(value); break;
                case "UpdateIP": _UpdateIP = ValidHelper.ToString(value); break;
                case "UpdateTime": _UpdateTime = ValidHelper.ToDateTime(value); break;
                case "Remark": _Remark = ValidHelper.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    /// <summary>租户</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Tenant? Tenant => Extends.Get(nameof(Tenant), k => Tenant.FindById(TenantId));

    /// <summary>租户</summary>
    [Map(nameof(TenantId), typeof(Tenant), "Id")]
    public String? TenantName => Tenant?.ToString();

    /// <summary>管理者</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public User? Manager => Extends.Get(nameof(Manager), k => User.FindByID(ManagerId));

    /// <summary>管理者</summary>
    [Map(nameof(ManagerId), typeof(User), "ID")]
    public String? ManagerName => Manager?.ToString();

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static Department? FindByID(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.ID == id);
    }

    /// <summary>根据名称查找</summary>
    /// <param name="name">名称</param>
    /// <returns>实体列表</returns>
    public static IList<Department> FindAllByName(String name)
    {
        if (name.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Name.EqualIgnoreCase(name));

        return FindAll(_.Name == name);
    }

    /// <summary>根据父级、名称查找</summary>
    /// <param name="parentId">父级</param>
    /// <param name="name">名称</param>
    /// <returns>实体列表</returns>
    public static IList<Department> FindAllByParentIDAndName(Int32 parentId, String name)
    {
        if (parentId < 0) return [];
        if (name.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ParentID == parentId && e.Name.EqualIgnoreCase(name));

        return FindAll(_.ParentID == parentId & _.Name == name);
    }

    /// <summary>根据代码查找</summary>
    /// <param name="code">代码</param>
    /// <returns>实体列表</returns>
    public static IList<Department> FindAllByCode(String? code)
    {
        if (code == null) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Code.EqualIgnoreCase(code));

        return FindAll(_.Code == code);
    }

    /// <summary>根据租户查找</summary>
    /// <param name="tenantId">租户</param>
    /// <returns>实体列表</returns>
    public static IList<Department> FindAllByTenantId(Int32 tenantId)
    {
        if (tenantId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.TenantId == tenantId);

        return FindAll(_.TenantId == tenantId);
    }
    #endregion

    #region 字段名
    /// <summary>取得部门字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field ID = FindByName("ID");

        /// <summary>租户</summary>
        public static readonly Field TenantId = FindByName("TenantId");

        /// <summary>代码</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>全名</summary>
        public static readonly Field FullName = FindByName("FullName");

        /// <summary>父级</summary>
        public static readonly Field ParentID = FindByName("ParentID");

        /// <summary>层级。树状结构的层级</summary>
        public static readonly Field Level = FindByName("Level");

        /// <summary>排序。同级内排序</summary>
        public static readonly Field Sort = FindByName("Sort");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>可见</summary>
        public static readonly Field Visible = FindByName("Visible");

        /// <summary>管理者</summary>
        public static readonly Field ManagerId = FindByName("ManagerId");

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

        /// <summary>创建者</summary>
        public static readonly Field CreateUser = FindByName("CreateUser");

        /// <summary>创建用户</summary>
        public static readonly Field CreateUserID = FindByName("CreateUserID");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新者</summary>
        public static readonly Field UpdateUser = FindByName("UpdateUser");

        /// <summary>更新用户</summary>
        public static readonly Field UpdateUserID = FindByName("UpdateUserID");

        /// <summary>更新地址</summary>
        public static readonly Field UpdateIP = FindByName("UpdateIP");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得部门字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String ID = "ID";

        /// <summary>租户</summary>
        public const String TenantId = "TenantId";

        /// <summary>代码</summary>
        public const String Code = "Code";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>全名</summary>
        public const String FullName = "FullName";

        /// <summary>父级</summary>
        public const String ParentID = "ParentID";

        /// <summary>层级。树状结构的层级</summary>
        public const String Level = "Level";

        /// <summary>排序。同级内排序</summary>
        public const String Sort = "Sort";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>可见</summary>
        public const String Visible = "Visible";

        /// <summary>管理者</summary>
        public const String ManagerId = "ManagerId";

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

        /// <summary>创建者</summary>
        public const String CreateUser = "CreateUser";

        /// <summary>创建用户</summary>
        public const String CreateUserID = "CreateUserID";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新者</summary>
        public const String UpdateUser = "UpdateUser";

        /// <summary>更新用户</summary>
        public const String UpdateUserID = "UpdateUserID";

        /// <summary>更新地址</summary>
        public const String UpdateIP = "UpdateIP";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>备注</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
