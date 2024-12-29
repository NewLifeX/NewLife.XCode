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
using NewLife.Remoting;
using NewLife.Threading;
using NewLife.Web;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;
using XCode.Shards;

namespace Company.MyName;

public partial class User : Entity<User>
{
    #region 对象操作
    static User()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(Sex));

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add(new UserModule { AllowEmpty = false });
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add(new IPModule { AllowEmpty = false });

        // 实体缓存
        // var ec = Meta.Cache;
        // ec.Expire = 60;

        // 单对象缓存
        var sc = Meta.SingleCache;
        // sc.Expire = 60;
        sc.FindSlaveKeyMethod = k => Find(_.Name == k);
        sc.GetSlaveKeyMethod = e => e.Name;
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        //if (method == DataMethod.Delete) return true;
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
        if (Name.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Name), "名称不能为空！");

        // 建议先调用基类方法，基类方法会做一些统一处理
        if (!base.Valid(method)) return false;

        // 在新插入数据或者修改了指定字段时进行修正

        // 保留2位小数
        //Ex3 = Math.Round(Ex3, 2);

        // 处理当前已登录用户信息，可以由UserModule过滤器代劳
        /*var user = ManageProvider.User;
        if (user != null)
        {
            if (!Dirtys[nameof(UpdateUserID)]) UpdateUserID = user.ID;
        }*/
        //if (!Dirtys[nameof(UpdateTime)]) UpdateTime = DateTime.Now;
        //if (!Dirtys[nameof(UpdateIP)]) UpdateIP = ManageProvider.UserHost;

        // 检查唯一索引
        // CheckExist(method == DataMethod.Insert, nameof(Name));

        return true;
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //protected override void InitData()
    //{
    //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
    //    if (Meta.Session.Count > 0) return;

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化User[用户]数据……");

    //    var entity = new User();
    //    entity.Name = "abc";
    //    entity.Password = "abc";
    //    entity.DisplayName = "abc";
    //    entity.Sex = 0;
    //    entity.Mail = "abc";
    //    entity.MailVerified = true;
    //    entity.Mobile = "abc";
    //    entity.MobileVerified = true;
    //    entity.Code = "abc";
    //    entity.AreaId = 0;
    //    entity.Avatar = "abc";
    //    entity.RoleID = 0;
    //    entity.RoleIds = "abc";
    //    entity.DepartmentID = 0;
    //    entity.Online = true;
    //    entity.Enable = true;
    //    entity.Age = 0;
    //    entity.Birthday = DateTime.Now;
    //    entity.Logins = 0;
    //    entity.LastLogin = DateTime.Now;
    //    entity.LastLoginIP = "abc";
    //    entity.RegisterTime = DateTime.Now;
    //    entity.RegisterIP = "abc";
    //    entity.OnlineTime = 0;
    //    entity.Ex1 = 0;
    //    entity.Ex2 = 0;
    //    entity.Ex3 = 0.0;
    //    entity.Ex4 = "abc";
    //    entity.Ex5 = "abc";
    //    entity.Ex6 = "abc";
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化User[用户]数据！");
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
    /// <param name="name">名称。登录用户名</param>
    /// <param name="mail">邮件。支持登录</param>
    /// <param name="mobile">手机。支持登录</param>
    /// <param name="code">代码。身份证、员工编码等，支持登录</param>
    /// <param name="roleId">角色。主要角色</param>
    /// <param name="departmentId">部门。组织机构</param>
    /// <param name="mailVerified">邮箱验证。邮箱是否已通过验证</param>
    /// <param name="mobileVerified">手机验证。手机是否已通过验证</param>
    /// <param name="areaId">地区。省市区</param>
    /// <param name="online">在线</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<User> Search(String name, String? mail, String? mobile, String? code, Int32 roleId, Int32 departmentId, Boolean? mailVerified, Boolean? mobileVerified, Int32 areaId, Boolean? online, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!name.IsNullOrEmpty()) exp &= _.Name == name;
        if (!mail.IsNullOrEmpty()) exp &= _.Mail == mail;
        if (!mobile.IsNullOrEmpty()) exp &= _.Mobile == mobile;
        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (roleId >= 0) exp &= _.RoleID == roleId;
        if (departmentId >= 0) exp &= _.DepartmentID == departmentId;
        if (mailVerified != null) exp &= _.MailVerified == mailVerified;
        if (mobileVerified != null) exp &= _.MobileVerified == mobileVerified;
        if (areaId >= 0) exp &= _.AreaId == areaId;
        if (online != null) exp &= _.Online == online;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }

    // Select Count(ID) as ID,Mail From User Where CreateTime>'2020-01-24 00:00:00' Group By Mail Order By ID Desc limit 20
    static readonly FieldCache<User> _MailCache = new(nameof(Mail))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取邮件列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetMailList() => _MailCache.FindAllName();

    // Select Count(ID) as ID,Mobile From User Where CreateTime>'2020-01-24 00:00:00' Group By Mobile Order By ID Desc limit 20
    static readonly FieldCache<User> _MobileCache = new(nameof(Mobile))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取手机列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetMobileList() => _MobileCache.FindAllName();

    // Select Count(ID) as ID,Code From User Where CreateTime>'2020-01-24 00:00:00' Group By Code Order By ID Desc limit 20
    static readonly FieldCache<User> _CodeCache = new(nameof(Code))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取代码列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetCodeList() => _CodeCache.FindAllName();
    #endregion

    #region 业务操作
    #endregion
}
