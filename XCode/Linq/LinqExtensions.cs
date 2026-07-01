using System.Linq;
using System.Linq.Expressions;

namespace XCode.Linq;

/// <summary>LINQ 扩展方法。提供 WhereIf、Include 等增强查询方法</summary>
public static class LinqExtensions
{
    /// <summary>内联条件判断。条件满足时才应用Where，否则忽略</summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="source">查询源</param>
    /// <param name="condition">条件，为true时应用predicate</param>
    /// <param name="predicate">过滤表达式</param>
    /// <returns></returns>
    /// <example>
    /// <code>
    /// var list = UserInfo.Query
    ///     .WhereIf(!name.IsNullOrEmpty(), u => u.Name.Contains(name))
    ///     .WhereIf(age > 0, u => u.Age == age)
    ///     .WhereIf(deptId > 0, u => u.DeptId == deptId)
    ///     .OrderByDescending(u => u.Id)
    ///     .ToList();
    /// </code>
    /// </example>
    public static IQueryable<T> WhereIf<T>(this IQueryable<T> source, Boolean condition, Expression<Func<T, Boolean>> predicate)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));

        return condition ? source.Where(predicate) : source;
    }

    /// <summary>预加载关联实体到缓存。与XCode内存联表策略互补</summary>
    /// <typeparam name="T">主实体类型</typeparam>
    /// <param name="source">查询源</param>
    /// <param name="relatedType">关联实体类型</param>
    /// <returns></returns>
    /// <remarks>
    /// Include 会预加载关联实体的整表缓存（EntityCache），使后续的内存联表操作直接命中缓存。
    /// 适用于小表关联场景，与 XCode 内存联表策略互补。
    /// </remarks>
    /// <example>
    /// <code>
    /// var users = UserInfo.Query.Include(typeof(Role)).Where(u => u.Enable == true).ToList();
    /// </code>
    /// </example>
    public static IQueryable<T> Include<T>(this IQueryable<T> source, Type relatedType)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (relatedType == null) throw new ArgumentNullException(nameof(relatedType));

        var provider = source.Provider as EntityQueryProvider;
        provider?.AddInclude(relatedType);

        return source;
    }
}
