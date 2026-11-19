using System.ComponentModel;
using System.Runtime.Serialization;
using System.Security.Principal;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using XCode.Configuration;

namespace XCode.Membership;

/// <summary>管理员</summary>
/// <remarks>
/// 基础实体类应该是只有一个泛型参数的，需要用到别的类型时，可以继承一个，也可以通过虚拟重载等手段让基类实现
/// </remarks>
public partial class User : LogEntity<User>, IUser, IAuthUser, IIdentity, IDataScope, IFieldScope, IDataScopeFieldProvider
{
    #region 对象操作
    static User()
    {
        //// 用于引发基类的静态构造函数
        //var entity = new TEntity();

        //!!! 曾经这里导致产生死锁
        // 这里是静态构造函数，访问Factory引发EntityFactory.CreateOperate，
        // 里面的EnsureInit会等待实体类实例化完成，实体类的静态构造函数还卡在这里
        // 不过这不是理由，同一个线程遇到同一个锁不会堵塞
        // 发生死锁的可能性是这里引发EnsureInit，而另一个线程提前引发EnsureInit拿到锁
        var df = Meta.Factory.AdditionalFields;
        df.Add(__.Logins);
        //df.Add(__.OnlineTime);
        //Meta.Factory.FullInsert = false;

        // 单对象缓存
        var sc = Meta.SingleCache;
        sc.FindSlaveKeyMethod = k => Find(__.Name, k);
        sc.GetSlaveKeyMethod = e => e.Name;

        Meta.Interceptors.Add<UserInterceptor>();
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add<IPInterceptor>();
        Meta.Interceptors.Add<DataScopeInterceptor>();
    }

    /// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal protected override void InitData()
    {
        if (Meta.Count > 0) return;

        if (XTrace.Debug) XTrace.WriteLine("Initializing {0} user data...", typeof(User).Name);

        Add("admin", null, 1, "管理员");
        //Add("poweruser", null, 2, "高级用户");
        //Add("user", null, 3, "普通用户");

        if (XTrace.Debug) XTrace.WriteLine("Finished initializing {0} user data!", typeof(User).Name);
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        if (method == DataMethod.Delete) return true;

        // 清理富文本编辑器遗留的空内容（如 <p><br></p>），避免脏值写入数据库并经 SSO 同步扩散；随本次更新写库清除
        if (EmptyHtmlHelper.IsEmptyHtml(Remark)) Remark = null;

        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        if (Name.IsNullOrEmpty()) throw new ArgumentNullException(__.Name, L.T("Username cannot be empty!"));

        // 新用户默认角色。仅当没有任何角色时生效，避免干扰下方对 RoleIds 的角色整理；
        // 不依赖数据库默认值3（可能因角色删除或顺序变化而失效）
        if (method == DataMethod.Insert && RoleId <= 0 && RoleIds.IsNullOrEmpty())
        {
            var role = Role.FindByName("普通用户") ?? Role.FindByName("游客");
            if (role != null) RoleId = role.Id;
        }

        //if (RoleId < 1) throw new ArgumentNullException(__.RoleId, "没有指定角色！");

        //var pass = Password;
        //if (isNew)
        //{
        //    if (!pass.IsNullOrEmpty() && pass.Length != 32) Password = pass.MD5();
        //}
        //else
        //{
        //    // 编辑修改密码
        //    if (IsDirty(__.Password))
        //    {
        //        if (!pass.IsNullOrEmpty())
        //            Password = pass.MD5();
        //        else
        //            Dirtys[__.Password] = false;
        //    }
        //}

        // 重新整理角色（仅自有角色，避免租户上下文下把租户角色写回本表）
        var ids = GetOwnRoleIds();
        if (ids.Length > 0)
        {
            RoleId = ids[0];
            if (ids.Length == 1)
                RoleIds = null;
            else
            {
                var str = ids.Skip(1).Join();
                if (!str.IsNullOrEmpty()) str = "," + str + ",";
                RoleIds = str;
            }
        }

        // 自动计算年龄
        if (Birthday.Year > 1000) Age = (Int32)((DateTime.Now - Birthday).TotalDays / 365);

        //if (AreaId <= 0) FixArea(LastLoginIP);

        return base.Valid(method);
    }

    /// <summary>删除用户</summary>
    /// <returns></returns>
    protected override Int32 OnDelete()
    {
        if (Meta.Count <= 1 && FindCount() <= 1) throw new Exception(L.T("At least one enabled account must be retained!"));

        return base.OnDelete();
    }
    #endregion

    #region 扩展属性
    /// <summary>物理地址</summary>
    [DisplayName("物理地址")]
    public String? LastLoginAddress => LastLoginIP?.IPToAddress();

    ///// <summary>部门</summary>
    //[XmlIgnore, IgnoreDataMember, ScriptIgnore]
    //public Department Department => Extends.Get(nameof(Department), k => Department.FindByID(DepartmentId));

    ///// <summary>部门</summary>
    //[Category("登录信息")]
    //[Map(__.DepartmentId, typeof(Department), __.Id)]
    //public String DepartmentName => Department?.Path;

    /// <summary>
    /// 地区名
    /// </summary>
    [Map(nameof(AreaId))]
    public String? AreaName => Area.FindById(AreaId)?.Path;
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [Obsolete("请使用 FindById")]
    public static User? FindByID(Int32 id) => FindById(id);

    /// <summary>直接从数据库查询用户，绕过实体缓存与单对象缓存</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象；不存在时返回 null</returns>
    [Obsolete("请使用 FindByIdFromDatabase")]
    public static User? FindByIDFromDatabase(Int32 id) => FindByIdFromDatabase(id);

    /// <summary>直接从数据库查询用户，绕过实体缓存与单对象缓存</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象；不存在时返回 null</returns>
    public static User? FindByIdFromDatabase(Int32 id)
    {
        if (id <= 0) return null;

        return Find(_.Id == id);
    }

    /// <summary>根据名称查找</summary>
    /// <param name="name">名称</param>
    /// <returns></returns>
    public static User? FindByName(String name)
    {
        if (name.IsNullOrEmpty()) return null;

        if (Meta.Count < 1000) return Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));

        // 单对象缓存
        return Meta.SingleCache.GetItemWithSlaveKey(name) as User;
    }

    /// <summary>根据邮箱地址查找</summary>
    /// <param name="mail"></param>
    /// <returns></returns>
    public static User? FindByMail(String mail)
    {
        if (mail.IsNullOrEmpty()) return null;

        if (Meta.Count < 1000) return Meta.Cache.Find(e => e.Mail.EqualIgnoreCase(mail));

        return Find(__.Mail, mail);
    }

    /// <summary>根据手机号码查找</summary>
    /// <param name="mobile"></param>
    /// <returns></returns>
    public static User? FindByMobile(String mobile)
    {
        if (mobile.IsNullOrEmpty()) return null;

        if (Meta.Count < 1000) return Meta.Cache.Find(e => e.Mobile == mobile);

        return Find(__.Mobile, mobile);
    }

    /// <summary>根据唯一代码查找</summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public static User? FindByCode(String code)
    {
        if (code.IsNullOrEmpty()) return null;

        if (Meta.Count < 1000) return Meta.Cache.Find(e => e.Code.EqualIgnoreCase(code));

        return Find(__.Code, code);
    }

    /// <summary>为登录而查找账号，搜索名称、邮箱、手机、代码</summary>
    /// <param name="account"></param>
    /// <returns></returns>
    public static User? FindForLogin(String account)
    {
        if (account.IsNullOrEmpty()) return null;

        var user = Find(_.Name == account);
        if (user == null && account.Contains("@")) user = Find(_.Mail == account);
        if (user == null && account.ToLong() > 0) user = Find(_.Mobile == account);
        if (user == null) user = Find(_.Code == account);

        return user;
    }

    /// <summary>根据角色查找</summary>
    /// <param name="roleId">角色</param>
    /// <returns>实体列表</returns>
    [Obsolete("请使用 FindAllByRoleId")]
    public static IList<User> FindAllByRoleID(Int32 roleId) => FindAllByRoleId(roleId);
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="key"></param>
    /// <param name="roleId"></param>
    /// <param name="isEnable"></param>
    /// <param name="p"></param>
    /// <returns></returns>
    public static IList<User> Search(String key, Int32 roleId, Boolean? isEnable, PageParameter p) => Search(key, roleId, isEnable, DateTime.MinValue, DateTime.MinValue, p);

    /// <summary>高级查询</summary>
    /// <param name="key"></param>
    /// <param name="roleId"></param>
    /// <param name="isEnable"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="p"></param>
    /// <returns></returns>
    public static IList<User> Search(String key, Int32 roleId, Boolean? isEnable, DateTime start, DateTime end, PageParameter p)
    {
        var exp = _.LastLogin.Between(start, end);
        if (roleId > 0) exp &= _.RoleId == roleId | _.RoleIds.Contains("," + roleId + ",");
        if (isEnable != null) exp &= _.Enable == isEnable;

        // 先精确查询，再模糊
        if (!key.IsNullOrEmpty())
        {
            var list = FindAll(exp & (_.Code == key | _.Name == key | _.DisplayName == key | _.Mail == key | _.Mobile == key), p);
            if (list.Count > 0) return list;

            exp &= (_.Code.Contains(key) | _.Name.Contains(key) | _.DisplayName.Contains(key) | _.Mail.Contains(key) | _.Mobile.Contains(key));
        }

        return FindAll(exp, p);
    }

    /// <summary>高级搜索</summary>
    /// <param name="roleId">角色</param>
    /// <param name="departmentId">部门</param>
    /// <param name="enable">启用</param>
    /// <param name="start">登录时间开始</param>
    /// <param name="end">登录时间结束</param>
    /// <param name="key">关键字，搜索代码、名称、昵称、手机、邮箱</param>
    /// <param name="page"></param>
    /// <returns></returns>
    public static IList<User> Search(Int32 roleId, Int32 departmentId, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();
        if (roleId >= 0) exp &= _.RoleId == roleId | _.RoleIds.Contains("," + roleId + ",");
        if (departmentId >= 0) exp &= _.DepartmentId == departmentId;
        if (enable != null) exp &= _.Enable == enable.Value;
        exp &= _.LastLogin.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Code.Contains(key) | _.Name.Contains(key) | _.DisplayName.Contains(key) | _.Mobile.Contains(key) | _.Mail.Contains(key);

        return FindAll(exp, page);
    }

    /// <summary>高级搜索</summary>
    /// <param name="roleIds">角色</param>
    /// <param name="departmentIds">部门</param>
    /// <param name="enable">启用</param>
    /// <param name="start">登录时间开始</param>
    /// <param name="end">登录时间结束</param>
    /// <param name="key">关键字，搜索代码、名称、昵称、手机、邮箱</param>
    /// <param name="page"></param>
    /// <returns></returns>
    public static IList<User> Search(Int32[] roleIds, Int32[] departmentIds, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page) => Search(roleIds, departmentIds, null, enable, start, end, key, page);

    /// <summary>高级搜索</summary>
    /// <param name="roleIds">角色</param>
    /// <param name="departmentIds">部门</param>
    /// <param name="areaIds">地区</param>
    /// <param name="enable">启用</param>
    /// <param name="start">登录时间开始</param>
    /// <param name="end">登录时间结束</param>
    /// <param name="key">关键字，搜索代码、名称、昵称、手机、邮箱</param>
    /// <param name="page"></param>
    /// <returns></returns>
    public static IList<User> Search(Int32[]? roleIds, Int32[]? departmentIds, Int32[]? areaIds, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
        => Search(roleIds, departmentIds, areaIds, enable, -1, null, null, null, start, end, key, page);

    /// <summary>高级搜索（含性别、在线与验证状态）</summary>
    /// <param name="roleIds">角色</param>
    /// <param name="departmentIds">部门</param>
    /// <param name="areaIds">地区</param>
    /// <param name="enable">启用</param>
    /// <param name="sex">性别。-1表示不限</param>
    /// <param name="online">在线</param>
    /// <param name="mailVerified">邮箱验证</param>
    /// <param name="mobileVerified">手机验证</param>
    /// <param name="start">登录时间开始</param>
    /// <param name="end">登录时间结束</param>
    /// <param name="key">关键字，搜索代码、名称、昵称、手机、邮箱</param>
    /// <param name="page">分页参数信息</param>
    /// <returns>实体列表</returns>
    public static IList<User> Search(Int32[]? roleIds, Int32[]? departmentIds, Int32[]? areaIds, Boolean? enable, Int32 sex, Boolean? online, Boolean? mailVerified, Boolean? mobileVerified, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();
        if (roleIds != null && roleIds.Length > 0)
        {
            var exp2 = new WhereExpression();
            exp2 |= _.RoleId.In(roleIds);
            foreach (var rid in roleIds)
            {
                exp2 |= _.RoleIds.Contains("," + rid + ",");
            }
            exp &= exp2;
        }
        if (departmentIds != null && departmentIds.Length > 0) exp &= _.DepartmentId.In(departmentIds);
        if (areaIds != null && areaIds.Length > 0) exp &= _.AreaId.In(areaIds);
        if (enable != null) exp &= _.Enable == enable.Value;
        if (sex >= 0) exp &= _.Sex == (SexKinds)sex;
        if (online != null) exp &= _.Online == online.Value;
        if (mailVerified != null) exp &= _.MailVerified == mailVerified.Value;
        if (mobileVerified != null) exp &= _.MobileVerified == mobileVerified.Value;
        exp &= _.LastLogin.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Code.Contains(key) | _.Name.Contains(key) | _.DisplayName.Contains(key) | _.Mobile.Contains(key) | _.Mail.Contains(key);

        return FindAll(exp, page);
    }

    /// <summary>构建活跃与新增用户统计列</summary>
    /// <param name="t1">当日</param>
    /// <param name="t7">7日前</param>
    /// <param name="t30">30日前</param>
    /// <returns>查询列表达式</returns>
    public static ConcatExpression BuildStatSelects(DateTime t1, DateTime t7, DateTime t30)
    {
        var selects = new ConcatExpression();
        selects &= _.LastLogin.SumLarge($"'{t1:yyyy-MM-dd}'", "activeT1");
        selects &= _.LastLogin.SumLarge($"'{t7:yyyy-MM-dd}'", "activeT7");
        selects &= _.LastLogin.SumLarge($"'{t30:yyyy-MM-dd}'", "activeT30");
        selects &= _.RegisterTime.SumLarge($"'{t1:yyyy-MM-dd}'", "newT1");
        selects &= _.RegisterTime.SumLarge($"'{t7:yyyy-MM-dd}'", "newT7");
        selects &= _.RegisterTime.SumLarge($"'{t30:yyyy-MM-dd}'", "newT30");

        return selects;
    }

    /// <summary>在租户中搜索</summary>
    /// <param name="tenantId">租户</param>
    /// <param name="roleIds"></param>
    /// <param name="departmentIds"></param>
    /// <param name="areaIds"></param>
    /// <param name="enable"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="key"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    public static IList<User>? SearchWithTenant(Int32 tenantId, Int32[] roleIds, Int32[] departmentIds, Int32[] areaIds, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = TenantUser._.TenantId == tenantId;
        if (roleIds != null && roleIds.Length > 0)
        {
            var exp2 = new WhereExpression();
            exp2 |= _.RoleId.In(roleIds);
            foreach (var rid in roleIds)
            {
                exp2 |= _.RoleIds.Contains("," + rid + ",");
            }
            exp &= exp2;
        }
        if (departmentIds != null && departmentIds.Length > 0) exp &= _.DepartmentId.In(departmentIds);
        if (areaIds != null && areaIds.Length > 0) exp &= _.AreaId.In(areaIds);
        if (enable != null) exp &= _.Enable == enable.Value;
        exp &= _.LastLogin.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Code.Contains(key) | _.Name.Contains(key) | _.DisplayName.Contains(key) | _.Mobile.Contains(key) | _.Mail.Contains(key);

        var sql = $"SELECT User.* FROM User INNER JOIN TenantUser ON User.Id= TenantUser.UserId where {exp} ";

        return Meta.Session.Dal.Query<User>(sql, null, page)?.ToList();
    }
    #endregion

    #region 扩展操作
    /// <summary>添加用户，如果存在则直接返回</summary>
    /// <param name="name"></param>
    /// <param name="pass"></param>
    /// <param name="roleid"></param>
    /// <param name="display"></param>
    /// <returns></returns>
    public static User Add(String name, String? pass, Int32 roleid = 1, String? display = null)
    {
        //var entity = Find(_.Name == name);
        //if (entity != null) return entity;

        if (pass.IsNullOrEmpty()) pass = name;

        var entity = new User
        {
            Name = name,
            Password = pass.MD5(),
            DisplayName = display,
            RoleId = roleid,
            Enable = true
        };

        entity.Save();

        return entity;
    }

    /// <summary>清除用户与租户关系中指定角色的引用。用于角色删除后的数据一致性维护</summary>
    /// <param name="roleId">角色编号</param>
    /// <returns>受影响的数据行数</returns>
    public static Int32 ClearRole(Int32 roleId)
    {
        if (roleId <= 0) return 0;

        var count = 0;

        // 清理用户主角色与角色组
        foreach (var user in FindAll(_.RoleId == roleId | _.RoleIds.Contains("," + roleId + ",")))
        {
            if (user.RoleId == roleId)
            {
                // 主角色被删除，从角色组中提升一个作为主角色
                var ids = user.RoleIds.SplitAsInt();
                if (ids.Length > 0)
                {
                    user.RoleId = ids[0];
                    user.RoleIds = ids.Length > 1 ? "," + ids.Skip(1).Join(",") + "," : null;
                }
                else
                {
                    user.RoleId = 0;
                    user.RoleIds = null;
                }
            }
            else
            {
                var ids = user.RoleIds.SplitAsInt().Where(e => e != roleId).ToArray();
                user.RoleIds = ids.Length > 0 ? "," + ids.Join(",") + "," : null;
            }

            user.Update();
            count++;
        }

        // 清理租户关系中的角色
        foreach (var tu in TenantUser.FindAll(TenantUser._.RoleId == roleId | TenantUser._.RoleIds.Contains("," + roleId + ",")))
        {
            if (tu.RoleId == roleId)
            {
                var ids = tu.RoleIds.SplitAsInt();
                if (ids.Length > 0)
                {
                    tu.RoleId = ids[0];
                    tu.RoleIds = ids.Length > 1 ? "," + ids.Skip(1).Join(",") + "," : null;
                }
                else
                {
                    tu.RoleId = 0;
                    tu.RoleIds = null;
                }
            }
            else
            {
                var ids = tu.RoleIds.SplitAsInt().Where(e => e != roleId).ToArray();
                tu.RoleIds = ids.Length > 0 ? "," + ids.Join(",") + "," : null;
            }

            tu.Update();
            count++;
        }

        return count;
    }

    /// <summary>已重载。显示友好名字</summary>
    /// <returns></returns>
    public override String ToString() => DisplayName.IsNullOrEmpty() ? Name : DisplayName;

    /// <summary>
    /// 修正地区
    /// </summary>
    public void FixArea(String? ip)
    {
        if (ip.IsNullOrEmpty()) return;

        var list = Area.SearchIP(ip);
        if (list.Count > 0) AreaId = list[list.Count - 1].Id;
    }
    #endregion

    #region 业务
    /// <summary>登录。借助回调来验证密码</summary>
    /// <param name="username"></param>
    /// <param name="onValid"></param>
    /// <returns></returns>
    public static User Login(String username, Action<User> onValid)
    {
        if (String.IsNullOrEmpty(username)) throw new ArgumentNullException(nameof(username));
        if (onValid == null) throw new ArgumentNullException(nameof(onValid));

        try
        {
            // 过滤帐号中的空格，防止出现无操作无法登录的情况
            var account = username.Trim();
            //var user = FindByName(account);
            // 登录时必须从数据库查找用户，缓存中的用户对象密码字段可能为空
            var user = Find(__.Name, account);
            if (user == null) throw new EntityException("Account {0} does not exist!", account);

            if (!user.Enable) throw new EntityException("Account {0} is disabled.", account);

            // 验证用户
            onValid(user);

            user.SaveLoginInfo();

            WriteLog(LogActions.Login, true, username);

            return user;
        }
        catch (Exception ex)
        {
            WriteLog(LogActions.Login, false, L.T("Login failed for account [{0}]! {1}", username, ex.Message));
            throw;
        }
    }

    /// <summary>登录</summary>
    /// <param name="username"></param>
    /// <param name="password"></param>
    /// <param name="rememberme">是否记住密码</param>
    /// <returns></returns>
    public static User Login(String username, String password, Boolean rememberme = false)
    {
        if (String.IsNullOrEmpty(username)) throw new ArgumentNullException(nameof(username));
        //if (String.IsNullOrEmpty(password)) throw new ArgumentNullException("password");

        try
        {
            return Login(username, password, 1);
        }
        catch (Exception ex)
        {
            WriteLog(LogActions.Login, false, L.T("Login failed for account [{0}]! {1}", username, ex.Message));
            throw;
        }
    }

    static User Login(String username, String password, Int32 hashTimes)
    {
        if (String.IsNullOrEmpty(username)) throw new ArgumentNullException(nameof(username), L.T("The account does not exist!"));

        // 过滤帐号中的空格，防止出现无操作无法登录的情况
        var account = username.Trim();
        //var user = FindByName(account);
        // 登录时必须从数据库查找用户，缓存中的用户对象密码字段可能为空
        var user = Find(__.Name, account);
        if (user == null) throw new EntityException("Account {0} does not exist!", account);

        if (!user.Enable) throw new EntityException("Account {0} is disabled.", account);

        // 数据库为空密码，任何密码均可登录
        if (!String.IsNullOrEmpty(user.Password))
        {
            if (hashTimes > 0)
            {
                var p = password;
                if (!String.IsNullOrEmpty(p))
                {
                    for (var i = 0; i < hashTimes; i++)
                    {
                        p = p.MD5();
                    }
                }
                if (!p.EqualIgnoreCase(user.Password)) throw new EntityException("Incorrect password!");
            }
            else
            {
                var p = user.Password;
                for (var i = 0; i > hashTimes && p != null; i--)
                {
                    p = p?.MD5();
                }
                if (!p.EqualIgnoreCase(password)) throw new EntityException("Incorrect password!");
            }
        }
        else
        {
            if (hashTimes > 0)
            {
                var p = password;
                if (!String.IsNullOrEmpty(p))
                {
                    for (var i = 0; i < hashTimes; i++)
                    {
                        p = p.MD5();
                    }
                }
                password = p;
            }
            user.Password = password;
        }

        //Current = user;

        user.SaveLoginInfo();

        if (hashTimes == -1)
            WriteLog(LogActions.AutoLogin, true, username);
        else
            WriteLog(LogActions.Login, true, username);

        return user;
    }

    /// <summary>保存登录信息</summary>
    /// <returns></returns>
    public virtual Int32 SaveLoginInfo()
    {
        Logins++;
        LastLogin = DateTime.Now;
        var ip = ManageProvider.UserHost;
        if (!ip.IsNullOrEmpty()) LastLoginIP = ip;

        Online = true;

        if (AreaId <= 0) FixArea(LastLoginIP);

        return Update();
    }

    /// <summary>注销</summary>
    public virtual void Logout()
    {
        Online = false;
        SaveAsync();
    }

    /// <summary>注册用户。第一注册用户自动抢管理员</summary>
    public virtual void Register()
    {
        using var tran = Meta.CreateTrans();
        //!!! 第一个用户注册时，如果只有一个默认admin账号，则自动抢管理员
        if (Meta.Count < 3 && FindCount() <= 1)
        {
            var list = FindAll();
            if (list.Count == 0 || list.Count == 1 && list[0].DisableAdmin())
            {
                RoleId = 1;
                Enable = true;
            }
        }

        RegisterTime = DateTime.Now;
        RegisterIP = ManageProvider.UserHost;

        Insert();

        tran.Commit();
    }

    /// <summary>禁用默认管理员</summary>
    /// <returns></returns>
    private Boolean DisableAdmin()
    {
        if (!Name.EqualIgnoreCase("admin")) return false;
        if (!Password.EqualIgnoreCase("admin".MD5())) return false;

        Enable = false;
        RoleId = 4;

        Save();

        return true;
    }
    #endregion

    #region 权限
    /// <summary>角色</summary>
    /// <remarks>扩展属性不缓存空对象，一般来说，每个管理员都有对应的角色，如果没有，可能是在初始化</remarks>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    IRole? IUser.Role => Role;

    /// <summary>角色集合</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public virtual IRole[] Roles => Extends.Get(nameof(Roles), k => GetRoleIds().Select(e => ManageProvider.Get<IRole>()?.FindByID(e)).Where(e => e != null).Cast<IRole>().ToArray()) ?? [];

    /// <summary>获取当前上下文下的角色列表。主角色在前，其它角色升序在后；进入租户后以租户关系角色组替代自有角色组进行鉴权</summary>
    /// <returns></returns>
    [Obsolete("请使用 GetRoleIds")]
    public Int32[] GetRoleIDs() => GetRoleIds();

    /// <summary>获取当前上下文下的角色列表。主角色在前，其它角色升序在后；进入租户后以租户关系角色组替代自有角色组进行鉴权</summary>
    /// <returns></returns>
    public virtual Int32[] GetRoleIds()
    {
        // 曾经尝试过在自有角色组上叠加租户角色（TenantUser.RoleId 插入最前），
        // 但 TenantUser 的设计语义是"替代自有角色组进行鉴权"，叠加会导致 User.Roles 与 ManageProvider.Has 两套鉴权路径不一致
        var tenantId = TenantContext.CurrentId;
        if (tenantId > 0)
        {
            var tu = TenantUser.FindByTenantIdAndUserId(tenantId, Id);
            if (tu != null && tu.Enable)
            {
                var ids = tu.RoleIds.SplitAsInt().OrderBy(e => e).ToList();
                if (tu.RoleId > 0) ids.Insert(0, tu.RoleId);

                return ids.Distinct().ToArray();
            }
        }

        return GetOwnRoleIds();
    }

    /// <summary>获取自有角色列表。主角色在前，其它角色升序在后</summary>
    /// <returns></returns>
    [Obsolete("请使用 GetOwnRoleIds")]
    public Int32[] GetOwnRoleIDs() => GetOwnRoleIds();

    /// <summary>获取自有角色列表。主角色在前，其它角色升序在后</summary>
    /// <returns></returns>
    public virtual Int32[] GetOwnRoleIds()
    {
        var ids = RoleIds.SplitAsInt().OrderBy(e => e).ToList();
        if (RoleId > 0) ids.Insert(0, RoleId);

        return ids.Distinct().ToArray();
    }

    ///// <summary>角色名</summary>
    //[Category("登录信息")]
    //[Map(__.RoleId, typeof(Role), "Id")]
    //public virtual String RoleName => Role + "";

    /// <summary>角色组名</summary>
    [Map(__.RoleIds)]
    public virtual String? RoleNames => Extends.Get(nameof(RoleNames), k => RoleIds.SplitAsInt().Select(e => ManageProvider.Get<IRole>()?.FindByID(e)).Where(e => e != null).Cast<IRole>().Select(e => e.Name).Join());

    /// <summary>用户是否拥有当前菜单的指定权限</summary>
    /// <param name="menu">指定菜单</param>
    /// <param name="flags">是否拥有多个权限中的任意一个，或的关系。如果需要表示与的关系，可以传入一个多权限位合并</param>
    /// <returns></returns>
    public Boolean Has(IMenu menu, params PermissionFlags[] flags) => Role.HasRoles(Roles, menu, flags);

    #endregion

    #region IManageUser 成员
    /// <summary>昵称</summary>
    String? IManageUser.NickName { get => DisplayName; set => DisplayName = value; }

    String IIdentity.Name => Name;

    String IIdentity.AuthenticationType => "XCode";

    Boolean IIdentity.IsAuthenticated => true;
    #endregion

    #region 兼容别名
    /// <summary>编号</summary>
    /// <remarks>兼容旧版本调用方，新代码请使用 Id</remarks>
    [Obsolete("请使用 Id")]
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
#if NET5_0_OR_GREATER
    [System.Text.Json.Serialization.JsonIgnore]
#endif
    public Int32 ID { get => Id; set => Id = value; }

    /// <summary>角色</summary>
    /// <remarks>兼容旧版本字段名，新代码请使用 RoleId</remarks>
    [Obsolete("请使用 RoleId")]
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
#if NET5_0_OR_GREATER
    [System.Text.Json.Serialization.JsonIgnore]
#endif
    public Int32 RoleID { get => RoleId; set => RoleId = value; }

    /// <summary>部门</summary>
    /// <remarks>兼容旧版本字段名，新代码请使用 DepartmentId</remarks>
    [Obsolete("请使用 DepartmentId")]
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
#if NET5_0_OR_GREATER
    [System.Text.Json.Serialization.JsonIgnore]
#endif
    public Int32 DepartmentID { get => DepartmentId; set => DepartmentId = value; }

    /// <summary>更新者</summary>
    /// <remarks>兼容旧版本字段名，新代码请使用 UpdateUserId</remarks>
    [Obsolete("请使用 UpdateUserId")]
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
#if NET5_0_OR_GREATER
    [System.Text.Json.Serialization.JsonIgnore]
#endif
    public Int32 UpdateUserID { get => UpdateUserId; set => UpdateUserId = value; }

    #endregion

    #region 兼容方法
    /// <summary>根据部门查找用户</summary>
    [Obsolete("请使用 FindAllByDepartmentId")]
    public static IList<User> FindAllByDepartmentID(Int32 departmentId) => FindAllByDepartmentId(departmentId);
    #endregion

    #region IDataScope 成员
    Int32 IUserScope.UserId { get => Id; set => Id = value; }
    Int32 IDepartmentScope.DepartmentId { get => DepartmentId; set => DepartmentId = value; }

    #endregion

    #region IFieldScope 成员
    String[] IFieldScope.GetSensitiveFields() => [__.Password];
    Boolean IFieldScope.CanViewSensitiveFields(Int32 userId) => userId == Id;
    #endregion

    #region IDataScopeFieldProvider 成员
    FieldItem? IDataScopeFieldProvider.GetUserField() => _.Id;
    FieldItem? IDataScopeFieldProvider.GetDepartmentField() => _.DepartmentId;
    FieldItem? IDataScopeFieldProvider.GetTenantField() => null;
    #endregion
}

/// <summary>用户</summary>
public partial interface IUser
{
    /// <summary>角色</summary>
    IRole? Role { get; }

    /// <summary>角色集合</summary>
    IRole[] Roles { get; }

    /// <summary>角色名</summary>
    String? RoleName { get; }

    /// <summary>用户是否拥有当前菜单的指定权限</summary>
    /// <param name="menu">指定菜单</param>
    /// <param name="flags">是否拥有多个权限中的任意一个，或的关系。如果需要表示与的关系，可以传入一个多权限位合并</param>
    /// <returns></returns>
    Boolean Has(IMenu menu, params PermissionFlags[] flags);

    /// <summary>注销</summary>
    void Logout();

    /// <summary>保存</summary>
    Int32 Save();
}

//public partial class UserModel
//{
//    IRole IUser.Role => throw new NotImplementedException();

//    IRole[] IUser.Roles => throw new NotImplementedException();

//    String IUser.RoleName => throw new NotImplementedException();

//    Boolean IUser.Has(IMenu menu, params PermissionFlags[] flags) => throw new NotImplementedException();
//    void IUser.Logout() => throw new NotImplementedException();
//    Int32 IUser.Save() => throw new NotImplementedException();
//}