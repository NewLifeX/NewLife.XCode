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

    /// <summary>分页查询。等价于 Skip((page - 1) * size).Take(size)</summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="source">查询源</param>
    /// <param name="page">页码，从 1 开始</param>
    /// <param name="size">每页大小</param>
    /// <returns></returns>
    /// <remarks>对齐 FreeSql 的 Page 用法，简化分页代码</remarks>
    /// <example>
    /// <code>
    /// var list = dal.Select&lt;User&gt;().Where(u => u.Enable == true).Page(2, 20).ToList();
    /// </code>
    /// </example>
    public static IQueryable<T> Page<T>(this IQueryable<T> source, Int32 page, Int32 size)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page), "页码必须大于等于 1");
        if (size < 0) throw new ArgumentOutOfRangeException(nameof(size), "每页大小不能为负数");

        return source.Skip((page - 1) * size).Take(size);
    }
}
