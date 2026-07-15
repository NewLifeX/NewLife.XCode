using System;
using XCode.Model;
using Xunit;

namespace XUnitTest.XCode.Navigation;

/// <summary>导航属性注册表测试</summary>
[Collection("Database")]
public class NavigationRegistryTests
{
    public NavigationRegistryTests()
    {
        NavigationRegistry.Global.Clear();
    }

    [Fact(DisplayName = "Register_New_ReturnsTrue")]
    public void Register_New_ReturnsTrue()
    {
        var nav = new NavigationProperty
        {
            Name = "Role",
            Type = NavigationType.HasOne,
            SourceType = typeof(FakeUser),
            TargetType = typeof(FakeRole),
        };

        var ok = NavigationRegistry.Global.Register(nav);
        Assert.True(ok);
        Assert.Equal(1, NavigationRegistry.Global.NavigationCount);
    }

    [Fact(DisplayName = "Register_Duplicate_ReturnsFalse")]
    public void Register_Duplicate_ReturnsFalse()
    {
        var nav = new NavigationProperty
        {
            Name = "Role",
            Type = NavigationType.HasOne,
            SourceType = typeof(FakeUser),
            TargetType = typeof(FakeRole),
        };

        NavigationRegistry.Global.Register(nav);
        var ok = NavigationRegistry.Global.Register(nav);
        Assert.False(ok);
        Assert.Equal(1, NavigationRegistry.Global.NavigationCount);
    }

    [Fact(DisplayName = "GetNavigations_ReturnsRegistered")]
    public void GetNavigations_ReturnsRegistered()
    {
        var nav1 = new NavigationProperty { Name = "Role", Type = NavigationType.HasOne, SourceType = typeof(FakeUser), TargetType = typeof(FakeRole) };
        var nav2 = new NavigationProperty { Name = "Orders", Type = NavigationType.HasMany, SourceType = typeof(FakeUser), TargetType = typeof(FakeOrder) };

        NavigationRegistry.Global.Register(nav1);
        NavigationRegistry.Global.Register(nav2);

        var navs = NavigationRegistry.Global.GetNavigations(typeof(FakeUser));
        Assert.Equal(2, navs.Count);
    }

    [Fact(DisplayName = "GetNavigations_UnknownType_ReturnsEmpty")]
    public void GetNavigations_UnknownType_ReturnsEmpty()
    {
        var navs = NavigationRegistry.Global.GetNavigations(typeof(String));
        Assert.Empty(navs);
    }

    [Fact(DisplayName = "Find_ByName_ReturnsNav")]
    public void Find_ByName_ReturnsNav()
    {
        var nav = new NavigationProperty { Name = "Role", Type = NavigationType.HasOne, SourceType = typeof(FakeUser), TargetType = typeof(FakeRole) };
        NavigationRegistry.Global.Register(nav);

        var found = NavigationRegistry.Global.Find(typeof(FakeUser), "Role");
        Assert.NotNull(found);
        Assert.Equal("Role", found!.Name);
        Assert.Equal(typeof(FakeRole), found.TargetType);
    }

    [Fact(DisplayName = "Find_UnknownName_ReturnsNull")]
    public void Find_UnknownName_ReturnsNull()
    {
        var found = NavigationRegistry.Global.Find(typeof(FakeUser), "Nonexistent");
        Assert.Null(found);
    }

    [Fact(DisplayName = "Clear_RemovesAll")]
    public void Clear_RemovesAll()
    {
        var nav = new NavigationProperty { Name = "Role", Type = NavigationType.HasOne, SourceType = typeof(FakeUser), TargetType = typeof(FakeRole) };
        NavigationRegistry.Global.Register(nav);

        NavigationRegistry.Global.Clear();
        Assert.Equal(0, NavigationRegistry.Global.NavigationCount);
        Assert.Equal(0, NavigationRegistry.Global.SourceTypeCount);
    }
}

// 测试用的假实体类型（不涉及数据库）
public class FakeUser { }
public class FakeRole { }
public class FakeOrder { }
