using System;
using System.Linq;
using NewLife;
using XCode;
using XCode.DataAccessLayer;
using XCode.Linq;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Linq;

/// <summary>LINQ 单元测试：覆盖 EntityQueryable / EntityQueryProvider / LinqExtensions / FindAllWhereIf 公开 API</summary>
[Collection("Database")]
public class LinqTests : IDisposable
{
    private String _connName;

    public LinqTests()
    {
        _connName = "LinqTest";
        var file = $"Data\\LinqTest_{Guid.NewGuid():n}.db";
        DAL.AddConnStr(_connName, $"Data Source={file}", null, "SQLite");
        DAL.Create(_connName).Execute("CREATE TABLE IF NOT EXISTS Role (ID INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT)");
        Role.Meta.ConnName = _connName;
    }

    public void Dispose() => Role.Meta.ConnName = "Membership";

    // ====== 1. Query 属性 ======

    [Fact] public void Query_Type() => Assert.IsType<EntityQueryable<Role>>(Role.Query);
    [Fact] public void Query_SameInstance() => Assert.Same(Role.Query, Role.Query);

    // ====== 2. EntityQueryable 构造与属性 ======

    [Fact] public void Queryable_ElementType() => Assert.Equal(typeof(Role), Role.Query.ElementType);
    [Fact] public void Queryable_Provider_Type() => Assert.IsType<EntityQueryProvider>(Role.Query.Provider);
    [Fact] public void Queryable_NullProvider_Throws() => Assert.Throws<ArgumentNullException>(() => new EntityQueryable<Role>(null));
    [Fact] public void Queryable_NullExpression_Throws() => Assert.Throws<ArgumentNullException>(() => new EntityQueryable<Role>(new EntityQueryProvider(Role.Meta.Factory), null));

    // ====== 3. 基础查询操作（无数据库依赖） ======

    [Fact] public void ToList_NotNull() => Assert.NotNull(Role.Query.ToList());
    [Fact] public void ToArray_NotNull() => Assert.NotNull(Role.Query.ToArray());

    // ====== 4. WhereIf 扩展 ======

    [Fact] public void WhereIf_NullSource_Throws() { IQueryable<Role> q = null; Assert.Throws<ArgumentNullException>(() => q.WhereIf(true, r => r.Enable)); }
    [Fact] public void WhereIf_NullPredicate_Throws() => Assert.Throws<ArgumentNullException>(() => Role.Query.WhereIf(true, null));

    // ====== 6. Include 预加载（null 参数验证） ======

    [Fact] public void Include_Null_Throws() => Assert.Throws<ArgumentNullException>(() => Role.Query.Include(null));

    // ====== 7. FindAllWhereIf ======

    [Fact] public void FindAllWhereIf_NotNull() => Assert.NotNull(Role.FindAllWhereIf());

    // ====== 8. EntityQueryProvider ======

    [Fact] public void Provider_Ctor_NotNull() { var p = new EntityQueryProvider(Role.Meta.Factory); Assert.NotNull(p); }
    [Fact] public void Provider_Factory() { var p = new EntityQueryProvider(Role.Meta.Factory); Assert.Same(Role.Meta.Factory, p.Factory); Assert.Same(Role.Meta.Session, p.Session); }
    [Fact] public void Provider_CreateQuery_Null_Throws() { var p = new EntityQueryProvider(Role.Meta.Factory); Assert.Throws<ArgumentNullException>(() => p.CreateQuery<Role>(null)); }
    [Fact] public void Provider_Execute_Null_Throws() { var p = new EntityQueryProvider(Role.Meta.Factory); Assert.Throws<ArgumentNullException>(() => p.Execute<Object>(null)); }
    [Fact] public void Provider_AddInclude_Null_Ok() { new EntityQueryProvider(Role.Meta.Factory).AddInclude(null); }
    [Fact] public void Provider_AddInclude_Duplicate_Ok() { var p = new EntityQueryProvider(Role.Meta.Factory); p.AddInclude(typeof(Role)); p.AddInclude(typeof(Role)); }
}
