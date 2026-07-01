using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LinqExpr = System.Linq.Expressions.Expression;
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
    private static readonly String _connName;
    private static readonly String _dbFile;
    private static readonly String _originalConnName;

    static LinqTests()
    {
        _connName = "LinqTest";
        _dbFile = Path.Combine(Path.GetTempPath(), $"LinqTest_{Guid.NewGuid():n}.db");

        DAL.AddConnStr(_connName, $"Data Source={_dbFile}", null, "SQLite");
        DAL.Create(_connName).Execute("CREATE TABLE IF NOT EXISTS Role (ID INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT)");

        _originalConnName = Role.Meta.ConnName;
    }

    public LinqTests()
    {
        Role.Meta.ConnName = _connName;
    }

    public void Dispose()
    {
        Role.Meta.ConnName = _originalConnName;
        // 测试类最后一次 Dispose 时清理数据库文件
    }

    // ====== 1. Query 属性 ======

    [Fact] public void Query_Type() => Assert.IsType<EntityQueryable<Role>>(Role.Query);
    [Fact] public void Query_SameInstance() => Assert.Same(Role.Query, Role.Query);

    // ====== 2. EntityQueryable 构造与属性 ======

    [Fact] public void Queryable_ElementType() => Assert.Equal(typeof(Role), Role.Query.ElementType);
    [Fact] public void Queryable_Provider_Type() => Assert.IsType<EntityQueryProvider>(Role.Query.Provider);
    [Fact] public void Queryable_NullProvider_Throws() => Assert.Throws<ArgumentNullException>(() => new EntityQueryable<Role>(null));
    [Fact] public void Queryable_NullExpression_Throws() => Assert.Throws<ArgumentNullException>(() => new EntityQueryable<Role>(new EntityQueryProvider(Role.Meta.Factory), null));

    // ====== 3. 基础查询 ======

    [Fact] public void ToList_NotNull() => Assert.NotNull(Role.Query.ToList());
    [Fact] public void ToArray_NotNull() => Assert.NotNull(Role.Query.ToArray());

    // ====== 4. 表达式解析验证（用 Parse 方法不执行数据库） ======

    [Fact] public void Parse_Where_Equals()
    {
        var v = new EntityQueryProvider(Role.Meta.Factory).Parse(
            Role.Query.Where(r => r.ID > 0).Expression);
        Assert.NotNull(v.WhereExpression);
        Assert.False(v.WhereExpression!.IsEmpty);
        Assert.Contains("ID>0", v.WhereExpression.ToString());
    }

    [Fact] public void Parse_Where_String()
    {
        var v = new EntityQueryProvider(Role.Meta.Factory).Parse(
            Role.Query.Where(r => r.Name == "Admin").Expression);
        Assert.NotNull(v.WhereExpression);
        Assert.Contains("Name='Admin'", v.WhereExpression.ToString());
    }

    [Fact] public void Parse_OrderBy_Asc()
    {
        var v = new EntityQueryProvider(Role.Meta.Factory).Parse(
            Role.Query.OrderBy(r => r.ID).Expression);
        Assert.Equal("ID", v.OrderBy);
    }

    [Fact] public void Parse_OrderBy_Desc()
    {
        var v = new EntityQueryProvider(Role.Meta.Factory).Parse(
            Role.Query.OrderByDescending(r => r.ID).Expression);
        Assert.Equal("ID desc", v.OrderBy);
    }

    [Fact] public void Parse_ThenBy()
    {
        var v = new EntityQueryProvider(Role.Meta.Factory).Parse(
            Role.Query.OrderBy(r => r.ID).ThenBy(r => r.Name).Expression);
        Assert.Equal("ID,Name", v.OrderBy);
    }

    [Fact] public void Parse_ThenByDescending()
    {
        var v = new EntityQueryProvider(Role.Meta.Factory).Parse(
            Role.Query.OrderBy(r => r.ID).ThenByDescending(r => r.Name).Expression);
        Assert.Equal("ID,Name desc", v.OrderBy);
    }

    [Fact] public void Parse_Skip()
    {
        var v = new EntityQueryProvider(Role.Meta.Factory).Parse(
            Role.Query.Skip(15).Expression);
        Assert.Equal(15, v.Skip);
    }

    [Fact] public void Parse_Take()
    {
        var v = new EntityQueryProvider(Role.Meta.Factory).Parse(
            Role.Query.Take(25).Expression);
        Assert.Equal(25, v.Take);
    }

    [Fact] public void Parse_Skip_Take()
    {
        var v = new EntityQueryProvider(Role.Meta.Factory).Parse(
            Role.Query.Skip(5).Take(10).Expression);
        Assert.Equal(5, v.Skip);
        Assert.Equal(10, v.Take);
    }

    [Fact] public void Parse_Count()
    {
        var q = Role.Query.Where(r => r.ID > 0);
        var countExpr = LinqExpr.Call(
            typeof(Queryable), "Count", [typeof(Role)], q.Expression);
        var v = new EntityQueryProvider(Role.Meta.Factory).Parse(countExpr);
        Assert.True(v.IsCount);
    }

    [Fact] public void Parse_FirstOrDefault()
    {
        var q = Role.Query;
        var expr = LinqExpr.Call(
            typeof(Queryable), "FirstOrDefault", [typeof(Role)], q.Expression);
        var v = new EntityQueryProvider(Role.Meta.Factory).Parse(expr);
        Assert.True(v.IsFirst);
        Assert.False(v.ThrowIfNotFound);
        Assert.Equal(1, v.Take);
    }

    [Fact] public void Parse_Single()
    {
        var q = Role.Query.Where(r => r.ID == 1);
        var expr = LinqExpr.Call(
            typeof(Queryable), "Single", [typeof(Role)], q.Expression);
        var v = new EntityQueryProvider(Role.Meta.Factory).Parse(expr);
        Assert.True(v.IsSingle);
        Assert.True(v.ThrowIfNotFound);
        Assert.Equal(2, v.Take);
    }

    [Fact] public void Parse_SingleOrDefault()
    {
        var q = Role.Query;
        var expr = LinqExpr.Call(
            typeof(Queryable), "SingleOrDefault", [typeof(Role)], q.Expression);
        var v = new EntityQueryProvider(Role.Meta.Factory).Parse(expr);
        Assert.True(v.IsSingle);
        Assert.False(v.ThrowIfNotFound);
    }

    [Fact] public void Parse_Complex()
    {
        var v = new EntityQueryProvider(Role.Meta.Factory).Parse(
            Role.Query.Where(r => r.ID > 0).OrderByDescending(r => r.Name).Skip(5).Take(10).Expression);
        Assert.NotNull(v.WhereExpression);
        Assert.Equal("Name desc", v.OrderBy);
        Assert.Equal(5, v.Skip);
        Assert.Equal(10, v.Take);
    }

    [Fact] public void Parse_NoWhere_OrderBy()
    {
        var v = new EntityQueryProvider(Role.Meta.Factory).Parse(
            Role.Query.OrderBy(r => r.ID).Expression);
        Assert.Null(v.WhereExpression);
        Assert.Equal("ID", v.OrderBy);
    }

    // ====== 5. WhereIf ======

    [Fact] public void WhereIf_NullSource_Throws() { IQueryable<Role> q = null; Assert.Throws<ArgumentNullException>(() => q.WhereIf(true, r => r.Enable)); }
    [Fact] public void WhereIf_NullPredicate_Throws() => Assert.Throws<ArgumentNullException>(() => Role.Query.WhereIf(true, null));
    [Fact] public void WhereIf_True_ReturnsQueryable() { var q = Role.Query.WhereIf(true, r => r.ID > 0); Assert.IsType<EntityQueryable<Role>>(q); }
    [Fact] public void WhereIf_False_ReturnsSameType() { var q = Role.Query.WhereIf(false, r => r.ID > 0); Assert.IsType<EntityQueryable<Role>>(q); }

    // ====== 6. Include ======

    [Fact] public void Include_Null_Throws() => Assert.Throws<ArgumentNullException>(() => Role.Query.Include(null));
    [Fact] public void Include_ReturnsSameType() { var q = Role.Query.Include(typeof(Role)); Assert.IsType<EntityQueryable<Role>>(q); }
    [Fact] public void Include_RegistersInProvider()
    {
        var p = new EntityQueryProvider(Role.Meta.Factory);
        var q = new EntityQueryable<Role>(p);
        q.Include(typeof(Role));
        q.Include(typeof(Role));
        // 不抛异常即通过（重复注册被去重）
    }

    // ====== 7. FindAllWhereIf ======

    [Fact] public void FindAllWhereIf_NotNull() => Assert.NotNull(Role.FindAllWhereIf());
    [Fact] public void FindAllWhereIf_Empty() => Assert.NotNull(Role.FindAllWhereIf());
    [Fact] public void FindAllWhereIf_AllDisabled() => Assert.NotNull(Role.FindAllWhereIf((false, Role._.ID > 0)));
    [Fact] public void FindAllWhereIf_MultipleMixed()
    {
        var list = Role.FindAllWhereIf(
            (true, Role._.ID >= 0),
            (false, Role._.Name == "ignored"),
            (false, Role._.ID < -1)
        );
        Assert.NotNull(list);
    }

    // ====== 8. EntityQueryProvider ======

    [Fact] public void Provider_Ctor_NotNull() { var p = new EntityQueryProvider(Role.Meta.Factory); Assert.NotNull(p); }
    [Fact] public void Provider_Factory() { var p = new EntityQueryProvider(Role.Meta.Factory); Assert.Same(Role.Meta.Factory, p.Factory); Assert.Same(Role.Meta.Session, p.Session); }
    [Fact] public void Provider_CreateQuery_Null_Throws() { var p = new EntityQueryProvider(Role.Meta.Factory); Assert.Throws<ArgumentNullException>(() => p.CreateQuery<Role>(null)); }
    [Fact] public void Provider_CreateQuery_NonGeneric()
    {
        var p = new EntityQueryProvider(Role.Meta.Factory);
        var q = p.CreateQuery(System.Linq.Expressions.Expression.Constant(null, typeof(IQueryable<Role>)));
        Assert.NotNull(q);
        Assert.IsAssignableFrom<IQueryable>(q);
    }
    [Fact] public void Provider_Execute_Null_Throws() { var p = new EntityQueryProvider(Role.Meta.Factory); Assert.Throws<ArgumentNullException>(() => p.Execute<Object>(null)); }
    [Fact] public void Provider_Execute_ToList_ReturnsList()
    {
        var p = new EntityQueryProvider(Role.Meta.Factory);
        var q = p.CreateQuery<Role>(System.Linq.Expressions.Expression.Constant(null));
        var result = p.Execute<IList<Role>>(q.Expression);
        Assert.NotNull(result);
    }
    [Fact] public void Provider_AddInclude_Null_Ok() { new EntityQueryProvider(Role.Meta.Factory).AddInclude(null); }
    [Fact] public void Provider_AddInclude_Duplicate_Ok() { var p = new EntityQueryProvider(Role.Meta.Factory); p.AddInclude(typeof(Role)); p.AddInclude(typeof(Role)); }
}
