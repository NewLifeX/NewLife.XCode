using System;
using System.Linq;
using XCode.Code;
using Xunit;

namespace XUnitTest.XCode.Code;

public class MemberSectionTests
{
    [Fact]
    public void Parse()
    {
        var code =
        """

            #region 扩展属性
            /// <summary>部门</summary>
            [XmlIgnore, IgnoreDataMember, ScriptIgnore]
            public Department Department => Extends.Get(nameof(Department), k => Department.FindByID(DepartmentID));

            /// <summary>部门</summary>
            [Map(nameof(DepartmentID), typeof(Department), "ID")]
            [Category("登录信息")]
            public String DepartmentName => Department?.Name;
            #endregion

            #region 扩展查询
            /// <summary>根据编号查找</summary>
            /// <param name="id">编号</param>
            /// <returns>实体对象</returns>
            public static User FindByID(Int32 id)
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
            /// <returns>实体对象</returns>
            public static User FindByName(String name)
            {
                if (name.IsNullOrEmpty()) return null;

                // 实体缓存
                if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));

                // 单对象缓存
                //return Meta.SingleCache.GetItemWithSlaveKey(name) as User;

                return Find(_.Name == name);
            }
            /// <summary>根据邮件查找</summary>
            /// <param name="mail">邮件</param>
            /// <returns>实体列表</returns>
            public static IList<User> FindAllByMail(String mail)
            {
                if (mail.IsNullOrEmpty()) return new List<User>();

                // 实体缓存
                if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Mail.EqualIgnoreCase(mail));

                return FindAll(_.Mail == mail);
            }
            /// <summary>根据手机查找</summary>
            /// <param name="mobile">手机</param>
            /// <returns>实体列表</returns>
            public static IList<User> FindAllByMobile(String mobile)
            {
                if (mobile.IsNullOrEmpty()) return new List<User>();

                // 实体缓存
                if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Mobile.EqualIgnoreCase(mobile));

                return FindAll(_.Mobile == mobile);
            }

            ///// <summary>根据代码查找</summary>
            ///// <param name="code">代码</param>
            ///// <returns>实体列表</returns>
            //public static IList<User> FindAllByCode(String code)
            //{
            //    if (code.IsNullOrEmpty()) return new List<User>();
            //
            //    // 实体缓存
            //    if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Code.EqualIgnoreCase(code));
            //
            //    return FindAll(_.Code == code);
            //}

            /// <summary>根据角色查找</summary>
            /// <param name="roleId">角色</param>
            /// <returns>实体列表</returns>
            public static IList<User> FindAllByRoleID(Int32 roleId)
            {
                if (roleId <= 0) return new List<User>();

                // 实体缓存
                if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.RoleID == roleId);

                return FindAll(_.RoleID == roleId);
            }
            #endregion


        """;

        var lines = code.Split(Environment.NewLine);

        var list = MemberSection.Parse(lines);

        Assert.NotNull(list);
        Assert.Equal(8, list.Count);

        Assert.Equal("Department", list[0].Name);
        Assert.Equal("DepartmentName", list[1].Name);

        Assert.Equal("FindByID", list[2].Name);
        Assert.Equal("FindByName", list[3].Name);
        Assert.Equal("FindAllByMail", list[4].Name);
        Assert.Equal("FindAllByMobile", list[5].Name);
        Assert.Equal("FindAllByCode", list[6].Name);
        Assert.Equal("FindAllByRoleID", list[7].Name);

        Assert.Equal("FindByID(Int32 id)", list[2].FullName);
        Assert.Equal("FindByName(String name)", list[3].FullName);
        Assert.Equal("FindAllByMail(String mail)", list[4].FullName);
        Assert.Equal("FindAllByMobile(String mobile)", list[5].FullName);
        Assert.Equal("FindAllByCode(String code)", list[6].FullName);
        Assert.Equal("FindAllByRoleID(Int32 roleId)", list[7].FullName);

        //Assert.Equal(lines.Length, list.Sum(e => e.Lines.Length));
    }

    [Fact]
    public void Parse2()
    {
        var code =
"""

    #region 扩展属性
    /// <summary>顶级根。它的Childs就是各个省份</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public static Area Root { get; } = new Area();

    /// <summary>父级</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Area Parent => Extends.Get(nameof(Parent), k => FindByID(ParentID) ?? Root);

    /// <summary>所有父级</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public IList<Area> AllParents => Extends.Get(nameof(AllParents), k =>
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

        return list;
    });

    /// <summary>父级路径</summary>
    public String ParentPath
    {
        get
        {
            var list = AllParents;
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
    public IList<Area> Childs => Extends.Get(nameof(Childs), k => FindAllByParentID(ID).Where(e => e.Enable).ToList());

    /// <summary>子孙级区域。支持省市区，不支持乡镇街道</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public IList<Area> AllChilds => Extends.Get(nameof(AllChilds), k =>
    {
        var list = new List<Area>();
        foreach (var item in Childs)
        {
            list.Add(item);
            if (item.Level < 3) list.AddRange(item.AllChilds);
        }
        return list;
    });

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

""";

        var lines = code.Split(Environment.NewLine);

        var list = MemberSection.Parse(lines);

        Assert.NotNull(list);
        Assert.Equal(16, list.Count);

        Assert.Equal("Root", list[0].Name);
        Assert.Equal("Parent", list[1].Name);
        Assert.Equal("AllParents", list[2].Name);
        Assert.Equal("ParentPath", list[3].Name);
        Assert.Equal("Path", list[4].Name);
        Assert.Equal("Childs", list[5].Name);
        Assert.Equal("AllChilds", list[6].Name);
        Assert.Equal("IsVirtual", list[7].Name);

        Assert.Equal("FindByID", list[8].Name);
        Assert.Equal("FindByIDs", list[9].Name);
        Assert.Equal("FindByName", list[10].Name);
        Assert.Equal("FindAllByName", list[11].Name);
        Assert.Equal("FindByNames", list[12].Name);
        Assert.Equal("FindByFullName", list[13].Name);
        Assert.Equal("_pcache", list[14].Name);
        Assert.Equal("FindAllByParentID", list[15].Name);

        Assert.Equal("FindByID(Int32 id)", list[8].FullName);
        Assert.Equal("FindByIDs(params Int32[] ids)", list[9].FullName);
        Assert.Equal("FindByName(Int32 parentId, String name)", list[10].FullName);
        Assert.Equal("FindAllByName(String name)", list[11].FullName);
        Assert.Equal("FindByNames(params String[] names)", list[12].FullName);
        Assert.Equal("FindByFullName(String name)", list[13].FullName);
        Assert.Equal("_pcache", list[14].FullName);
        Assert.Equal("FindAllByParentID(Int32 parentid)", list[15].FullName);

        //Assert.Equal(lines.Length, list.Sum(e => e.Lines.Length));
    }

    [Fact]
    public void Parse3()
    {
        var code =
"""
    #region 扩展属性
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Product Product => Extends.Get(nameof(Product), k => Product.FindById(ProductId));

    [Map(nameof(ProductId), typeof(Product), "Id")]
    public String ProductName => Product?.Name;

    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public DeviceGroup Group => Extends.Get(nameof(Group), k => DeviceGroup.FindById(GroupId));

    [Map(nameof(GroupId), typeof(DeviceGroup), "Id")]
    public String GroupPath => Group?.Name;

    [Category("基本信息")]
    [Map(nameof(CityId))]
    public String AreaName => Area.FindByIDs(CityId, ProvinceId)?.Path;

    /// <summary>父级设备</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    //[ScriptIgnore]
    public Device Parent => Extends.Get(nameof(Parent), k => Device.FindById(ParentId));

    /// <summary>父级设备</summary>
    [Map(nameof(ParentId), typeof(Device), "Id")]
    public String ParentName => Parent?.Name;

    /// <summary>子设备。借助扩展属性缓存</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public IList<Device> Childs => Extends.Get(nameof(Childs), k => FindAllByParent(Id));

    /// <summary>设备属性。借助扩展属性缓存</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public IList<DeviceProperty> Properties => Extends.Get(nameof(Properties), k => DeviceProperty.FindAllByDeviceId(Id));

    /// <summary>设备服务。借助扩展属性缓存</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public IList<DeviceService> Services => Extends.Get(nameof(Services), k => DeviceService.FindAllByDeviceId(Id));

    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    protected Boolean IgnoreVaild { get; set; } = false;
    #endregion
""";

        var lines = code.Split(Environment.NewLine);

        var list = MemberSection.Parse(lines);

        Assert.NotNull(list);
        Assert.Equal(11, list.Count);

        Assert.Equal("Product", list[0].Name);
        Assert.Equal("ProductName", list[1].Name);
        Assert.Equal("Group", list[2].Name);
        Assert.Equal("GroupPath", list[3].Name);
        Assert.Equal("AreaName", list[4].Name);
        Assert.Equal("Parent", list[5].Name);
        Assert.Equal("ParentName", list[6].Name);
        Assert.Equal("Childs", list[7].Name);
        Assert.Equal("Properties", list[8].Name);
        Assert.Equal("Services", list[9].Name);
        Assert.Equal("IgnoreVaild", list[10].Name);
    }

    [Fact]
    public void GetMethods()
    {
        var code =
        """
            #region 扩展属性
            /// <summary>部门</summary>
            [XmlIgnore, IgnoreDataMember, ScriptIgnore]
            public Department Department => Extends.Get(nameof(Department), k => Department.FindByID(DepartmentID));

            /// <summary>部门</summary>
            [Map(nameof(DepartmentID), typeof(Department), "ID")]
            [Category("登录信息")]
            public String DepartmentName => Department?.Name;
            #endregion

            #region 扩展查询
            /// <summary>根据编号查找</summary>
            /// <param name="id">编号</param>
            /// <returns>实体对象</returns>
            public static User FindByID(Int32 id)
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
            /// <returns>实体对象</returns>
            public static User FindByName(String name)
            {
                if (name.IsNullOrEmpty()) return null;

                // 实体缓存
                if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));

                // 单对象缓存
                //return Meta.SingleCache.GetItemWithSlaveKey(name) as User;

                return Find(_.Name == name);
            }
            /// <summary>根据邮件查找</summary>
            /// <param name="mail">邮件</param>
            /// <returns>实体列表</returns>
            public static IList<User> FindAllByMail(String mail)
            {
                if (mail.IsNullOrEmpty()) return new List<User>();

                // 实体缓存
                if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Mail.EqualIgnoreCase(mail));

                return FindAll(_.Mail == mail);
            }
            /// <summary>根据手机查找</summary>
            /// <param name="mobile">手机</param>
            /// <returns>实体列表</returns>
            public static IList<User> FindAllByMobile(String mobile)
            {
                if (mobile.IsNullOrEmpty()) return new List<User>();

                // 实体缓存
                if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Mobile.EqualIgnoreCase(mobile));

                return FindAll(_.Mobile == mobile);
            }

            ///// <summary>根据代码查找</summary>
            ///// <param name="code">代码</param>
            ///// <returns>实体列表</returns>
            //public static IList<User> FindAllByCode(String code)
            //{
            //    if (code.IsNullOrEmpty()) return new List<User>();
            //
            //    // 实体缓存
            //    if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Code.EqualIgnoreCase(code));
            //
            //    return FindAll(_.Code == code);
            //}

            /// <summary>根据角色查找</summary>
            /// <param name="roleId">角色</param>
            /// <returns>实体列表</returns>
            public static IList<User> FindAllByRoleID(Int32 roleId)
            {
                if (roleId <= 0) return new List<User>();

                // 实体缓存
                if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.RoleID == roleId);

                return FindAll(_.RoleID == roleId);
            }
            #endregion

            public static IList<Area> FindAllByName(String name) => Meta.Cache.Entities.FindAll(e => e.Name == name || e.FullName == name);

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
        
        """;

        var list = MemberSection.GetMethods(code);

        Assert.NotNull(list);
        Assert.Equal(8, list.Count);

        Assert.Equal("FindByID", list[0].Name);
        Assert.Equal("FindByName", list[1].Name);
        Assert.Equal("FindAllByMail", list[2].Name);
        Assert.Equal("FindAllByMobile", list[3].Name);
        Assert.Equal("FindAllByCode", list[4].Name);
        Assert.Equal("FindAllByRoleID", list[5].Name);
        Assert.Equal("FindAllByName", list[6].Name);
        Assert.Equal("FindAllByParentIDAndName", list[7].Name);

        Assert.Equal("FindByID(Int32)", list[0].FullName);
        Assert.Equal("FindByName(String)", list[1].FullName);
        Assert.Equal("FindAllByMail(String)", list[2].FullName);
        Assert.Equal("FindAllByMobile(String)", list[3].FullName);
        Assert.Equal("FindAllByCode(String)", list[4].FullName);
        Assert.Equal("FindAllByRoleID(Int32)", list[5].FullName);
        Assert.Equal("FindAllByName(String)", list[6].FullName);
        Assert.Equal("FindAllByParentIDAndName(Int32,String)", list[7].FullName);

        //Assert.Equal(lines.Length, list.Sum(e => e.Lines.Length));
    }
}
