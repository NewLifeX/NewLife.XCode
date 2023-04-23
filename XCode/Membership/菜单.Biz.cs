using System.Reflection;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Collections;
using NewLife.Log;
using NewLife.Reflection;

namespace XCode.Membership;

/// <summary>菜单</summary>
[EntityFactory(typeof(MenuFactory))]
public partial class Menu : EntityTree<Menu>, IMenu
{
    #region 对象操作
    static Menu()
    {
        // 引发内部
        new Menu();

        //EntityFactory.Register(typeof(Menu), new MenuFactory());

        //ObjectContainer.Current.AutoRegister<IMenuFactory, MenuFactory>();

        Meta.Modules.Add<UserModule>();
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();
    }

    /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否新数据</param>
    public override void Valid(Boolean isNew)
    {
        if (String.IsNullOrEmpty(Name)) throw new ArgumentNullException(__.Name, _.Name.DisplayName + "不能为空！");

        base.Valid(isNew);

        if (Icon == "&#xe63f;") Icon = null;

        SavePermission();
    }

    /// <summary>已重载。调用Save时写日志，而调用Insert和Update时不写日志</summary>
    /// <returns></returns>
    public override Int32 Save()
    {
        // 先处理一次，否则可能因为别的字段没有修改而没有脏数据
        SavePermission();

        //if (Icon.IsNullOrWhiteSpace()) Icon = "&#xe63f;";

        // 更改日志保存顺序，先保存才能获取到id
        var action = "添加";
        var isNew = IsNullKey;
        if (!isNew)
        {
            // 没有修改时不写日志
            if (!HasDirty) return 0;

            action = "修改";

            // 必须提前写修改日志，否则修改后脏数据失效，保存的日志为空
            LogProvider.Provider.WriteLog(action, this);
        }

        var result = base.Save();

        if (isNew) LogProvider.Provider.WriteLog(action, this);

        return result;
    }

    /// <summary>删除。</summary>
    /// <returns></returns>
    protected override Int32 OnDelete()
    {
        var err = "";
        try
        {
            // 递归删除子菜单
            var rs = 0;
            using var ts = Meta.CreateTrans();
            rs += base.OnDelete();

            var ms = Childs;
            if (ms != null && ms.Count > 0)
            {
                foreach (var item in ms)
                {
                    rs += item.Delete();
                }
            }

            ts.Commit();

            return rs;
        }
        catch (Exception ex)
        {
            err = ex.Message;
            throw;
        }
        finally
        {
            LogProvider.Provider.WriteLog("删除", this, err);
        }
    }

    /// <summary>加载权限字典</summary>
    protected override void OnLoad()
    {
        base.OnLoad();

        // 构造权限字典
        LoadPermission();
    }

    /// <summary>如果Permission被修改，则重新加载</summary>
    /// <param name="fieldName"></param>
    protected override void OnPropertyChanged(String fieldName)
    {
        base.OnPropertyChanged(fieldName);

        if (fieldName == __.Permission) LoadPermission();
    }
    #endregion

    #region 扩展属性
    /// <summary></summary>
    public String Url2 => Url?.Replace("~", "");

    /// <summary>父菜单名</summary>
    public virtual String ParentMenuName { get => Parent?.Name; set { } }

    /// <summary>必要的菜单。必须至少有角色拥有这些权限，如果没有则自动授权给系统角色</summary>
    internal static Int32[] Necessaries
    {
        get
        {
            // 找出所有的必要菜单，如果没有，则表示全部都是必要
            var list = FindAllWithCache();
            var list2 = list.Where(e => e.Necessary).ToList();
            if (list2.Count > 0) list = list2;

            return list.Select(e => e.ID).ToArray();
        }
    }

    /// <summary>友好名称。优先显示名</summary>
    public String FriendName => DisplayName.IsNullOrWhiteSpace() ? Name : DisplayName;
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static Menu FindByID(Int32 id)
    {
        if (id <= 0) return null;

        return Meta.Cache.Find(e => e.ID == id);
    }

    /// <summary>根据名字查找</summary>
    /// <param name="name">名称</param>
    /// <returns></returns>
    public static Menu FindByName(String name) => Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));

    /// <summary>根据全名查找</summary>
    /// <param name="name">全名</param>
    /// <returns></returns>
    public static Menu FindByFullName(String name) => Meta.Cache.Find(e => e.FullName.EqualIgnoreCase(name));

    /// <summary>根据Url查找</summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public static Menu FindByUrl(String url) => Meta.Cache.Find(e => e.Url.EqualIgnoreCase(url));

    /// <summary>根据名字查找，支持路径查找</summary>
    /// <param name="name">名称</param>
    /// <returns></returns>
    public static Menu FindForName(String name)
    {
        var entity = FindByName(name);
        if (entity != null) return entity;

        return Root.FindByPath(name, _.Name, _.DisplayName);
    }

    /// <summary>查找指定菜单的子菜单</summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static List<Menu> FindAllByParentID(Int32 id) => Meta.Cache.FindAll(e => e.ParentID == id).OrderByDescending(e => e.Sort).ThenBy(e => e.ID).ToList();

    /// <summary>取得当前角色的子菜单，有权限、可显示、排序</summary>
    /// <param name="filters"></param>
    /// <param name="inclInvisible">包含不可见菜单</param>
    /// <returns></returns>
    public IList<IMenu> GetSubMenus(Int32[] filters, Boolean inclInvisible = false)
    {
        var list = Childs;
        if (list == null || list.Count <= 0) return new List<IMenu>();

        if (!inclInvisible) list = list.Where(e => e.Visible).ToList();
        if (list == null || list.Count <= 0) return new List<IMenu>();

        return list.Where(e => filters.Contains(e.ID)).Cast<IMenu>().ToList();
    }

    /// <summary>根据名称查找</summary>
    /// <param name="name">名称</param>
    /// <returns>实体列表</returns>
    public static IList<Menu> FindAllByName(String name)
    {
        if (name.IsNullOrEmpty()) return new List<Menu>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Name.EqualIgnoreCase(name));

        return FindAll(_.Name == name);
    }

    /// <summary>根据父编号、名称查找</summary>
    /// <param name="parentId">父编号</param>
    /// <param name="name">名称</param>
    /// <returns>实体对象</returns>
    public static Menu FindByParentIDAndName(Int32 parentId, String name)
    {

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ParentID == parentId && e.Name.EqualIgnoreCase(name));

        return Find(_.ParentID == parentId & _.Name == name);
    }
    #endregion

    #region 扩展操作
    /// <summary>添加子菜单</summary>
    /// <param name="name"></param>
    /// <param name="displayName"></param>
    /// <param name="fullName"></param>
    /// <param name="url"></param>
    /// <returns></returns>
    public IMenu Add(String name, String displayName, String fullName, String url)
    {
        var entity = new Menu
        {
            Name = name,
            DisplayName = displayName,
            FullName = fullName,
            Url = url,
            ParentID = ID,

            Visible = ID == 0 || displayName != null
        };

        entity.Save();

        return entity;
    }
    #endregion

    #region 扩展权限
    /// <summary>可选权限子项</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    public Dictionary<Int32, String> Permissions { get; set; } = new Dictionary<Int32, String>();

    private void LoadPermission()
    {
        Permissions.Clear();
        if (String.IsNullOrEmpty(Permission)) return;

        var dic = Permission.SplitAsDictionary("#", ",");
        foreach (var item in dic)
        {
            var resid = item.Key.ToInt();
            Permissions[resid] = item.Value;
        }
    }

    private void SavePermission()
    {
        // 不能这样子直接清空，因为可能没有任何改变，而这么做会两次改变脏数据，让系统以为有改变
        //Permission = null;
        if (Permissions.Count <= 0)
        {
            //Permission = null;
            SetItem(__.Permission, null);
            return;
        }

        var sb = Pool.StringBuilder.Get();
        // 根据资源按照从小到大排序一下
        foreach (var item in Permissions.OrderBy(e => e.Key))
        {
            if (sb.Length > 0) sb.Append(',');
            sb.AppendFormat("{0}#{1}", item.Key, item.Value);
        }
        SetItem(__.Permission, sb.Put(true));
    }
    #endregion

    #region 日志
    ///// <summary>写日志</summary>
    ///// <param name="action">操作</param>
    ///// <param name="remark">备注</param>
    //public static void WriteLog(String action, String remark) => LogProvider.Provider.WriteLog(typeof(Menu), action, remark);
    #endregion

    #region 辅助
    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String ToString()
    {
        var path = GetFullPath(true, "\\", e => e.FriendName);
        if (!path.IsNullOrEmpty()) return path;

        return FriendName;
    }
    #endregion

    #region IMenu 成员
    /// <summary>取得全路径的实体，由上向下排序</summary>
    /// <param name="includeSelf">是否包含自己</param>
    /// <param name="separator">分隔符</param>
    /// <param name="func">回调</param>
    /// <returns></returns>
    String IMenu.GetFullPath(Boolean includeSelf, String separator, Func<IMenu, String> func)
    {
        Func<Menu, String> d = null;
        if (func != null) d = item => func(item);

        return GetFullPath(includeSelf, separator, d);
    }

    //IMenu IMenu.Add(String name, String displayName, String fullName, String url) => Add(name, displayName, fullName, url);

    /// <summary>父菜单</summary>
    IMenu IMenu.Parent => Parent;

    /// <summary>子菜单</summary>
    IList<IMenu> IMenu.Childs => Childs.OfType<IMenu>().ToList();

    /// <summary>子孙菜单</summary>
    IList<IMenu> IMenu.AllChilds => AllChilds.OfType<IMenu>().ToList();

    /// <summary>根据层次路径查找</summary>
    /// <param name="path">层次路径</param>
    /// <returns></returns>
    IMenu IMenu.FindByPath(String path) => FindByPath(path, _.Name, _.DisplayName);
    #endregion

    #region 菜单工厂
    /// <summary>菜单工厂</summary>
    public class MenuFactory : DefaultEntityFactory, IMenuFactory
    {
        #region IMenuFactory 成员
        IMenu IMenuFactory.Root => Root;

        /// <summary>根据编号找到菜单</summary>
        /// <param name="id"></param>
        /// <returns></returns>
        IMenu IMenuFactory.FindByID(Int32 id) => FindByID(id);

        /// <summary>根据Url找到菜单</summary>
        /// <param name="url"></param>
        /// <returns></returns>
        IMenu IMenuFactory.FindByUrl(String url) => FindByUrl(url);

        /// <summary>根据全名找到菜单</summary>
        /// <param name="fullName"></param>
        /// <returns></returns>
        IMenu IMenuFactory.FindByFullName(String fullName) => FindByFullName(fullName);

        /// <summary>获取指定菜单下，当前用户有权访问的子菜单。</summary>
        /// <param name="menuid"></param>
        /// <param name="user"></param>
        /// <param name="inclInvisible">是否包含不可见菜单</param>
        /// <returns></returns>
        IList<IMenu> IMenuFactory.GetMySubMenus(Int32 menuid, IUser user, Boolean inclInvisible)
        {
            var factory = this as IMenuFactory;
            var root = factory.Root;

            // 当前用户
            //var user = ManageProvider.Provider.Current as IUser;
            var rs = user?.Roles;
            if (rs == null || rs.Length == 0) return new List<IMenu>();

            IMenu menu = null;

            // 找到菜单
            if (menuid > 0) menu = FindByID(menuid);

            if (menu == null)
            {
                menu = root;
                if (menu == null || menu.Childs == null || menu.Childs.Count <= 0) return new List<IMenu>();
            }

            return menu.GetSubMenus(rs.SelectMany(e => e.Resources).ToArray(), inclInvisible);
        }
        #endregion
    }
    #endregion
}

/// <summary>菜单接口</summary>
public partial interface IMenu
{
    /// <summary>取得全路径的实体，由上向下排序</summary>
    /// <param name="includeSelf">是否包含自己</param>
    /// <param name="separator">分隔符</param>
    /// <param name="func">回调</param>
    /// <returns></returns>
    String GetFullPath(Boolean includeSelf, String separator, Func<IMenu, String> func);

    /// <summary>添加子菜单</summary>
    /// <param name="name"></param>
    /// <param name="displayName"></param>
    /// <param name="fullName"></param>
    /// <param name="url"></param>
    /// <returns></returns>
    IMenu Add(String name, String displayName, String fullName, String url);

    /// <summary>父菜单</summary>
    IMenu Parent { get; }

    /// <summary>子菜单</summary>
    IList<IMenu> Childs { get; }

    /// <summary>子孙菜单</summary>
    IList<IMenu> AllChilds { get; }

    /// <summary>根据层次路径查找。因为需要指定在某个菜单子级查找路径，所以是成员方法而不是静态方法</summary>
    /// <param name="path">层次路径</param>
    /// <returns></returns>
    IMenu FindByPath(String path);

    /// <summary>排序上升</summary>
    void Up();

    /// <summary>排序下降</summary>
    void Down();

    /// <summary></summary>
    /// <param name="filters"></param>
    /// <param name="inclInvisible">是否包含不可见菜单</param>
    /// <returns></returns>
    IList<IMenu> GetSubMenus(Int32[] filters, Boolean inclInvisible);

    /// <summary>可选权限子项</summary>
    Dictionary<Int32, String> Permissions { get; }
}

//partial class MenuModel
//{
//    IMenu IMenu.Parent => throw new NotImplementedException();

//    IList<IMenu> IMenu.Childs => throw new NotImplementedException();

//    IList<IMenu> IMenu.AllChilds => throw new NotImplementedException();

//    Dictionary<Int32, String> IMenu.Permissions => throw new NotImplementedException();

//    String IMenu.GetFullPath(Boolean includeSelf, String separator, Func<IMenu, String> func) => throw new NotImplementedException();
//    IMenu IMenu.Add(String name, String displayName, String fullName, String url) => throw new NotImplementedException();
//    IMenu IMenu.FindByPath(String path) => throw new NotImplementedException();
//    void IMenu.Up() => throw new NotImplementedException();
//    void IMenu.Down() => throw new NotImplementedException();
//    IList<IMenu> IMenu.GetSubMenus(Int32[] filters, Boolean inclInvisible) => throw new NotImplementedException();
//}