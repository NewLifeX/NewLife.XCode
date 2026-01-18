using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife.Data;

namespace XCode.Membership;

[ModelCheckMode(ModelCheckModes.CheckTableWhenFirstUse)]
public partial class TenantUser : Entity<TenantUser>, ITenantScope
{
    #region 对象操作

    static TenantUser()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(TenantId));

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Interceptors.Add<UserInterceptor>();
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add<IPInterceptor>();
        Meta.Interceptors.Add<TenantInterceptor>();
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        if (method == DataMethod.Delete) return true;

        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        // 重新整理角色
        var ids = GetRoleIDs();
        if (ids.Length > 0)
        {
            RoleId = ids[0];
            var str = ids.Skip(1).Join();
            if (!str.IsNullOrEmpty()) str = "," + str + ",";
            RoleIds = str;
        }

        return base.Valid(method);
    }
    #endregion 对象操作

    #region 扩展属性

    ///// <summary>租户</summary>
    //[XmlIgnore, IgnoreDataMember, ScriptIgnore]
    ////[ScriptIgnore]
    //public Tenant Tenant => Extends.Get(nameof(Tenant), k => Tenant.FindById(TenantId));

    ///// <summary>租户</summary>
    //[Map(nameof(TenantId), typeof(Tenant), "Id")]
    //public String TenantName => Tenant?.Name;

    ///// <summary>用户</summary>
    //[XmlIgnore, IgnoreDataMember, ScriptIgnore]
    ////[ScriptIgnore]
    //public User User => Extends.Get(nameof(User), k => User.FindByID(UserId));

    ///// <summary>用户</summary>
    //[Map(nameof(UserId), typeof(User), "ID")]
    //public String UserName => User?.Name;

    /// <summary>角色</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    IRole? ITenantUser.Role => Role;

    ///// <summary>角色</summary>
    //[Map(nameof(RoleId), typeof(Role), "ID")]
    //public String RoleName => Role?.Name;

    /// <summary>角色集合</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public virtual IRole[] Roles => Extends.Get(nameof(Roles), k => GetRoleIDs().Select(e => ManageProvider.Get<IRole>()?.FindByID(e)).Where(e => e != null).Cast<IRole>().ToArray()) ?? [];

    /// <summary>获取角色列表。主角色在前，其它角色升序在后</summary>
    /// <returns></returns>
    public virtual Int32[] GetRoleIDs()
    {
        var ids = RoleIds.SplitAsInt().OrderBy(e => e).ToList();
        if (RoleId > 0) ids.Insert(0, RoleId);

        return ids.Distinct().ToArray();
    }

    /// <summary>角色组名</summary>
    [Map(__.RoleIds)]
    public virtual String? RoleNames => Extends.Get(nameof(RoleNames), k => RoleIds.SplitAsInt().Select(e => ManageProvider.Get<IRole>()?.FindByID(e)).Where(e => e != null).Cast<IRole>().Select(e => e.Name).Join());

    //public virtual String RoleName => Extends.Get(nameof(RoleName), k => ManageProvider.Get<IRole>()?.FindByID(k).Name);

    #endregion 扩展属性

    #region 扩展查询

    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static TenantUser? FindById(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据租户、用户查找</summary>
    /// <param name="tenantId">租户</param>
    /// <param name="userId">用户</param>
    /// <returns>实体对象</returns>
    public static TenantUser? FindByTenantIdAndUserId(Int32 tenantId, Int32 userId)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.TenantId == tenantId && e.UserId == userId);

        return Find(_.TenantId == tenantId & _.UserId == userId);
    }

    /// <summary>根据用户查找</summary>
    /// <param name="userId">用户</param>
    /// <param name="isAll">是否包含停用(默认不包含停用)</param>
    /// <returns>实体列表</returns>
    public static IList<TenantUser> FindAllByUserId(Int32 userId, Boolean isAll = false)
    {
        if (userId <= 0) return new List<TenantUser>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return isAll ? Meta.Cache.FindAll(e => e.UserId == userId) : Meta.Cache.FindAll(e => e.UserId == userId && e.Enable);

        var exp = new WhereExpression();
        exp &= _.UserId == userId;
        if (!isAll) exp &= _.Enable == true;

        return FindAll(exp);
    }

    /// <summary>根据用户查找</summary>
    /// <param name="userId">用户</param>
    /// <returns>实体列表</returns>
    public static IList<TenantUser> FindAllByUserId(Int32 userId)
    {
        if (userId <= 0) return new List<TenantUser>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.UserId == userId);

        return FindAll(_.UserId == userId);
    }

    /// <summary>根据租户查询用户id</summary>
    /// <param name="tenantId"></param>
    /// <returns></returns>
    public static IList<TenantUser> FindAllByTenantId(Int32 tenantId)
    {
        if (tenantId <= 0) return new List<TenantUser>();

        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.TenantId == tenantId);

        return FindAll(_.TenantId == tenantId);
    }

    #endregion 扩展查询

    #region 高级查询

    /// <summary>高级查询</summary>
    /// <param name="tenantId">租户</param>
    /// <param name="userId">用户</param>
    /// <param name="roleId">角色</param>
    /// <param name="enable">是否启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<TenantUser> Search(Int32 tenantId, Int32 userId, Int32 roleId, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (tenantId > 0) exp &= _.TenantId == tenantId;
        if (userId > 0) exp &= _.UserId == userId;
        if (roleId > 0) exp &= _.RoleId == roleId;
        if (enable != null) exp &= _.Enable == enable;

        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.RoleIds.Contains(key) | _.CreateIP.Contains(key) | _.UpdateIP.Contains(key) | _.Remark.Contains(key);

        return FindAll(exp, page);
    }

    // Select Count(Id) as Id,Category From TenantUser Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    //static readonly FieldCache<TenantUser> _CategoryCache = new FieldCache<TenantUser>(nameof(Category))
    //{
    //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    //};

    ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    ///// <returns></returns>
    //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();

    #endregion 高级查询

    #region 业务操作

    /// <summary>转模型</summary>
    /// <returns></returns>
    public TenantUserModel ToModel()
    {
        var model = new TenantUserModel();
        model.Copy(this);

        return model;
    }

    /// <summary>用户是否拥有当前菜单的指定权限</summary>
    /// <param name="menu">指定菜单</param>
    /// <param name="flags">是否拥有多个权限中的任意一个，或的关系。如果需要表示与的关系，可以传入一个多权限位合并</param>
    /// <returns></returns>
    public Boolean Has(IMenu menu, params PermissionFlags[] flags)
    {
        if (menu == null) throw new ArgumentNullException(nameof(menu));

        // 角色集合
        var rs = Roles;

        // 如果没有指定权限子项，则指判断是否拥有资源
        if (flags == null || flags.Length == 0) return rs.Any(r => r.Has(menu.ID));

        foreach (var item in flags)
        {
            // 如果判断None，则直接返回
            if (item == PermissionFlags.None) return true;

            // 菜单必须拥有这些权限位才行
            if (menu.Permissions.ContainsKey((Int32)item))
            {
                //// 如果判断None，则直接返回
                //if (item == PermissionFlags.None) return true;

                if (rs.Any(r => r.Has(menu.ID, item))) return true;
            }
        }

        return false;
    }

    #endregion 业务操作
}

/// <summary>租户关系</summary>
public partial interface ITenantUser
{
    /// <summary>角色</summary>
    IRole? Role { get; }

    /// <summary>角色集合</summary>
    IRole[] Roles { get; }

    /// <summary>角色名</summary>
    String RoleName { get; }

    /// <summary>用户是否拥有当前菜单的指定权限</summary>
    /// <param name="menu">指定菜单</param>
    /// <param name="flags">是否拥有多个权限中的任意一个，或的关系。如果需要表示与的关系，可以传入一个多权限位合并</param>
    /// <returns></returns>
    Boolean Has(IMenu menu, params PermissionFlags[] flags);
}