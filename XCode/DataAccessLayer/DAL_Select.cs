using System.Linq;
using XCode.Linq;

namespace XCode.DataAccessLayer;

partial class DAL
{
    #region LINQ 查询入口
    /// <summary>启动实体流式 LINQ 查询，自动绑定当前 DAL 连接</summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <returns>可链式调用的 IQueryable&lt;T&gt;，支持 Where/WhereIf/OrderBy/Skip/Take/ToList 等标准 LINQ 操作</returns>
    /// <remarks>
    /// 与 <c>Entity&lt;T&gt;.Query</c>（始终使用默认连接）不同，<c>DAL.Select&lt;T&gt;()</c> 将查询绑定到当前 DAL 实例的连接，
    /// 解决多数据库连接场景下需要指定目标连接的问题。
    /// </remarks>
    /// <example>
    /// <code>
    /// var dal = DAL.Create("ConnA");
    /// var list = dal.Select&lt;User&gt;()
    ///     .WhereIf(!name.IsNullOrEmpty(), u => u.UserName.Contains(name))
    ///     .WhereIf(age > 0, u => u.Age == age)
    ///     .OrderByDescending(u => u.Id)
    ///     .Skip((page - 1) * size).Take(size)
    ///     .ToList();
    /// </code>
    /// </example>
    public IQueryable<T> Select<T>() where T : Entity<T>, new()
    {
        var factory = typeof(T).AsFactory();
        var provider = new EntityQueryProvider(factory, ConnName);
        return new EntityQueryable<T>(provider);
    }
    #endregion
}
