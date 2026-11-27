using System.Collections;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Reflection;
using XCode;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace XCode.Membership;

/// <summary>字典参数</summary>
[ModelCheckMode(ModelCheckModes.CheckTableWhenFirstUse)]
public partial class Parameter : Entity<Parameter>
{

    #region 兼容别名

    /// <summary>编号</summary>
    /// <remarks>兼容旧版本字段名，新代码请使用 Id</remarks>
    [Obsolete("请使用 Id")]
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
#if NET5_0_OR_GREATER
    [System.Text.Json.Serialization.JsonIgnore]
#endif
    public Int32 ID { get => Id; set => Id = value; }

    /// <summary>用户</summary>
    /// <remarks>兼容旧版本字段名，新代码请使用 UserId</remarks>
    [Obsolete("请使用 UserId")]
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
#if NET5_0_OR_GREATER
    [System.Text.Json.Serialization.JsonIgnore]
#endif
    public Int32 UserID { get => UserId; set => UserId = value; }

    /// <summary>创建者</summary>
    /// <remarks>兼容旧版本字段名，新代码请使用 CreateUserId</remarks>
    [Obsolete("请使用 CreateUserId")]
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
#if NET5_0_OR_GREATER
    [System.Text.Json.Serialization.JsonIgnore]
#endif
    public Int32 CreateUserID { get => CreateUserId; set => CreateUserId = value; }

    /// <summary>更新者</summary>
    /// <remarks>兼容旧版本字段名，新代码请使用 UpdateUserId</remarks>
    [Obsolete("请使用 UpdateUserId")]
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
#if NET5_0_OR_GREATER
    [System.Text.Json.Serialization.JsonIgnore]
#endif
    public Int32 UpdateUserID { get => UpdateUserId; set => UpdateUserId = value; }

    /// <summary>根据用户查找参数</summary>
    [Obsolete("请使用 FindAllByUserId")]
    public static IList<Parameter> FindAllByUserID(Int32 userId) => FindAllByUserId(userId);

    /// <summary>根据用户和分类查找参数</summary>
    [Obsolete("请使用 FindAllByUserIdAndCategory")]
    public static IList<Parameter> FindAllByUserID(Int32 userId, String? category) => FindAllByUserIdAndCategory(userId, category);

    /// <summary>根据用户和分类查找参数</summary>
    [Obsolete("请使用 FindAllByUserIdAndCategory")]
    public static IList<Parameter> FindAllByUserIDAndCategory(Int32 userId, String? category) => FindAllByUserIdAndCategory(userId, category);

    /// <summary>根据用户、分类和名称查找参数</summary>
    [Obsolete("请使用 FindByUserIdAndCategoryAndName")]
    public static Parameter? FindByUserIDAndCategoryAndName(Int32 userId, String? category, String name) => FindByUserIdAndCategoryAndName(userId, category, name);
    #endregion
    #region 对象操作
    static Parameter()
    {
        // 累加字段
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(__.Kind);

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Filters.Add<UserFilter>();
        Meta.Filters.Add<TimeFilter>();
        Meta.Filters.Add<IPFilter>();
    }
    #endregion

    #region 扩展属性
    ///// <summary>用户</summary>
    //[XmlIgnore, IgnoreDataMember]
    //public IManageUser User => Extends.Get(nameof(User), k => Membership.User.FindByID(UserId));

    ///// <summary>用户名</summary>
    //[Map(nameof(UserId))]
    //public String UserName => UserId == 0 ? "全局" : (User + "");
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    [Obsolete("请使用 FindById")]
    public static Parameter? FindByID(Int32 id) => FindById(id);


    /// <summary>根据分类查找。直接查库，不走实体缓存，用于集群配置同步等需要最新写入时间的场景</summary>
    /// <param name="category">分类</param>
    /// <param name="userId">用户。默认0表示全局</param>
    /// <returns>实体列表</returns>
    public static IList<Parameter> FindAllByCategory(String category, Int32 userId = 0) =>
        FindAll(_.UserId == userId & _.Category == category);

    /// <summary>根据编号删除参数</summary>
    /// <param name="id">编号</param>
    /// <returns>影响行数</returns>
    [Obsolete("请使用 DeleteById")]
    public static Int32 DeleteByID(Int32 id) => DeleteById(id);

    /// <summary>根据编号删除</summary>
    /// <param name="id">编号</param>
    /// <returns></returns>
    public static Int32 DeleteById(Int32 id) => Delete(_.Id == id);

    /// <summary>根据分类统计参数数量</summary>
    /// <param name="category">分类</param>
    /// <returns>参数数量</returns>
    public static Int64 FindCountByCategory(String category) => FindCount(_.Category == category);

    /// <summary>根据用户删除参数</summary>
    /// <param name="userId">用户</param>
    /// <returns>影响行数</returns>
    [Obsolete("请使用 DeleteByUserId")]
    public static Int32 DeleteByUserID(Int32 userId) => DeleteByUserId(userId);

    /// <summary>根据用户删除</summary>
    /// <param name="userId">用户</param>
    /// <returns></returns>
    public static Int32 DeleteByUserId(Int32 userId) => Delete(_.UserId == userId);

    /// <summary>根据用户统计参数数量</summary>
    /// <param name="userId">用户</param>
    /// <returns>参数数量</returns>
    [Obsolete("请使用 FindCountByUserId")]
    public static Int64 FindCountByUserID(Int32 userId) => FindCountByUserId(userId);

    /// <summary>根据用户统计数量</summary>
    /// <param name="userId">用户</param>
    /// <returns></returns>
    public static Int64 FindCountByUserId(Int32 userId) => FindCount(_.UserId == userId);
    #endregion

    #region 高级查询
    /// <summary>高级搜索</summary>
    /// <param name="userId"></param>
    /// <param name="category"></param>
    /// <param name="enable"></param>
    /// <param name="key"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    public static IList<Parameter> Search(Int32 userId, String category, Boolean? enable, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (userId >= 0) exp &= _.UserId == userId;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        if (enable != null) exp &= _.Enable == enable.Value;
        if (!key.IsNullOrEmpty()) exp &= _.Name == key | _.Value.Contains(key);

        return FindAll(exp, page);
    }

    /// <summary>获取 或 添加 参数，支持指定默认值</summary>
    /// <param name="userId"></param>
    /// <param name="category"></param>
    /// <param name="name"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static Parameter GetOrAdd(Int32 userId, String category, String name, String? defaultValue = null)
    {
        var list = FindAllByUserId(userId);
        var p = list.FirstOrDefault(e => e.Category == category && e.Name == name);
        if (p == null)
        {
            p = new Parameter { UserId = userId, Category = category, Name = name, Enable = true, Value = defaultValue };

            try
            {
                p.Insert();
            }
            catch
            {
                var p2 = Find(_.UserId == userId & _.Category == category & _.Name == name);
                if (p2 != null) return p2;
            }
        }

        return p;
    }
    #endregion

    #region 业务操作
    /// <summary>根据种类返回数据</summary>
    /// <returns></returns>
    public Object? GetValue()
    {
        var str = Value?.Trim();
        if (str.IsNullOrEmpty()) str = LongValue?.Trim();
        if (str.IsNullOrEmpty()) return null;

        switch (Kind)
        {
            case ParameterKinds.List: return GetList<String>();
            case ParameterKinds.Hash: return GetHash<String, String>();
            default:
                break;
        }

        switch (Kind)
        {
            case ParameterKinds.Boolean: return str.ToBoolean();
            case ParameterKinds.Int:
                var v = str.ToLong();
                return (v is >= Int32.MaxValue or <= Int32.MinValue) ? (Object)v : (Int32)v;
            case ParameterKinds.Double: return str.ToDouble();
            case ParameterKinds.DateTime: return str.ToDateTime();
            case ParameterKinds.String: return str;
        }

        return str;
    }

    /// <summary>设置数据，自动识别种类</summary>
    /// <param name="value"></param>
    public void SetValue(Object value)
    {
        if (value == null)
        {
            //Kind = ParameterKinds.Normal;
            Value = null;
            LongValue = null;
            Remark = null;
            return;
        }

        // 列表
        if (value is IList list)
        {
            SetList(list);
            return;
        }

        // 名值
        if (value is IDictionary dic)
        {
            SetHash(dic);
            return;
        }

        switch (value.GetType().GetTypeCode())
        {
            case TypeCode.Boolean:
                Kind = ParameterKinds.Boolean;
                SetValueInternal(value.ToString().ToLower());
                break;
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
                Kind = ParameterKinds.Int;
                SetValueInternal(value + "");
                break;
            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                Kind = ParameterKinds.Double;
                SetValueInternal(value + "");
                break;
            case TypeCode.DateTime:
                Kind = ParameterKinds.DateTime;
                SetValueInternal(((DateTime)value).ToFullString());
                break;
            case TypeCode.Char:
            case TypeCode.String:
                Kind = ParameterKinds.String;
                SetValueInternal(value + "");
                break;
            case TypeCode.Empty:
            case TypeCode.Object:
            case TypeCode.DBNull:
            default:
                Kind = ParameterKinds.Normal;
                SetValueInternal(value + "");
                break;
        }
    }

    private void SetValueInternal(String str)
    {
        if (str.Length < 200)
        {
            Value = str;
            LongValue = null;
        }
        else
        {
            Value = null;
            LongValue = str;
        }
    }

    /// <summary>获取列表</summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T[] GetList<T>()
    {
        var str = Value?.Trim();
        if (str.IsNullOrEmpty()) str = LongValue?.Trim();

        var arr = str.Split(",", ";", "，");
        return arr.Select(e => e.ChangeType<T>()!).ToArray();
    }

    /// <summary>获取名值对</summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    public IDictionary<TKey, TValue> GetHash<TKey, TValue>()
    {
        var str = Value?.Trim();
        if (str.IsNullOrEmpty()) str = LongValue?.Trim();

        var dic = str.SplitAsDictionary("=", ",");
        return dic.ToDictionary(e => e.Key.ChangeType<TKey>()!, e => e.Value.ChangeType<TValue>()!);
    }

    /// <summary>设置列表</summary>
    /// <param name="list"></param>
    public void SetList(IList list)
    {
        Kind = ParameterKinds.List;

        var sb = Pool.StringBuilder.Get();
        foreach (var item in list)
        {
            if (sb.Length > 0) sb.Append(',');
            sb.Append(item);
        }
        SetValueInternal(sb.Return(true));
    }

    /// <summary>设置名值对</summary>
    /// <param name="dic"></param>
    public void SetHash(IDictionary dic)
    {
        Kind = ParameterKinds.Hash;

        var sb = Pool.StringBuilder.Get();
        foreach (DictionaryEntry item in dic)
        {
            if (sb.Length > 0) sb.Append(',');
            sb.AppendFormat("{0}={1}", item.Key, item.Value);
        }
        SetValueInternal(sb.Return(true));
    }
    #endregion
}