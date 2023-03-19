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

namespace XCode.Membership
{
    public partial class TenantUser : Entity<TenantUser>
    {
        #region 对象操作
        static TenantUser()
        {
            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(nameof(UserId));

            // 过滤器 UserModule、TimeModule、IPModule
            Meta.Modules.Add<UserModule>();
            Meta.Modules.Add<TimeModule>();
            Meta.Modules.Add<IPModule>();
        }

        /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew">是否插入</param>
        public override void Valid(Boolean isNew)
        {
            // 如果没有脏数据，则不需要进行任何处理
            if (!HasDirty) return;

            // 建议先调用基类方法，基类方法会做一些统一处理
            base.Valid(isNew);

            // 在新插入数据或者修改了指定字段时进行修正
            // 处理当前已登录用户信息，可以由UserModule过滤器代劳
            /*var user = ManageProvider.User;
            if (user != null)
            {
                if (isNew && !Dirtys[nameof(CreateUserId)]) CreateUserId = user.ID;
                if (!Dirtys[nameof(UpdateUserId)]) UpdateUserId = user.ID;
            }*/
            //if (isNew && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
            //if (!Dirtys[nameof(UpdateTime)]) UpdateTime = DateTime.Now;
            //if (isNew && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;
            //if (!Dirtys[nameof(UpdateIP)]) UpdateIP = ManageProvider.UserHost;

            // 检查唯一索引
            // CheckExist(isNew, nameof(TenantId), nameof(UserId));
        }

        ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
        //[EditorBrowsable(EditorBrowsableState.Never)]
        //protected override void InitData()
        //{
        //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
        //    if (Meta.Session.Count > 0) return;

        //    if (XTrace.Debug) XTrace.WriteLine("开始初始化TenantUser[租户用户关系]数据……");

        //    var entity = new TenantUser();
        //    entity.TenantId = "abc";
        //    entity.UserId = 0;
        //    entity.CreateUserId = 0;
        //    entity.CreateTime = DateTime.Now;
        //    entity.CreateIP = "abc";
        //    entity.UpdateUserId = 0;
        //    entity.UpdateTime = DateTime.Now;
        //    entity.UpdateIP = "abc";
        //    entity.Remark = "abc";
        //    entity.Insert();

        //    if (XTrace.Debug) XTrace.WriteLine("完成初始化TenantUser[租户用户关系]数据！");
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
        /// <summary>用户</summary>
        [XmlIgnore, IgnoreDataMember]
        //[ScriptIgnore]
        public User User => Extends.Get(nameof(User), k => User.FindByID(UserId));

        /// <summary>用户</summary>
        [Map(nameof(UserId), typeof(User), "Id")]
        public String UserName => User?.Name;

        /// <summary>租户</summary>
        [XmlIgnore, IgnoreDataMember]
        public Tenant Tenant => Extends.Get(nameof(Tenant), k => Tenant.FindById(TenantId));

        /// <summary>租户名称</summary>
        [Map(nameof(TenantName), typeof(Tenant), "Id")]
        public String TenantName => Tenant?.Name;
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static TenantUser FindById(Int32 id)
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
        public static TenantUser FindByTenantIdAndUserId(Int32 tenantId, Int32 userId)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.TenantId == tenantId && e.UserId == userId);

            return Find(_.TenantId == tenantId & _.UserId == userId);
        }

        /// <summary>根据用户查询</summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public static IList<TenantUser> FindAllByUserId(Int32 userId)
        {
            if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.UserId == userId);

            return FindAll(_.UserId == userId);
        }

        /// <summary>根据用户编号集合批量查询</summary>
        /// <param name="userIds"></param>
        /// <returns></returns>
        public static IList<TenantUser> FindAllByUserIds(IEnumerable<Int32> userIds)
        {
            if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => userIds.Contains(e.UserId));

            return FindAll(_.UserId.In(userIds));
        }

        /// <summary>根据租户查询</summary>
        /// <param name="tenantId"></param>
        /// <returns></returns>
        public static IList<TenantUser> FindAllByTenantId(Int32 tenantId)
        {
            if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.TenantId == tenantId);

            return FindAll(_.TenantId == tenantId);
        }

        /// <summary>根据租户编号集合批量查询</summary>
        /// <param name="tenantIds"></param>
        /// <returns></returns>
        public static IList<TenantUser> FindAllByTenantIds(IEnumerable<Int32> tenantIds)
        {
            if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => tenantIds.Contains(e.TenantId));

            return FindAll(_.TenantId.In(tenantIds));
        }
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="tenantId">租户</param>
        /// <param name="userId">用户</param>
        /// <param name="start">更新时间开始</param>
        /// <param name="end">更新时间结束</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<TenantUser> Search(String tenantId, Int32 userId, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (!tenantId.IsNullOrEmpty()) exp &= _.TenantId == tenantId;
            if (userId >= 0) exp &= _.UserId == userId;
            exp &= _.UpdateTime.Between(start, end);
            if (!key.IsNullOrEmpty()) exp &= _.TenantId.Contains(key) | _.CreateIP.Contains(key) | _.UpdateIP.Contains(key) | _.Remark.Contains(key);

            return FindAll(exp, page);
        }

        // Select Count(Id) as Id,TenantId From TenantUser Where CreateTime>'2020-01-24 00:00:00' Group By TenantId Order By Id Desc limit 20
        static readonly FieldCache<TenantUser> _TenantIdCache = new FieldCache<TenantUser>(nameof(TenantId))
        {
            //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
        };

        /// <summary>获取租户列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
        /// <returns></returns>
        public static IDictionary<String, String> GetTenantIdList() => _TenantIdCache.FindAllName();
        #endregion

        #region 业务操作
        #endregion
    }
}