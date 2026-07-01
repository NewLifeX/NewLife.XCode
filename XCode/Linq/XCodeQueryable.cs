using System.Collections;
using LinqExpression = System.Linq.Expressions.Expression;

namespace XCode.Linq;

/// <summary>实体可查询对象。将标准LINQ操作符翻译为XCode实体查询</summary>
/// <typeparam name="T">实体类型</typeparam>
/// <remarks>
/// 为 XCode 实体提供 IQueryable&lt;T&gt; 接口，支持 Where/OrderBy/Skip/Take/ToList/First/Count 等标准 LINQ 操作。
/// 每次调用 Where/OrderBy 等方法会返回新的 EntityQueryable，延迟执行直到 ToList/First/Count 等终端方法。
/// </remarks>
public class EntityQueryable<T> : IQueryable<T>, IOrderedQueryable<T>
{
    #region 属性
    /// <summary>查询提供者</summary>
    public IQueryProvider Provider { get; }

    /// <summary>LINQ表达式</summary>
    public LinqExpression Expression { get; }

    /// <summary>元素类型</summary>
    public Type ElementType => typeof(T);
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    /// <param name="provider"></param>
    /// <param name="expression"></param>
    public EntityQueryable(IQueryProvider provider, LinqExpression expression)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    /// <summary>实例化（使用常量表达式）</summary>
    /// <param name="provider"></param>
    public EntityQueryable(IQueryProvider provider)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Expression = LinqExpression.Constant(this);
    }
    #endregion

    #region IEnumerable
    /// <summary>获取枚举器。触发查询执行</summary>
    /// <returns></returns>
    public IEnumerator<T> GetEnumerator()
    {
        var result = Provider.Execute<IList<T>>(Expression);
        return result.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    #endregion
}
