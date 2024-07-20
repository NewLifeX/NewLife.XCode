using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Threading;
using NewLife.Web;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;
using XCode.Shards;

namespace XCode.Membership666;

public partial class UserLog : Entity<UserLog>
{
    #region 对象操作
    static UserLog()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(LinkID));

        // 按月分表
        Meta.ShardPolicy = new TimeShardPolicy(nameof(DataTime), Meta.Factory)
        {
                TablePolicy = "{0}_{1:yyMM}",
                Step = TimeSpan.FromDays(30),
        };

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add(new UserModule { AllowEmpty = false });
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add(new IPModule { AllowEmpty = false });
        Meta.Modules.Add<TraceModule>();
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        //if (method == DataMethod.Delete) return true;
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        // 建议先调用基类方法，基类方法会做一些统一处理
        if (!base.Valid(method)) return false;

        // 在新插入数据或者修改了指定字段时进行修正

        // 保留2位小数
        //Ex3 = Math.Round(Ex3, 2);

        // 处理当前已登录用户信息，可以由UserModule过滤器代劳
        /*var user = ManageProvider.User;
        if (user != null)
        {
            if (method == DataMethod.Insert && !Dirtys[nameof(CreateUserID)]) CreateUserID = user.ID;
        }*/
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;

        return true;
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //protected override void InitData()
    //{
    //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
    //    if (Meta.Session.Count > 0) return;

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化UserLog[用户日志]数据……");

    //    var entity = new UserLog();
    //    entity.DataTime = DateTime.Now;
    //    entity.Category = "abc";
    //    entity.Action = "abc";
    //    entity.LinkID = 0;
    //    entity.Success = true;
    //    entity.UserName = "abc";
    //    entity.Ex1 = 0;
    //    entity.Ex2 = 0;
    //    entity.Ex3 = 0.0;
    //    entity.Ex4 = "abc";
    //    entity.Ex5 = "abc";
    //    entity.Ex6 = "abc";
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化UserLog[用户日志]数据！");
    //}

    ///// <summary>已重载。基类先调用Valid(true)验证数据，然后在事务保护内调用OnInsert</summary>
    ///// <returns></returns>
    //public override Int32 Insert()
    //{
    //    return base.Insert();
    //}

    ///// <summary>已重载。在事务保护范围内处理业务，位于Valid之后</summary>
    ///// <returns></returns>
    //protected override Int32 OnDelete()
    //{
    //    return base.OnDelete();
    //}
    #endregion

    #region 扩展属性
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="category">类别</param>
    /// <param name="action">操作</param>
    /// <param name="linkId">链接</param>
    /// <param name="createUserId">创建用户</param>
    /// <param name="start">时间开始</param>
    /// <param name="end">时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<UserLog> Search(String? category, String? action, Int32 linkId, Int32 createUserId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        if (!action.IsNullOrEmpty()) exp &= _.Action == action;
        if (linkId >= 0) exp &= _.LinkID == linkId;
        if (createUserId >= 0) exp &= _.CreateUserID == createUserId;
        exp &= _.CreateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Category.Contains(key) | _.Action.Contains(key) | _.UserName.Contains(key) | _.Ex4.Contains(key) | _.Ex5.Contains(key) | _.Ex6.Contains(key) | _.TraceId.Contains(key) | _.CreateUser.Contains(key) | _.CreateIP.Contains(key) | _.Remark.Contains(key);

        return FindAll(exp, page);
    }

    // Select Count(ID) as ID,Action From UserLog Where CreateTime>'2020-01-24 00:00:00' Group By Action Order By ID Desc limit 20
    static readonly FieldCache<UserLog> _ActionCache = new FieldCache<UserLog>(nameof(Action))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取操作列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetActionList() => _ActionCache.FindAllName();

    // Select Count(ID) as ID,Category From UserLog Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By ID Desc limit 20
    static readonly FieldCache<UserLog> _CategoryCache = new FieldCache<UserLog>(nameof(Category))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作
    public IUserLog ToModel()
    {
        var model = new UserLog();
        model.Copy(this);

        return model;
    }

    #endregion
}
