using System;
using System.ComponentModel;
using NewLife;
using NewLife.Log;
using NewLife.Security;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Membership;

/// <summary>用户测试。操作共享 Membership 库，需与其他数据库测试串行，避免并行读写干扰</summary>
[Collection("Database")]
public class UserTests
{
    [Fact]
    public void TestRoleIds()
    {
        var user = new User
        {
            Name = Rand.NextString(16),
            RoleIds = ",3,2,1,7,4",
        };
        user.Insert();

        Assert.Equal(1, user.RoleID);
        Assert.Equal(4, user.RoleIds.SplitAsInt().Length);
        Assert.Equal(",2,3,4,7,", user.RoleIds);

        var user2 = User.FindByKey(user.ID);
        Assert.Equal(1, user2.RoleID);
        Assert.Equal(4, user2.RoleIds.SplitAsInt().Length);
        Assert.Equal(",2,3,4,7,", user2.RoleIds);

        user2.RoleIds = "5,3,9,2,";
        user2.Update();

        var user3 = User.FindByKey(user.ID);
        Assert.Equal(1, user3.RoleID);
        Assert.Equal(4, user3.RoleIds.SplitAsInt().Length);
        Assert.Equal(",2,3,5,9,", user3.RoleIds);

        var dal = User.Meta.Session.Dal;
        var str = dal.QuerySingle<String>("select roleIds from user where id=@id", new { id = user.ID });
        Assert.Equal(",2,3,5,9,", str);

        //var ids = dal.QuerySingle<Int32[]>("select roleIds from user where id=@id", new { id = user.ID });
        //Assert.Equal(new[] { 2, 3, 5, 9 }, ids);
    }

    [Fact]
    public void StringLength()
    {
        var user = new User { Name = Rand.NextString(64) };
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => user.Insert());
        Assert.Equal("Name", ex.ParamName);
        Assert.Equal("[Name/名称@User]长度限制50字符[ID=0] (Parameter 'Name')", ex.Message);
    }

    [Fact]
    public void GetOrAdd()
    {
        var name = Rand.NextString(8);
        XTrace.WriteLine("GetOrAdd {0}", name);

        var user = User.GetOrAdd(name, k => User.FindByName(k), k => new User { Name = k });
        Assert.NotNull(user);

        XTrace.WriteLine("GetOrAdd2 {0}", name);

        var user2 = User.GetOrAdd(name, k => User.FindByName(k), k => new User { Name = k });
        Assert.NotNull(user2);
        Assert.Equal(user.ID, user2.ID);

        user.Delete();
    }

    [Fact]
    public void GetOrAdd3()
    {
        var name = Rand.NextString(8);

        //var u = new User { ["name"] = name };
        //u.Insert();
        //var u = new User();
        //u.SetItem("Name", name);
        //u.Insert();

        XTrace.WriteLine("GetOrAdd {0}", name);

        var user = User.GetOrAdd("name", name);
        Assert.NotNull(user);

        XTrace.WriteLine("GetOrAdd2 {0}", name);

        var user2 = User.GetOrAdd("name", name);
        Assert.NotNull(user2);
        Assert.Equal(user.ID, user2.ID);

        user.Delete();
    }

    [Fact]
    [DisplayName("新用户无角色时自动分配默认角色")]
    public void DefaultRole_WhenNoRoles()
    {
        // 确保默认角色存在
        var role = Role.Add("普通用户", false);
        Assert.NotNull(role);

        var user = new User { Name = Rand.NextString(16) };
        user.Insert();

        try
        {
            // 默认角色为"普通用户"或"游客"
            Assert.True(user.RoleID > 0);
        }
        finally
        {
            user.Delete();
        }
    }

    [Fact]
    [DisplayName("租户上下文下角色列表替代为租户关系角色")]
    public void GetRoleIDs_WithTenantContext_UsesTenantRoles()
    {
        var roleA = Role.Add("租户测试A" + Rand.NextString(4), false, RoleTypes.普通, DataScopes.本部门);
        var roleB = Role.Add("租户测试B" + Rand.NextString(4), false, RoleTypes.普通, DataScopes.本部门);

        var user = new User { Name = Rand.NextString(16), RoleID = roleA.ID };
        user.Insert();

        var tu = new TenantUser { TenantId = 9527, UserId = user.ID, RoleId = roleB.ID, Enable = true };
        tu.Insert();

        try
        {
            // 无租户上下文：返回自有角色
            TenantContext.Current = null!;
            Assert.Equal(new[] { roleA.ID }, user.GetRoleIDs());
            Assert.Equal(new[] { roleA.ID }, user.GetOwnRoleIDs());

            // 有租户上下文：以租户关系角色替代自有角色
            TenantContext.Current = new TenantContext { TenantId = 9527 };
            Assert.Equal(new[] { roleB.ID }, user.GetRoleIDs());
            Assert.Equal(new[] { roleA.ID }, user.GetOwnRoleIDs());
        }
        finally
        {
            TenantContext.Current = null!;
            tu.Delete();
            user.Delete();
            roleB.Delete();
            roleA.Delete();
        }
    }
}