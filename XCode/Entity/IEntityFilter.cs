using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using NewLife.Data;
using NewLife.Reflection;
using XCode.Configuration;
using XCode.DataAccessLayer;

#pragma warning disable CS0618 // 兼容层内部需要适配旧接口
namespace XCode;

/// <summary>实体操作上下文。在添删改查过程中传递实体、条件、结果与异常</summary>
public class EntityContext : IExtend
{
    #region 属性
    /// <summary>操作类型</summary>
    public DataMethod Action { get; set; }

    /// <summary>实体工厂</summary>
    public IEntityFactory? Factory { get; set; }

    /// <summary>实体会话</summary>
    public IEntitySession? Session { get; set; }

    /// <summary>是否为查询操作。新增查询枚举时需同步调整 FindSQL 上界</summary>
    public Boolean IsQuery => Action >= DataMethod.Find && Action <= DataMethod.FindSQL;

    /// <summary>是否为写入操作。Create 不在 Insert..Replace 区间内</summary>
    public Boolean IsWrite => Action >= DataMethod.Insert && Action <= DataMethod.Replace;

    /// <summary>创建实体对象时是否用于编辑</summary>
    public Boolean ForEdit { get; set; }

    /// <summary>单个实体。单条添删改时有效</summary>
    public IEntity? Entity { get; set; }

    /// <summary>实体列表。非 null 表示批量操作；单条操作使用 <see cref="Entity"/></summary>
    public IList<IEntity>? Entities { get; set; }

    /// <summary>原始SQL</summary>
    public String? Sql { get; set; }

    /// <summary>查询生成器</summary>
    public SelectBuilder? Builder { get; set; }

    /// <summary>操作结果。写入为影响行数，查询为数据表/实体列表/计数</summary>
    public Object? Result { get; set; }

    /// <summary>影响行数</summary>
    public Int32 AffectedRows { get; set; }

    /// <summary>异常</summary>
    public Exception? Exception { get; set; }

    /// <summary>异常已处理。过滤器处理异常后不再向外抛出</summary>
    public Boolean ExceptionHandled { get; set; }

    /// <summary>取消操作。跳过真实操作，但仍执行后置过滤器</summary>
    public Boolean Cancel { get; set; }

    private IDictionary<String, Object?>? _items;
    /// <summary>过滤器之间传递业务数据</summary>
    public IDictionary<String, Object?> Items => _items ??= new Dictionary<String, Object?>();

    /// <summary>按名称访问扩展数据</summary>
    /// <param name="key">键</param>
    /// <returns></returns>
    public Object? this[String key]
    {
        get => Items.TryGetValue(key, out var value) ? value : null;
        set => Items[key] = value;
    }
    #endregion

    #region 方法
    /// <summary>追加查询条件</summary>
    /// <param name="expression">条件表达式</param>
    public void AppendWhere(Expression? expression)
    {
        if (expression == null || expression.IsEmpty) return;
        if (Builder == null) return;

        var db = Session?.Dal.Db;
        var sql = expression.GetString(db, null);
        if (!sql.IsNullOrEmpty()) Builder.Where = Builder.Where.IsNullOrEmpty() ? sql : $"({Builder.Where}) And ({sql})";
    }

    /// <summary>追加查询条件字符串。调用方负责转义</summary>
    /// <param name="whereClause">条件字符串</param>
    public void AppendWhere(String? whereClause)
    {
        if (whereClause.IsNullOrEmpty() || Builder == null) return;

        Builder.Where = Builder.Where.IsNullOrEmpty() ? whereClause : $"({Builder.Where}) And ({whereClause})";
    }

    /// <summary>中止操作。安全校验失败时使用，操作不会执行，后置过滤器完成后抛出异常</summary>
    /// <param name="exception">异常</param>
    public void Abort(Exception exception)
    {
        Cancel = true;
        Exception = exception;
    }

    /// <summary>获取指定类型的实体</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <returns></returns>
    public TEntity? GetEntity<TEntity>() where TEntity : class, IEntity => Entity as TEntity;

    /// <summary>重置一次操作的上下文</summary>
    /// <param name="action">操作类型</param>
    /// <param name="factory">实体工厂</param>
    /// <param name="session">实体会话</param>
    internal void Prepare(DataMethod action, IEntityFactory? factory, IEntitySession? session)
    {
        Action = action;
        Factory = factory;
        Session = session;

        ForEdit = false;
        Entity = null;
        Entities = null;
        Sql = null;
        Builder = null;
        Result = null;
        AffectedRows = 0;
        Exception = null;
        ExceptionHandled = false;
        Cancel = false;
    }
    #endregion
}

/// <summary>实体操作过滤器。在添删改查前后执行，用于租户隔离、数据权限、审计等横切逻辑</summary>
/// <remarks>
/// 自 v12.4 起，<see cref="IEntityFilter"/> 是 XCode 唯一的实体操作管道。
/// 旧拦截器由 LegacyEntityFilter 适配，仅用于兼容已有代码。
/// </remarks>
public interface IEntityFilter
{
    /// <summary>优先级。越小越先执行，租户 -1000，数据权限 -900，审计 0</summary>
    Int32 Order { get; }

    /// <summary>为指定实体类初始化过滤器，返回是否支持</summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>是否支持该实体类型</returns>
    Boolean Init(Type entityType);

    /// <summary>操作执行前</summary>
    /// <param name="context">操作上下文</param>
    void OnExecuting(EntityContext context);

    /// <summary>操作执行后</summary>
    /// <param name="context">操作上下文</param>
    void OnExecuted(EntityContext context);
}

/// <summary>实体操作过滤器基类</summary>
public abstract class EntityFilter : IEntityFilter
{
    #region 属性
    /// <summary>优先级。越小越先执行</summary>
    public virtual Int32 Order { get; set; }

    /// <summary>自动清理超长字符串字段，默认为true</summary>
    /// <remarks>当设置字符串值时，如果超过字段长度限制则自动截断</remarks>
    public Boolean AutoTrim { get; set; } = true;
    #endregion

    #region 初始化
    private readonly ConcurrentDictionary<Type, Boolean> _inited = [];

    /// <summary>为指定实体类初始化过滤器，返回是否支持</summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>是否支持该实体类型</returns>
    public Boolean Init(Type entityType) => _inited.GetOrAdd(entityType, OnInit);

    /// <summary>为指定实体类初始化过滤器，返回是否支持</summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>是否支持该实体类型，默认返回true</returns>
    protected virtual Boolean OnInit(Type entityType) => true;
    #endregion

    #region 执行
    /// <summary>操作执行前</summary>
    /// <param name="context">操作上下文</param>
    public virtual void OnExecuting(EntityContext context) { }

    /// <summary>操作执行后</summary>
    /// <param name="context">操作上下文</param>
    public virtual void OnExecuted(EntityContext context) { }
    #endregion

    #region 辅助
    private static readonly ConcurrentDictionary<Type, FieldItem[]> _fields = [];

    /// <summary>获取实体类的字段信息，带缓存</summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>字段信息数组</returns>
    protected static FieldItem[] GetFields(Type entityType) => _fields.GetOrAdd(entityType, t => t.AsFactory().Fields);

    /// <summary>提前设置字段信息，加速初始化过程</summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="fields">字段信息数组</param>
    internal static void SetFields(Type entityType, FieldItem[] fields) => _fields.TryAdd(entityType, fields);

    /// <summary>获取本次操作涉及的实体列表。批量操作返回 Entities，单条操作返回只含 Entity 的列表</summary>
    /// <param name="context">操作上下文</param>
    /// <returns>实体列表</returns>
    protected static IList<IEntity> GetEntities(EntityContext context)
    {
        if (context.Entities is { Count: > 0 }) return context.Entities;
        if (context.Entity == null) return [];

        return [context.Entity];
    }

    /// <summary>设置未被修改的实体数据项</summary>
    /// <param name="fields">字段集合</param>
    /// <param name="entity">实体对象</param>
    /// <param name="name">字段名</param>
    /// <param name="value">要设置的值</param>
    /// <returns>是否成功设置了数据</returns>
    protected virtual Boolean SetNoDirtyItem(ICollection<FieldItem> fields, IEntity entity, String name, Object? value)
    {
        var fi = fields.FirstOrDefault(e => name.EqualIgnoreCase(e.Name, e.ColumnName));
        if (fi == null) return false;

        name = fi.Name;
        if (!entity.IsDirty(name)) return entity.SetItem(name, value);

        return false;
    }

    /// <summary>覆盖实体数据项，如果是默认值则无视脏数据标记</summary>
    /// <param name="fields">字段集合</param>
    /// <param name="entity">实体对象</param>
    /// <param name="name">字段名</param>
    /// <param name="value">要设置的值</param>
    /// <returns>是否成功设置了数据</returns>
    protected virtual Boolean SetItem(ICollection<FieldItem> fields, IEntity entity, String name, Object value)
    {
        var fi = fields.FirstOrDefault(e => name.EqualIgnoreCase(e.Name, e.ColumnName));
        if (fi == null) return false;

        name = fi.Name;
        if (fi.Type.IsInt())
        {
            if (entity[name].ToLong() != 0) return false;
        }
        else if (fi.Type == typeof(String))
        {
            if (entity[name] is String str && !str.IsNullOrEmpty()) return false;

            if (AutoTrim && value is String str2)
            {
                if (fi.Length > 0 && str2.Length > fi.Length) value = str2.Substring(0, fi.Length);
            }
        }
        else if (fi.Type == typeof(DateTime))
        {
            if (entity[name] is DateTime dt && dt.Year > 2000) return false;
        }

        return entity.SetItem(name, value);
    }
    #endregion
}

/// <summary>实体过滤器作用域。仅提供显式绕过过滤器的能力，例如系统维护、跨租户管理</summary>
/// <remarks>
/// 过滤器是默认执行路径，本类只用于明确表达“这里要越过过滤器”，不参与管道内部流程控制。
/// <code>
/// using var scope = EntityFilterScope.SuppressAll();
/// // 此范围内所有实体过滤器不执行
/// </code>
/// </remarks>
public static class EntityFilterScope
{
    #region 属性
    private static readonly Type _all = typeof(EntityFilterScope);

#if NET45
    private static readonly ThreadLocal<HashSet<Type>?> _suppressed = new();
#else
    private static readonly System.Threading.AsyncLocal<HashSet<Type>?> _suppressed = new();
#endif
    #endregion

    #region 方法
    /// <summary>是否存在抑制作用域</summary>
    internal static Boolean HasSuppressed => _suppressed.Value != null;

    /// <summary>判断指定过滤器是否被抑制</summary>
    /// <param name="filterType">过滤器类型</param>
    /// <returns></returns>
    internal static Boolean IsSuppressed(Type filterType)
    {
        var set = _suppressed.Value;
        if (set == null) return false;

        if (set.Contains(_all) || set.Contains(filterType)) return true;

        // 支持按基类/接口抑制
        foreach (var item in set)
        {
            if (item.IsAssignableFrom(filterType)) return true;
        }

        return false;
    }

    /// <summary>抑制指定类型的过滤器，返回可释放作用域</summary>
    /// <typeparam name="TFilter">过滤器类型</typeparam>
    /// <returns></returns>
    public static IDisposable Suppress<TFilter>() where TFilter : IEntityFilter => Push(typeof(TFilter));

    /// <summary>抑制所有过滤器，返回可释放作用域</summary>
    /// <returns></returns>
    public static IDisposable SuppressAll() => Push(_all);

    private static IDisposable Push(Type type)
    {
        var old = _suppressed.Value;
        var set = old == null ? [] : new HashSet<Type>(old);
        set.Add(type);
        _suppressed.Value = set;

        return new RestoreScope(() => _suppressed.Value = old);
    }

    private sealed class RestoreScope(Action restore) : IDisposable
    {
        private Action? _restore = restore;

        public void Dispose() => Interlocked.Exchange(ref _restore, null)?.Invoke();
    }
    #endregion
}

#pragma warning restore CS0618
