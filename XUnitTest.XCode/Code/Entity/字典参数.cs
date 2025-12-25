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

/// <summary>字典参数。管理用户或系统全局的名值对数据，常用于参数配置场合</summary>
[Serializable]
[DataObject]
[Description("字典参数。管理用户或系统全局的名值对数据，常用于参数配置场合")]
[BindIndex("IU_Parameter_UserID_Category_Name", true, "UserID,Category,Name")]
[BindIndex("IX_Parameter_Category_Name", false, "Category,Name")]
[BindIndex("IX_Parameter_UpdateTime", false, "UpdateTime")]
[BindTable("Parameter", Description = "字典参数。管理用户或系统全局的名值对数据，常用于参数配置场合", ConnName = "Membership666", DbType = DatabaseType.None)]
public partial class Parameter : IParameter, IEntity<IParameter>
{
    #region 属性
    private Int32 _ID;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("ID", "编号", "")]
    public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

    private Int32 _UserID;
    /// <summary>用户。按用户区分参数，用户0表示系统级</summary>
    [DisplayName("用户")]
    [Description("用户。按用户区分参数，用户0表示系统级")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UserID", "用户。按用户区分参数，用户0表示系统级", "")]
    public Int32 UserID { get => _UserID; set { if (OnPropertyChanging("UserID", value)) { _UserID = value; OnPropertyChanged("UserID"); } } }

    private String? _Category;
    /// <summary>类别</summary>
    [DisplayName("类别")]
    [Description("类别")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Category", "类别", "")]
    public String? Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private String _Name = null!;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String? _Value;
    /// <summary>数值</summary>
    [DisplayName("数值")]
    [Description("数值")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Value", "数值", "")]
    public String? Value { get => _Value; set { if (OnPropertyChanging("Value", value)) { _Value = value; OnPropertyChanged("Value"); } } }

    private Boolean _Readonly;
    /// <summary>只读标记。用于验证关键字处理</summary>
    [DisplayName("只读标记")]
    [Description("只读标记。用于验证关键字处理")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Readonly", "只读标记。用于验证关键字处理", "")]
    public Boolean Readonly { get => _Readonly; set { if (OnPropertyChanging("Readonly", value)) { _Readonly = value; OnPropertyChanged("Readonly"); } } }

    private String? _LongValue;
    /// <summary>长数值</summary>
    [DisplayName("长数值")]
    [Description("长数值")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("LongValue", "长数值", "")]
    public String? LongValue { get => _LongValue; set { if (OnPropertyChanging("LongValue", value)) { _LongValue = value; OnPropertyChanged("LongValue"); } } }

    private XCode.Membership.ParameterKinds _Kind;
    /// <summary>种类。0普通，21列表，22名值</summary>
    [DisplayName("种类")]
    [Description("种类。0普通，21列表，22名值")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Kind", "种类。0普通，21列表，22名值", "")]
    public XCode.Membership.ParameterKinds Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Int32 _Ex1;
    /// <summary>扩展1</summary>
    [Category("扩展")]
    [DisplayName("扩展1")]
    [Description("扩展1")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Ex1", "扩展1", "")]
    public Int32 Ex1 { get => _Ex1; set { if (OnPropertyChanging("Ex1", value)) { _Ex1 = value; OnPropertyChanged("Ex1"); } } }

    private Decimal _Ex2;
    /// <summary>扩展2</summary>
    [Category("扩展")]
    [DisplayName("扩展2")]
    [Description("扩展2")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Ex2", "扩展2", "", Precision = 19, Scale = 4)]
    public Decimal Ex2 { get => _Ex2; set { if (OnPropertyChanging("Ex2", value)) { _Ex2 = value; OnPropertyChanged("Ex2"); } } }

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
    public void Copy(IParameter model)
    {
        ID = model.ID;
        UserID = model.UserID;
        Category = model.Category;
        Name = model.Name;
        Value = model.Value;
        Readonly = model.Readonly;
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

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object? this[String name]
    {
        get => name switch
        {
            "ID" => _ID,
            "UserID" => _UserID,
            "Category" => _Category,
            "Name" => _Name,
            "Value" => _Value,
            "Readonly" => _Readonly,
            "LongValue" => _LongValue,
            "Kind" => _Kind,
            "Enable" => _Enable,
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
                case "ID": _ID = value.ToInt(); break;
                case "UserID": _UserID = value.ToInt(); break;
                case "Category": _Category = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Value": _Value = Convert.ToString(value); break;
                case "Readonly": _Readonly = value.ToBoolean(); break;
                case "LongValue": _LongValue = Convert.ToString(value); break;
                case "Kind": _Kind = (XCode.Membership.ParameterKinds)value.ToInt(); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "Ex1": _Ex1 = value.ToInt(); break;
                case "Ex2": _Ex2 = Convert.ToDecimal(value); break;
                case "Ex3": _Ex3 = value.ToDouble(); break;
                case "Ex4": _Ex4 = Convert.ToString(value); break;
                case "Ex5": _Ex5 = Convert.ToString(value); break;
                case "Ex6": _Ex6 = Convert.ToString(value); break;
                case "CreateUser": _CreateUser = Convert.ToString(value); break;
                case "CreateUserID": _CreateUserID = value.ToInt(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateUser": _UpdateUser = Convert.ToString(value); break;
                case "UpdateUserID": _UpdateUserID = value.ToInt(); break;
                case "UpdateIP": _UpdateIP = Convert.ToString(value); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    /// <summary>用户</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public User? User => Extends.Get(nameof(User), k => User.FindByID(UserID));

    /// <summary>用户</summary>
    [Map(nameof(UserID), typeof(User), "ID")]
    public String? UserName => User?.ToString();

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static Parameter? FindByID(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.ID == id);
    }

    /// <summary>根据用户、类别、名称查找</summary>
    /// <param name="userId">用户</param>
    /// <param name="category">类别</param>
    /// <param name="name">名称</param>
    /// <returns>实体对象</returns>
    public static Parameter? FindByUserIDAndCategoryAndName(Int32 userId, String? category, String name)
    {
        if (userId < 0) return null;
        if (category == null) return null;
        if (name.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.UserID == userId && e.Category.EqualIgnoreCase(category) && e.Name.EqualIgnoreCase(name));

        return Find(_.UserID == userId & _.Category == category & _.Name == name);
    }

    /// <summary>根据用户查找</summary>
    /// <param name="userId">用户</param>
    /// <returns>实体列表</returns>
    public static IList<Parameter> FindAllByUserID(Int32 userId)
    {
        if (userId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.UserID == userId);

        return FindAll(_.UserID == userId);
    }

    /// <summary>根据用户、类别查找</summary>
    /// <param name="userId">用户</param>
    /// <param name="category">类别</param>
    /// <returns>实体列表</returns>
    public static IList<Parameter> FindAllByUserIDAndCategory(Int32 userId, String? category)
    {
        if (userId < 0) return [];
        if (category == null) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.UserID == userId && e.Category.EqualIgnoreCase(category));

        return FindAll(_.UserID == userId & _.Category == category);
    }

    /// <summary>根据类别、名称查找</summary>
    /// <param name="category">类别</param>
    /// <param name="name">名称</param>
    /// <returns>实体列表</returns>
    public static IList<Parameter> FindAllByCategoryAndName(String? category, String name)
    {
        if (category == null) return [];
        if (name.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Category.EqualIgnoreCase(category) && e.Name.EqualIgnoreCase(name));

        return FindAll(_.Category == category & _.Name == name);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="userId">用户。按用户区分参数，用户0表示系统级</param>
    /// <param name="category">类别</param>
    /// <param name="@readonly">只读标记。用于验证关键字处理</param>
    /// <param name="kind">种类。0普通，21列表，22名值</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<Parameter> Search(Int32 userId, String? category, Boolean? @readonly, XCode.Membership.ParameterKinds kind, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (userId >= 0) exp &= _.UserID == userId;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        if (@readonly != null) exp &= _.Readonly == @readonly;
        if (kind >= 0) exp &= _.Kind == kind;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得字典参数字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field ID = FindByName("ID");

        /// <summary>用户。按用户区分参数，用户0表示系统级</summary>
        public static readonly Field UserID = FindByName("UserID");

        /// <summary>类别</summary>
        public static readonly Field Category = FindByName("Category");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>数值</summary>
        public static readonly Field Value = FindByName("Value");

        /// <summary>只读标记。用于验证关键字处理</summary>
        public static readonly Field Readonly = FindByName("Readonly");

        /// <summary>长数值</summary>
        public static readonly Field LongValue = FindByName("LongValue");

        /// <summary>种类。0普通，21列表，22名值</summary>
        public static readonly Field Kind = FindByName("Kind");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

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

    /// <summary>取得字典参数字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String ID = "ID";

        /// <summary>用户。按用户区分参数，用户0表示系统级</summary>
        public const String UserID = "UserID";

        /// <summary>类别</summary>
        public const String Category = "Category";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>数值</summary>
        public const String Value = "Value";

        /// <summary>只读标记。用于验证关键字处理</summary>
        public const String Readonly = "Readonly";

        /// <summary>长数值</summary>
        public const String LongValue = "LongValue";

        /// <summary>种类。0普通，21列表，22名值</summary>
        public const String Kind = "Kind";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

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
