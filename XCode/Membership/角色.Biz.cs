using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife.Collections;
using NewLife.Log;

namespace XCode.Membership;

/// <summary>角色</summary>
public partial class Role : LogEntity<Role>, IRole, ITenantSource
{
    #region 对象操作

    static Role()
    {
        //Meta.Factory.FullInsert = false;

        Meta.Modules.Add<UserModule>();
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();
        Meta.Modules.Add<TenantModule>();
    }

    /// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected internal override void InitData()
    {
        if (Meta.Count > 0)
        {
            // 必须有至少一个可用的系统角色
            //var list = Meta.Cache.Entities.ToList();
            // InitData中用缓存将会导致二次调用InitData，从而有一定几率死锁
            var list = FindAll().ToList();
            if (list.Count > 0 && !list.Any(e => e.IsSystem))
            {
                // 如果没有，让第一个角色作为系统角色
                var role = list[0];
                role.IsSystem = true;
                role.Type = RoleTypes.系统;

                XTrace.WriteLine("必须有至少一个可用的系统角色，修改{0}为系统角色！", role.Name);

                role.Save();
            }
        }
        else
        {
            if (XTrace.Debug) XTrace.WriteLine("开始初始化{0}角色数据……", typeof(Role).Name);

            Add("管理员", true, RoleTypes.系统, DataScopes.全部, "默认拥有全部最高权限，由系统工程师使用，安装配置整个系统");
            //Add("租户管理员", false, "SAAS平台租户管理员");
            Add("高级用户", false, RoleTypes.普通, DataScopes.本部门及下级, "业务管理人员，可以管理业务模块，可以分配授权用户等级");
            Add("普通用户", false, RoleTypes.普通, DataScopes.本部门, "普通业务人员，可以使用系统常规业务模块功能");
            Add("游客", false, RoleTypes.普通, DataScopes.仅本人, "新注册默认属于游客");

            if (XTrace.Debug) XTrace.WriteLine("完成初始化{0}角色数据！", typeof(Role).Name);
        }

        //CheckRole();
        //// 当前处于事务之中，下面使用Menu会触发异步检查架构，SQLite单线程机制可能会造成死锁
        //ThreadPoolX.QueueUserWorkItem(CheckRole);
    }

    /// <summary>初始化时执行必要的权限检查，以防万一管理员无法操作</summary>
    public static void CheckRole()
    {
        // InitData中用缓存将会导致二次调用InitData，从而有一定几率死锁
        var list = FindAll();

        // 如果某些菜单已经被删除，但是角色权限表仍然存在，则删除
        var menus = Menu.FindAll();
        var ids = menus.Select(e => e.ID).ToArray();
        foreach (var role in list)
        {
            if (!role.CheckValid(ids)) XTrace.WriteLine("删除[{0}]中的无效资源权限！", role);
        }

        // 所有角色都有权进入管理平台，否则无法使用后台
        var menu = menus.FirstOrDefault(e => e.Name == "Admin" || e.Name == "System");
        if (menu != null)
        {
            foreach (var role in list)
            {
                role.Set(menu.ID, PermissionFlags.Detail);
            }
        }
        list.Save();

        // 系统角色
        var sys = list.Where(e => e.IsSystem).OrderBy(e => e.ID).FirstOrDefault();
        if (sys == null) return;

        // 如果没有任何角色拥有权限管理的权限，那是很悲催的事情
        var count = 0;
        foreach (var item in menus)
        {
            //if (item.Visible && !list.Any(e => e.Has(item.ID, PermissionFlags.Detail)))
            if (!list.Any(e => e.Has(item.ID, PermissionFlags.Detail)))
            {
                count++;
                sys.Set(item.ID, PermissionFlags.All);

                XTrace.WriteLine("没有任何角色拥有菜单[{0}]的权限", item.Name);
            }
        }
        if (count > 0)
        {
            XTrace.WriteLine("共有{0}个菜单，没有任何角色拥有权限，准备授权第一系统角色[{1}]拥有其完全管理权！", count, sys);
            sys.Save();

            // 更新缓存
            Meta.Cache.Clear("CheckRole", true);
        }
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        if (method == DataMethod.Delete) return true;

        if (Name.IsNullOrEmpty()) throw new ArgumentNullException(__.Name, _.Name.DisplayName + "不能为空！");

        if (Type == 0)
        {
            if (TenantId > 0)
                Type = RoleTypes.租户;
            else
                Type = IsSystem ? RoleTypes.系统 : RoleTypes.普通;
        }

        if (DataScope == 0)
        {
            DataScope = Type switch
            {
                RoleTypes.系统 => DataScopes.全部,
                RoleTypes.普通 => Name.Contains("高级") ? DataScopes.本部门及下级 : DataScopes.本部门,
                RoleTypes.租户 => DataScopes.本部门及下级,
                _ => DataScopes.仅本人,
            };
        }

        SavePermission();

        return base.Valid(method);
    }

    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override Int32 Delete()
    {
        var entity = this;
        var name = entity.Name;
        if (String.IsNullOrEmpty(name))
        {
            entity = FindByID(ID);
            if (entity != null) name = entity.Name;
        }

        if (Meta.Count <= 1 && FindCount() <= 1)
        {
            var msg = $"至少保留一个角色[{name}]禁止删除！";
            WriteLog("删除", true, msg);

            throw new XException(msg);
        }

        if (entity!.IsSystem)
        {
            var msg = $"系统角色[{name}]禁止删除！";
            WriteLog("删除", true, msg);

            throw new XException(msg);
        }

        return base.Delete();
    }

    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override Int32 Save()
    {
        // 先处理一次，否则可能因为别的字段没有修改而没有脏数据
        SavePermission();

        return base.Save();
    }

    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override Int32 Update()
    {
        // 先处理一次，否则可能因为别的字段没有修改而没有脏数据
        SavePermission();

        return base.Update();
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

    #endregion 对象操作

    #region 扩展查询

    /// <summary>根据编号查找角色</summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static Role? FindByID(Int32 id)
    {
        if (id <= 0 || Meta.Cache.Entities == null || Meta.Cache.Entities.Count <= 0) return null;

        return Meta.Cache.Entities.ToArray().FirstOrDefault(e => e.ID == id);
    }

    /// <summary>根据编号查找角色</summary>
    /// <param name="id"></param>
    /// <returns></returns>
    IRole? IRole.FindByID(Int32 id) => FindByID(id);

    /// <summary>根据名称查找角色</summary>
    /// <param name="name">名称</param>
    /// <returns></returns>
    public static Role? FindByName(String name)
    {
        if (String.IsNullOrEmpty(name) || Meta.Cache.Entities == null || Meta.Cache.Entities.Count <= 0) return null;

        return Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));
    }

    /// <summary>根据租户、名称查找</summary>
    /// <param name="tenantId">租户</param>
    /// <param name="name">名称</param>
    /// <returns>实体对象</returns>
    public static Role? FindByTenantIdAndName(Int32 tenantId, String name)
    {
        if (tenantId < 0) return null;
        if (name.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.TenantId == tenantId && e.Name.EqualIgnoreCase(name));

        return Find(_.TenantId == tenantId & _.Name == name);
    }

    /// <summary>根据租户查找</summary>
    /// <param name="tenantId">租户</param>
    /// <returns>实体列表</returns>
    public static IList<Role> FindAllByTenantId(Int32 tenantId)
    {
        if (tenantId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.TenantId == tenantId);

        return FindAll(_.TenantId == tenantId);
    }
    #endregion 扩展查询

    #region 扩展权限

    private IDictionary<Int32, PermissionFlags>? _Permissions;

    /// <summary>本角色权限集合</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public IDictionary<Int32, PermissionFlags> Permissions => _Permissions ??= new Dictionary<Int32, PermissionFlags>();

    /// <summary>是否拥有指定资源的指定权限</summary>
    /// <param name="resourceId"></param>
    /// <param name="flag"></param>
    /// <returns></returns>
    public Boolean Has(Int32 resourceId, PermissionFlags flag = PermissionFlags.None)
    {
        var pf = PermissionFlags.None;
        if (!Permissions.TryGetValue(resourceId, out pf)) return false;
        if (flag == PermissionFlags.None) return true;

        return pf.Has(flag);
    }

    private void Remove(Int32 resourceId)
    {
        if (Permissions.ContainsKey(resourceId)) Permissions.Remove(resourceId);
    }

    /// <summary>获取权限</summary>
    /// <param name="resourceId"></param>
    /// <returns></returns>
    public PermissionFlags Get(Int32 resourceId)
    {
        if (!Permissions.TryGetValue(resourceId, out var pf)) return PermissionFlags.None;

        return pf;
    }

    /// <summary>设置该角色拥有指定资源的指定权限</summary>
    /// <param name="resourceId"></param>
    /// <param name="flag"></param>
    public void Set(Int32 resourceId, PermissionFlags flag = PermissionFlags.All)
    {
        if (Permissions.TryGetValue(resourceId, out var pf))
        {
            Permissions[resourceId] = pf | flag;
        }
        else
        {
            if (flag != PermissionFlags.None) Permissions.Add(resourceId, flag);
        }
    }

    /// <summary>重置该角色指定的权限</summary>
    /// <param name="resourceId"></param>
    /// <param name="flag"></param>
    public void Reset(Int32 resourceId, PermissionFlags flag)
    {
        if (Permissions.TryGetValue(resourceId, out var pf))
        {
            Permissions[resourceId] = pf & ~flag;
        }
    }

    /// <summary>检查是否有无效权限项，有则删除</summary>
    /// <param name="resourceIds"></param>
    internal Boolean CheckValid(Int32[] resourceIds)
    {
        if (resourceIds == null || resourceIds.Length == 0) return true;

        var ps = Permissions;
        var count = ps.Count;

        var list = new List<Int32>();
        foreach (var item in ps)
        {
            if (!resourceIds.Contains(item.Key)) list.Add(item.Key);
        }
        // 删除无效项
        foreach (var item in list)
        {
            ps.Remove(item);
        }

        return count == ps.Count;
    }

    private void LoadPermission()
    {
        Permissions.Clear();
        if (String.IsNullOrEmpty(Permission)) return;

        var dic = Permission.SplitAsDictionary("#", ",");
        foreach (var item in dic)
        {
            var resourceId = item.Key.ToInt();
            Permissions[resourceId] = (PermissionFlags)item.Value.ToInt();
        }
    }

    private void SavePermission()
    {
        var ps = _Permissions;
        if (ps == null) return;

        // 不能这样子直接清空，因为可能没有任何改变，而这么做会两次改变脏数据，让系统以为有改变
        //Permission = null;
        if (ps.Count <= 0)
        {
            //Permission = null;
            SetItem(__.Permission, null);
            return;
        }

        var sb = Pool.StringBuilder.Get();
        // 根据资源按照从小到大排序一下
        foreach (var item in ps.OrderBy(e => e.Key))
        {
            //// 跳过None
            //if (item.Value == PermissionFlags.None) continue;
            // 不要跳过None，因为None表示只读

            if (sb.Length > 0) sb.Append(',');
            sb.AppendFormat("{0}#{1}", item.Key, (Int32)item.Value);
        }
        SetItem(__.Permission, sb.Return(true));
    }

    /// <summary>当前角色拥有的资源</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Int32[] Resources => Permissions.Keys.ToArray();

    #endregion 扩展权限

    #region 业务

    /// <summary>根据名称查找角色，若不存在则创建</summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static IRole GetOrAdd(String name)
    {
        if (name.IsNullOrEmpty()) return null!;

        return Add(name, false);
    }

    /// <summary>根据名称查找角色，若不存在则创建</summary>
    /// <param name="name"></param>
    /// <returns></returns>
    IRole IRole.GetOrAdd(String name)
    {
        if (name.IsNullOrEmpty()) return null!;

        return Add(name, false);
    }

    /// <summary>添加角色，如果存在，则直接返回，否则创建</summary>
    /// <param name="name">角色名称</param>
    /// <param name="issys">是否系统角色</param>
    /// <param name="remark">备注</param>
    /// <returns></returns>
    public static Role Add(String name, Boolean issys, String? remark = null)
    {
        //var entity = FindByName(name);
        var entity = Find(__.Name, name);
        if (entity != null) return entity;

        entity = new Role
        {
            Name = name,
            Type = issys ? RoleTypes.系统 : RoleTypes.普通,
            IsSystem = issys,
            Enable = true,
            Remark = remark
        };
        entity.Save();

        return entity;
    }

    /// <summary>添加角色，如果存在，则直接返回，否则创建</summary>
    /// <param name="name">角色名称</param>
    /// <param name="issys">是否系统角色</param>
    /// <param name="type">角色类型</param>
    /// <param name="scope"></param>
    /// <param name="remark">备注</param>
    /// <returns></returns>
    public static Role Add(String name, Boolean issys, RoleTypes type, DataScopes scope, String? remark = null)
    {
        //var entity = FindByName(name);
        var entity = Find(__.Name, name);
        if (entity != null) return entity;

        entity = new Role
        {
            Name = name,
            Type = type,
            DataScope = scope,
            IsSystem = issys,
            Enable = true,
            Remark = remark
        };
        entity.Save();

        return entity;
    }

    #endregion 业务
}

/// <summary>角色</summary>
public partial interface IRole
{
    /// <summary>本角色权限集合</summary>
    IDictionary<Int32, PermissionFlags> Permissions { get; }

    /// <summary>是否拥有指定资源的指定权限</summary>
    /// <param name="resourceId"></param>
    /// <param name="flag"></param>
    /// <returns></returns>
    Boolean Has(Int32 resourceId, PermissionFlags flag = PermissionFlags.None);

    /// <summary>获取权限</summary>
    /// <param name="resourceId"></param>
    /// <returns></returns>
    PermissionFlags Get(Int32 resourceId);

    /// <summary>设置该角色拥有指定资源的指定权限</summary>
    /// <param name="resourceId"></param>
    /// <param name="flag"></param>
    void Set(Int32 resourceId, PermissionFlags flag = PermissionFlags.Detail);

    /// <summary>重置该角色指定的权限</summary>
    /// <param name="resourceId"></param>
    /// <param name="flag"></param>
    void Reset(Int32 resourceId, PermissionFlags flag);

    /// <summary>当前角色拥有的资源</summary>
    Int32[] Resources { get; }

    /// <summary>根据编号查找角色</summary>
    /// <param name="id"></param>
    /// <returns></returns>
    IRole? FindByID(Int32 id);

    /// <summary>根据名称查找角色，若不存在则创建</summary>
    /// <param name="name"></param>
    /// <returns></returns>
    IRole GetOrAdd(String name);

    /// <summary>保存</summary>
    /// <returns></returns>
    Int32 Save();
}

//partial class RoleModel
//{
//    IDictionary<Int32, PermissionFlags> IRole.Permissions => throw new NotImplementedException();

//    Int32[] IRole.Resources => throw new NotImplementedException();

//    Boolean IRole.Has(Int32 resourceId, PermissionFlags flag) => throw new NotImplementedException();
//    PermissionFlags IRole.Get(Int32 resourceId) => throw new NotImplementedException();
//    void IRole.Set(Int32 resourceId, PermissionFlags flag) => throw new NotImplementedException();
//    void IRole.Reset(Int32 resourceId, PermissionFlags flag) => throw new NotImplementedException();
//    IRole IRole.FindByID(Int32 id) => throw new NotImplementedException();
//    IRole IRole.GetOrAdd(String name) => throw new NotImplementedException();
//    Int32 IRole.Save() => throw new NotImplementedException();
//}