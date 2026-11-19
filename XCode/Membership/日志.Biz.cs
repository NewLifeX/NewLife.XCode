using NewLife;
using NewLife.Data;
using XCode.Cache;
using XCode.Exceptions;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace XCode.Membership;

/// <summary>日志</summary>
public partial class Log : Entity<Log>, IUserScope, IDataScopeFieldProvider
{

    #region 兼容别名

    /// <summary>编号</summary>
    /// <remarks>兼容旧版本字段名，新代码请使用 Id</remarks>
    [Obsolete("请使用 Id")]
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
#if NET5_0_OR_GREATER
    [System.Text.Json.Serialization.JsonIgnore]
#endif
    public Int64 ID { get => Id; set => Id = value; }

    /// <summary>关联编号</summary>
    /// <remarks>兼容旧版本字段名，新代码请使用 LinkId</remarks>
    [Obsolete("请使用 LinkId")]
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
#if NET5_0_OR_GREATER
    [System.Text.Json.Serialization.JsonIgnore]
#endif
    public Int64 LinkID { get => LinkId; set => LinkId = value; }

    /// <summary>创建者</summary>
    /// <remarks>兼容旧版本字段名，新代码请使用 CreateUserId</remarks>
    [Obsolete("请使用 CreateUserId")]
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
#if NET5_0_OR_GREATER
    [System.Text.Json.Serialization.JsonIgnore]
#endif
    public Int32 CreateUserID { get => CreateUserId; set => CreateUserId = value; }

    /// <summary>根据类别和关联编号查找日志</summary>
    [Obsolete("请使用 FindAllByCategoryAndLinkId")]
    public static IList<Log> FindAllByCategoryAndLinkID(String? category, Int64 linkId) => FindAllByCategoryAndLinkId(category, linkId);
    #endregion
    #region 对象操作
    static Log()
    {
        Meta.Table.DataTable.InsertOnly = true;
        //Meta.Factory.FullInsert = false;

        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add<UserInterceptor>();
        Meta.Interceptors.Add<IPInterceptor>();
        Meta.Interceptors.Add<TraceInterceptor>();
        Meta.Interceptors.Add<DataScopeInterceptor>();

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
    protected override Int32 OnUpdate() => throw new Exception(L.T("Modifying logs is prohibited!"));

    /// <summary></summary>
    /// <returns></returns>
    protected override Int32 OnDelete() => throw new Exception(L.T("Deleting logs is prohibited!"));
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
        if (userid > 0) exp &= _.CreateUserId == userid;

        // 主键带有时间戳
        var snow = Meta.Factory.Snow;
        if (snow != null)
            exp &= _.Id.Between(start, end, snow);
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
        if (userid > 0) exp &= _.CreateUserId == userid;

        // 主键带有时间戳
        var snow = Meta.Factory.Snow;
        if (snow != null)
            exp &= _.Id.Between(start, end, snow);
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
        if (linkId > 0) exp &= _.LinkId == linkId;
        if (success != null) exp &= _.Success == success;
        if (userid > 0) exp &= _.CreateUserId == userid;

        // 主键带有时间戳
        var snow = Meta.Factory.Snow;
        if (snow != null)
            exp &= _.Id.Between(start, end, snow);
        else
            exp &= _.CreateTime.Between(start, end);

        if (!key.IsNullOrEmpty()) exp &= _.Remark.Contains(key);

        return FindAll(exp, p);
    }

    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    [Obsolete("请使用 FindById")]
    public static Log? FindByID(Int64 id) => FindById(id);

    /// <summary>根据创建用户、编号查找</summary>
    /// <param name="createUserId">创建用户</param>
    /// <param name="id">编号</param>
    /// <returns>实体列表</returns>
    [Obsolete("请使用 FindAllByCreateUserIdAndId")]
    public static IList<Log> FindAllByCreateUserIDAndID(Int32 createUserId, Int64 id) => FindAllByCreateUserIdAndId(createUserId, id);

    /// <summary>根据创建用户和编号查找</summary>
    /// <param name="createUserId">创建用户</param>
    /// <param name="id">编号</param>
    /// <returns>实体列表</returns>
    public static IList<Log> FindAllByCreateUserIdAndId(Int32 createUserId, Int64 id)
    {

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.CreateUserId == createUserId && e.Id == id);

        return FindAll(_.CreateUserId == createUserId & _.Id == id);
    }

    /// <summary>根据创建用户查找</summary>
    /// <param name="createUserId">创建用户</param>
    /// <returns>实体列表</returns>
    [Obsolete("请使用 FindAllByCreateUserId")]
    public static IList<Log> FindAllByCreateUserID(Int32 createUserId) => FindAllByCreateUserId(createUserId);

    /// <summary>根据操作查找日志</summary>
    /// <param name="action">操作</param>
    /// <returns>实体列表</returns>
    public static IList<Log> FindAllByAction(String action)
    {
        if (action.IsNullOrEmpty()) return [];

        return FindAll(_.Action == action);
    }

    /// <summary>按创建时间统计日志数量</summary>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <param name="success">成功状态。为空时不限</param>
    /// <returns>日志数量</returns>
    public static Int64 FindCountByCreateTime(DateTime start, DateTime end, Boolean? success = null)
    {
        var snow = Meta.Factory.Snow;
        Expression exp = snow != null
            ? _.Id.Between(start, end, snow)
            : _.CreateTime.Between(start, end);
        if (success != null) exp &= _.Success == success;

        return FindCount(exp, null, null, 0, 0);
    }

    /// <summary>按创建时间查询日志</summary>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <param name="page">分页参数信息</param>
    /// <returns>日志列表</returns>
    public static IList<Log> FindAllByTimeRange(DateTime start, DateTime end, PageParameter page)
    {
        var snow = Meta.Factory.Snow;
        var exp = snow != null
            ? _.Id.Between(start, end, snow)
            : _.CreateTime.Between(start, end);

        return FindAll(exp, page);
    }

    /// <summary>按操作关键字与创建时间查询日志，按编号倒序</summary>
    /// <param name="keywords">操作关键字集合，任一匹配即可</param>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <param name="count">最大条数</param>
    /// <returns>日志列表</returns>
    public static IList<Log> FindAllByActionKeywords(String[] keywords, DateTime start, DateTime end, Int32 count)
    {
        var exp = new WhereExpression();
        if (keywords != null && keywords.Length > 0)
        {
            var exp2 = new WhereExpression();
            foreach (var item in keywords)
            {
                exp2 |= _.Action.Contains(item);
            }
            exp &= exp2;
        }

        var snow = Meta.Factory.Snow;
        exp &= snow != null
            ? _.Id.Between(start, end, snow)
            : _.CreateTime.Between(start, end);

        return FindAll(exp, _.Id.Desc(), null, 0, count);
    }

    /// <summary>按创建时间查询日志，按编号倒序</summary>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <param name="count">最大条数</param>
    /// <returns>日志列表</returns>
    public static IList<Log> FindAllByCreateTimeRange(DateTime start, DateTime end, Int32 count)
    {
        var snow = Meta.Factory.Snow;
        var exp = snow != null
            ? _.Id.Between(start, end, snow)
            : _.CreateTime.Between(start, end);

        return FindAll(exp, _.Id.Desc(), null, 0, count);
    }
    #endregion

    #region 扩展操作
    // Select Count(Id) as Id,Category From Log Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
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

    /// <summary>删除指定日期之前的系统日志（CreateUserId=0）</summary>
    /// <param name="date">截止日期</param>
    /// <returns>影响行数</returns>
    public static Int32 DeleteBefore(DateTime date)
    {
        var snow = Meta.Factory.Snow;
        Expression exp = snow != null ? _.Id < snow.GetId(date) : _.CreateTime < date;
        exp &= _.CreateUserId == 0;

        try
        {
            return Delete(exp);
        }
        catch (XSqlException)
        {
            // SQLite 下日志表较大时删除可能报错，改为分批查询后逐批删除
            var rs = 0;
            for (var i = 0; i < 100; i++)
            {
                var list = FindAll(exp, null, null, 0, 10000);
                if (list.Count == 0) break;

                rs += list.Delete();
            }

            return rs;
        }
    }
    #endregion

    #region 业务
    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String ToString() => $"{Category} {Action} {UserName} {CreateTime:yyyy-MM-dd HH:mm:ss} {Remark}";
    #endregion

    #region IUserScope 成员
    // 日志表没有部门列，仅按创建用户过滤；本部门/自定义等范围不会 join 用户表放大成同事数据
    Int32 IUserScope.UserId { get => CreateUserId; set => CreateUserId = value; }

    #endregion

    #region IDataScopeFieldProvider 成员
    XCode.Configuration.FieldItem? IDataScopeFieldProvider.GetUserField() => _.CreateUserId;
    XCode.Configuration.FieldItem? IDataScopeFieldProvider.GetDepartmentField() => null;
    XCode.Configuration.FieldItem? IDataScopeFieldProvider.GetTenantField() => null;
    #endregion
}