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

public partial class Area : Entity<Area>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 10000;

    static Area()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(ParentID));

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<TimeModule>();

        // 实体缓存
        // var ec = Meta.Cache;
        // ec.Expire = 60;
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
        //Longitude = Math.Round(Longitude, 2);
        //Latitude = Math.Round(Latitude, 2);
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
        //if (!Dirtys[nameof(UpdateTime)]) UpdateTime = DateTime.Now;

        return true;
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //protected override void InitData()
    //{
    //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
    //    if (Meta.Session.Count > 0) return;

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化Area[地区]数据……");

    //    var entity = new Area();
    //    entity.ID = 0;
    //    entity.Name = "abc";
    //    entity.FullName = "abc";
    //    entity.ParentID = 0;
    //    entity.Level = 0;
    //    entity.Kind = "abc";
    //    entity.English = "abc";
    //    entity.PinYin = "abc";
    //    entity.JianPin = "abc";
    //    entity.TelCode = "abc";
    //    entity.ZipCode = "abc";
    //    entity.Longitude = 0.0;
    //    entity.Latitude = 0.0;
    //    entity.GeoHash = "abc";
    //    entity.Enable = true;
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化Area[地区]数据！");
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

    // Select Count(Id) as Id,PinYin From Area Where CreateTime>'2020-01-24 00:00:00' Group By PinYin Order By Id Desc limit 20
    static readonly FieldCache<Area> _PinYinCache = new(nameof(PinYin))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取拼音列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetPinYinList() => _PinYinCache.FindAllName();

    // Select Count(Id) as Id,JianPin From Area Where CreateTime>'2020-01-24 00:00:00' Group By JianPin Order By Id Desc limit 20
    static readonly FieldCache<Area> _JianPinCache = new(nameof(JianPin))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取简拼列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetJianPinList() => _JianPinCache.FindAllName();

    // Select Count(Id) as Id,GeoHash From Area Where CreateTime>'2020-01-24 00:00:00' Group By GeoHash Order By Id Desc limit 20
    static readonly FieldCache<Area> _GeoHashCache = new(nameof(GeoHash))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取地址编码列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetGeoHashList() => _GeoHashCache.FindAllName();
    #endregion

    #region 业务操作
    public IArea ToModel()
    {
        var model = new Area();
        model.Copy(this);

        return model;
    }

    #endregion
}
