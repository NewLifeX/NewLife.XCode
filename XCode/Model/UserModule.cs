using NewLife;
using XCode.Membership;

namespace XCode;

/// <summary>用户模型</summary>
public class UserModule : EntityModule
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
    public UserModule() { }

    /// <summary>实例化</summary>
    /// <param name="provider"></param>
    public UserModule(IManageProvider provider) => Provider = provider;
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

    /// <summary>创建实体对象</summary>
    /// <param name="entity"></param>
    /// <param name="forEdit"></param>
    protected override void OnCreate(IEntity entity, Boolean forEdit)
    {
        if (forEdit) OnValid(entity, DataMethod.Insert);
    }

    /// <summary>验证数据，自动加上创建和更新的信息</summary>
    /// <param name="entity"></param>
    /// <param name="method"></param>
    protected override Boolean OnValid(IEntity entity, DataMethod method)
    {
        if (method == DataMethod.Delete) return true;
        if (method == DataMethod.Update && !entity.HasDirty) return true;

        var fs = GetFields(entity.GetType());

        // 当前登录用户
        var prv = Provider ?? ManageProvider.Provider;
        var user = prv?.Current;

        // 新增时如果没有当前用户，尝试使用环境变量中的用户名
        if (user == null && method == DataMethod.Insert)
            user = new User { Name = Environment.UserName };

        if (user != null)
        {
            switch (method)
            {
                case DataMethod.Insert:
                    SetItem(fs, entity, __.CreateUserID, user.ID);
                    SetItem(fs, entity, __.CreateUser, user + "");
                    SetItem(fs, entity, __.UpdateUserID, user.ID);
                    SetItem(fs, entity, __.UpdateUser, user + "");
                    break;
                case DataMethod.Update:
                    SetNoDirtyItem(fs, entity, __.UpdateUserID, user.ID);
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

        return true;
    }
}
