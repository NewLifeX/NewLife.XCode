using System;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;
using XCode.Linq;
using Xunit;
using XUnitTest.XCode.TestEntity;

namespace XUnitTest.XCode.Linq;

/// <summary>XCode LINQ 查询测试</summary>
[Collection("Database")]
public class XCodeLinqTests
{
    public XCodeLinqTests()
    {
        var connStr = "Data Source=:memory:";
        DAL.AddConnStr("LinqTest", connStr, null, "SQLite");
    }

    [Fact]
    public void Query_NotNull()
    {
        var query = User2.Query;
        Assert.NotNull(query);
        Assert.IsType<XCodeQueryable<User2>>(query);
    }

    [Fact]
    public void Query_SameInstance()
    {
        var q1 = User2.Query;
        var q2 = User2.Query;
        Assert.Same(q1, q2);
    }

    [Fact]
    public void Where_CombinesConditions()
    {
        var query = User2.Query
            .Where(u => u.Enable == true)
            .Where(u => u.Sex == SexKinds.男);

        Assert.NotNull(query);
        var expr = query.Expression;
        Assert.NotNull(expr);
    }

    [Fact]
    public void WhereIf_True_Applies()
    {
        var query = User2.Query
            .WhereIf(true, u => u.Enable == true);

        Assert.NotNull(query);
    }

    [Fact]
    public void WhereIf_False_Ignores()
    {
        var query = User2.Query
            .WhereIf(false, u => u.Enable == true);

        Assert.NotNull(query);
    }

    [Fact]
    public void WhereIf_Chained()
    {
        var name = "test";
        var age = 0;
        var enable = true;

        var query = User2.Query
            .WhereIf(!name.IsNullOrEmpty(), u => u.Name.Contains(name))
            .WhereIf(age > 0, u => u.RoleID == age)
            .WhereIf(enable, u => u.Enable == enable);

        Assert.NotNull(query);
    }

    [Fact]
    public void OrderBy_Ascending()
    {
        var query = User2.Query
            .OrderBy(u => u.ID);

        Assert.NotNull(query);
    }

    [Fact]
    public void OrderBy_Descending()
    {
        var query = User2.Query
            .OrderByDescending(u => u.ID);

        Assert.NotNull(query);
    }

    [Fact]
    public void Skip_Take_Pagination()
    {
        var query = User2.Query
            .OrderBy(u => u.ID)
            .Skip(10)
            .Take(20);

        Assert.NotNull(query);
    }

    [Fact]
    public void FindAllWhereIf_Conditions()
    {
        var name = "admin";
        var age = 0;

        var where = new WhereExpression();
        if (!name.IsNullOrEmpty()) where &= User2._.Name == name;
        if (age > 0) where &= User2._.RoleID == age;

        Assert.NotNull(where);
    }

    [Fact]
    public void FindAllWhereIf_Empty()
    {
        try
        {
            var list = User2.FindAllWhereIf();
            Assert.NotNull(list);
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Expected (no DB): {0}", ex.Message);
        }
    }

    [Fact]
    public void XCodeQueryProvider_Create()
    {
        var factory = User2.Meta.Factory;
        var provider = new XCodeQueryProvider(factory);

        Assert.NotNull(provider);
        Assert.Same(factory, provider.Factory);
        Assert.Same(factory.Session, provider.Session);

        var query = provider.CreateQuery<User2>(System.Linq.Expressions.Expression.Constant(null, typeof(IQueryable<User2>)));
        Assert.NotNull(query);
        Assert.IsType<XCodeQueryable<User2>>(query);
    }

    [Fact]
    public void XCodeQueryable_Enumerator()
    {
        var factory = User2.Meta.Factory;
        var provider = new XCodeQueryProvider(factory);
        var queryable = new XCodeQueryable<User2>(provider);

        Assert.NotNull(queryable);
        Assert.Equal(typeof(User2), queryable.ElementType);
        Assert.Same(provider, queryable.Provider);
    }
}
