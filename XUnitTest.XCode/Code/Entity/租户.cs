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

/// <summary>租户。多租户SAAS平台，用于隔离业务数据</summary>
[Serializable]
[DataObject]
[Description("租户。多租户SAAS平台，用于隔离业务数据")]
[BindIndex("IU_Tenant_Code", true, "Code")]
[BindTable("Tenant", Description = "租户。多租户SAAS平台，用于隔离业务数据", ConnName = "Membership666", DbType = DatabaseType.None)]
public partial class Tenant : ITenant, IEntity<ITenant>
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String? _Code;
    /// <summary>编码。唯一编码</summary>
    [DisplayName("编码")]
    [Description("编码。唯一编码")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Code", "编码。唯一编码", "")]
    public String? Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private String? _Name;
    /// <summary>名称。显示名称</summary>
    [DisplayName("名称")]
    [Description("名称。显示名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称。显示名称", "", Master = true)]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Int32 _ManagerId;
    /// <summary>管理者</summary>
    [DisplayName("管理者")]
    [Description("管理者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ManagerId", "管理者", "")]
    public Int32 ManagerId { get => _ManagerId; set { if (OnPropertyChanging("ManagerId", value)) { _ManagerId = value; OnPropertyChanged("ManagerId"); } } }

    private String? _RoleIds;
    /// <summary>角色组。租户可选的角色集合，不同级别的租户所拥有的角色不一样，高级功能也会不同</summary>
    [DisplayName("角色组")]
    [Description("角色组。租户可选的角色集合，不同级别的租户所拥有的角色不一样，高级功能也会不同")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("RoleIds", "角色组。租户可选的角色集合，不同级别的租户所拥有的角色不一样，高级功能也会不同", "")]
    public String? RoleIds { get => _RoleIds; set { if (OnPropertyChanging("RoleIds", value)) { _RoleIds = value; OnPropertyChanged("RoleIds"); } } }

    private String? _Logo;
    /// <summary>图标。附件路径</summary>
    [DisplayName("图标")]
    [Description("图标。附件路径")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Logo", "图标。附件路径", "", ItemType = "image")]
    public String? Logo { get => _Logo; set { if (OnPropertyChanging("Logo", value)) { _Logo = value; OnPropertyChanged("Logo"); } } }

    private String? _DatabaseName;
    /// <summary>数据库。分库用的数据库名</summary>
    [DisplayName("数据库")]
    [Description("数据库。分库用的数据库名")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("DatabaseName", "数据库。分库用的数据库名", "")]
    public String? DatabaseName { get => _DatabaseName; set { if (OnPropertyChanging("DatabaseName", value)) { _DatabaseName = value; OnPropertyChanged("DatabaseName"); } } }

    private String? _TableName;
    /// <summary>数据表。分表用的数据表前缀</summary>
    [DisplayName("数据表")]
    [Description("数据表。分表用的数据表前缀")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TableName", "数据表。分表用的数据表前缀", "")]
    public String? TableName { get => _TableName; set { if (OnPropertyChanging("TableName", value)) { _TableName = value; OnPropertyChanged("TableName"); } } }

    private DateTime _Expired;
    /// <summary>过期时间。达到该时间后，自动禁用租户，空表示永不过期</summary>
    [DisplayName("过期时间")]
    [Description("过期时间。达到该时间后，自动禁用租户，空表示永不过期")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("Expired", "过期时间。达到该时间后，自动禁用租户，空表示永不过期", "")]
    public DateTime Expired { get => _Expired; set { if (OnPropertyChanging("Expired", value)) { _Expired = value; OnPropertyChanged("Expired"); } } }

    private Int32 _CreateUserId;
    /// <summary>创建者</summary>
    [Category("扩展")]
    [DisplayName("创建者")]
    [Description("创建者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateUserId", "创建者", "")]
    public Int32 CreateUserId { get => _CreateUserId; set { if (OnPropertyChanging("CreateUserId", value)) { _CreateUserId = value; OnPropertyChanged("CreateUserId"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [Category("扩展")]
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private String? _CreateIP;
    /// <summary>创建地址</summary>
    [Category("扩展")]
    [DisplayName("创建地址")]
    [Description("创建地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateIP", "创建地址", "")]
    public String? CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }

    private Int32 _UpdateUserId;
    /// <summary>更新者</summary>
    [Category("扩展")]
    [DisplayName("更新者")]
    [Description("更新者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdateUserId", "更新者", "")]
    public Int32 UpdateUserId { get => _UpdateUserId; set { if (OnPropertyChanging("UpdateUserId", value)) { _UpdateUserId = value; OnPropertyChanged("UpdateUserId"); } } }

    private DateTime _UpdateTime;
    /// <summary>更新时间</summary>
    [Category("扩展")]
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdateTime", "更新时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }

    private String? _UpdateIP;
    /// <summary>更新地址</summary>
    [Category("扩展")]
    [DisplayName("更新地址")]
    [Description("更新地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateIP", "更新地址", "")]
    public String? UpdateIP { get => _UpdateIP; set { if (OnPropertyChanging("UpdateIP", value)) { _UpdateIP = value; OnPropertyChanged("UpdateIP"); } } }

    private String? _Remark;
    /// <summary>描述</summary>
    [Category("扩展")]
    [DisplayName("描述")]
    [Description("描述")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Remark", "描述", "")]
    public String? Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(ITenant model)
    {
        Id = model.Id;
        Code = model.Code;
        Name = model.Name;
        Enable = model.Enable;
        ManagerId = model.ManagerId;
        RoleIds = model.RoleIds;
        Logo = model.Logo;
        DatabaseName = model.DatabaseName;
        TableName = model.TableName;
        Expired = model.Expired;
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
            "Id" => _Id,
            "Code" => _Code,
            "Name" => _Name,
            "Enable" => _Enable,
            "ManagerId" => _ManagerId,
            "RoleIds" => _RoleIds,
            "Logo" => _Logo,
            "DatabaseName" => _DatabaseName,
            "TableName" => _TableName,
            "Expired" => _Expired,
            "CreateUserId" => _CreateUserId,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
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
                case "Id": _Id = value.ToInt(); break;
                case "Code": _Code = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "ManagerId": _ManagerId = value.ToInt(); break;
                case "RoleIds": _RoleIds = Convert.ToString(value); break;
                case "Logo": _Logo = Convert.ToString(value); break;
                case "DatabaseName": _DatabaseName = Convert.ToString(value); break;
                case "TableName": _TableName = Convert.ToString(value); break;
                case "Expired": _Expired = value.ToDateTime(); break;
                case "CreateUserId": _CreateUserId = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
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
    public static Tenant? FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据编码查找</summary>
    /// <param name="code">编码</param>
    /// <returns>实体对象</returns>
    public static Tenant? FindByCode(String? code)
    {
        if (code == null) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Code.EqualIgnoreCase(code));

        return Find(_.Code == code);
    }
    #endregion

    #region 字段名
    /// <summary>取得租户字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>编码。唯一编码</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>名称。显示名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>管理者</summary>
        public static readonly Field ManagerId = FindByName("ManagerId");

        /// <summary>角色组。租户可选的角色集合，不同级别的租户所拥有的角色不一样，高级功能也会不同</summary>
        public static readonly Field RoleIds = FindByName("RoleIds");

        /// <summary>图标。附件路径</summary>
        public static readonly Field Logo = FindByName("Logo");

        /// <summary>数据库。分库用的数据库名</summary>
        public static readonly Field DatabaseName = FindByName("DatabaseName");

        /// <summary>数据表。分表用的数据表前缀</summary>
        public static readonly Field TableName = FindByName("TableName");

        /// <summary>过期时间。达到该时间后，自动禁用租户，空表示永不过期</summary>
        public static readonly Field Expired = FindByName("Expired");

        /// <summary>创建者</summary>
        public static readonly Field CreateUserId = FindByName("CreateUserId");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>更新者</summary>
        public static readonly Field UpdateUserId = FindByName("UpdateUserId");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>更新地址</summary>
        public static readonly Field UpdateIP = FindByName("UpdateIP");

        /// <summary>描述</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得租户字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>编码。唯一编码</summary>
        public const String Code = "Code";

        /// <summary>名称。显示名称</summary>
        public const String Name = "Name";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>管理者</summary>
        public const String ManagerId = "ManagerId";

        /// <summary>角色组。租户可选的角色集合，不同级别的租户所拥有的角色不一样，高级功能也会不同</summary>
        public const String RoleIds = "RoleIds";

        /// <summary>图标。附件路径</summary>
        public const String Logo = "Logo";

        /// <summary>数据库。分库用的数据库名</summary>
        public const String DatabaseName = "DatabaseName";

        /// <summary>数据表。分表用的数据表前缀</summary>
        public const String TableName = "TableName";

        /// <summary>过期时间。达到该时间后，自动禁用租户，空表示永不过期</summary>
        public const String Expired = "Expired";

        /// <summary>创建者</summary>
        public const String CreateUserId = "CreateUserId";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>更新者</summary>
        public const String UpdateUserId = "UpdateUserId";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>更新地址</summary>
        public const String UpdateIP = "UpdateIP";

        /// <summary>描述</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
