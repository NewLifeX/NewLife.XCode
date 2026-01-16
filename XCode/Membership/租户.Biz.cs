using NewLife;
using NewLife.Common;
using NewLife.Data;

namespace XCode.Membership;

[ModelCheckMode(ModelCheckModes.CheckTableWhenFirstUse)]
public partial class Tenant : Entity<Tenant>, ITenantSource
{
    #region 对象操作

    static Tenant()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(CreateUserId));

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<UserModule>();
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

        if (Code.IsNullOrEmpty()) Code = PinYin.GetFirst(Name);

        // 管理者
        if (method == DataMethod.Insert && ManagerId == 0) ManagerId = ManageProvider.Provider?.Current?.ID ?? 0;

        return base.Valid(method);
    }

    #endregion 对象操作

    #region 扩展属性

    /// <summary>角色组名</summary>
    [Map(__.RoleIds)]
    public virtual String RoleNames => Extends.Get(nameof(RoleNames), k => RoleIds.SplitAsInt().Select(e => ManageProvider.Get<IRole>()?.FindByID(e)).Where(e => e != null).Select(e => e.Name).Join());

    Int32 ITenantSource.TenantId { get => Id; set => Id = value; }

    #endregion 扩展属性

    #region 扩展查询

    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static Tenant? FindById(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据名称查找</summary>
    /// <param name="name">名称</param>
    /// <returns>实体列表</returns>
    public static IList<Tenant> FindAllByName(String name)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Name.EqualIgnoreCase(name));

        return FindAll(_.Name == name);
    }

    /// <summary>根据管理员编号查询</summary>
    /// <param name="managerId"></param>
    /// <returns></returns>
    public static Tenant FindByManagerId(Int32 managerId)
    {
        if (managerId <= 0) return null;

        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ManagerId == managerId);

        return Find(_.ManagerId == managerId);
    }

    #endregion 扩展查询

    #region 高级查询

    /// <summary>高级查询</summary>
    /// <param name="name">名称</param>
    /// <param name="managerId">租户管理员</param>
    /// <param name="enable">是否启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<Tenant> Search(String name, Int32 managerId, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!name.IsNullOrEmpty()) exp &= _.Name == name;
        if (managerId > 0) exp &= _.ManagerId == managerId;
        if (enable != null) exp &= _.Enable == enable;

        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.CreateIP.Contains(key) | _.UpdateIP.Contains(key) | _.Remark.Contains(key);

        return FindAll(exp, page);
    }

    // Select Count(Id) as Id,Category From Tenant Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    //static readonly FieldCache<Tenant> _CategoryCache = new FieldCache<Tenant>(nameof(Category))
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
    public TenantModel ToModel()
    {
        var model = new TenantModel();
        model.Copy(this);

        return model;
    }

    #endregion 业务操作
}