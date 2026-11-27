using XCode.Membership;

namespace XCode;

/// <summary>用户过滤器。自动填充创建人和更新人信息</summary>
public class UserFilter : EntityFilter
{
    #region 静态引用
    /// <summary>字段名</summary>
    public class __
    {
        /// <summary>创建人</summary>
        public static String CreateUserID = nameof(CreateUserID);

        /// <summary>创建人</summary>
        public static String CreateUser = nameof(CreateUser);

        /// <summary>更新人</summary>
        public static String UpdateUserID = nameof(UpdateUserID);

        /// <summary>更新人</summary>
        public static String UpdateUser = nameof(UpdateUser);
    }
    #endregion

    #region 属性
    /// <summary>当前用户提供者</summary>
    public IManageProvider? Provider { get; set; }

    /// <summary>允许空内容。在没有当前用户信息时，是否允许填充空内容，若允许可能是清空上一次更新人。默认false</summary>
    public Boolean AllowEmpty { get; set; }
    #endregion

    #region 构造函数
    /// <summary>实例化</summary>
    public UserFilter() { }

    /// <summary>实例化</summary>
    /// <param name="provider"></param>
    public UserFilter(IManageProvider provider) => Provider = provider;
    #endregion

    /// <summary>初始化。检查是否匹配</summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    protected override Boolean OnInit(Type entityType)
    {
        var fs = GetFields(entityType);
        foreach (var fi in fs)
        {
            if (fi.Type == typeof(Int32) || fi.Type == typeof(Int64))
            {
                if (fi.Name.EqualIgnoreCase(__.CreateUserID, __.UpdateUserID)) return true;
            }
            else if (fi.Type == typeof(String))
            {
                if (fi.Name.EqualIgnoreCase(__.CreateUser, __.UpdateUser)) return true;
            }
        }

        return false;
    }

    /// <summary>操作执行前填充用户字段</summary>
    /// <param name="context">操作上下文</param>
    public override void OnExecuting(EntityContext context)
    {
        if (context.Action == DataMethod.Create)
        {
            if (context.ForEdit && context.Entity != null) Fill(context.Entity, DataMethod.Insert);
            return;
        }

        if (context.Action is not (DataMethod.Insert or DataMethod.Update)) return;

        foreach (var entity in GetEntities(context))
        {
            if (context.Action == DataMethod.Update && !entity.HasDirty) continue;

            Fill(entity, context.Action);
        }
    }

    private void Fill(IEntity entity, DataMethod method)
    {
        var fs = GetFields(entity.GetType());

        // 当前登录用户
        var prv = Provider ?? ManageProvider.Provider;
        var user = prv?.Current;

        // 新增时如果没有当前用户，尝试使用环境变量中的用户名
        if (user == null && method == DataMethod.Insert)
        {
            // 如果用户名为空，或者是root或Administrator，则使用计算机名
            var name = Environment.UserName;
            if (name.IsNullOrEmpty() || name.EqualIgnoreCase("root", "Administrator")) name = Environment.MachineName;
            user = new User { Name = name };
        }

        if (user != null)
        {
            switch (method)
            {
                case DataMethod.Insert:
                    SetItem(fs, entity, __.CreateUserID, user.Id);
                    SetItem(fs, entity, __.CreateUser, user + "");
                    SetItem(fs, entity, __.UpdateUserID, user.Id);
                    SetItem(fs, entity, __.UpdateUser, user + "");
                    break;
                case DataMethod.Update:
                    SetNoDirtyItem(fs, entity, __.UpdateUserID, user.Id);
                    SetNoDirtyItem(fs, entity, __.UpdateUser, user + "");
                    break;
            }
        }
        else if (AllowEmpty)
        {
            // 在没有当前登录用户的场合，把更新者清零
            SetNoDirtyItem(fs, entity, __.UpdateUserID, 0);
            SetNoDirtyItem(fs, entity, __.UpdateUser, "");
        }
    }
}

/// <summary>用户拦截器。兼容旧类名，实际实现见 <see cref="UserFilter"/></summary>
[Obsolete("请使用 UserFilter；运行 xcode 可自动迁移")]
public class UserInterceptor : UserFilter
{
    /// <summary>实例化</summary>
    public UserInterceptor() { }

    /// <summary>实例化</summary>
    /// <param name="provider"></param>
    public UserInterceptor(IManageProvider provider) : base(provider) { }
}