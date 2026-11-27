using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Serialization;
using XCode;
using XCode.DataAccessLayer;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Membership;

/// <summary>内置实体旧字段名兼容层测试</summary>
/// <remarks>
/// ID/CreateUserID 等字段统一改为 Id/CreateUserId 后，旧属性名以过时别名保留在 Biz 分部类，
/// 且不参与实体字段映射与序列化，避免下游项目升级时源码直接编译失败。
/// </remarks>
public class CompatAliasTests
{
    private static readonly (Type Type, String OldName, String NewName)[] _aliases =
    [
        (typeof(Area), "ID", "Id"),
        (typeof(Area), "ParentID", "ParentId"),
        (typeof(Department), "ID", "Id"),
        (typeof(Department), "ParentID", "ParentId"),
        (typeof(Department), "CreateUserID", "CreateUserId"),
        (typeof(Department), "UpdateUserID", "UpdateUserId"),
        (typeof(Menu), "ID", "Id"),
        (typeof(Menu), "ParentID", "ParentId"),
        (typeof(Menu), "CreateUserID", "CreateUserId"),
        (typeof(Menu), "UpdateUserID", "UpdateUserId"),
        (typeof(Role), "ID", "Id"),
        (typeof(Role), "CreateUserID", "CreateUserId"),
        (typeof(Role), "UpdateUserID", "UpdateUserId"),
        (typeof(User), "ID", "Id"),
        (typeof(User), "RoleID", "RoleId"),
        (typeof(User), "DepartmentID", "DepartmentId"),
        (typeof(User), "UpdateUserID", "UpdateUserId"),
        (typeof(Parameter), "ID", "Id"),
        (typeof(Parameter), "UserID", "UserId"),
        (typeof(Parameter), "CreateUserID", "CreateUserId"),
        (typeof(Parameter), "UpdateUserID", "UpdateUserId"),
        (typeof(Log), "ID", "Id"),
        (typeof(Log), "LinkID", "LinkId"),
        (typeof(Log), "CreateUserID", "CreateUserId"),
    ];

    public static TheoryData<Type, String, String> AliasData
    {
        get
        {
            var data = new TheoryData<Type, String, String>();
            foreach (var item in _aliases) data.Add(item.Type, item.OldName, item.NewName);
            return data;
        }
    }

    [Theory(DisplayName = "旧字段名以过时别名映射到新字段名")]
    [MemberData(nameof(AliasData))]
    public void OldNameAliasesNewName(Type type, String oldName, String newName)
    {
        var pi = type.GetProperty(oldName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(pi);
        Assert.NotNull(pi!.GetCustomAttribute<ObsoleteAttribute>());

        var pn = type.GetProperty(newName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(pn);
        Assert.Null(pn!.GetCustomAttribute<ObsoleteAttribute>());

        var entity = Activator.CreateInstance(type)!;
        var value = Convert.ChangeType(7, pi.PropertyType);
        pi.SetValue(entity, value);

        Assert.Equal(value, pn.GetValue(entity));
    }

    [Theory(DisplayName = "旧字段名别名不参与序列化")]
    [MemberData(nameof(AliasData))]
    public void OldNameAliasIsIgnoredBySerialization(Type type, String oldName, String newName)
    {
        var pi = type.GetProperty(oldName, BindingFlags.Public | BindingFlags.Instance)!;
        var pn = type.GetProperty(newName, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(pn);
        Assert.NotNull(pi.GetCustomAttribute<XmlIgnoreAttribute>());
        Assert.NotNull(pi.GetCustomAttribute<IgnoreDataMemberAttribute>());
        Assert.NotNull(pi.GetCustomAttribute<ScriptIgnoreAttribute>());
    }

    [Theory(DisplayName = "旧字段名索引器仍可访问")]
    [InlineData("ID", 11)]
    [InlineData("RoleID", 12)]
    [InlineData("DepartmentID", 13)]
    public void IndexerReadsOldName(String name, Int32 expected)
    {
        var user = new User { Id = 11, RoleId = 12, DepartmentId = 13 };

        Assert.Equal(expected, user[name].ToInt());
    }

    [Fact(DisplayName = "旧字段名不进入实体字段集合")]
    public void AliasIsNotAnEntityField()
    {
        Assert.DoesNotContain(User.Meta.Factory.Fields, e => e.Name == "ID");
        Assert.DoesNotContain(User.Meta.Factory.Fields, e => e.Name == "RoleID");

        // 大小写不敏感查找仍然命中真实字段
        var field = User.Meta.Factory.Table.FindByName("ID");
        Assert.NotNull(field);
        Assert.Equal("Id", field!.Name);
        Assert.Equal("Id", field.ColumnName + "");
    }

    [Fact(DisplayName = "旧字段名不进入 JSON 序列化")]
    public void OldNameAliasIsNotSerialized()
    {
        var user = new User { Id = 21, RoleId = 22, DepartmentId = 23 };

        var json = user.ToJson();
        Assert.Contains("\"Id\":21", json);
        Assert.DoesNotContain("\"ID\":", json);
        Assert.DoesNotContain("\"RoleID\":", json);
        Assert.DoesNotContain("\"DepartmentID\":", json);
    }

    [Theory(DisplayName = "被移除的旧方法名保留过时转发")]
    [InlineData(typeof(Menu), "FindByID", "FindById", new[] { typeof(Int32) })]
    [InlineData(typeof(Role), "FindByID", "FindById", new[] { typeof(Int32) })]
    [InlineData(typeof(Log), "FindByID", "FindById", new[] { typeof(Int64) })]
    [InlineData(typeof(User), "FindByID", "FindById", new[] { typeof(Int32) })]
    [InlineData(typeof(Parameter), "FindByID", "FindById", new[] { typeof(Int32) })]
    [InlineData(typeof(User), "FindAllByRoleID", "FindAllByRoleId", new[] { typeof(Int32) })]
    [InlineData(typeof(Menu), "FindByParentIDAndName", "FindByParentIdAndName", new[] { typeof(Int32), typeof(String) })]
    [InlineData(typeof(Menu), "FindAllByParentID", "FindAllByParentId", new[] { typeof(Int32) })]
    [InlineData(typeof(Menu), "FindByUrlAndParentID", "FindByUrlAndParentId", new[] { typeof(String), typeof(Int32) })]
    [InlineData(typeof(Area), "FindByIDs", "FindByIds", new[] { typeof(Int32[]) })]
    [InlineData(typeof(Department), "FindAllByIDs", "FindAllByIds", new[] { typeof(Int32[]) })]
    [InlineData(typeof(Role), "FindAllByIDs", "FindAllByIds", new[] { typeof(Int32[]) })]
    [InlineData(typeof(Department), "FindByNameAndParentID", "FindByNameAndParentId", new[] { typeof(String), typeof(Int32) })]
    [InlineData(typeof(Log), "FindAllByCreateUserID", "FindAllByCreateUserId", new[] { typeof(Int32) })]
    [InlineData(typeof(Log), "FindAllByCreateUserIDAndID", "FindAllByCreateUserIdAndId", new[] { typeof(Int32), typeof(Int64) })]
    [InlineData(typeof(Parameter), "DeleteByID", "DeleteById", new[] { typeof(Int32) })]
    [InlineData(typeof(Parameter), "DeleteByUserID", "DeleteByUserId", new[] { typeof(Int32) })]
    [InlineData(typeof(Parameter), "FindCountByUserID", "FindCountByUserId", new[] { typeof(Int32) })]
    [InlineData(typeof(User), "GetRoleIDs", "GetRoleIds", new Type[0])]
    [InlineData(typeof(User), "GetOwnRoleIDs", "GetOwnRoleIds", new Type[0])]
    [InlineData(typeof(Department), "FindByID", "FindById", new[] { typeof(Int32) })]
    [InlineData(typeof(Department), "FindAllByParentIDAndName", "FindAllByParentIdAndName", new[] { typeof(Int32), typeof(String) })]
    [InlineData(typeof(Department), "FindAllByTenantIdAndParentIDAndName", "FindAllByTenantIdAndParentIdAndName", new[] { typeof(Int32), typeof(Int32), typeof(String) })]
    [InlineData(typeof(User), "FindAllByDepartmentID", "FindAllByDepartmentId", new[] { typeof(Int32) })]
    [InlineData(typeof(Log), "FindAllByCategoryAndLinkID", "FindAllByCategoryAndLinkId", new[] { typeof(String), typeof(Int64) })]
    [InlineData(typeof(Parameter), "FindAllByUserID", "FindAllByUserId", new[] { typeof(Int32) })]
    [InlineData(typeof(Parameter), "FindAllByUserID", "FindAllByUserIdAndCategory", new[] { typeof(Int32), typeof(String) })]
    [InlineData(typeof(Parameter), "FindAllByUserIDAndCategory", "FindAllByUserIdAndCategory", new[] { typeof(Int32), typeof(String) })]
    [InlineData(typeof(Parameter), "FindByUserIDAndCategoryAndName", "FindByUserIdAndCategoryAndName", new[] { typeof(Int32), typeof(String), typeof(String) })]
    public void OldMethodNameIsObsolete(Type type, String name, String newName, Type[] parameters)
    {
        var flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        var mi = type.GetMethod(name, flags, null, parameters, null);
        Assert.NotNull(mi);
        Assert.NotNull(mi!.GetCustomAttribute<ObsoleteAttribute>());

        // 新方法名同时存在
        var ni = type.GetMethod(newName, flags, null, parameters, null);
        Assert.NotNull(ni);
    }
}

