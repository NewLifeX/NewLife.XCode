﻿using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Caching;
using NewLife.Data;
using NewLife.Log;
using NewLife.Reflection;
using XCode.Model;
using XCode.Transform;

namespace XCode.Membership;

/// <summary>地区。行政区划数据</summary>
/// <remarks>
/// 民政局 http://www.mca.gov.cn/article/sj/xzqh/2020/2020/2020092500801.html
/// 统计局 http://www.stats.gov.cn/tjsj/tjbz/tjyqhdmhcxhfdm/2019/index.html
/// 
/// 民政局 http://www.mca.gov.cn/article/sj/xzqh/2019/2019/201912251506.html
/// 统计局 http://www.stats.gov.cn/tjsj/tjbz/tjyqhdmhcxhfdm/2018/index.html
/// </remarks>
[ModelCheckMode(ModelCheckModes.CheckTableWhenFirstUse)]
public partial class Area : Entity<Area>
{
    #region 对象操作
    static Area()
    {
        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<TimeModule>();

        Meta.Factory.MasterTime = _.UpdateTime;

        var sc = Meta.SingleCache;
        sc.MaxEntity = 100000;
        if (sc.Expire < 20 * 60) sc.Expire = 20 * 60;
        sc.Using = true;

        // 实体缓存三级地区
        var ec = Meta.Cache;
        ec.Expire = 10 * 60;
        ec.FillListMethod = () => FindAll(_.ID >= 100000 & _.ID <= 999999, _.ID.Asc(), null, 0, 0);
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        if (method == DataMethod.Delete) return true;

        FixLevel();

        // 名称
        if (Name.IsNullOrEmpty() || Name == FullName) FixName();
        if (FullName.IsNullOrEmpty()) FullName = Name;

        // 拼音
        if (PinYin.IsNullOrEmpty())
        {
            var py = NewLife.Common.PinYin.Get(Name);
            if (!py.IsNullOrEmpty()) PinYin = py;
        }
        if (JianPin.IsNullOrEmpty())
        {
            var py = NewLife.Common.PinYin.GetFirst(Name);
            if (!py.IsNullOrEmpty()) JianPin = py;
        }

        // 坐标
        //if (Longitude != 0 || Latitude != 0) GeoHash = NewLife.Data.GeoHash.Encode(Longitude, Latitude);
        if (method == DataMethod.Insert || Dirtys[nameof(Longitude)] || Dirtys[nameof(Latitude)])
        {
            if (Math.Abs(Longitude) > 0.001 || Math.Abs(Latitude) > 0.001)
                GeoHash = NewLife.Data.GeoHash.Encode(Longitude, Latitude);
        }

        return base.Valid(method);
    }

    /// <summary>初始化数据</summary>
    protected internal override void InitData()
    {
        // 预热数据
        if (Meta.Session.Count > 0)
        {
            ThreadPool.UnsafeQueueUserWorkItem(s =>
            {
                try
                {
                    Preload();
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }
            }, null);
        }
    }

    /// <summary>已重载。返回路径</summary>
    /// <returns></returns>
    public override String ToString() => Path;
    #endregion

    #region 扩展属性
    /// <summary>顶级根。它的Childs就是各个省份</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public static Area Root { get; } = new Area();

    /// <summary>父级</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Area Parent => Extends.Get(nameof(Parent), k => FindByID(ParentID) ?? Root)!;

    ///// <summary>所有父级，从高到底</summary>
    //[XmlIgnore, IgnoreDataMember, ScriptIgnore]
    //[Obsolete("=>GetAllParents")]
    //public IList<Area> AllParents => Extends.Get(nameof(AllParents), k => GetAllParents())!;

    /// <summary>所有父级，从高到底</summary>
    /// <returns></returns>
    public IList<Area> GetAllParents() => GetAllParents(false);

    /// <summary>所有父级，从高到底</summary>
    /// <param name="includeSelf">是否包括自己</param>
    /// <returns></returns>
    public IList<Area> GetAllParents(Boolean includeSelf)
    {
        var list = new List<Area>();
        var entity = Parent;
        while (entity != null)
        {
            if (entity.ID == 0 || list.Contains(entity)) break;

            list.Add(entity);

            entity = entity.Parent;
        }

        // 倒序
        list.Reverse();

        if (includeSelf && !list.Any(e => e.ID == ID)) list.Add(this);

        return list;
    }

    /// <summary>父级路径</summary>
    public String? ParentPath
    {
        get
        {
            var list = GetAllParents();
            if (list != null && list.Count > 0) return list.Where(r => !r.IsVirtual).Join("/", r => r.Name);

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
            if (IsVirtual) return p;

            return p + "/" + Name;
        }
    }

    /// <summary>下级地区</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public IList<Area> Childs => Extends.Get(nameof(Childs), k => FindAllByParentID(ID).Where(e => e.Enable).ToList())!;

    ///// <summary>子孙级区域。支持省市区，不支持乡镇街道</summary>
    //[XmlIgnore, IgnoreDataMember, ScriptIgnore]
    //[Obsolete("=>GetAllChilds")]
    //public IList<Area> AllChilds => Extends.Get(nameof(AllChilds), k => GetAllChilds())!;

    /// <summary>子孙级区域。支持省市区，不支持乡镇街道</summary>
    public IList<Area> GetAllChilds()
    {
        var list = new List<Area>();
        foreach (var item in Childs)
        {
            list.Add(item);
            if (item.Level < 3) list.AddRange(item.GetAllChilds());
        }
        return list;
    }

    /// <summary>是否虚拟地区</summary>
    public Boolean IsVirtual => Name.EqualIgnoreCase("市辖区", "直辖县", "直辖镇");
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static Area FindByID(Int32 id)
    {
        //if (id == 0) return Root;
        if (id <= 10_00_00 || id > 99_99_99_999) return null;

        //// 实体缓存
        //var r = Meta.Cache.Find(e => e.ID == id);
        //if (r != null) return r;

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.ID == id);
    }

    /// <summary>根据ID列表数组查询，一般先后查街道、区县、城市、省份</summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    public static Area FindByIDs(params Int32[] ids)
    {
        foreach (var item in ids)
        {
            if (item > 0)
            {
                var r = FindByID(item);
                if (r != null) return r;
            }
        }

        return null;
    }

    /// <summary>在指定地区下根据名称查找</summary>
    /// <param name="parentId">父级</param>
    /// <param name="name">名称</param>
    /// <returns>实体列表</returns>
    public static Area FindByName(Int32 parentId, String name)
    {
        // 支持0级下查找省份
        var r = parentId == 0 ? Root : FindByID(parentId);
        if (r == null) return null;

        return r.Childs.Find(e => e.Name == name || e.FullName == name);
    }

    /// <summary>根据名称查询三级地区，可能有多个地区同名</summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static IList<Area> FindAllByName(String name) => Meta.Cache.Entities.FindAll(e => e.Name == name || e.FullName == name);

    /// <summary>根据名称列表数组查询，依次查省份、城市、区县、街道</summary>
    /// <param name="names">名称列表</param>
    /// <returns></returns>
    public static Area FindByNames(params String[] names)
    {
        var r = Root;
        foreach (var item in names)
        {
            if (!item.IsNullOrEmpty())
            {
                var r2 = r.Childs.Find(e => e.Name == item || e.FullName == item);
                // 可能中间不叫市辖区，而是跟第一段同名。例如 上海-上海市-徐汇区
                if (r2 == null && item.EqualIgnoreCase(r.Name, r.FullName))
                {
                    r2 = r.Childs.FirstOrDefault(e => e.IsVirtual);
                }
                // 可能中间隔了一层市辖区，如上海青浦
                if (r2 == null)
                {
                    // 重庆有市辖区也有直辖县
                    var rs3 = r.Childs.FindAll(e => e.IsVirtual);
                    if (rs3 != null)
                    {
                        foreach (var r3 in rs3)
                        {
                            r2 = r3.Childs.Find(e => e.Name == item || e.FullName == item);
                            if (r2 != null) break;
                        }
                    }
                }
                if (r2 == null) return r;

                r = r2;
            }
        }

        return r;
    }

    /// <summary>根据名称从高向低分级查找，广度搜索，仅搜索三级</summary>
    /// <param name="name">名称</param>
    /// <returns>实体列表</returns>
    public static Area FindByFullName(String name)
    {
        // 从高向低，分级搜索
        var q = new Queue<Area>();
        q.Enqueue(Root);

        while (q.Count > 0)
        {
            var r = q.Dequeue();
            if (r != null)
            {
                // 子级进入队列
                foreach (var item in r.Childs)
                {
                    if (item.Name == name || item.FullName == name) return item;

                    // 仅搜索三级
                    if (item.Level < 3) q.Enqueue(item);
                }
            }
        }

        return null;
    }

    /// <summary>根据父级查子级，专属缓存</summary>
    private static readonly ICache _pcache = new MemoryCache { Expire = 20 * 60, Period = 10 * 60, };

    /// <summary>根据父级查找。三级地区使用实体缓存，四级地区使用专属缓存</summary>
    /// <param name="parentid">父级</param>
    /// <returns>实体列表</returns>
    public static IList<Area> FindAllByParentID(Int32 parentid)
    {
        if (parentid is < 0 or > 99_99_99) return new List<Area>();

        // 实体缓存
        var rs = Meta.Cache.FindAll(e => e.ParentID == parentid);
        // 有子节点，并且都是启用状态，则直接使用
        //if (rs.Count > 0 && rs.Any(e => e.Enable)) return rs;
        if (rs.Count > 0) return rs;

        var key = parentid + "";
        if (_pcache.TryGetValue(key, out rs)) return rs;

        rs = FindAll(_.ParentID == parentid, _.ID.Asc(), null, 0, 0);

        _pcache.Set(key, rs, 20 * 60);

        return rs;
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="parentid">父级</param>
    /// <param name="level"></param>
    /// <param name="idstart"></param>
    /// <param name="idend"></param>
    /// <param name="enable"></param>
    /// <param name="key"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    public static IList<Area> Search(Int32 parentid, Int32 level, Int32 idstart, Int32 idend, Boolean? enable, String key, DateTime start, DateTime end, PageParameter page)
    {
        var exp = _.UpdateTime.Between(start, end);

        if (parentid >= 0) exp &= _.ParentID == parentid;
        if (level > 0) exp &= _.Level == level;
        if (idstart > 0) exp &= _.ID >= idstart;
        if (idend > 0) exp &= _.ID <= idend;
        if (enable != null) exp &= _.Enable == enable;

        if (!key.IsNullOrEmpty())
        {
            if (key.ToLong() > 0)
            {
                var exp2 = new WhereExpression();
                if (key.Length is 6 or 9) exp2 |= _.ID == key;
                if (key.Length is 2 or 3) exp2 |= _.TelCode == key;
                if (key.Length == 6) exp2 |= _.ZipCode == key;

                exp &= exp2;
            }
            else
            {
                // 区分中英文，GeoHash全部小写
                if (Encoding.UTF8.GetByteCount(key) == key.Length)
                {
                    if (key == key.ToLower())
                        exp &= _.PinYin.StartsWith(key) | _.JianPin == key | _.GeoHash.StartsWith(key);
                    else
                        exp &= _.PinYin.StartsWith(key) | _.JianPin == key;
                }
                else
                    exp &= _.Name == key | _.FullName.StartsWith(key) | _.Kind == key;
            }
        }

        return FindAll(exp, page);
    }

    /// <summary>根据条件模糊搜索</summary>
    /// <param name="parentid">在指定级别下搜索，-1表示所有，非负数时支持字符串相似搜索</param>
    /// <param name="key"></param>
    /// <param name="enable"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public static IList<Area> Search(Int32 parentid, String key, Boolean? enable, Int32 count)
    {
        // 找到父节点所在位置，向后搜索子节点
        var r = parentid == 0 ? Root : FindByID(parentid);
        if (parentid >= 0 && r == null) return new List<Area>();

        if (r != null)
        {
            var set = SearchLike(r.ID, key, enable, count);
            if (set.Count > 0 || r.ID > 0) return set.Take(count).Select(e => e.Key).ToList();
        }

        return Search(parentid, -1, -1, -1, enable, key, DateTime.MinValue, DateTime.MinValue, new PageParameter { PageSize = count, Sort = nameof(ID) });
    }

    private static IDictionary<Area, Double> SearchLike(Int32 parentid, String key, Boolean? enable, Int32 count)
    {
        // 两级搜索，特殊处理直辖
        var list = (FindAllByParentID(parentid) as List<Area>)!;
        if (list.Count == 1 && list[0].Name.StartsWithIgnoreCase("直辖", "省辖", "市辖"))
            list = (FindAllByParentID(list[0].ID) as List<Area>)!;
        foreach (var item in list.ToArray())
        {
            var list2 = FindAllByParentID(item.ID);
            if (list2.Count > 0) list.AddRange(list2);
        }

        if (enable != null) list = list.Where(e => e.Enable == enable).ToList();
        if (key.IsNullOrEmpty()) return list.Take(count).ToDictionary(e => e, e => 1.0);

        {
            var list2 = list
                .Where(e => !e.Name.IsNullOrEmpty() && e.Name.Contains(key)
                || !e.FullName.IsNullOrEmpty() && e.FullName.Contains(key)
                || e.PinYin.StartsWithIgnoreCase(key)
                || e.JianPin.EqualIgnoreCase(key)
                || e.TelCode == key
                || e.ZipCode == key
                || e.GeoHash.StartsWithIgnoreCase(key)
                )
                .Take(count)
                .ToList();

            if (list2.Count > 0) return list2.ToDictionary(e => e, e => 1.0);
        }

        // 近似搜索
        return list.Match(key, e => e.FullName!).OrderByDescending(e => e.Value).Take(count).ToDictionary(e => e.Key, e => e.Value);
    }

    /// <summary>搜索地址所属地区（模糊匹配）</summary>
    /// <param name="address"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public static IList<KeyValuePair<Area, Double>> MatchAddress(String address, Int32 count = 10)
    {
        var set = new Dictionary<Area, Double>();

        // 一二级搜索，定位城市
        var dic = SearchLike(0, address, true, count);
        if (dic.Count == 0) return new List<KeyValuePair<Area, Double>>();

        // 三级搜索
        foreach (var item in dic)
        {
            var addr3 = address.TrimStart(item.Key.FullName, item.Key.Name);
            var dic3 = SearchLike(item.Key.ID, addr3, true, count);
            foreach (var elm in dic3)
            {
                var r3 = elm.Key;
                if (r3.ID > 999999)
                {
                    var val = item.Value + elm.Value;
                    if (!set.TryGetValue(r3, out var v) || v < val)
                        set[r3] = val;
                }
                else
                {
                    // 四级搜索
                    var addr4 = addr3.TrimStart(item.Key.FullName, item.Key.Name);
                    var dic4 = SearchLike(r3.ID, addr4, true, count);
                    foreach (var elm4 in dic4)
                    {
                        var r4 = elm4.Key;
                        var val = item.Value + elm.Value + elm4.Value;
                        if (!set.TryGetValue(r4, out var v) || v < val)
                            set[r4] = val;
                    }
                }
            }
        }

        // 排序
        return set.OrderByDescending(e => e.Value).Take(count).ToList();
    }

    private static Func<String, (String, String)>? _GetIp;
    private static Boolean _initGetIp;
    //private static String[] _zzq = new[] { "广西", "西藏", "新疆", "宁夏", "内蒙古" };
    /// <summary>根据IP地址搜索地区</summary>
    /// <param name="ip"></param>
    /// <param name="maxLevel">最大层级，默认3级</param>
    /// <returns></returns>
    public static IList<Area> SearchIP(String ip, Int32 maxLevel = 3)
    {
        if (!_initGetIp)
        {
            var ipr = NetHelper.IpResolver;
            if (ipr != null)
            {
                _GetIp = ipr.GetType().GetMethod("GetAddress", [typeof(String)]).As<Func<String, (String, String)>>(ipr);

                _initGetIp = true;
            }
        }

        // 新的IP解析器
        if (_GetIp != null)
        {
            var (area, addr) = _GetIp(ip);
            area = area?.TrimStart("中国\u2013");
            if (area.IsNullOrEmpty()) return [];

            var ss = area.Split("\u2013");
            var r = FindByNames(ss);
            if (r == null) return [];

            var list = r.GetAllParents(true);

            if (maxLevel > 0 && list.Count > maxLevel) list = list.Take(maxLevel).ToList();

            return list;
        }
        else
        {
            var address = ip.IPToAddress();
            if (address.IsNullOrEmpty()) return [];

            return SearchAddress(address, maxLevel);
        }
    }

    /// <summary>根据地址搜索地区，2字符逐级进行前缀匹配，支持越级</summary>
    /// <remarks>
    /// 支持越级搜索，例如“上海市华新中学”，越过市辖区和青浦区两级，最终匹配华新镇
    /// </remarks>
    /// <param name="address">省市区地址</param>
    /// <param name="maxLevel">最大层级，默认3级</param>
    /// <returns></returns>
    public static IList<Area> SearchAddress(String address, Int32 maxLevel = 3)
    {
        if (address.IsNullOrEmpty() || maxLevel <= 0) return [];

        var r = FindAddress(Root, address, maxLevel);
        if (r == null) return [];

        return r.GetAllParents(true);

        //// IP数据库里，缺失自治区分隔符
        //foreach (var item in _zzq)
        //{
        //    if (address.StartsWith(item) && !address.Contains("自治区"))
        //    {
        //        address = item + "自治区" + address[item.Length..];
        //        break;
        //    }
        //}

        //// 地址切分，至少把省分出来
        //var addrs = address.Split("省", "自治区", "市", "区", "自治县", "县", "自治州", " ");
        //if (addrs != null && addrs.Length >= 2)
        //{
        //    var prov = FindByName(0, addrs[0]);
        //    if (prov != null)
        //    {
        //        list.Add(prov);

        //        if (maxLevel == 2 && addrs.Length >= 2)
        //        {
        //            var city = FindByNames(addrs.Take(2).ToArray());
        //            if (city != null && city.ID != prov.ID) list.Add(city);
        //        }
        //        else if (maxLevel == 3)
        //        {
        //            var city = FindByNames(addrs);
        //            if (city != null && city.ID != prov.ID)
        //            {
        //                if (city.ParentID != prov.ID) list.Add(city.Parent);
        //                list.Add(city);
        //            }
        //        }
        //    }
        //}

        //return list;
    }

    /// <summary>在指定区域下按地址查找地区，前缀匹配</summary>
    /// <param name="root"></param>
    /// <param name="address"></param>
    /// <param name="maxLevel"></param>
    /// <returns></returns>
    static Area? FindAddress(Area root, String address, Int32 maxLevel)
    {
        var name = address.Substring(0, 2);
        var r = root.Childs.FirstOrDefault(e => e.Name.StartsWithIgnoreCase(name));
        if (r != null)
        {
            // 找到地区，如果层次足够，继续递归
            if (maxLevel <= 1) return r;

            address = address.TrimStart(r.FullName!, r.Name, name);
            var r2 = FindAddress(r, address, maxLevel - 1);

            return r2 ?? r;
        }

        // 层次足够，越级查找
        if (maxLevel >= 2)
        {
            foreach (var item in root.Childs)
            {
                var r2 = FindAddress(item, address, maxLevel - 1);
                if (r2 != null) return r2;
            }
        }

        return null;
    }
    #endregion

    #region 业务操作
    /// <summary>查找或创建地区</summary>
    /// <param name="id"></param>
    /// <param name="name"></param>
    /// <param name="parentid"></param>
    /// <param name="remark"></param>
    /// <returns></returns>
    public static Area Create(Int32 id, String name, Int32 parentid, String remark = null)
    {
        if (id <= 0) return null;

        // 一二三级地址是6位数字
        if (id < 100000) return null;
        if (id > 999999)
        {
            // 四级地址是9位数字
            if (parentid is < 100000 or > 999999) return null;
            if (id < 100000000) return null;
            if (id > 999999999) return null;
        }

        var r = FindByID(id) ?? Find(_.ID == id);
        if (r != null) return r;

        if (name.IsNullOrEmpty()) name = id + "";

        r = new Area
        {
            ID = id,
            Name = name,
            FullName = name,
            ParentID = parentid,
            Remark = remark
        };
        r.Insert();

        return r;
    }

    /// <summary>扫描预热数据</summary>
    public static Int32 Preload()
    {
        XTrace.WriteLine("Area.Preload 地区数据预热");

        var layer = 2;
        var count = 0;
        var list = Root.Childs.ToArray().ToList();
        for (var i = 0; i < layer; i++)
        {
            var bs = new List<Area>();
            foreach (var item in list)
            {
                // 香港澳门没有三层地区，一级直辖三级
                if (i == layer - 1 && item.ID > 810000) continue;

                bs.AddRange(item.Childs);
            }

            list = bs;
            count += bs.Count;
        }

        return count;
    }

    /// <summary>扫描并修正级别为0的数据</summary>
    /// <returns></returns>
    public static Int32 ScanFixLevel()
    {
        var ss = Meta.Session.Dal.Session;
        var old = ss.ShowSQL;
        ss.ShowSQL = false;

        var count = 0;
        var success = 0;
        var p = 0;
        while (true)
        {
            var list = FindAll(_.Level == 0 | _.Level.IsNull(), _.ID.Asc(), null, p, 1000);
            if (list.Count == 0) break;

            foreach (var item in list)
            {
                item.FixLevel();
            }

            p += list.Count;
            count += list.Count;

            success += list.Update();
        }

        ss.ShowSQL = old;

        XTrace.WriteLine("共扫描发现{0}个层级为0的地区，修正{1}个", count, success);

        return count;
    }

    /// <summary>从内容中分析得到地区。以民政部颁布的行政区划代码为准</summary>
    /// <param name="html"></param>
    /// <returns></returns>
    public static IEnumerable<Area> Parse(String html)
    {
        if (html.IsNullOrEmpty()) yield break;

        var p = 0;
        while (true)
        {
            var s = html.IndexOf("<tr ", p);
            if (s < 0) break;

            var e = html.IndexOf("</tr>", s);
            if (e < 0) break;

            // 分析数据
            var ss = html[s..e].Split("<td", "</td>");
            if (ss.Length >= 6)
            {
                var id = ss[3];
                var p2 = id.LastIndexOf('>');
                if (p2 >= 0) id = id[(p2 + 1)..];

                var name = ss[5];
                var p3 = name.LastIndexOf('>');
                if (p3 >= 0)
                {
                    var ns = name.Split("<span", "</span>");
                    if (ns.Length > 1)
                        name = ns.FirstOrDefault(e => !e.Contains(">"));
                    else
                        name = name[(p3 + 1)..];
                }
                name = name?.Trim().Trim('*').Trim();

                if (!id.IsNullOrEmpty() && id.ToInt() > 10_00_00 && !name.IsNullOrEmpty())
                {
                    var r = new Area
                    {
                        ID = id.ToInt(),
                        FullName = name,
                    };
                    yield return r;
                }
            }

            p = e;
        }
    }

    /// <summary>分析得到四级地区</summary>
    /// <param name="html"></param>
    /// <returns></returns>
    public static IEnumerable<Area> ParseLevel4(String html)
    {
        if (html.IsNullOrEmpty()) yield break;

        var p = 0;
        while (true)
        {
            var s = html.IndexOf("<tr class='towntr'>", p);
            if (s < 0) break;

            var e = html.IndexOf("</tr>", s);
            if (e < 0) break;

            // 分析数据
            var ss = html[s..e].Split("'>", "</a>");
            if (ss.Length > 4)
            {
                var id = ss[2].Trim().TrimEnd("000");
                var name = ss[4].Trim();
                if (!id.IsNullOrEmpty() && id.ToInt() > 10_00_00 && !name.IsNullOrEmpty())
                {
                    var r = new Area
                    {
                        ID = id.ToInt(),
                        FullName = name,
                    };
                    yield return r;
                }
            }

            p = e;
        }
    }

    /// <summary>从内容中分析得到地区并保存。以民政部颁布的行政区划代码为准</summary>
    /// <param name="html"></param>
    /// <returns></returns>
    public static IList<Area> ParseAndSave(String html)
    {
        var all = Parse(html).ToList();
        //File.WriteAllText("area.csv", all.Join("\r\n", e => $"{e.ID},{e.FullName}"));

        // 预备好所有三级数据
        var list = FindAll(_.ID > 10_00_00 & _.ID < 99_99_99);

        var rs = new List<Area>();
        foreach (var item in all)
        {
            // 查找是否已存在
            var r = list.Find(e => e.ID == item.ID);
            if (r == null && item.ID > 99_99_99) r = FindByID(item.ID);

            if (r == null)
            {
                r = item;

                // 找到它的上级
                var pid = GetParent(item.ID);

                // 部分区县由省直管，中间没有第二级
                if (pid > 0 && !list.Any(e => e.ID == pid) && !all.Any(e => e.ID == pid))
                {
                    var pid2 = GetParent(pid);
                    var r2 = all.Find(e => e.ID == pid2);
                    if (r2 != null)
                    {
                        var r3 = new Area
                        {
                            ID = pid,
                            ParentID = pid2,
                            Enable = true,
                        };

                        // 直辖市处理市辖区
                        if (r2.Name.EqualIgnoreCase("北京", "天津", "上海", "重庆") && r3.ID != 500200)
                            r3.Name = "市辖区";
                        else
                            r3.Name = "直辖县";

                        r3.FixLevel();
                        r3.FixName();

                        rs.Add(r3);
                        list.Add(r3);
                    }
                    else
                    {
                        XTrace.WriteLine("无法识别地区的父级 {0} {1}", item.ID, item.Name);
                    }
                }

                r.ParentID = pid;
                r.Enable = true;
            }
            else
            {
                r.FullName = item.FullName;
            }

            r.FixLevel();
            r.FixName();

            rs.Add(r);
        }

        // 有可能需要覆盖数据
        rs.Save(true);

        return rs;
    }

    private static Int32 GetParent(Int32 id)
    {
        if (id % 10000 == 0) return 0;
        if (id % 100 == 0) return id - (id % 10000);
        if (id <= 99_99_99) return id - (id % 100);

        return 0;
    }

    /// <summary>抓取并保存数据</summary>
    /// <param name="url">民政局行政区划统计数据</param>
    /// <returns></returns>
    public static Int32 FetchAndSave(String? url = null)
    {
        //if (url.IsNullOrEmpty()) url = "http://www.mca.gov.cn/article/sj/xzqh/2020/2020/2020092500801.html";
        //if (url.IsNullOrEmpty()) url = "https://www.mca.gov.cn/mzsj/xzqh/2022/202201xzqh.html";
        //if (url.IsNullOrEmpty()) url = "http://x.newlifex.com/202301xzqh.html";
        if (url.IsNullOrEmpty()) url = "http://x.newlifex.com/data/202401xzqh.html";

        var http = new HttpClient();
        var html = Task.Run(() => http.GetStringAsync(url)).Result;
        if (html.IsNullOrEmpty()) return 0;

        var rs = ParseAndSave(html);
        var count = rs.Count;

        //File.WriteAllText("area.csv", rs.Join("\r\n", e => $"{e.ID},{e.FullName}"));

        Meta.Session.ClearCache("FetchAndSave", false);

        //            // 拉取四级地区
        //            if (level4)
        //            {
        //#if __CORE__
        //                //Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        //                var encode = Encoding.GetEncoding("gb2312");
        //#else
        //                var encode = Encoding.Default;
        //#endif
        //                foreach (var item in rs)
        //                {
        //                    if (item.Level == 3)
        //                    {
        //                        var str = item.ID + "";
        //                        var url2 = $"http://www.stats.gov.cn/tjsj/tjbz/tjyqhdmhcxhfdm/2018/{str.Substring(0, 2)}/{str.Substring(2, 2)}/{str}.html";
        //                        XTrace.WriteLine("拉取[{0}/{1}]的四级地区 {2}", item.Name, item.ID, url2);

        //                        var buf = http.GetByteArrayAsync(url2).Result;
        //                        var html2 = encode.GetString(buf);
        //                        foreach (var elm in ParseLevel4(html2))
        //                        {
        //                            elm.ParentID = item.ID;

        //                            elm.FixLevel();
        //                            elm.FixName();

        //                            elm.SaveAsync();
        //                        }
        //                    }
        //                }
        //            }

        return count;
    }

    /// <summary>合并三级地区的数据</summary>
    /// <param name="list">外部数据源</param>
    /// <param name="addLose">是否添加缺失数据</param>
    /// <param name="enable">新增节点是否启用</param>
    /// <returns></returns>
    public static Int32 MergeLevel3(IList<Area> list, Boolean addLose, Boolean enable)
    {
        XTrace.WriteLine("合并三级地址：{0:n0}", list.Count);

        // 一次性加载三级地址
        var rs = FindAll(_.ID < 99_99_99);
        //var first = rs.Count == 0;

        var count = 0;
        var bs = new List<Area>();
        foreach (var r in list)
        {
            if (r.ID is < 10_00_00 or > 99_99_99) continue;

            //var r2 = FindByID(r.ID);
            var r2 = rs.FirstOrDefault(e => e.ID == r.ID);
            if (r2 == null)
            {
                if (!addLose) continue;

                if (r.ID == 441999 && r.Name == "东莞")
                {
                    r.Name = r.FullName = "直辖镇";
                    r.ParentID = 441900;
                    r.Enable = true;
                }
                else if (r.ID == 442099 && r.Name == "中山")
                {
                    r.Name = r.FullName = "直辖镇";
                    r.ParentID = 442000;
                    r.Enable = true;
                }
                else if (r.ID == 460499 && r.Name == "儋州")
                {
                    r.Name = r.FullName = "直辖镇";
                    r.ParentID = 460400;
                    r.Enable = true;
                }
                else if (r.ID == 620299 && r.Name == "嘉峪关")
                {
                    r.Name = r.FullName = "直辖镇";
                    r.ParentID = 620200;
                    r.Enable = true;
                }
                else
                {
                    // 三沙和港澳台强制启用
                    r.Enable = enable || r.ParentID == 460300 || r.ParentID > 700000 || r.Name.StartsWith("直辖");
                }

                XTrace.Log.Debug("新增 {0} {1} {2}", r.ID, r.Name, r.FullName);
                if (r.ParentID > 0 && !rs.Any(e => e.ID == r.ParentID)) XTrace.Log.Debug("未知父级 {0}", r.ParentID);

                r.PinYin = null;
                r.JianPin = null;
                //r.Enable = first;
                r.CreateTime = DateTime.Now;
                r.UpdateTime = DateTime.Now;
                r.Valid(DataMethod.Insert);
                //r.SaveAsync();

                bs.Add(r);
                rs.Add(r);

                count++;
            }
            else
            {
                if (r.FullName != r2.FullName) XTrace.Log.Debug("{0} {1} {2} => {3} {4}", r.ID, r.Name, r.FullName, r2.Name, r2.FullName);
                if (r.Name != r2.Name && r.Name.TrimEnd("市", "矿区", "林区", "区", "县") != r2.Name) XTrace.Log.Debug("{0} {1} {2} => {3} {4}", r.ID, r.Name, r.FullName, r2.Name, r2.FullName);

                // 合并字段
                if (!r.English.IsNullOrEmpty()) r2.English = r.English;
                if (!r.TelCode.IsNullOrEmpty()) r2.TelCode = r.TelCode;
                if (!r.ZipCode.IsNullOrEmpty()) r2.ZipCode = r.ZipCode;
                if (!r.Kind.IsNullOrEmpty()) r2.Kind = r.Kind;
                if (Math.Abs(r.Longitude) > 0.001) r2.Longitude = r.Longitude;
                if (Math.Abs(r.Latitude) > 0.001) r2.Latitude = r.Latitude;

                // 脏数据
                if (r2 is IEntity re && re.HasDirty)
                {
                    //r2.Valid(false);

                    XTrace.Log.Debug(re.Dirtys.Join(",", e => $"{e}={r2[e]}"));

                    //r2.SaveAsync();
                    bs.Add(r2);

                    count++;
                }
            }
        }

        //Meta.Factory.FullInsert = true;
        //count = bs.Save(true);
        count = bs.BatchUpsert(new BatchOption { FullInsert = true });

        XTrace.WriteLine("合并三级地址成功：{0:n0}", count);

        return count;
    }

    /// <summary>合并四级地区的数据</summary>
    /// <param name="list">外部数据源</param>
    /// <param name="addLose">是否添加缺失数据</param>
    /// <param name="enable">新增节点是否启用</param>
    /// <returns></returns>
    public static Int32 MergeLevel4(IList<Area> list, Boolean addLose, Boolean enable)
    {
        XTrace.WriteLine("合并四级地址：{0:n0}", list.Count);

        // 一次性加载四级地址
        var rs = FindAll(_.ID < 99_99_99_999);
        var bs = new List<Area>();

        var count = 0;
        foreach (var r in list)
        {
            if (r.ID is < 10_00_00_000 or > 99_99_99_999) continue;

            //var r2 = FindByID(r.ID);
            var r2 = rs.FirstOrDefault(e => e.ID == r.ID);
            if (r2 == null)
            {
                if (!addLose) continue;

                //XTrace.WriteLine("新增 {0} {1} {2}", r.ID, r.Name, r.FullName);
                if (r.ParentID > 0 && !rs.Any(e => e.ID == r.ParentID)) XTrace.Log.Debug("未知父级 {0}", r.ParentID);

                r.PinYin = null;
                r.JianPin = null;
                r.Enable = enable;
                r.CreateTime = DateTime.Now;
                r.UpdateTime = DateTime.Now;
                r.Valid(DataMethod.Insert);
                //r.SaveAsync();
                bs.Add(r);

                count++;
            }
            else
            {
                if (r.FullName != r2.FullName) XTrace.Log.Debug("{0} {1} {2} => {3} {4}", r.ID, r.Name, r.FullName, r2.Name, r2.FullName);
                if (r.Name != r2.Name && r.Name.TrimEnd("街道") != r2.Name) XTrace.Log.Debug("{0} {1} {2} => {3} {4}", r.ID, r.Name, r.FullName, r2.Name, r2.FullName);

                // 合并字段
                if (!r.English.IsNullOrEmpty()) r2.English = r.English;
                if (!r.TelCode.IsNullOrEmpty()) r2.TelCode = r.TelCode;
                if (!r.ZipCode.IsNullOrEmpty()) r2.ZipCode = r.ZipCode;
                if (!r.Kind.IsNullOrEmpty()) r2.Kind = r.Kind;
                if (Math.Abs(r.Longitude) > 0.001) r2.Longitude = r.Longitude;
                if (Math.Abs(r.Latitude) > 0.001) r2.Latitude = r.Latitude;

                // 脏数据
                if (r2 is IEntity re && re.HasDirty)
                {
                    //r2.Valid(false);

                    XTrace.Log.Debug(re.Dirtys.Join(",", e => $"{e}={r2[e]}"));

                    //r2.SaveAsync();
                    bs.Add(r2);

                    count++;
                }
            }
        }

        //Meta.Factory.FullInsert = true;
        //count = bs.Save(true);
        count = bs.BatchUpsert(new BatchOption { FullInsert = true });

        XTrace.WriteLine("合并四级地址成功：{0:n0}", count);

        return count;
    }

    /// <summary>从Csv文件导入并合并数据</summary>
    /// <param name="csvFile">Csv文件</param>
    /// <param name="addLose">是否添加缺失数据</param>
    /// <param name="level">需要导入的最高等级</param>
    /// <param name="enable">新增节点是否启用</param>
    /// <returns></returns>
    public static Int32 Import(String csvFile, Boolean addLose, Int32 level, Boolean enable)
    {
        var list = new List<Area>();

        if (csvFile.StartsWithIgnoreCase("http://", "https://"))
        {
            var http = new HttpClient();
            var stream = Task.Run(() => http.GetStreamAsync(csvFile)).Result;
            if (csvFile.EndsWithIgnoreCase(".gz"))
            {
                stream = new GZipStream(stream, CompressionMode.Decompress, true);
                stream = new BufferedStream(stream);
            }
            list = Meta.Factory.LoadCsv(stream).Cast<Area>().ToList();
        }
        else
            list.LoadCsv(csvFile);

        var count = 0;
        count += MergeLevel3(list, addLose, enable);

        Meta.Session.ClearCache("Import", false);

        // 等待异步写入的数据，导入四级地址时要做校验
        var retry = 20;
        while (retry-- > 0 && Area.FindCount() < 3639) Thread.Sleep(500);

        if (level >= 4) count += MergeLevel4(list, addLose, enable);

        Meta.Session.ClearCache("Import", false);

        return count;
    }

    /// <summary>导出数据到Csv文件</summary>
    /// <param name="csvFile"></param>
    /// <param name="level"></param>
    /// <returns></returns>
    public static Int32 Export(String csvFile, Int32 level = 4)
    {
        // Id抽取器
        var extracter = new IdExtracter(Meta.Session.Dal, Meta.TableName, _.ID.Field);
        extracter.Builder.Where = _.Level <= level;

        // 得到数据迭代
        var data = extracter.Fetch().Select(e => LoadData(e)).SelectMany(e => e);

        // 不要某些字段
        var fields = Meta.Factory.FieldNames.ToList();
        fields.Remove(nameof(CreateTime));
        fields.Remove(nameof(UpdateTime));
        fields.Remove(nameof(Remark));

        // 数据迭代保存到文件
        data.SaveCsv(csvFile, fields.ToArray());

        return extracter.TotalCount;
    }
    #endregion

    #region 大区
    private static readonly Dictionary<String, String[]> _big = new()
    {
        { "华北", new[] { "北京", "天津", "河北", "山西", "内蒙古" } },
        { "东北", new[] { "辽宁", "吉林", "黑龙江" } },
        { "华东", new[] { "上海", "江苏", "浙江", "安徽", "福建", "江西", "山东" } },
        { "华中", new[] { "河南", "湖北", "湖南" } },
        { "华南", new[] { "广东", "广西", "海南" } },
        { "西南", new[] { "重庆", "四川", "贵州", "云南", "西藏" } },
        { "西北", new[] { "陕西", "甘肃", "青海", "宁夏", "新疆" } },
        { "港澳台", new[] { "香港", "澳门", "台湾" } }
    };

    /// <summary>所属大区</summary>
    /// <returns></returns>
    public String GetBig()
    {
        foreach (var item in _big)
        {
            if (item.Value.Any(e => e == Name)) return item.Key;
        }

        return null;
    }
    #endregion

    #region 简写名称
    /// <summary>修正等级</summary>
    public void FixLevel()
    {
        // 计算父级编号和层级
        var id = ID;
        if (id <= 99_99_99)
        {
            if (id % 10000 == 0)
                Level = 1;
            else if (id % 100 == 0)
                Level = 2;
            else
                Level = 3;
        }
        else if (id <= 99_99_99_999)
            Level = 4;
    }

    private static readonly String[] minzu = ["汉族", "壮族", "满族", "回族", "苗族", "维吾尔族", "土家族", "彝族", "蒙古族", "藏族", "布依族", "侗族", "瑶族", "朝鲜族", "白族", "哈尼族", "哈萨克族", "黎族", "傣族", "畲族", "傈僳族", "仡佬族", "东乡族", "高山族", "拉祜族", "水族", "佤族", "纳西族", "羌族", "土族", "仫佬族", "锡伯族", "柯尔克孜族", "达斡尔族", "景颇族", "毛南族", "撒拉族", "布朗族", "塔吉克族", "阿昌族", "普米族", "鄂温克族", "怒族", "京族", "基诺族", "德昂族", "保安族", "俄罗斯族", "裕固族", "乌孜别克族", "门巴族", "鄂伦春族", "独龙族", "塔塔尔族", "赫哲族", "珞巴族"];

    private static readonly Dictionary<String, String> _map = new()
    {
        { "市辖区", "市辖区" },
        { "直辖县", "直辖县" },
        { "直辖镇", "直辖镇" },
        { "万柏林区", "万柏林" },
        { "白云鄂博矿区", "白云矿区" },
        { "沈北新区", "沈北新区" },
        { "金林区", "金林区" },
        { "士林区", "士林区" },
        { "杉林区", "杉林区" },
        { "茂林区", "茂林区" },
        { "坪林区", "坪林区" },
        { "树林区", "树林区" },
        { "巴林左旗", "左旗" },
        { "巴林右旗", "右旗" },
        { "克什克腾旗", "克旗" },
        { "土默特左旗", "土左旗" },
        { "土默特右旗", "土右旗" },
        { "达尔罕茂明安联合旗", "达茂旗" },
        { "阿鲁科尔沁旗", "阿旗" },
        { "翁牛特旗", "翁旗" },
        { "喀喇沁旗", "喀旗" },
        { "科尔沁左翼中旗", "科左中旗" },
        { "科尔沁左翼后旗", "科左后旗" },
        { "鄂托克前旗", "鄂前旗" },
        { "杭锦旗", "杭锦旗" },
        { "乌审旗", "乌审旗" },
        { "阿荣旗", "阿荣旗" },
        { "莫力达瓦达斡尔族自治旗", "莫旗" },
        { "鄂温克族自治旗", "鄂温克旗" },
        { "陈巴尔虎旗", "陈旗" },
        { "新巴尔虎左旗", "新左旗" },
        { "新巴尔虎右旗", "新右旗" },
        { "乌拉特前旗", "乌前旗" },
        { "乌拉特中旗", "乌中旗" },
        { "乌拉特后旗", "乌后旗" },
        { "杭锦后旗", "杭锦后旗" },
        { "察哈尔右翼前旗", "察右前旗" },
        { "察哈尔右翼中旗", "察右中旗" },
        { "察哈尔右翼后旗", "察右后旗" },
        { "四子王旗", "四子王旗" },
        { "科尔沁右翼前旗", "科右前旗" },
        { "科尔沁右翼中旗", "科右中旗" },
        { "扎赉特旗", "扎赉特旗" },
        { "阿巴嘎旗", "阿巴嘎旗" },
        { "苏尼特左旗", "东苏旗" },
        { "苏尼特右旗", "西苏旗" },
        { "东乌珠穆沁旗", "东乌旗" },
        { "西乌珠穆沁旗", "西乌旗" },
        { "太仆寺旗", "太旗" },
        { "镶黄旗", "镶黄旗" },
        { "正镶白旗", "正镶白旗" },
        { "正蓝旗", "正蓝旗" },
        { "阿拉善左旗", "阿左旗" },
        { "阿拉善右旗", "阿右旗" },
        { "六枝特区", "六枝" },
        { "博尔塔拉蒙古自治州", "博州" },
        { "巴音郭楞蒙古自治州", "巴州" },
        { "克孜勒苏柯尔克孜自治州", "克州" },
    };

    /// <summary>修正名称</summary>
    public void FixName()
    {
        if (FullName.IsNullOrEmpty()) return;

        var name = FullName;
        if (_map.TryGetValue(name, out var shortName))
        {
            name = shortName;
        }
        else
        {
            if (ParentID == 0)
            {
                name = name.TrimEnd("省", "市", "自治区", "壮族", "回族", "维吾尔", "特别行政区");
            }
            else if (Level <= 3)
            {
                if (name.Length > 8) name = name.TrimEnd("经济技术开发区");
                if (name.Length > 6) name = name.TrimEnd("技术开发区", "经济开发区", "产业开发区", "旅游开发区");
                if (name.Length > 4) name = name.TrimEnd("开发区", "管理区", "风景区");
                if (name.Length > 3) name = name.TrimEnd("地区", "林区", "矿区", "新区");
                if (name.Length > 2) name = name.TrimName("县", "市", "区", "盟", "旗", "州");

                if (name.Length > 3) name = name.TrimEnd("自治", "联合", "各族");

                for (var i = 0; i < minzu.Length; i++)
                {
                    var item = minzu[i];
                    // 去掉结尾的民族名称
                    if (name.Length > item.Length + 1 && name.EndsWith(item))
                    {
                        name = name.TrimEnd(item);
                        i = -1;
                    }
                    else if (item.Length >= 3 && item.EndsWith("族"))
                    {
                        item = item.TrimEnd('族');
                        if (name.Length > item.Length + 1 && name.EndsWith(item))
                        {
                            name = name.TrimEnd(item);
                            i = -1;
                        }
                    }
                }

                // 数据错误导致多一个字
                if (name.Length > 2) name = name.TrimEnd("自");
            }
            else if (Level == 4)
            {
                if (name.Length > 3)
                    name = name.TrimEnd("街道");
                else if (name.Length > 2)
                    name = name.TrimEnd("乡", "镇");
            }
        }

        Name = name;
    }
    #endregion
}

internal static class FixHelper
{
    public static String TrimName(this String name, params String[] ss)
    {
        foreach (var item in ss)
        {
            if (name.Length <= 2) break;

            name = name.TrimEnd(item);
        }

        return name;
    }
}