using System.Linq.Expressions;

namespace XCode.Linq;

/// <summary>XCode LINQ 扩展方法。提供 WhereIf 等内联条件拼接</summary>
public static class XCodeLinqExtensions
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
}
