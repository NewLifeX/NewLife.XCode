using System;
using System.ComponentModel;
using System.Linq;
using NewLife;
using NewLife.Security;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Membership;

/// <summary>角色测试。操作共享 Membership 库，需与其他数据库测试串行，避免并行读写干扰</summary>
[Collection("Database")]
[DisplayName("角色测试")]
public class RoleTests
{
    [Fact]
    [DisplayName("权限序列化与解析")]
    public void Permission_RoundTrip()
    {
        var role = Role.Add("权限测试" + Rand.NextString(4), false, RoleTypes.普通, DataScopes.本部门);
        role.Set(1, PermissionFlags.Detail);
        role.Set(2, PermissionFlags.Update | PermissionFlags.Delete);
        role.Save();

        try
        {
            var role2 = Role.FindByID(role.ID);
            Assert.NotNull(role2);

            // 解析自 "1#1,2#12"
            Assert.True(role2.Has(1, PermissionFlags.Detail));
            Assert.True(role2.Has(2, PermissionFlags.Update));
            Assert.True(role2.Has(2, PermissionFlags.Delete));
            Assert.False(role2.Has(2, PermissionFlags.Insert));

            // 序列化格式：资源ID#权限值，逗号分隔，按资源ID升序
            Assert.Equal("1#1,2#12", role2.Permission);
        }
        finally
        {
            role.Delete();
        }
    }

    [Fact]
    [DisplayName("HasRoles判断菜单权限")]
    public void HasRoles_MenuPermission()
    {
        var menu = new Menu { Name = Rand.NextString(8), Url = "/test/" + Rand.NextString(6), Visible = true };
        menu.Insert();
        menu.Permissions[(Int32)PermissionFlags.Detail] = "查看";
        menu.Permissions[(Int32)PermissionFlags.Update] = "修改";
        menu.Update();

        var role = Role.Add("权限角色" + Rand.NextString(4), false, RoleTypes.普通, DataScopes.本部门);
        role.Set(menu.ID, PermissionFlags.Detail);
        role.Save();

        try
        {
            // 拥有查看权限
            Assert.True(Role.HasRoles(new[] { role }, menu, PermissionFlags.Detail));
            // 不拥有修改权限
            Assert.False(Role.HasRoles(new[] { role }, menu, PermissionFlags.Update));
            // 未指定权限子项：只要拥有资源即返回 true
            Assert.True(Role.HasRoles(new[] { role }, menu));
            // 空角色集合
            Assert.False(Role.HasRoles(Array.Empty<IRole>(), menu, PermissionFlags.Detail));
        }
        finally
        {
            role.Delete();
            menu.Delete();
        }
    }

    [Fact]
    [DisplayName("删除角色后清理用户角色引用")]
    public void DeleteRole_CleansUserReferences()
    {
        var roleA = Role.Add("清理角色A" + Rand.NextString(4), false, RoleTypes.普通, DataScopes.本部门);
        var roleB = Role.Add("清理角色B" + Rand.NextString(4), false, RoleTypes.普通, DataScopes.本部门);

        var user = new User { Name = Rand.NextString(16), RoleID = roleA.ID, RoleIds = "," + roleB.ID + "," };
        user.Insert();
        Assert.Equal(roleA.ID, user.RoleID);

        try
        {
            // 删除主角色A：用户主角色提升为角色B
            roleA.Delete();
            var user2 = User.FindByID(user.ID);
            Assert.Equal(roleB.ID, user2.RoleID);
            Assert.True(user2.RoleIds.IsNullOrEmpty());

            // 删除次角色B：用户无角色
            roleB.Delete();
            var user3 = User.FindByID(user.ID);
            Assert.Equal(0, user3.RoleID);
            Assert.True(user3.RoleIds.IsNullOrEmpty());
        }
        finally
        {
            // 幂等清理，防止断言失败时遗留数据
            User.ClearRole(roleA.ID);
            User.ClearRole(roleB.ID);
            user.Delete();
        }
    }

    [Fact]
    [DisplayName("普通角色默认数据范围不按名称推断")]
    public void Valid_DataScopeDefault_OrdinaryRole_NotByName()
    {
        // 名称含"高级"，但未显式指定数据范围 → 默认本部门（不再按名称推断为"本部门及下级"）
        var role = Role.Add("高级用户测试" + Rand.NextString(4), false);
        try
        {
            Assert.Equal(DataScopes.本部门, role.DataScope);
        }
        finally
        {
            role.Delete();
        }
    }

    [Fact]
    [DisplayName("系统角色默认数据范围为全部")]
    public void Valid_DataScopeDefault_SystemRole_All()
    {
        var role = Role.Add("系统角色测试" + Rand.NextString(4), true);
        try
        {
            Assert.Equal(DataScopes.全部, role.DataScope);
        }
        finally
        {
            // 系统角色禁止删除，先降级再清理
            role.IsSystem = false;
            role.Update();
            role.Delete();
        }
    }
}
