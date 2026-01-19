using NewLife;
using NewLife.Data;
using XCode.Cache;

namespace XCode.Membership;

/// <summary>日志</summary>
public partial class Log : Entity<Log>
{
    #region 对象操作
    static Log()
    {
        Meta.Table.DataTable.InsertOnly = true;
        //Meta.Factory.FullInsert = false;

        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add<UserInterceptor>();
        Meta.Interceptors.Add<IPInterceptor>();
        Meta.Interceptors.Add<TraceInterceptor>();

#if !DEBUG
        // 关闭SQL日志
        ThreadPool.UnsafeQueueUserWorkItem(s => { Meta.Session.Dal.Db.ShowSQL = false; }, null);
#endif

        // 针对Mysql启用压缩表
        var table = Meta.Table.DataTable;
        table.Properties["ROW_FORMAT"] = "COMPRESSED";
        table.Properties["KEY_BLOCK_SIZE"] = "4";
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        if (method == DataMethod.Delete) return true;

        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        if (method == DataMethod.Insert)
        {
            // 自动设置当前登录用户
            if (!IsDirty(__.UserName)) UserName = ManageProvider.Provider?.Current + "";
        }

        // 处理过长的备注
        this.TrimExtraLong(_.Remark, _.UserName);

        return base.Valid(method);
    }

    /// <summary></summary>
    /// <returns></returns>
    protected override Int32 OnUpdate() => throw new Exception("禁止修改日志！");

    /// <summary></summary>
    /// <returns></returns>
    protected override Int32 OnDelete() => throw new Exception("禁止删除日志！");
    #endregion

    #region 扩展属性
    #endregion

    #region 扩展查询
    /// <summary>查询</summary>
    /// <param name="key"></param>
    /// <param name="userid"></param>
    /// <param name="category"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="p"></param>
    /// <returns></returns>
    [Obsolete]
    public static IList<Log> Search(String key, Int32 userid, String category, DateTime start, DateTime end, PageParameter p)
    {
        var exp = new WhereExpression();
        //if (!key.IsNullOrEmpty()) exp &= (_.Action == key | _.Remark.Contains(key));
        if (!category.IsNullOrEmpty() && category != "全部") exp &= _.Category == category;
        if (userid > 0) exp &= _.CreateUserID == userid;

        // 主键带有时间戳
        var snow = Meta.Factory.Snow;
        if (snow != null)
            exp &= _.ID.Between(start, end, snow);
        else
            exp &= _.CreateTime.Between(start, end);

        // 先精确查询，再模糊
        if (!key.IsNullOrEmpty())
        {
            var list = FindAll(exp & _.Action == key, p);
            if (list.Count > 0) return list;

            exp &= _.Action.Contains(key) | _.Remark.Contains(key);
        }

        return FindAll(exp, p);
    }

    /// <summary>查询</summary>
    /// <param name="category"></param>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="userid"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="key"></param>
    /// <param name="p"></param>
    /// <returns></returns>
    [Obsolete]
    public static IList<Log> Search(String category, String action, Boolean? success, Int32 userid, DateTime start, DateTime end, String key, PageParameter p)
    {
        var exp = new WhereExpression();

        if (!category.IsNullOrEmpty() && category != "全部") exp &= _.Category == category;
        if (!action.IsNullOrEmpty() && action != "全部") exp &= _.Action == action;
        if (success != null) exp &= _.Success == success;
        if (userid > 0) exp &= _.CreateUserID == userid;

        // 主键带有时间戳
        var snow = Meta.Factory.Snow;
        if (snow != null)
            exp &= _.ID.Between(start, end, snow);
        else
            exp &= _.CreateTime.Between(start, end);

        if (!key.IsNullOrEmpty()) exp &= _.Remark.Contains(key);

        return FindAll(exp, p);
    }

    /// <summary>查询</summary>
    /// <param name="category"></param>
    /// <param name="action"></param>
    /// <param name="linkId"></param>
    /// <param name="success"></param>
    /// <param name="userid"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="key"></param>
    /// <param name="p"></param>
    /// <returns></returns>
    public static IList<Log> Search(String category, String action, Int64 linkId, Boolean? success, Int32 userid, DateTime start, DateTime end, String key, PageParameter p)
    {
        var exp = new WhereExpression();

        if (!category.IsNullOrEmpty() && category != "全部") exp &= _.Category == category;
        if (!action.IsNullOrEmpty() && action != "全部") exp &= _.Action == action;
        if (linkId > 0) exp &= _.LinkID == linkId;
        if (success != null) exp &= _.Success == success;
        if (userid > 0) exp &= _.CreateUserID == userid;

        // 主键带有时间戳
        var snow = Meta.Factory.Snow;
        if (snow != null)
            exp &= _.ID.Between(start, end, snow);
        else
            exp &= _.CreateTime.Between(start, end);

        if (!key.IsNullOrEmpty()) exp &= _.Remark.Contains(key);

        return FindAll(exp, p);
    }

    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static Log? FindByID(Int64 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.ID == id);
    }

    /// <summary>根据创建用户、编号查找</summary>
    /// <param name="createUserId">创建用户</param>
    /// <param name="id">编号</param>
    /// <returns>实体列表</returns>
    public static IList<Log> FindAllByCreateUserIDAndID(Int32 createUserId, Int64 id)
    {

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.CreateUserID == createUserId && e.ID == id);

        return FindAll(_.CreateUserID == createUserId & _.ID == id);
    }

    /// <summary>根据创建用户查找</summary>
    /// <param name="createUserId">创建用户</param>
    /// <returns>实体列表</returns>
    public static IList<Log> FindAllByCreateUserID(Int32 createUserId)
    {
        if (createUserId <= 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.CreateUserID == createUserId);

        return FindAll(_.CreateUserID == createUserId);
    }
    #endregion

    #region 扩展操作
    // Select Count(ID) as ID,Category From Log Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By ID Desc limit 20
    static readonly FieldCache<Log> CategoryCache = new(__.Category)
    {
        Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取所有类别名称，最近30天</summary>
    /// <returns></returns>
    public static IDictionary<String, String> FindAllCategoryName() => CategoryCache.FindAllName();

    static readonly FieldCache<Log> ActionCache = new(__.Action)
    {
        Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取所有操作名称，最近30天</summary>
    /// <returns></returns>
    public static IDictionary<String, String> FindAllActionName() => ActionCache.FindAllName();
    #endregion

    #region 业务
    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String ToString() => $"{Category} {Action} {UserName} {CreateTime:yyyy-MM-dd HH:mm:ss} {Remark}";
    #endregion
}