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

/// <summary>用户。用户帐号信息</summary>
[Serializable]
[DataObject]
[Description("用户。用户帐号信息")]
[BindIndex("IU_User_Name", true, "Name")]
[BindIndex("IX_User_Mail", false, "Mail")]
[BindIndex("IX_User_Mobile", false, "Mobile")]
[BindIndex("IX_User_Code", false, "Code")]
[BindIndex("IX_User_RoleID", false, "RoleID")]
[BindIndex("IX_User_UpdateTime", false, "UpdateTime")]
[BindTable("User", Description = "用户。用户帐号信息", ConnName = "Membership666", DbType = DatabaseType.None)]
public partial class User : IUser, IEntity<IUser>
{
    #region 属性
    private Int32 _ID;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("ID", "编号", "")]
    public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

    private String _Name = null!;
    /// <summary>名称。登录用户名</summary>
    [DisplayName("名称")]
    [Description("名称。登录用户名")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Name", "名称。登录用户名", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String? _Password;
    /// <summary>密码</summary>
    [DisplayName("密码")]
    [Description("密码")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Password", "密码", "")]
    public String? Password { get => _Password; set { if (OnPropertyChanging("Password", value)) { _Password = value; OnPropertyChanged("Password"); } } }

    private String? _DisplayName;
    /// <summary>昵称</summary>
    [DisplayName("昵称")]
    [Description("昵称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("DisplayName", "昵称", "")]
    public String? DisplayName { get => _DisplayName; set { if (OnPropertyChanging("DisplayName", value)) { _DisplayName = value; OnPropertyChanged("DisplayName"); } } }

    private XCode.Membership.SexKinds _Sex;
    /// <summary>性别。未知、男、女</summary>
    [DisplayName("性别")]
    [Description("性别。未知、男、女")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Sex", "性别。未知、男、女", "")]
    public XCode.Membership.SexKinds Sex { get => _Sex; set { if (OnPropertyChanging("Sex", value)) { _Sex = value; OnPropertyChanged("Sex"); } } }

    private String? _Mail;
    /// <summary>邮件</summary>
    [DisplayName("邮件")]
    [Description("邮件")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Mail", "邮件", "", ItemType = "mail")]
    public String? Mail { get => _Mail; set { if (OnPropertyChanging("Mail", value)) { _Mail = value; OnPropertyChanged("Mail"); } } }

    private String? _Mobile;
    /// <summary>手机</summary>
    [DisplayName("手机")]
    [Description("手机")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Mobile", "手机", "", ItemType = "mobile")]
    public String? Mobile { get => _Mobile; set { if (OnPropertyChanging("Mobile", value)) { _Mobile = value; OnPropertyChanged("Mobile"); } } }

    private String? _Code;
    /// <summary>代码。身份证、员工编号等</summary>
    [DisplayName("代码")]
    [Description("代码。身份证、员工编号等")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Code", "代码。身份证、员工编号等", "")]
    public String? Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private Int32 _AreaId;
    /// <summary>地区。省市区</summary>
    [DisplayName("地区")]
    [Description("地区。省市区")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AreaId", "地区。省市区", "")]
    public Int32 AreaId { get => _AreaId; set { if (OnPropertyChanging("AreaId", value)) { _AreaId = value; OnPropertyChanged("AreaId"); } } }

    private String? _Avatar;
    /// <summary>头像</summary>
    [DisplayName("头像")]
    [Description("头像")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Avatar", "头像", "", ItemType = "image")]
    public String? Avatar { get => _Avatar; set { if (OnPropertyChanging("Avatar", value)) { _Avatar = value; OnPropertyChanged("Avatar"); } } }

    private Int32 _RoleID;
    /// <summary>角色。主要角色</summary>
    [Category("登录信息")]
    [DisplayName("角色")]
    [Description("角色。主要角色")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("RoleID", "角色。主要角色", "", DefaultValue = "3")]
    public Int32 RoleID { get => _RoleID; set { if (OnPropertyChanging("RoleID", value)) { _RoleID = value; OnPropertyChanged("RoleID"); } } }

    private String? _RoleIds;
    /// <summary>角色组。次要角色集合</summary>
    [Category("登录信息")]
    [DisplayName("角色组")]
    [Description("角色组。次要角色集合")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("RoleIds", "角色组。次要角色集合", "")]
    public String? RoleIds { get => _RoleIds; set { if (OnPropertyChanging("RoleIds", value)) { _RoleIds = value; OnPropertyChanged("RoleIds"); } } }

    private Int32 _DepartmentID;
    /// <summary>部门。组织机构</summary>
    [Category("登录信息")]
    [DisplayName("部门")]
    [Description("部门。组织机构")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DepartmentID", "部门。组织机构", "")]
    public Int32 DepartmentID { get => _DepartmentID; set { if (OnPropertyChanging("DepartmentID", value)) { _DepartmentID = value; OnPropertyChanged("DepartmentID"); } } }

    private Boolean _Online;
    /// <summary>在线</summary>
    [Category("登录信息")]
    [DisplayName("在线")]
    [Description("在线")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Online", "在线", "")]
    public Boolean Online { get => _Online; set { if (OnPropertyChanging("Online", value)) { _Online = value; OnPropertyChanged("Online"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [Category("登录信息")]
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Int32 _Age;
    /// <summary>年龄。周岁</summary>
    [DisplayName("年龄")]
    [Description("年龄。周岁")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Age", "年龄。周岁", "")]
    public Int32 Age { get => _Age; set { if (OnPropertyChanging("Age", value)) { _Age = value; OnPropertyChanged("Age"); } } }

    private DateTime _Birthday;
    /// <summary>生日。公历年月日</summary>
    [DisplayName("生日")]
    [Description("生日。公历年月日")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("Birthday", "生日。公历年月日", "")]
    public DateTime Birthday { get => _Birthday; set { if (OnPropertyChanging("Birthday", value)) { _Birthday = value; OnPropertyChanged("Birthday"); } } }

    private Int32 _Logins;
    /// <summary>登录次数</summary>
    [Category("登录信息")]
    [DisplayName("登录次数")]
    [Description("登录次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Logins", "登录次数", "")]
    public Int32 Logins { get => _Logins; set { if (OnPropertyChanging("Logins", value)) { _Logins = value; OnPropertyChanged("Logins"); } } }

    private DateTime _LastLogin;
    /// <summary>最后登录</summary>
    [Category("登录信息")]
    [DisplayName("最后登录")]
    [Description("最后登录")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastLogin", "最后登录", "")]
    public DateTime LastLogin { get => _LastLogin; set { if (OnPropertyChanging("LastLogin", value)) { _LastLogin = value; OnPropertyChanged("LastLogin"); } } }

    private String? _LastLoginIP;
    /// <summary>最后登录IP</summary>
    [Category("登录信息")]
    [DisplayName("最后登录IP")]
    [Description("最后登录IP")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("LastLoginIP", "最后登录IP", "")]
    public String? LastLoginIP { get => _LastLoginIP; set { if (OnPropertyChanging("LastLoginIP", value)) { _LastLoginIP = value; OnPropertyChanged("LastLoginIP"); } } }

    private DateTime _RegisterTime;
    /// <summary>注册时间</summary>
    [Category("登录信息")]
    [DisplayName("注册时间")]
    [Description("注册时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("RegisterTime", "注册时间", "")]
    public DateTime RegisterTime { get => _RegisterTime; set { if (OnPropertyChanging("RegisterTime", value)) { _RegisterTime = value; OnPropertyChanged("RegisterTime"); } } }

    private String? _RegisterIP;
    /// <summary>注册IP</summary>
    [Category("登录信息")]
    [DisplayName("注册IP")]
    [Description("注册IP")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("RegisterIP", "注册IP", "")]
    public String? RegisterIP { get => _RegisterIP; set { if (OnPropertyChanging("RegisterIP", value)) { _RegisterIP = value; OnPropertyChanged("RegisterIP"); } } }

    private Int32 _OnlineTime;
    /// <summary>在线时间。累计在线总时间，秒</summary>
    [Category("登录信息")]
    [DisplayName("在线时间")]
    [Description("在线时间。累计在线总时间，秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OnlineTime", "在线时间。累计在线总时间，秒", "", ItemType = "TimeSpan")]
    public Int32 OnlineTime { get => _OnlineTime; set { if (OnPropertyChanging("OnlineTime", value)) { _OnlineTime = value; OnPropertyChanged("OnlineTime"); } } }

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
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    [Category("扩展")]
    [DisplayName("扩展6")]
    [Description("扩展6")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Ex6", "扩展6", "")]
    public String? Ex6 { get => _Ex6; set { if (OnPropertyChanging("Ex6", value)) { _Ex6 = value; OnPropertyChanged("Ex6"); } } }

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
    public void Copy(IUser model)
    {
        ID = model.ID;
        Name = model.Name;
        Password = model.Password;
        DisplayName = model.DisplayName;
        Sex = model.Sex;
        Mail = model.Mail;
        Mobile = model.Mobile;
        Code = model.Code;
        AreaId = model.AreaId;
        Avatar = model.Avatar;
        RoleID = model.RoleID;
        RoleIds = model.RoleIds;
        DepartmentID = model.DepartmentID;
        Online = model.Online;
        Enable = model.Enable;
        Age = model.Age;
        Birthday = model.Birthday;
        Logins = model.Logins;
        LastLogin = model.LastLogin;
        LastLoginIP = model.LastLoginIP;
        RegisterTime = model.RegisterTime;
        RegisterIP = model.RegisterIP;
        OnlineTime = model.OnlineTime;
        Ex1 = model.Ex1;
        Ex2 = model.Ex2;
        Ex3 = model.Ex3;
        Ex4 = model.Ex4;
        Ex5 = model.Ex5;
        Ex6 = model.Ex6;
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
            "Name" => _Name,
            "Password" => _Password,
            "DisplayName" => _DisplayName,
            "Sex" => _Sex,
            "Mail" => _Mail,
            "Mobile" => _Mobile,
            "Code" => _Code,
            "AreaId" => _AreaId,
            "Avatar" => _Avatar,
            "RoleID" => _RoleID,
            "RoleIds" => _RoleIds,
            "DepartmentID" => _DepartmentID,
            "Online" => _Online,
            "Enable" => _Enable,
            "Age" => _Age,
            "Birthday" => _Birthday,
            "Logins" => _Logins,
            "LastLogin" => _LastLogin,
            "LastLoginIP" => _LastLoginIP,
            "RegisterTime" => _RegisterTime,
            "RegisterIP" => _RegisterIP,
            "OnlineTime" => _OnlineTime,
            "Ex1" => _Ex1,
            "Ex2" => _Ex2,
            "Ex3" => _Ex3,
            "Ex4" => _Ex4,
            "Ex5" => _Ex5,
            "Ex6" => _Ex6,
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
                case "Name": _Name = Convert.ToString(value); break;
                case "Password": _Password = Convert.ToString(value); break;
                case "DisplayName": _DisplayName = Convert.ToString(value); break;
                case "Sex": _Sex = (XCode.Membership.SexKinds)value.ToInt(); break;
                case "Mail": _Mail = Convert.ToString(value); break;
                case "Mobile": _Mobile = Convert.ToString(value); break;
                case "Code": _Code = Convert.ToString(value); break;
                case "AreaId": _AreaId = value.ToInt(); break;
                case "Avatar": _Avatar = Convert.ToString(value); break;
                case "RoleID": _RoleID = value.ToInt(); break;
                case "RoleIds": _RoleIds = Convert.ToString(value); break;
                case "DepartmentID": _DepartmentID = value.ToInt(); break;
                case "Online": _Online = value.ToBoolean(); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "Age": _Age = value.ToInt(); break;
                case "Birthday": _Birthday = value.ToDateTime(); break;
                case "Logins": _Logins = value.ToInt(); break;
                case "LastLogin": _LastLogin = value.ToDateTime(); break;
                case "LastLoginIP": _LastLoginIP = Convert.ToString(value); break;
                case "RegisterTime": _RegisterTime = value.ToDateTime(); break;
                case "RegisterIP": _RegisterIP = Convert.ToString(value); break;
                case "OnlineTime": _OnlineTime = value.ToInt(); break;
                case "Ex1": _Ex1 = value.ToInt(); break;
                case "Ex2": _Ex2 = value.ToInt(); break;
                case "Ex3": _Ex3 = value.ToDouble(); break;
                case "Ex4": _Ex4 = Convert.ToString(value); break;
                case "Ex5": _Ex5 = Convert.ToString(value); break;
                case "Ex6": _Ex6 = Convert.ToString(value); break;
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
    /// <summary>地区</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public XCode.Membership.Area? Area => Extends.Get(nameof(Area), k => XCode.Membership.Area.FindByID(AreaId));

    /// <summary>地区</summary>
    [Map(nameof(AreaId), typeof(XCode.Membership.Area), "ID")]
    public String? AreaPath => Area?.Path;

    /// <summary>角色</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Role? Role => Extends.Get(nameof(Role), k => Role.FindByID(RoleID));

    /// <summary>角色</summary>
    [Map(nameof(RoleID), typeof(Role), "ID")]
    [Category("登录信息")]
    public String? RoleName => Role?.Name;

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static User? FindByID(Int32 id)
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
    /// <returns>实体对象</returns>
    public static User? FindByName(String name)
    {
        if (name.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));

        // 单对象缓存
        return Meta.SingleCache.GetItemWithSlaveKey(name) as User;

        //return Find(_.Name == name);
    }

    /// <summary>根据邮件查找</summary>
    /// <param name="mail">邮件</param>
    /// <returns>实体列表</returns>
    public static IList<User> FindAllByMail(String? mail)
    {
        if (mail == null) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Mail.EqualIgnoreCase(mail));

        return FindAll(_.Mail == mail);
    }

    /// <summary>根据手机查找</summary>
    /// <param name="mobile">手机</param>
    /// <returns>实体列表</returns>
    public static IList<User> FindAllByMobile(String? mobile)
    {
        if (mobile == null) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Mobile.EqualIgnoreCase(mobile));

        return FindAll(_.Mobile == mobile);
    }

    /// <summary>根据代码查找</summary>
    /// <param name="code">代码</param>
    /// <returns>实体列表</returns>
    public static IList<User> FindAllByCode(String? code)
    {
        if (code == null) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Code.EqualIgnoreCase(code));

        return FindAll(_.Code == code);
    }

    /// <summary>根据角色查找</summary>
    /// <param name="roleId">角色</param>
    /// <returns>实体列表</returns>
    public static IList<User> FindAllByRoleID(Int32 roleId)
    {
        if (roleId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.RoleID == roleId);

        return FindAll(_.RoleID == roleId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="mail">邮件</param>
    /// <param name="mobile">手机</param>
    /// <param name="code">代码。身份证、员工编号等</param>
    /// <param name="roleId">角色。主要角色</param>
    /// <param name="sex">性别。未知、男、女</param>
    /// <param name="areaId">地区。省市区</param>
    /// <param name="online">在线</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<User> Search(String? mail, String? mobile, String? code, Int32 roleId, XCode.Membership.SexKinds sex, Int32 areaId, Boolean? online, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!mail.IsNullOrEmpty()) exp &= _.Mail == mail;
        if (!mobile.IsNullOrEmpty()) exp &= _.Mobile == mobile;
        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (roleId >= 0) exp &= _.RoleID == roleId;
        if (sex >= 0) exp &= _.Sex == sex;
        if (areaId >= 0) exp &= _.AreaId == areaId;
        if (online != null) exp &= _.Online == online;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得用户字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field ID = FindByName("ID");

        /// <summary>名称。登录用户名</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>密码</summary>
        public static readonly Field Password = FindByName("Password");

        /// <summary>昵称</summary>
        public static readonly Field DisplayName = FindByName("DisplayName");

        /// <summary>性别。未知、男、女</summary>
        public static readonly Field Sex = FindByName("Sex");

        /// <summary>邮件</summary>
        public static readonly Field Mail = FindByName("Mail");

        /// <summary>手机</summary>
        public static readonly Field Mobile = FindByName("Mobile");

        /// <summary>代码。身份证、员工编号等</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>地区。省市区</summary>
        public static readonly Field AreaId = FindByName("AreaId");

        /// <summary>头像</summary>
        public static readonly Field Avatar = FindByName("Avatar");

        /// <summary>角色。主要角色</summary>
        public static readonly Field RoleID = FindByName("RoleID");

        /// <summary>角色组。次要角色集合</summary>
        public static readonly Field RoleIds = FindByName("RoleIds");

        /// <summary>部门。组织机构</summary>
        public static readonly Field DepartmentID = FindByName("DepartmentID");

        /// <summary>在线</summary>
        public static readonly Field Online = FindByName("Online");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>年龄。周岁</summary>
        public static readonly Field Age = FindByName("Age");

        /// <summary>生日。公历年月日</summary>
        public static readonly Field Birthday = FindByName("Birthday");

        /// <summary>登录次数</summary>
        public static readonly Field Logins = FindByName("Logins");

        /// <summary>最后登录</summary>
        public static readonly Field LastLogin = FindByName("LastLogin");

        /// <summary>最后登录IP</summary>
        public static readonly Field LastLoginIP = FindByName("LastLoginIP");

        /// <summary>注册时间</summary>
        public static readonly Field RegisterTime = FindByName("RegisterTime");

        /// <summary>注册IP</summary>
        public static readonly Field RegisterIP = FindByName("RegisterIP");

        /// <summary>在线时间。累计在线总时间，秒</summary>
        public static readonly Field OnlineTime = FindByName("OnlineTime");

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

    /// <summary>取得用户字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String ID = "ID";

        /// <summary>名称。登录用户名</summary>
        public const String Name = "Name";

        /// <summary>密码</summary>
        public const String Password = "Password";

        /// <summary>昵称</summary>
        public const String DisplayName = "DisplayName";

        /// <summary>性别。未知、男、女</summary>
        public const String Sex = "Sex";

        /// <summary>邮件</summary>
        public const String Mail = "Mail";

        /// <summary>手机</summary>
        public const String Mobile = "Mobile";

        /// <summary>代码。身份证、员工编号等</summary>
        public const String Code = "Code";

        /// <summary>地区。省市区</summary>
        public const String AreaId = "AreaId";

        /// <summary>头像</summary>
        public const String Avatar = "Avatar";

        /// <summary>角色。主要角色</summary>
        public const String RoleID = "RoleID";

        /// <summary>角色组。次要角色集合</summary>
        public const String RoleIds = "RoleIds";

        /// <summary>部门。组织机构</summary>
        public const String DepartmentID = "DepartmentID";

        /// <summary>在线</summary>
        public const String Online = "Online";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>年龄。周岁</summary>
        public const String Age = "Age";

        /// <summary>生日。公历年月日</summary>
        public const String Birthday = "Birthday";

        /// <summary>登录次数</summary>
        public const String Logins = "Logins";

        /// <summary>最后登录</summary>
        public const String LastLogin = "LastLogin";

        /// <summary>最后登录IP</summary>
        public const String LastLoginIP = "LastLoginIP";

        /// <summary>注册时间</summary>
        public const String RegisterTime = "RegisterTime";

        /// <summary>注册IP</summary>
        public const String RegisterIP = "RegisterIP";

        /// <summary>在线时间。累计在线总时间，秒</summary>
        public const String OnlineTime = "OnlineTime";

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
