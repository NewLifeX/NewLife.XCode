using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewLife;
using XCode;
using XCode.DataAccessLayer;
using XCode.Linq;
using XCode.Membership;
using XCode.Model;
using Xunit;

namespace XUnitTest.XCode.Navigation;

/// <summary>Include 导航预加载测试</summary>
[Collection("Database")]
public class IncludeTests : IDisposable
{
    private static readonly String _connName;
    private static readonly String _dbFile;

    static IncludeTests()
    {
        _connName = "NavIncludeTest";
        _dbFile = Path.Combine(Path.GetTempPath(), $"NavIncludeTest_{Guid.NewGuid():n}.db");

        DAL.AddConnStr(_connName, $"Data Source={_dbFile}", null, "SQLite");
        DAL.Create(_connName).Execute("CREATE TABLE IF NOT EXISTS Role (ID INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT)");
        DAL.Create(_connName).Execute("CREATE TABLE IF NOT EXISTS Department (ID INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT)");
    }

    public IncludeTests()
    {
        Role.Meta.ConnName = _connName;
        Department.Meta.ConnName = _connName;
    }

    public void Dispose()
    {
    }

    // ====== Include(Type) ======

    [Fact(DisplayName = "Include_Type_ReturnsSameType")]
    public void Include_Type_ReturnsSameType()
    {
        var q = Role.Query.Include(typeof(Role));
        Assert.IsType<EntityQueryable<Role>>(q);
    }

    [Fact(DisplayName = "Include_Type_Null_Throws")]
    public void Include_Type_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Role.Query.Include(null));
    }

    [Fact(DisplayName = "Include_RegistersInProvider")]
    public void Include_RegistersInProvider()
    {
        var q = (EntityQueryable<Role>)Role.Query;
        var provider = (EntityQueryProvider)q.Provider;
        provider.AddInclude(typeof(Role));
        // 如果没抛异常说明注册成功
    }

    // ====== 导航属性 Include 与注册 ======

    [Fact(DisplayName = "Include_Navigation_WithRegisteredNav_DoesNotThrow")]
    public void Include_Navigation_WithRegisteredNav_DoesNotThrow()
    {
        // 注册一个模拟导航
        var nav = new NavigationProperty
        {
            Name = "Role",
            Type = NavigationType.HasOne,
            SourceType = typeof(Role),
            TargetType = typeof(Role),
        };
        NavigationRegistry.Global.Register(nav);

        // Include 通过 Type 参数预加载
        var q = Role.Query.Include(typeof(Role));
        Assert.NotNull(q);
        Assert.IsAssignableFrom<IQueryable<Role>>(q);
    }

    [Fact(DisplayName = "NavigationRegistry_Register_GetNavigations")]
    public void NavigationRegistry_Register_GetNavigations()
    {
        var nav = new NavigationProperty
        {
            Name = "Department",
            Type = NavigationType.HasOne,
            SourceType = typeof(Role),
            TargetType = typeof(Department),
        };
        NavigationRegistry.Global.Register(nav);

        var navs = NavigationRegistry.Global.GetNavigations(typeof(Role));
        Assert.Contains(navs, n => n.Name == "Department");
    }

    [Fact(DisplayName = "NavigationRegistry_Global_NotNull")]
    public void NavigationRegistry_Global_NotNull()
    {
        Assert.NotNull(NavigationRegistry.Global);
    }
}
