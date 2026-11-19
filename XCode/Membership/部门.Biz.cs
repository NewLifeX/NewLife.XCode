using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife.Common;
using NewLife.Data;
using NewLife.Log;

namespace XCode.Membership;

/// <summary>部门。组织机构，多级树状结构</summary>
public partial class Department : Entity<Department>, ITenantScope, IDepartmentScope, IDataScopeFieldProvider
{

    #region 兼容别名

    /// <summary>编号</summary>
    /// <remarks>兼容旧版本字段名，新代码请使用 Id</remarks>
    [Obsolete("请使用 Id")]
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
#if NET5_0_OR_GREATER
    [System.Text.Json.Serialization.JsonIgnore]
#endif
    public Int32 ID { get => Id; set => Id = value; }

    /// <summary>父级</summary>
    /// <remarks>兼容旧版本字段名，新代码请使用 ParentId</remarks>
    [Obsolete("请使用 ParentId")]
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
#if NET5_0_OR_GREATER
    [System.Text.Json.Serialization.JsonIgnore]
#endif
    public Int32 ParentID { get => ParentId; set => ParentId = value; }

    /// <summary>创建者</summary>
    /// <remarks>兼容旧版本字段名，新代码请使用 CreateUserId</remarks>
    [Obsolete("请使用 CreateUserId")]
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
#if NET5_0_OR_GREATER
    [System.Text.Json.Serialization.JsonIgnore]
#endif
    public Int32 CreateUserID { get => CreateUserId; set => CreateUserId = value; }

    /// <summary>更新者</summary>
    /// <remarks>兼容旧版本字段名，新代码请使用 UpdateUserId</remarks>
    [Obsolete("请使用 UpdateUserId")]
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
#if NET5_0_OR_GREATER
    [System.Text.Json.Serialization.JsonIgnore]
#endif
    public Int32 UpdateUserID { get => UpdateUserId; set => UpdateUserId = value; }

    /// <summary>根据编号查找部门</summary>
    [Obsolete("请使用 FindById")]
    public static Department? FindByID(Int32 id) => FindById(id);

    /// <summary>根据父级和名称查找部门</summary>
    [Obsolete("请使用 FindAllByParentIdAndName")]
    public static IList<Department> FindAllByParentIDAndName(Int32 parentId, String name) => FindAllByParentIdAndName(parentId, name);

    /// <summary>根据租户、父级和名称查找部门</summary>
    [Obsolete("请使用 FindAllByTenantIdAndParentIdAndName")]
    public static IList<Department> FindAllByTenantIdAndParentIDAndName(Int32 tenantId, Int32 parentId, String name) => FindAllByTenantIdAndParentIdAndName(tenantId, parentId, name);
    #endregion
    #region 对象操作
    private static Int32 MaxCacheCount = 10000;

    static Department()
    {
        //// 用于引发基类的静态构造函数，所有层次的泛型实体类都应该有一个
        //var entity = new Department();

        // 累加字段
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(__.ParentId);

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Interceptors.Add(new UserInterceptor { AllowEmpty = false });
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add<IPInterceptor>();
        Meta.Interceptors.Add<TenantInterceptor>();
        Meta.Interceptors.Add<DataScopeInterceptor>();
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        if (method == DataMethod.Delete) return true;

        // 清理富文本编辑器遗留的空内容（如 <p><br></p>），避免脏值写入数据库并经 SSO 同步扩散；随本次更新写库清除
        if (EmptyHtmlHelper.IsEmptyHtml(Remark)) Remark = null;

        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
        if (Name.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Name), L.T("The name cannot be empty!"));

        if (Code.IsNullOrEmpty()) Code = PinYin.GetFirst(Name);

        if (Type == 0 && !IsDirty("Type") && !Name.IsNullOrEmpty())
        {
            if (Name.Contains("公司"))
                Type = DepartmentTypes.公司;
            else if (Name.Contains('部'))
                Type = DepartmentTypes.部门;
            else if (Name.Contains('组'))
                Type = DepartmentTypes.小组;
            else if (Name.Contains("项目"))
                Type = DepartmentTypes.虚拟;
            else
                Type = DepartmentTypes.部门;
        }

        // 管理者
        if (method == DataMethod.Insert && ManagerId == 0) ManagerId = ManageProvider.Provider?.Current?.ID ?? 0;

        if (!base.Valid(method)) return false;

        return true;
    }

    /// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal protected override void InitData()
    {
        if (Meta.Count > 0) return;

        if (XTrace.Debug) XTrace.WriteLine("Initializing {0} data...", typeof(Department).Name);

        var root = Add("总公司", "001", 0);
        Add("行政部", "011", root.Id);
        Add("技术部", "012", root.Id);
        Add("生产部", "013", root.Id);

        root = Add("上海分公司", "101", 0);
        Add("行政部", "111", root.Id);
        Add("市场部", "112", root.Id);

        if (XTrace.Debug) XTrace.WriteLine("Finished initializing {0} data!", typeof(Department).Name);
    }

    /// <summary>添加用户，如果存在则直接返回</summary>
    /// <param name="name"></param>
    /// <param name="code"></param>
    /// <param name="parentid"></param>
    /// <returns></returns>
    public static Department Add(String name, String code, Int32 parentid)
    {
        var entity = new Department
        {
            Name = name,
            Code = code,
            ParentId = parentid,
            Enable = true,
            Visible = true,
        };

        entity.Save();

        return entity;
    }
    #endregion

    #region 扩展属性
    /// <summary>父级</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Department? Parent => Extends.Get(nameof(Department), k => FindById(ParentId));

    /// <summary>父级</summary>
    [Map(__.ParentId, typeof(Department), __.Id)]
    public String? ParentName => Parent?.ToString();

    /// <summary>父级路径</summary>
    public String? ParentPath
    {
        get
        {
            var list = new List<Department>();
            var ids = new List<Int32>();
            var p = Parent;
            while (p != null && !ids.Contains(p.Id))
            {
                list.Add(p);
                ids.Add(p.Id);

                p = p.Parent;
            }
            if (list != null && list.Count > 0) return list.Where(r => r.Visible).Join("/", r => r.Name);

            return Parent?.Name;
        }
    }

    /// <summary>路径</summary>
    public String Path
    {
        get
        {
            var p = ParentPath;
            if (p.IsNullOrEmpty()) return Name;
            if (!Visible) return p;

            return p + "/" + Name;
        }
    }

    /// <summary>下级部门</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public IEnumerable<Department>? Childs => Extends.Get(nameof(Childs), k => FindAllByTenantIdAndParentId(TenantId, Id).OrderBy(e => e.Id));
    #endregion

    #region 扩展查询
    /// <summary>根据代码查找</summary>
    /// <param name="code">代码</param>
    /// <returns>实体对象</returns>
    public static Department? FindByCode(String code)
    {
        if (code.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Code == code);

        return Find(_.Code == code);
    }

    /// <summary>根据名称、父级查找</summary>
    /// <param name="name">名称</param>
    /// <param name="parentid">父级</param>
    /// <returns>实体对象</returns>
    [Obsolete("请使用 FindByNameAndParentId")]
    public static Department? FindByNameAndParentID(String name, Int32 parentid) => FindByNameAndParentId(name, parentid);

    /// <summary>根据名称和父级查找</summary>
    /// <param name="name">名称</param>
    /// <param name="parentid">父级</param>
    /// <returns>实体对象</returns>
    public static Department? FindByNameAndParentId(String name, Int32 parentid)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Name == name && e.ParentId == parentid);

        return Find(_.Name == name & _.ParentId == parentid);
    }

    /// <summary>根据租户、父级、名称查找</summary>
    /// <param name="tenantId">租户</param>
    /// <param name="parentId">父级</param>
    /// <param name="name">名称</param>
    /// <returns>实体对象</returns>
    public static Department? FindByTenantIdAndParentIdAndName(Int32 tenantId, Int32 parentId, String name)
    {
        if (tenantId < 0) return null;
        if (parentId < 0) return null;
        if (name.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.TenantId == tenantId && e.ParentId == parentId && e.Name.EqualIgnoreCase(name));

        return Find(_.TenantId == tenantId & _.ParentId == parentId & _.Name == name);
    }

    /// <summary>根据租户、父级查找</summary>
    /// <param name="tenantId">租户</param>
    /// <param name="parentId">父级</param>
    /// <returns>实体列表</returns>
    public static IList<Department> FindAllByTenantIdAndParentId(Int32 tenantId, Int32 parentId)
    {
        if (tenantId < 0) return [];
        if (parentId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.TenantId == tenantId && e.ParentId == parentId);

        return FindAll(_.TenantId == tenantId & _.ParentId == parentId);
    }

    /// <summary>根据编号集合查找</summary>
    /// <param name="ids">编号集合</param>
    /// <returns>实体列表</returns>
    [Obsolete("请使用 FindAllByIds")]
    public static IList<Department> FindAllByIDs(Int32[] ids) => FindAllByIds(ids);

    /// <summary>根据编号列表查找部门</summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    public static IList<Department> FindAllByIds(Int32[] ids)
    {
        if (ids == null || ids.Length == 0) return [];

        return FindAll(_.Id.In(ids));
    }

    /// <summary>查找启用且可见的部门</summary>
    /// <returns>实体列表</returns>
    public static IList<Department> FindAllEnabled() => FindAll(_.Enable == true & _.Visible == true);
    #endregion

    #region 高级查询
    /// <summary>高级搜索</summary>
    /// <param name="parentId"></param>
    /// <param name="enable"></param>
    /// <param name="visible"></param>
    /// <param name="key"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    public static IList<Department> Search(Int32 parentId, Boolean? enable, Boolean? visible, String key, PageParameter page)
    {
        var exp = new WhereExpression();
        if (parentId >= 0) exp &= _.ParentId == parentId;
        if (enable != null) exp &= _.Enable == enable.Value;
        if (visible != null) exp &= _.Visible == visible.Value;
        if (!key.IsNullOrEmpty()) exp &= _.Code.Contains(key) | _.Name.Contains(key) | _.FullName.Contains(key);

        return FindAll(exp, page);
    }

    /// <summary>高级搜索</summary>
    /// <param name="tenantId"></param>
    /// <param name="type"></param>
    /// <param name="parentId"></param>
    /// <param name="managerId"></param>
    /// <param name="enable"></param>
    /// <param name="visible"></param>
    /// <param name="key"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    public static IList<Department> Search(Int32 tenantId, DepartmentTypes type, Int32 parentId, Int32 managerId, Boolean? enable, Boolean? visible, String key, PageParameter page)
    {
        var exp = new WhereExpression();
        if (tenantId >= 0) exp &= _.TenantId == tenantId;
        if (type >= 0) exp &= _.Type == type;
        if (parentId >= 0) exp &= _.ParentId == parentId;
        if (managerId >= 0) exp &= _.ManagerId == managerId;
        if (enable != null) exp &= _.Enable == enable.Value;
        if (visible != null) exp &= _.Visible == visible.Value;
        if (!key.IsNullOrEmpty()) exp &= _.Code.Contains(key) | _.Name.Contains(key) | _.FullName.Contains(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 业务操作
    #endregion

    #region IDepartmentScope 成员
    Int32 IDepartmentScope.DepartmentId { get => Id; set => Id = value; }

    #endregion

    #region IDataScopeFieldProvider 成员
    XCode.Configuration.FieldItem? IDataScopeFieldProvider.GetUserField() => null;
    XCode.Configuration.FieldItem? IDataScopeFieldProvider.GetDepartmentField() => _.Id;
    XCode.Configuration.FieldItem? IDataScopeFieldProvider.GetTenantField() => _.TenantId;
    #endregion
}