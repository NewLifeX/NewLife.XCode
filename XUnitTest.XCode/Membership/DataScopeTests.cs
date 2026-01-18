using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using NewLife;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Membership;

/// <summary>数据权限测试</summary>
/// <remarks>
/// 围绕 IUserScope、IDepartmentScope、IDataScope、IDataScopeFieldProvider、IFieldScope、
/// DataScopeContext、DataScopeInterceptor 以及相关辅助类进行全面测试
/// </remarks>
[DisplayName("数据权限测试")]
public class DataScopeTests : IDisposable
{
    public DataScopeTests()
    {
        // 每个测试前清理上下文
        DataScopeContext.Current = null;
        DataScopeContext.ClearCache();
    }

    public void Dispose()
    {
        // 清理上下文
        DataScopeContext.Current = null;
        DataScopeContext.ClearCache();
    }

    #region DataScopeContext 静态属性测试
    [Fact]
    [DisplayName("Current属性设置和获取")]
    public void DataScopeContext_Current_SetAndGet()
    {
        // Arrange
        var ctx = new DataScopeContext { UserId = 123, DepartmentId = 456 };

        // Act
        DataScopeContext.Current = ctx;

        // Assert
        Assert.Same(ctx, DataScopeContext.Current);
        Assert.Equal(123, DataScopeContext.Current.UserId);
        Assert.Equal(456, DataScopeContext.Current.DepartmentId);
    }

    [Fact]
    [DisplayName("Current为null时返回null")]
    public void DataScopeContext_Current_WhenNull_ReturnsNull()
    {
        // Arrange
        DataScopeContext.Current = null;

        // Act & Assert
        Assert.Null(DataScopeContext.Current);
    }

    [Fact]
    [DisplayName("IsSystem在DataScope为全部时返回true")]
    public void DataScopeContext_IsSystem_WhenScopeAll_ReturnsTrue()
    {
        // Arrange
        var ctx = new DataScopeContext { DataScope = DataScopes.全部 };

        // Act & Assert
        Assert.True(ctx.IsSystem);
    }

    [Fact]
    [DisplayName("IsSystem在DataScope不为全部时返回false")]
    public void DataScopeContext_IsSystem_WhenScopeNotAll_ReturnsFalse()
    {
        // Arrange
        var ctx = new DataScopeContext { DataScope = DataScopes.仅本人 };

        // Act & Assert
        Assert.False(ctx.IsSystem);
    }
    #endregion

    #region DataScopeContext.Create 测试
    [Fact]
    [DisplayName("Create用户为null时返回null")]
    public void DataScopeContext_Create_NullUser_ReturnsNull()
    {
        // Act
        var result = DataScopeContext.Create(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    [DisplayName("Create设置基本属性")]
    public void DataScopeContext_Create_SetsBasicProperties()
    {
        // Arrange
        var user = new MockUser { ID = 100, DepartmentID = 200 };
        var role = new MockRole { ID = 1, IsSystem = false, DataScope = DataScopes.本部门 };
        user.Role = role;
        user.Roles = [role];

        // Act
        var result = DataScopeContext.Create(user);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(100, result.UserId);
        Assert.Equal(200, result.DepartmentId);
        Assert.Equal(DataScopes.本部门, result.DataScope);
    }

    [Fact]
    [DisplayName("Create系统角色返回全部权限")]
    public void DataScopeContext_Create_SystemRole_ReturnsAllScope()
    {
        // Arrange
        var user = new MockUser { ID = 100, DepartmentID = 200 };
        var role = new MockRole { ID = 1, IsSystem = true, DataScope = DataScopes.仅本人 };
        user.Role = role;
        user.Roles = [role];

        // Act
        var result = DataScopeContext.Create(user);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(DataScopes.全部, result.DataScope);
        Assert.True(result.IsSystem);
        Assert.True(result.ViewSensitive);
    }

    [Fact]
    [DisplayName("Create多角色取最大权限")]
    public void DataScopeContext_Create_MultipleRoles_TakesMaxPermission()
    {
        // Arrange
        var user = new MockUser { ID = 100, DepartmentID = 200 };
        var role1 = new MockRole { ID = 1, IsSystem = false, DataScope = DataScopes.仅本人 };
        var role2 = new MockRole { ID = 2, IsSystem = false, DataScope = DataScopes.本部门及下级 };
        user.Role = role1;
        user.Roles = [role1, role2];

        // Act
        var result = DataScopeContext.Create(user);

        // Assert
        Assert.NotNull(result);
        // 本部门及下级(1) < 仅本人(3)，取最小值即最大权限
        Assert.Equal(DataScopes.本部门及下级, result.DataScope);
    }

    [Fact]
    [DisplayName("Create无角色时默认仅本人")]
    public void DataScopeContext_Create_NoRoles_DefaultsToSelfOnly()
    {
        // Arrange
        var user = new MockUser { ID = 100, DepartmentID = 200 };
        user.Role = null!;
        user.Roles = [];

        // Act
        var result = DataScopeContext.Create(user);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(DataScopes.仅本人, result.DataScope);
    }

    [Fact]
    [DisplayName("Create敏感字段权限任一角色有权即可")]
    public void DataScopeContext_Create_ViewSensitive_AnyRoleHasPermission()
    {
        // Arrange
        var user = new MockUser { ID = 100, DepartmentID = 200 };
        var role1 = new MockRole { ID = 1, IsSystem = false, DataScope = DataScopes.本部门, ViewSensitive = false };
        var role2 = new MockRole { ID = 2, IsSystem = false, DataScope = DataScopes.本部门, ViewSensitive = true };
        user.Role = role1;
        user.Roles = [role1, role2];

        // Act
        var result = DataScopeContext.Create(user);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ViewSensitive);
    }
    #endregion

    #region DataScopeContext.ClearCache 测试
    [Fact]
    [DisplayName("ClearCache清除指定用户缓存")]
    public void DataScopeContext_ClearCache_SpecificUser()
    {
        // Arrange & Act
        DataScopeContext.ClearCache(100);

        // Assert - 不应抛出异常
    }

    [Fact]
    [DisplayName("ClearCache清除所有缓存")]
    public void DataScopeContext_ClearCache_All()
    {
        // Arrange & Act
        DataScopeContext.ClearCache(0);

        // Assert - 不应抛出异常
    }
    #endregion

    #region DataScopeContext 多线程测试
    [Fact]
    [DisplayName("Current在多线程下隔离")]
    public void DataScopeContext_Current_ThreadIsolation()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext { UserId = 1 };
        var thread2UserId = 0;

        // Act
        var task = Task.Run(() =>
        {
            DataScopeContext.Current = new DataScopeContext { UserId = 2 };
            thread2UserId = DataScopeContext.Current?.UserId ?? 0;
        });
        task.Wait();

        // Assert
        Assert.Equal(1, DataScopeContext.Current?.UserId); // 主线程仍然是 1
        Assert.Equal(2, thread2UserId); // 子线程是 2
    }
    #endregion

    #region DataScopeInterceptor 测试
    [Fact]
    [DisplayName("OnInit实现IDataScope返回true")]
    public void DataScopeInterceptor_OnInit_WithIDataScope_ReturnsTrue()
    {
        // Arrange
        var module = new DataScopeInterceptor();

        // Act
        var result = module.Init(typeof(DataScopeTestEntity));

        // Assert
        Assert.True(result);
    }

    [Fact]
    [DisplayName("OnInit实现IUserScope返回true")]
    public void DataScopeInterceptor_OnInit_WithIUserScope_ReturnsTrue()
    {
        // Arrange
        var module = new DataScopeInterceptor();

        // Act
        var result = module.Init(typeof(UserScopeTestEntity));

        // Assert
        Assert.True(result);
    }

    [Fact]
    [DisplayName("OnInit实现IDepartmentScope返回true")]
    public void DataScopeInterceptor_OnInit_WithIDepartmentScope_ReturnsTrue()
    {
        // Arrange
        var module = new DataScopeInterceptor();

        // Act
        var result = module.Init(typeof(DepartmentScopeTestEntity));

        // Assert
        Assert.True(result);
    }

    [Fact]
    [DisplayName("OnInit未实现任何接口返回false")]
    public void DataScopeInterceptor_OnInit_WithoutInterface_ReturnsFalse()
    {
        // Arrange
        var module = new DataScopeInterceptor();

        // Act
        var result = module.Init(typeof(NoScopeTestEntity));

        // Assert
        Assert.False(result);
    }

    [Fact]
    [DisplayName("OnCreate有上下文时自动设置UserId")]
    public void DataScopeInterceptor_OnCreate_WithContext_SetsUserId()
    {
        // Arrange
        var module = new DataScopeInterceptor();
        DataScopeContext.Current = new DataScopeContext { UserId = 789, DepartmentId = 456 };
        var entity = new UserScopeTestEntity();

        // Act
        module.Create(entity, false);

        // Assert
        Assert.Equal(789, entity.UserId);
    }

    [Fact]
    [DisplayName("OnCreate有上下文时自动设置DepartmentId")]
    public void DataScopeInterceptor_OnCreate_WithContext_SetsDepartmentId()
    {
        // Arrange
        var module = new DataScopeInterceptor();
        DataScopeContext.Current = new DataScopeContext { UserId = 789, DepartmentId = 456 };
        var entity = new DepartmentScopeTestEntity();

        // Act
        module.Create(entity, false);

        // Assert
        Assert.Equal(456, entity.DepartmentId);
    }

    [Fact]
    [DisplayName("OnCreate有上下文时自动设置IDataScope")]
    public void DataScopeInterceptor_OnCreate_WithContext_SetsDataScope()
    {
        // Arrange
        var module = new DataScopeInterceptor();
        DataScopeContext.Current = new DataScopeContext { UserId = 789, DepartmentId = 456 };
        var entity = new DataScopeTestEntity();

        // Act
        module.Create(entity, false);

        // Assert
        Assert.Equal(789, entity.UserId);
        Assert.Equal(456, entity.DepartmentId);
    }

    [Fact]
    [DisplayName("OnCreate无上下文时不设置")]
    public void DataScopeInterceptor_OnCreate_WithoutContext_DoesNotSet()
    {
        // Arrange
        var module = new DataScopeInterceptor();
        DataScopeContext.Current = null;
        var entity = new DataScopeTestEntity { UserId = 0, DepartmentId = 0 };

        // Act
        module.Create(entity, false);

        // Assert
        Assert.Equal(0, entity.UserId);
        Assert.Equal(0, entity.DepartmentId);
    }

    [Fact]
    [DisplayName("OnCreate已有值时不覆盖")]
    public void DataScopeInterceptor_OnCreate_ExistingValue_DoesNotOverwrite()
    {
        // Arrange
        var module = new DataScopeInterceptor();
        DataScopeContext.Current = new DataScopeContext { UserId = 789, DepartmentId = 456 };
        var entity = new DataScopeTestEntity { UserId = 100, DepartmentId = 200 };

        // Act
        module.Create(entity, false);

        // Assert
        Assert.Equal(100, entity.UserId);
        Assert.Equal(200, entity.DepartmentId);
    }
    #endregion

    #region DataScopeInterceptor.OnValid Insert 测试
    [Fact]
    [DisplayName("OnValid_Insert_系统角色通过")]
    public void DataScopeInterceptor_OnValid_Insert_SystemRole_Passes()
    {
        // Arrange
        var module = new DataScopeInterceptor();
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 100,
            DepartmentId = 200,
            DataScope = DataScopes.全部
        };
        var entity = new DataScopeTestEntity { UserId = 999, DepartmentId = 888 };

        // Act
        var result = module.Valid(entity, DataMethod.Insert);

        // Assert
        Assert.True(result);
    }

    [Fact]
    [DisplayName("OnValid_Insert_UserId为0时自动设置")]
    public void DataScopeInterceptor_OnValid_Insert_ZeroUserId_AutoSets()
    {
        // Arrange
        var module = new DataScopeInterceptor();
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 100,
            DepartmentId = 200,
            DataScope = DataScopes.仅本人
        };
        var entity = new UserScopeTestEntity { UserId = 0 };

        // Act
        var result = module.Valid(entity, DataMethod.Insert);

        // Assert
        Assert.True(result);
        Assert.Equal(100, entity.UserId);
    }

    [Fact]
    [DisplayName("OnValid_Insert_DepartmentId为0时自动设置")]
    public void DataScopeInterceptor_OnValid_Insert_ZeroDepartmentId_AutoSets()
    {
        // Arrange
        var module = new DataScopeInterceptor();
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 100,
            DepartmentId = 200,
            DataScope = DataScopes.本部门,
            AccessibleDepartmentIds = [200]
        };
        var entity = new DepartmentScopeTestEntity { DepartmentId = 0 };

        // Act
        var result = module.Valid(entity, DataMethod.Insert);

        // Assert
        Assert.True(result);
        Assert.Equal(200, entity.DepartmentId);
    }

    [Fact]
    [DisplayName("OnValid_Insert_无上下文时通过")]
    public void DataScopeInterceptor_OnValid_Insert_NoContext_Passes()
    {
        // Arrange
        var module = new DataScopeInterceptor();
        DataScopeContext.Current = null;
        var entity = new DataScopeTestEntity { UserId = 999, DepartmentId = 888 };

        // Act
        var result = module.Valid(entity, DataMethod.Insert);

        // Assert
        Assert.True(result);
    }
    #endregion

    #region DataScopeInterceptor.OnValid Update/Delete 测试
    [Fact]
    [DisplayName("OnValid_Update_无脏数据时跳过校验")]
    public void DataScopeInterceptor_OnValid_Update_NoDirty_SkipsValidation()
    {
        // Arrange
        var module = new DataScopeInterceptor();
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 100,
            DepartmentId = 200,
            DataScope = DataScopes.仅本人
        };
        var entity = new DataScopeTestEntity { UserId = 999, DepartmentId = 888 };
        // 不修改任何属性，所以没有脏数据

        // Act
        var result = module.Valid(entity, DataMethod.Update);

        // Assert
        Assert.True(result);
    }

    [Fact]
    [DisplayName("OnValid_Update_系统角色通过")]
    public void DataScopeInterceptor_OnValid_Update_SystemRole_Passes()
    {
        // Arrange
        var module = new DataScopeInterceptor();
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 100,
            DepartmentId = 200,
            DataScope = DataScopes.全部
        };
        var entity = new DataScopeTestEntity { UserId = 999, DepartmentId = 888 };
        entity.Name = "test"; // 制造脏数据

        // Act
        var result = module.Valid(entity, DataMethod.Update);

        // Assert
        Assert.True(result);
    }

    [Fact]
    [DisplayName("OnValid_Delete_系统角色通过")]
    public void DataScopeInterceptor_OnValid_Delete_SystemRole_Passes()
    {
        // Arrange
        var module = new DataScopeInterceptor();
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 100,
            DepartmentId = 200,
            DataScope = DataScopes.全部
        };
        var entity = new DataScopeTestEntity { UserId = 999, DepartmentId = 888 };

        // Act
        var result = module.Valid(entity, DataMethod.Delete);

        // Assert
        Assert.True(result);
    }

    [Fact]
    [DisplayName("OnValid_Delete_仅本人_本人数据通过")]
    public void DataScopeInterceptor_OnValid_Delete_SelfOnly_OwnData_Passes()
    {
        // Arrange
        var module = new DataScopeInterceptor();
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 100,
            DepartmentId = 200,
            DataScope = DataScopes.仅本人
        };
        var entity = new UserScopeTestEntity { UserId = 100 };

        // Act
        var result = module.Valid(entity, DataMethod.Delete);

        // Assert
        Assert.True(result);
    }

    [Fact]
    [DisplayName("OnValid_Delete_本部门_同部门数据通过")]
    public void DataScopeInterceptor_OnValid_Delete_Department_SameDept_Passes()
    {
        // Arrange
        var module = new DataScopeInterceptor();
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 100,
            DepartmentId = 200,
            DataScope = DataScopes.本部门,
            AccessibleDepartmentIds = [200]
        };
        var entity = new DepartmentScopeTestEntity { DepartmentId = 200 };

        // Act
        var result = module.Valid(entity, DataMethod.Delete);

        // Assert
        Assert.True(result);
    }
    #endregion

    #region DataScopeHelper.CanAccess 测试
    [Fact]
    [DisplayName("CanAccess_IDataScope_无上下文返回true")]
    public void DataScopeHelper_CanAccess_IDataScope_NoContext_ReturnsTrue()
    {
        // Arrange
        DataScopeContext.Current = null;
        var entity = new DataScopeTestEntity { UserId = 100, DepartmentId = 200 };

        // Act
        var result = DataScopeHelper.CanAccess((IDataScope)entity);

        // Assert
        Assert.True(result);
    }

    [Fact]
    [DisplayName("CanAccess_IDataScope_系统角色返回true")]
    public void DataScopeHelper_CanAccess_IDataScope_SystemRole_ReturnsTrue()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext { DataScope = DataScopes.全部 };
        var entity = new DataScopeTestEntity { UserId = 999, DepartmentId = 888 };

        // Act
        var result = DataScopeHelper.CanAccess((IDataScope)entity);

        // Assert
        Assert.True(result);
    }

    [Fact]
    [DisplayName("CanAccess_IDataScope_仅本人_本人数据返回true")]
    public void DataScopeHelper_CanAccess_IDataScope_SelfOnly_OwnData_ReturnsTrue()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 100,
            DataScope = DataScopes.仅本人
        };
        var entity = new DataScopeTestEntity { UserId = 100, DepartmentId = 200 };

        // Act
        var result = DataScopeHelper.CanAccess((IDataScope)entity);

        // Assert
        Assert.True(result);
    }

    [Fact]
    [DisplayName("CanAccess_IDataScope_仅本人_他人数据返回false")]
    public void DataScopeHelper_CanAccess_IDataScope_SelfOnly_OthersData_ReturnsFalse()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 100,
            DataScope = DataScopes.仅本人
        };
        var entity = new DataScopeTestEntity { UserId = 999, DepartmentId = 200 };

        // Act
        var result = DataScopeHelper.CanAccess((IDataScope)entity);

        // Assert
        Assert.False(result);
    }

    [Fact]
    [DisplayName("CanAccess_IDataScope_本部门_同部门返回true")]
    public void DataScopeHelper_CanAccess_IDataScope_Department_SameDept_ReturnsTrue()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 100,
            DepartmentId = 200,
            DataScope = DataScopes.本部门,
            AccessibleDepartmentIds = [200]
        };
        var entity = new DataScopeTestEntity { UserId = 999, DepartmentId = 200 };

        // Act
        var result = DataScopeHelper.CanAccess((IDataScope)entity);

        // Assert
        Assert.True(result);
    }

    [Fact]
    [DisplayName("CanAccess_IDataScope_本部门_不同部门返回false")]
    public void DataScopeHelper_CanAccess_IDataScope_Department_DiffDept_ReturnsFalse()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 100,
            DepartmentId = 200,
            DataScope = DataScopes.本部门,
            AccessibleDepartmentIds = [200]
        };
        var entity = new DataScopeTestEntity { UserId = 999, DepartmentId = 300 };

        // Act
        var result = DataScopeHelper.CanAccess((IDataScope)entity);

        // Assert
        Assert.False(result);
    }

    [Fact]
    [DisplayName("CanAccess_IUserScope_无上下文返回true")]
    public void DataScopeHelper_CanAccess_IUserScope_NoContext_ReturnsTrue()
    {
        // Arrange
        DataScopeContext.Current = null;
        var entity = new UserScopeTestEntity { UserId = 100 };

        // Act
        var result = DataScopeHelper.CanAccess((IUserScope)entity);

        // Assert
        Assert.True(result);
    }

    [Fact]
    [DisplayName("CanAccess_IUserScope_系统角色返回true")]
    public void DataScopeHelper_CanAccess_IUserScope_SystemRole_ReturnsTrue()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext { DataScope = DataScopes.全部 };
        var entity = new UserScopeTestEntity { UserId = 999 };

        // Act
        var result = DataScopeHelper.CanAccess((IUserScope)entity);

        // Assert
        Assert.True(result);
    }

    [Fact]
    [DisplayName("CanAccess_IUserScope_本人数据返回true")]
    public void DataScopeHelper_CanAccess_IUserScope_OwnData_ReturnsTrue()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 100,
            DataScope = DataScopes.仅本人
        };
        var entity = new UserScopeTestEntity { UserId = 100 };

        // Act
        var result = DataScopeHelper.CanAccess((IUserScope)entity);

        // Assert
        Assert.True(result);
    }

    [Fact]
    [DisplayName("CanAccess_IUserScope_他人数据返回false")]
    public void DataScopeHelper_CanAccess_IUserScope_OthersData_ReturnsFalse()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 100,
            DataScope = DataScopes.仅本人
        };
        var entity = new UserScopeTestEntity { UserId = 999 };

        // Act
        var result = DataScopeHelper.CanAccess((IUserScope)entity);

        // Assert
        Assert.False(result);
    }

    [Fact]
    [DisplayName("CanAccess_IDepartmentScope_无上下文返回true")]
    public void DataScopeHelper_CanAccess_IDepartmentScope_NoContext_ReturnsTrue()
    {
        // Arrange
        DataScopeContext.Current = null;
        var entity = new DepartmentScopeTestEntity { DepartmentId = 200 };

        // Act
        var result = DataScopeHelper.CanAccess((IDepartmentScope)entity);

        // Assert
        Assert.True(result);
    }

    [Fact]
    [DisplayName("CanAccess_IDepartmentScope_系统角色返回true")]
    public void DataScopeHelper_CanAccess_IDepartmentScope_SystemRole_ReturnsTrue()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext { DataScope = DataScopes.全部 };
        var entity = new DepartmentScopeTestEntity { DepartmentId = 999 };

        // Act
        var result = DataScopeHelper.CanAccess((IDepartmentScope)entity);

        // Assert
        Assert.True(result);
    }

    [Fact]
    [DisplayName("CanAccess_IDepartmentScope_可访问部门返回true")]
    public void DataScopeHelper_CanAccess_IDepartmentScope_AccessibleDept_ReturnsTrue()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext
        {
            DepartmentId = 200,
            DataScope = DataScopes.本部门,
            AccessibleDepartmentIds = [200, 201, 202]
        };
        var entity = new DepartmentScopeTestEntity { DepartmentId = 201 };

        // Act
        var result = DataScopeHelper.CanAccess((IDepartmentScope)entity);

        // Assert
        Assert.True(result);
    }

    [Fact]
    [DisplayName("CanAccess_IDepartmentScope_不可访问部门返回false")]
    public void DataScopeHelper_CanAccess_IDepartmentScope_InaccessibleDept_ReturnsFalse()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext
        {
            DepartmentId = 200,
            DataScope = DataScopes.本部门,
            AccessibleDepartmentIds = [200]
        };
        var entity = new DepartmentScopeTestEntity { DepartmentId = 300 };

        // Act
        var result = DataScopeHelper.CanAccess((IDepartmentScope)entity);

        // Assert
        Assert.False(result);
    }

    [Fact]
    [DisplayName("CanAccess_IDepartmentScope_可访问部门为null返回true")]
    public void DataScopeHelper_CanAccess_IDepartmentScope_NullAccessibleDepts_ReturnsTrue()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext
        {
            DepartmentId = 200,
            DataScope = DataScopes.本部门,
            AccessibleDepartmentIds = null
        };
        var entity = new DepartmentScopeTestEntity { DepartmentId = 300 };

        // Act
        var result = DataScopeHelper.CanAccess((IDepartmentScope)entity);

        // Assert
        Assert.True(result);
    }
    #endregion

    #region DataScopeHelper.GetAccessibleDepartmentIds 测试
    [Fact]
    [DisplayName("GetAccessibleDepartmentIds_无角色返回空数组")]
    public void DataScopeHelper_GetAccessibleDepartmentIds_NoRoles_ReturnsEmpty()
    {
        // Act
        var result = DataScopeHelper.GetAccessibleDepartmentIds(100, []);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    [DisplayName("GetAccessibleDepartmentIds_系统角色返回null")]
    public void DataScopeHelper_GetAccessibleDepartmentIds_SystemRole_ReturnsNull()
    {
        // Arrange
        var roles = new IRole[] { new MockRole { IsSystem = true } };

        // Act
        var result = DataScopeHelper.GetAccessibleDepartmentIds(100, roles);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    [DisplayName("GetAccessibleDepartmentIds_全部权限返回null")]
    public void DataScopeHelper_GetAccessibleDepartmentIds_AllScope_ReturnsNull()
    {
        // Arrange
        var roles = new IRole[] { new MockRole { IsSystem = false, DataScope = DataScopes.全部 } };

        // Act
        var result = DataScopeHelper.GetAccessibleDepartmentIds(100, roles, DataScopes.全部);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    [DisplayName("GetAccessibleDepartmentIds_本部门返回单部门")]
    public void DataScopeHelper_GetAccessibleDepartmentIds_DepartmentScope_ReturnsSingleDept()
    {
        // Arrange
        var roles = new IRole[] { new MockRole { IsSystem = false, DataScope = DataScopes.本部门 } };

        // Act
        var result = DataScopeHelper.GetAccessibleDepartmentIds(100, roles, DataScopes.本部门);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Contains(100, result);
    }

    [Fact]
    [DisplayName("GetAccessibleDepartmentIds_仅本人返回空数组")]
    public void DataScopeHelper_GetAccessibleDepartmentIds_SelfOnly_ReturnsEmpty()
    {
        // Arrange
        var roles = new IRole[] { new MockRole { IsSystem = false, DataScope = DataScopes.仅本人 } };

        // Act
        var result = DataScopeHelper.GetAccessibleDepartmentIds(100, roles, DataScopes.仅本人);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    [DisplayName("GetAccessibleDepartmentIds_自定义合并角色部门")]
    public void DataScopeHelper_GetAccessibleDepartmentIds_Custom_MergesRoleDepts()
    {
        // Arrange
        var role1 = new MockRole { IsSystem = false, DataScope = DataScopes.自定义, DataDepartmentIds = "100,101" };
        var role2 = new MockRole { IsSystem = false, DataScope = DataScopes.自定义, DataDepartmentIds = "102,103" };
        var roles = new IRole[] { role1, role2 };

        // Act
        var result = DataScopeHelper.GetAccessibleDepartmentIds(100, roles, DataScopes.自定义);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(100, result);
        Assert.Contains(101, result);
        Assert.Contains(102, result);
        Assert.Contains(103, result);
    }

    [Fact]
    [DisplayName("GetAccessibleDepartmentIds_部门Id为0时返回空")]
    public void DataScopeHelper_GetAccessibleDepartmentIds_ZeroDeptId_ReturnsEmpty()
    {
        // Arrange
        var roles = new IRole[] { new MockRole { IsSystem = false, DataScope = DataScopes.本部门 } };

        // Act
        var result = DataScopeHelper.GetAccessibleDepartmentIds(0, roles, DataScopes.本部门);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
    #endregion

    #region DataScopeHelper.ParseDepartmentIds 测试
    [Fact]
    [DisplayName("ParseDepartmentIds_空字符串返回空数组")]
    public void DataScopeHelper_ParseDepartmentIds_Empty_ReturnsEmpty()
    {
        // Act
        var result = DataScopeHelper.ParseDepartmentIds("");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    [DisplayName("ParseDepartmentIds_null返回空数组")]
    public void DataScopeHelper_ParseDepartmentIds_Null_ReturnsEmpty()
    {
        // Act
        var result = DataScopeHelper.ParseDepartmentIds(null);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    [DisplayName("ParseDepartmentIds_正常解析")]
    public void DataScopeHelper_ParseDepartmentIds_Normal_Parses()
    {
        // Act
        var result = DataScopeHelper.ParseDepartmentIds("100,200,300");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
        Assert.Contains(100, result);
        Assert.Contains(200, result);
        Assert.Contains(300, result);
    }
    #endregion

    #region DataScopeHelper.ApplyDataScope 测试
    [Fact]
    [DisplayName("ApplyDataScope_有上下文时添加条件")]
    public void DataScopeHelper_ApplyDataScope_WithContext_AddsCondition()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 123,
            DataScope = DataScopes.仅本人
        };
        var where = new WhereExpression();

        // Act
        var result = where.ApplyDataScope<UserScopeTestEntity>();

        // Assert
        var sql = result.ToString();
        Assert.Contains("UserId", sql);
        Assert.Contains("123", sql);
    }

    [Fact]
    [DisplayName("ApplyDataScope_无上下文时不添加条件")]
    public void DataScopeHelper_ApplyDataScope_NoContext_DoesNotAddCondition()
    {
        // Arrange
        DataScopeContext.Current = null;
        var where = new WhereExpression();

        // Act
        var result = where.ApplyDataScope<UserScopeTestEntity>();

        // Assert
        Assert.True(result.IsEmpty);
    }

    [Fact]
    [DisplayName("ApplyDataScope_系统角色不添加条件")]
    public void DataScopeHelper_ApplyDataScope_SystemRole_DoesNotAddCondition()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 123,
            DataScope = DataScopes.全部
        };
        var where = new WhereExpression();

        // Act
        var result = where.ApplyDataScope<UserScopeTestEntity>();

        // Assert
        Assert.True(result.IsEmpty);
    }

    [Fact]
    [DisplayName("ApplyDataScope_部门范围添加部门条件")]
    public void DataScopeHelper_ApplyDataScope_DepartmentScope_AddsDeptCondition()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 123,
            DepartmentId = 456,
            DataScope = DataScopes.本部门,
            AccessibleDepartmentIds = [456]
        };
        var where = new WhereExpression();

        // Act
        var result = where.ApplyDataScope<DepartmentScopeTestEntity>();

        // Assert
        var sql = result.ToString();
        Assert.Contains("DepartmentId", sql);
        Assert.Contains("456", sql);
    }

    [Fact]
    [DisplayName("ApplyDataScope_工厂为null时返回原条件")]
    public void DataScopeHelper_ApplyDataScope_NullFactory_ReturnsOriginal()
    {
        // Arrange
        var where = new WhereExpression();

        // Act
        var result = where.ApplyDataScope(null!);

        // Assert
        Assert.Same(where, result);
    }
    #endregion

    #region DataScopeHelper.ApplyScope 测试
    [Fact]
    [DisplayName("ApplyScope_同时应用租户和数据权限")]
    public void DataScopeHelper_ApplyScope_AppliesBothFilters()
    {
        // Arrange
        TenantContext.Current = new TenantContext { TenantId = 100 };
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 123,
            DataScope = DataScopes.仅本人
        };
        var where = new WhereExpression();

        // Act
        var result = where.ApplyScope<TenantAndDataScopeTestEntity>();

        // Assert
        var sql = result.ToString();
        // 应同时包含租户和用户条件
        Assert.Contains("TenantId", sql);
        Assert.Contains("UserId", sql);
    }
    #endregion

    #region FieldScopeHelper.MaskSensitiveFields 测试
    [Fact]
    [DisplayName("MaskSensitiveFields_非IFieldScope不处理")]
    public void FieldScopeHelper_MaskSensitiveFields_NonFieldScope_NoAction()
    {
        // Arrange
        var entity = new NoScopeTestEntity { Name = "test" };

        // Act
        var result = FieldScopeHelper.MaskSensitiveFields(entity);

        // Assert
        Assert.Same(entity, result);
    }

    [Fact]
    [DisplayName("MaskSensitiveFields_有权限不遮蔽")]
    public void FieldScopeHelper_MaskSensitiveFields_HasPermission_NoMask()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 100,
            ViewSensitive = true
        };
        var entity = new FieldScopeTestEntity { UserId = 100, Password = "secret123" };

        // Act
        var result = FieldScopeHelper.MaskSensitiveFields(entity);

        // Assert
        Assert.Equal("secret123", ((FieldScopeTestEntity)result).Password);
    }

    [Fact]
    [DisplayName("MaskSensitiveFields_本人数据不遮蔽")]
    public void FieldScopeHelper_MaskSensitiveFields_OwnData_NoMask()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 100,
            ViewSensitive = false
        };
        var entity = new FieldScopeTestEntity { UserId = 100, Password = "secret123" };

        // Act
        var result = FieldScopeHelper.MaskSensitiveFields(entity);

        // Assert
        Assert.Equal("secret123", ((FieldScopeTestEntity)result).Password);
    }

    [Fact]
    [DisplayName("MaskSensitiveFields_无权限遮蔽敏感字段")]
    public void FieldScopeHelper_MaskSensitiveFields_NoPermission_Masks()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 100,
            ViewSensitive = false
        };
        var entity = new FieldScopeTestEntity { UserId = 200, Password = "secret123" };

        // Act
        var result = FieldScopeHelper.MaskSensitiveFields(entity);

        // Assert
        Assert.Equal("***", ((FieldScopeTestEntity)result).Password);
    }

    [Fact]
    [DisplayName("MaskSensitiveFields_自定义遮蔽值")]
    public void FieldScopeHelper_MaskSensitiveFields_CustomMaskValue()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 100,
            ViewSensitive = false
        };
        var entity = new FieldScopeTestEntity { UserId = 200, Password = "secret123" };

        // Act
        var result = FieldScopeHelper.MaskSensitiveFields(entity, maskValue: "******");

        // Assert
        Assert.Equal("******", ((FieldScopeTestEntity)result).Password);
    }

    [Fact]
    [DisplayName("MaskSensitiveFields_无上下文使用实体判断")]
    public void FieldScopeHelper_MaskSensitiveFields_NoContext_UsesEntityLogic()
    {
        // Arrange
        DataScopeContext.Current = null;
        ManageProvider.Provider = null;
        var entity = new FieldScopeTestEntity { UserId = 100, Password = "secret123" };

        // Act
        var result = FieldScopeHelper.MaskSensitiveFields(entity);

        // Assert
        // 无上下文且无当前用户时，应该遮蔽
        Assert.Equal("***", ((FieldScopeTestEntity)result).Password);
    }

    [Fact]
    [DisplayName("MaskSensitiveFields_批量遮蔽")]
    public void FieldScopeHelper_MaskSensitiveFields_BatchMask()
    {
        // Arrange
        DataScopeContext.Current = new DataScopeContext
        {
            UserId = 100,
            ViewSensitive = false
        };
        var list = new List<FieldScopeTestEntity>
        {
            new() { UserId = 100, Password = "pass1" }, // 本人，不遮蔽
            new() { UserId = 200, Password = "pass2" }, // 他人，遮蔽
            new() { UserId = 300, Password = "pass3" }  // 他人，遮蔽
        };

        // Act
        var result = FieldScopeHelper.MaskSensitiveFields(list);

        // Assert
        Assert.Equal("pass1", result[0].Password); // 本人数据不遮蔽
        Assert.Equal("***", result[1].Password);
        Assert.Equal("***", result[2].Password);
    }

    [Fact]
    [DisplayName("MaskSensitiveFields_空列表返回空")]
    public void FieldScopeHelper_MaskSensitiveFields_EmptyList_ReturnsEmpty()
    {
        // Act
        var result = FieldScopeHelper.MaskSensitiveFields<FieldScopeTestEntity>([]);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    [DisplayName("MaskSensitiveFields_null列表返回null")]
    public void FieldScopeHelper_MaskSensitiveFields_NullList_ReturnsNull()
    {
        // Act
        var result = FieldScopeHelper.MaskSensitiveFields<FieldScopeTestEntity>(null!);

        // Assert
        Assert.Null(result);
    }
    #endregion

    #region IDataScopeFieldProvider 测试
    [Fact]
    [DisplayName("IDataScopeFieldProvider_自定义用户字段")]
    public void IDataScopeFieldProvider_CustomUserField()
    {
        // Arrange
        var entity = new CustomFieldEntity();

        // Act
        var field = entity.GetUserField();

        // Assert
        Assert.NotNull(field);
        Assert.Equal("CreatorId", field.Name);
    }

    [Fact]
    [DisplayName("IDataScopeFieldProvider_自定义部门字段")]
    public void IDataScopeFieldProvider_CustomDepartmentField()
    {
        // Arrange
        var entity = new CustomFieldEntity();

        // Act
        var field = entity.GetDepartmentField();

        // Assert
        Assert.NotNull(field);
        Assert.Equal("OrgId", field.Name);
    }

    [Fact]
    [DisplayName("IDataScopeFieldProvider_自定义租户字段")]
    public void IDataScopeFieldProvider_CustomTenantField()
    {
        // Arrange
        var entity = new CustomFieldEntity();

        // Act
        var field = entity.GetTenantField();

        // Assert
        Assert.NotNull(field);
        Assert.Equal("CompanyId", field.Name);
    }
    #endregion

    #region User 类接口实现测试
    [Fact]
    [DisplayName("User实现IDataScope")]
    public void User_Implements_IDataScope()
    {
        // Arrange
        var user = new User { ID = 100, DepartmentID = 200 };
        var dataScope = (IDataScope)user;

        // Act & Assert
        Assert.Equal(100, dataScope.UserId);
        Assert.Equal(200, dataScope.DepartmentId);

        // 测试设置
        dataScope.UserId = 101;
        dataScope.DepartmentId = 201;
        Assert.Equal(101, user.ID);
        Assert.Equal(201, user.DepartmentID);
    }

    [Fact]
    [DisplayName("User实现IFieldScope")]
    public void User_Implements_IFieldScope()
    {
        // Arrange
        var user = new User { ID = 100, Password = "test123" };
        var fieldScope = (IFieldScope)user;

        // Act
        var sensitiveFields = fieldScope.GetSensitiveFields();
        var canView = fieldScope.CanViewSensitiveFields(100);
        var cannotView = fieldScope.CanViewSensitiveFields(200);

        // Assert
        Assert.Contains("Password", sensitiveFields);
        Assert.True(canView);
        Assert.False(cannotView);
    }

    [Fact]
    [DisplayName("User实现IDataScopeFieldProvider")]
    public void User_Implements_IDataScopeFieldProvider()
    {
        // Arrange
        var user = new User();
        var provider = (IDataScopeFieldProvider)user;

        // Act
        var userField = provider.GetUserField();
        var deptField = provider.GetDepartmentField();
        var tenantField = provider.GetTenantField();

        // Assert
        Assert.NotNull(userField);
        Assert.Equal("ID", userField.Name);
        Assert.NotNull(deptField);
        Assert.Equal("DepartmentID", deptField.Name);
        Assert.Null(tenantField);
    }
    #endregion

    #region DataScopes 枚举测试
    [Fact]
    [DisplayName("DataScopes枚举值正确")]
    public void DataScopes_EnumValues_Correct()
    {
        // Assert
        Assert.Equal(0, (Int32)DataScopes.全部);
        Assert.Equal(1, (Int32)DataScopes.本部门及下级);
        Assert.Equal(2, (Int32)DataScopes.本部门);
        Assert.Equal(3, (Int32)DataScopes.仅本人);
        Assert.Equal(4, (Int32)DataScopes.自定义);
    }
    #endregion
}

#region 测试用实体类
/// <summary>实现 IDataScope 的测试实体（同时具有用户和部门）</summary>
[Serializable]
[DataObject]
[Description("数据权限测试实体")]
[BindTable("DataScopeTestEntity", Description = "数据权限测试实体", ConnName = "test_datascope", DbType = DatabaseType.None)]
public partial class DataScopeTestEntity : Entity<DataScopeTestEntity>, IDataScope
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String? _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "")]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Int32 _UserId;
    /// <summary>用户标识</summary>
    [DisplayName("用户标识")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UserId", "用户标识", "")]
    public Int32 UserId { get => _UserId; set { if (OnPropertyChanging("UserId", value)) { _UserId = value; OnPropertyChanged("UserId"); } } }

    private Int32 _DepartmentId;
    /// <summary>部门标识</summary>
    [DisplayName("部门标识")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DepartmentId", "部门标识", "")]
    public Int32 DepartmentId { get => _DepartmentId; set { if (OnPropertyChanging("DepartmentId", value)) { _DepartmentId = value; OnPropertyChanged("DepartmentId"); } } }
    #endregion

    #region 获取/设置 字段值
    public override Object? this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "Name" => _Name,
            "UserId" => _UserId,
            "DepartmentId" => _DepartmentId,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "UserId": _UserId = value.ToInt(); break;
                case "DepartmentId": _DepartmentId = value.ToInt(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 字段名
    public partial class _
    {
        public static readonly Field Id = FindByName("Id");
        public static readonly Field Name = FindByName("Name");
        public static readonly Field UserId = FindByName("UserId");
        public static readonly Field DepartmentId = FindByName("DepartmentId");
        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }
    #endregion
}

/// <summary>仅实现 IUserScope 的测试实体</summary>
[Serializable]
[DataObject]
[Description("用户范围测试实体")]
[BindTable("UserScopeTestEntity", Description = "用户范围测试实体", ConnName = "test_datascope", DbType = DatabaseType.None)]
public partial class UserScopeTestEntity : Entity<UserScopeTestEntity>, IUserScope
{
    #region 属性
    private Int32 _Id;
    [DisplayName("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String? _Name;
    [DisplayName("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "")]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Int32 _UserId;
    [DisplayName("用户标识")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UserId", "用户标识", "")]
    public Int32 UserId { get => _UserId; set { if (OnPropertyChanging("UserId", value)) { _UserId = value; OnPropertyChanged("UserId"); } } }
    #endregion

    #region 获取/设置 字段值
    public override Object? this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "Name" => _Name,
            "UserId" => _UserId,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "UserId": _UserId = value.ToInt(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 字段名
    public partial class _
    {
        public static readonly Field Id = FindByName("Id");
        public static readonly Field Name = FindByName("Name");
        public static readonly Field UserId = FindByName("UserId");
        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }
    #endregion
}

/// <summary>仅实现 IDepartmentScope 的测试实体</summary>
[Serializable]
[DataObject]
[Description("部门范围测试实体")]
[BindTable("DepartmentScopeTestEntity", Description = "部门范围测试实体", ConnName = "test_datascope", DbType = DatabaseType.None)]
public partial class DepartmentScopeTestEntity : Entity<DepartmentScopeTestEntity>, IDepartmentScope
{
    #region 属性
    private Int32 _Id;
    [DisplayName("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String? _Name;
    [DisplayName("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "")]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Int32 _DepartmentId;
    [DisplayName("部门标识")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DepartmentId", "部门标识", "")]
    public Int32 DepartmentId { get => _DepartmentId; set { if (OnPropertyChanging("DepartmentId", value)) { _DepartmentId = value; OnPropertyChanged("DepartmentId"); } } }
    #endregion

    #region 获取/设置 字段值
    public override Object? this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "Name" => _Name,
            "DepartmentId" => _DepartmentId,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "DepartmentId": _DepartmentId = value.ToInt(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 字段名
    public partial class _
    {
        public static readonly Field Id = FindByName("Id");
        public static readonly Field Name = FindByName("Name");
        public static readonly Field DepartmentId = FindByName("DepartmentId");
        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }
    #endregion
}

/// <summary>未实现任何数据权限接口的测试实体</summary>
[Serializable]
[DataObject]
[Description("无范围测试实体")]
[BindTable("NoScopeTestEntity", Description = "无范围测试实体", ConnName = "test_datascope", DbType = DatabaseType.None)]
public partial class NoScopeTestEntity : Entity<NoScopeTestEntity>
{
    #region 属性
    private Int32 _Id;
    [DisplayName("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String? _Name;
    [DisplayName("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "")]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }
    #endregion

    #region 获取/设置 字段值
    public override Object? this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "Name" => _Name,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 字段名
    public partial class _
    {
        public static readonly Field Id = FindByName("Id");
        public static readonly Field Name = FindByName("Name");
        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }
    #endregion
}

/// <summary>实现 IFieldScope 的测试实体（带敏感字段）</summary>
[Serializable]
[DataObject]
[Description("字段范围测试实体")]
[BindTable("FieldScopeTestEntity", Description = "字段范围测试实体", ConnName = "test_datascope", DbType = DatabaseType.None)]
public partial class FieldScopeTestEntity : Entity<FieldScopeTestEntity>, IUserScope, IFieldScope
{
    #region 属性
    private Int32 _Id;
    [DisplayName("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _UserId;
    [DisplayName("用户标识")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UserId", "用户标识", "")]
    public Int32 UserId { get => _UserId; set { if (OnPropertyChanging("UserId", value)) { _UserId = value; OnPropertyChanged("UserId"); } } }

    private String? _Password;
    [DisplayName("密码")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Password", "密码", "")]
    public String? Password { get => _Password; set { if (OnPropertyChanging("Password", value)) { _Password = value; OnPropertyChanged("Password"); } } }
    #endregion

    #region IFieldScope
    public String[] GetSensitiveFields() => ["Password"];
    public Boolean CanViewSensitiveFields(Int32 userId) => userId == UserId;
    #endregion

    #region 获取/设置 字段值
    public override Object? this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "UserId" => _UserId,
            "Password" => _Password,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "UserId": _UserId = value.ToInt(); break;
                case "Password": _Password = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 字段名
    public partial class _
    {
        public static readonly Field Id = FindByName("Id");
        public static readonly Field UserId = FindByName("UserId");
        public static readonly Field Password = FindByName("Password");
        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }
    #endregion
}

/// <summary>同时实现 ITenantScope 和 IUserScope 的测试实体</summary>
[Serializable]
[DataObject]
[Description("租户和数据权限测试实体")]
[BindTable("TenantAndDataScopeTestEntity", Description = "租户和数据权限测试实体", ConnName = "test_datascope", DbType = DatabaseType.None)]
public partial class TenantAndDataScopeTestEntity : Entity<TenantAndDataScopeTestEntity>, ITenantScope, IUserScope
{
    #region 属性
    private Int32 _Id;
    [DisplayName("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _TenantId;
    [DisplayName("租户标识")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TenantId", "租户标识", "")]
    public Int32 TenantId { get => _TenantId; set { if (OnPropertyChanging("TenantId", value)) { _TenantId = value; OnPropertyChanged("TenantId"); } } }

    private Int32 _UserId;
    [DisplayName("用户标识")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UserId", "用户标识", "")]
    public Int32 UserId { get => _UserId; set { if (OnPropertyChanging("UserId", value)) { _UserId = value; OnPropertyChanged("UserId"); } } }
    #endregion

    #region 获取/设置 字段值
    public override Object? this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "TenantId" => _TenantId,
            "UserId" => _UserId,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "TenantId": _TenantId = value.ToInt(); break;
                case "UserId": _UserId = value.ToInt(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 字段名
    public partial class _
    {
        public static readonly Field Id = FindByName("Id");
        public static readonly Field TenantId = FindByName("TenantId");
        public static readonly Field UserId = FindByName("UserId");
        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }
    #endregion
}

/// <summary>实现 IDataScopeFieldProvider 的自定义字段实体</summary>
[Serializable]
[DataObject]
[Description("自定义字段实体")]
[BindTable("CustomFieldEntity", Description = "自定义字段实体", ConnName = "test_datascope", DbType = DatabaseType.None)]
public partial class CustomFieldEntity : Entity<CustomFieldEntity>, IDataScope, ITenantScope, IDataScopeFieldProvider
{
    #region 属性
    private Int32 _Id;
    [DisplayName("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _CreatorId;
    [DisplayName("创建者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreatorId", "创建者", "")]
    public Int32 CreatorId { get => _CreatorId; set { if (OnPropertyChanging("CreatorId", value)) { _CreatorId = value; OnPropertyChanged("CreatorId"); } } }

    private Int32 _OrgId;
    [DisplayName("组织")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OrgId", "组织", "")]
    public Int32 OrgId { get => _OrgId; set { if (OnPropertyChanging("OrgId", value)) { _OrgId = value; OnPropertyChanged("OrgId"); } } }

    private Int32 _CompanyId;
    [DisplayName("公司")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CompanyId", "公司", "")]
    public Int32 CompanyId { get => _CompanyId; set { if (OnPropertyChanging("CompanyId", value)) { _CompanyId = value; OnPropertyChanged("CompanyId"); } } }
    #endregion

    #region IDataScope
    Int32 IUserScope.UserId { get => CreatorId; set => CreatorId = value; }
    Int32 IDepartmentScope.DepartmentId { get => OrgId; set => OrgId = value; }
    #endregion

    #region ITenantScope
    Int32 ITenantScope.TenantId { get => CompanyId; set => CompanyId = value; }
    #endregion

    #region IDataScopeFieldProvider
    public FieldItem? GetUserField() => Meta.Table.FindByName("CreatorId");
    public FieldItem? GetDepartmentField() => Meta.Table.FindByName("OrgId");
    public FieldItem? GetTenantField() => Meta.Table.FindByName("CompanyId");
    #endregion

    #region 获取/设置 字段值
    public override Object? this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "CreatorId" => _CreatorId,
            "OrgId" => _OrgId,
            "CompanyId" => _CompanyId,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "CreatorId": _CreatorId = value.ToInt(); break;
                case "OrgId": _OrgId = value.ToInt(); break;
                case "CompanyId": _CompanyId = value.ToInt(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 字段名
    public partial class _
    {
        public static readonly Field Id = FindByName("Id");
        public static readonly Field CreatorId = FindByName("CreatorId");
        public static readonly Field OrgId = FindByName("OrgId");
        public static readonly Field CompanyId = FindByName("CompanyId");
        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }
    #endregion
}
#endregion

#region Mock 类
/// <summary>Mock 用户类</summary>
public class MockUser : IUser
{
    public Int32 ID { get; set; }
    public String Name { get; set; } = "";
    public String? Password { get; set; }
    public String? DisplayName { get; set; }
    public SexKinds Sex { get; set; }
    public String? Mail { get; set; }
    public Boolean MailVerified { get; set; }
    public String? Mobile { get; set; }
    public Boolean MobileVerified { get; set; }
    public String? Code { get; set; }
    public Int32 AreaId { get; set; }
    public String? Avatar { get; set; }
    public Int32 RoleID { get; set; }
    public String? RoleIds { get; set; }
    public Int32 DepartmentID { get; set; }
    public Boolean Online { get; set; }
    public Boolean Enable { get; set; }
    public Int32 Age { get; set; }
    public DateTime Birthday { get; set; }
    public Int32 Logins { get; set; }
    public DateTime LastLogin { get; set; }
    public String? LastLoginIP { get; set; }
    public DateTime RegisterTime { get; set; }
    public String? RegisterIP { get; set; }
    public Int32 OnlineTime { get; set; }
    public Int32 Ex1 { get; set; }
    public Int32 Ex2 { get; set; }
    public Double Ex3 { get; set; }
    public String? Ex4 { get; set; }
    public String? Ex5 { get; set; }
    public String? Ex6 { get; set; }
    public String? Remark { get; set; }

    public IRole? Role { get; set; }
    public IRole[] Roles { get; set; } = [];
    public String? RoleName => Role?.Name;

    public Boolean Has(IMenu menu, params PermissionFlags[] flags) => false;
    public void Logout() { }
    public Int32 Save() => 0;
}

/// <summary>Mock 角色类</summary>
public class MockRole : IRole
{
    public Int32 ID { get; set; }
    public String Name { get; set; } = "";
    public RoleTypes Type { get; set; }
    public Boolean Enable { get; set; } = true;
    public Boolean IsSystem { get; set; }
    public Int32 TenantId { get; set; }
    public DataScopes DataScope { get; set; }
    public String? DataDepartmentIds { get; set; }
    public Boolean ViewSensitive { get; set; }
    public String? Permission { get; set; }
    public Int32 Sort { get; set; }
    public Int32 Ex1 { get; set; }
    public Int32 Ex2 { get; set; }
    public Double Ex3 { get; set; }
    public String? Ex4 { get; set; }
    public String? Ex5 { get; set; }
    public String? Ex6 { get; set; }
    public String? CreateUser { get; set; }
    public Int32 CreateUserID { get; set; }
    public String? CreateIP { get; set; }
    public DateTime CreateTime { get; set; }
    public String? UpdateUser { get; set; }
    public Int32 UpdateUserID { get; set; }
    public String? UpdateIP { get; set; }
    public DateTime UpdateTime { get; set; }
    public String? Remark { get; set; }

    // IRole 接口方法实现
    private readonly Dictionary<Int32, PermissionFlags> _permissions = [];
    public IDictionary<Int32, PermissionFlags> Permissions => _permissions;
    public Int32[] Resources => [.. _permissions.Keys];

    public Boolean Has(Int32 resourceId, PermissionFlags flag = PermissionFlags.None)
    {
        if (!_permissions.TryGetValue(resourceId, out var pf)) return false;
        if (flag == PermissionFlags.None) return true;
        return pf.Has(flag);
    }

    public PermissionFlags Get(Int32 resourceId)
    {
        if (!_permissions.TryGetValue(resourceId, out var pf)) return PermissionFlags.None;
        return pf;
    }

    public void Set(Int32 resourceId, PermissionFlags flag = PermissionFlags.Detail)
    {
        if (_permissions.ContainsKey(resourceId))
            _permissions[resourceId] |= flag;
        else
            _permissions[resourceId] = flag;
    }

    public void Reset(Int32 resourceId, PermissionFlags flag)
    {
        if (_permissions.ContainsKey(resourceId))
            _permissions[resourceId] &= ~flag;
    }

    public IRole? FindByID(Int32 id) => null;
    public IRole GetOrAdd(String name) => this;
    public Int32 Save() => 0;
}
#endregion
