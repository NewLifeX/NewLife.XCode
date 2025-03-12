﻿using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Common;
using NewLife.Data;
using NewLife.Log;

namespace XCode.Membership;

/// <summary>部门。组织机构，多级树状结构</summary>
public partial class Department : Entity<Department>, ITenantSource
{
    #region 对象操作
    static Department()
    {
        //// 用于引发基类的静态构造函数，所有层次的泛型实体类都应该有一个
        //var entity = new Department();

        // 累加字段
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(__.ParentID);

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add(new UserModule { AllowEmpty = false });
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();
        Meta.Modules.Add<TenantModule>();
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        if (method == DataMethod.Delete) return true;

        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
        if (Name.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Name), "名称不能为空！");

        if (Code.IsNullOrEmpty()) Code = PinYin.GetFirst(Name);

        // 管理者
        if (method == DataMethod.Insert && ManagerId == 0) ManagerId = ManageProvider.Provider?.Current?.ID ?? 0;

        if (!base.Valid(method)) return false;

        return base.Valid(method);
    }

    /// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal protected override void InitData()
    {
        if (Meta.Count > 0) return;

        if (XTrace.Debug) XTrace.WriteLine("开始初始化{0}数据……", typeof(Department).Name);

        var root = Add("总公司", "001", 0);
        Add("行政部", "011", root.ID);
        Add("技术部", "012", root.ID);
        Add("生产部", "013", root.ID);

        root = Add("上海分公司", "101", 0);
        Add("行政部", "111", root.ID);
        Add("市场部", "112", root.ID);

        if (XTrace.Debug) XTrace.WriteLine("完成初始化{0}数据！", typeof(Department).Name);
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
            ParentID = parentid,
            Enable = true,
            Visible = true,
        };

        entity.Save();

        return entity;
    }
    #endregion

    #region 扩展属性
    ///// <summary>租户</summary>
    //[XmlIgnore, IgnoreDataMember, ScriptIgnore]
    //public Tenant Tenant => Extends.Get(nameof(Tenant), k => Tenant.FindById(TenantId));

    ///// <summary>租户</summary>
    //[Map(nameof(TenantId), typeof(Tenant), "Id")]
    //public String TenantName => Tenant?.Name;

    ///// <summary>管理者</summary>
    //[XmlIgnore, IgnoreDataMember, ScriptIgnore]
    //public User Manager => Extends.Get(nameof(Manager), k => User.FindByID(ManagerId));

    ///// <summary>管理者</summary>
    //[Map(__.ManagerId, typeof(User), __.ID)]
    //public String ManagerName => Manager?.ToString();

    /// <summary>父级</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Department? Parent => Extends.Get(nameof(Department), k => FindByID(ParentID));

    /// <summary>父级</summary>
    [Map(__.ParentID, typeof(Department), __.ID)]
    public String? ParentName => Parent?.ToString();

    /// <summary>父级路径</summary>
    public String ParentPath
    {
        get
        {
            var list = new List<Department>();
            var ids = new List<Int32>();
            var p = Parent;
            while (p != null && !ids.Contains(p.ID))
            {
                list.Add(p);
                ids.Add(p.ID);

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

    /// <summary>
    /// 获取子集合
    /// </summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public IEnumerable<Department>? ChildList => Extends.Get(nameof(ChildList), k => FindAllByParentId(ID).OrderBy(e => e.ID));

    /// <summary>
    ///是否存在子集
    /// </summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Boolean subset { get; set; }
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static Department FindByID(Int32 id)
    {
        if (id <= 0) return null;

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
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Name == name);

        return FindAll(_.Name == name);
    }

    /// <summary>根据名称、父级查找</summary>
    /// <param name="name">名称</param>
    /// <param name="parentid">父级</param>
    /// <returns>实体对象</returns>
    public static Department FindByNameAndParentID(String name, Int32 parentid)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Name == name && e.ParentID == parentid);

        return Find(_.Name == name & _.ParentID == parentid);
    }

    /// <summary>根据代码查找</summary>
    /// <param name="code">代码</param>
    /// <returns>实体对象</returns>
    public static Department FindByCode(String code)
    {
        if (code.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Code == code);

        return Find(_.Code == code);
    }

    /// <summary>根据父级、名称查找</summary>
    /// <param name="parentId">父级</param>
    /// <param name="name">名称</param>
    /// <returns>实体列表</returns>
    public static IList<Department> FindAllByParentIDAndName(Int32 parentId, String name)
    {

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ParentID == parentId && e.Name.EqualIgnoreCase(name));

        return FindAll(_.ParentID == parentId & _.Name == name);
    }

    /// <summary>根据租户查找</summary>
    /// <param name="tenantId">租户</param>
    /// <returns>实体列表</returns>
    public static IList<Department> FindAllByTenantId(Int32 tenantId)
    {
        if (tenantId <= 0) return new List<Department>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.TenantId == tenantId);

        return FindAll(_.TenantId == tenantId);
    }

    /// <summary>根据所属父级Id查找</summary>
    /// <param name="parentID">所属父级Id</param>
    /// <returns>实体列表</returns>
    public static IList<Department> FindAllByParentId(Int32 parentID)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ParentID == parentID);

        return FindAll(_.ParentID == parentID);
    }
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
        if (parentId >= 0) exp &= _.ParentID == parentId;
        if (enable != null) exp &= _.Enable == enable.Value;
        if (visible != null) exp &= _.Visible == visible.Value;
        if (!key.IsNullOrEmpty()) exp &= _.Code.Contains(key) | _.Name.Contains(key) | _.FullName.Contains(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 业务操作
    #endregion
}