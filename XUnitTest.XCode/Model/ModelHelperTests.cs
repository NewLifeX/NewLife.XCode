using System;
using System.IO;
using NewLife.Collections;
using XCode.Code;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.Model;

public class ModelHelperTests
{
    [Fact]
    public void Import2012()
    {
        var file = "Model/Member2012.xml";
        var option = new EntityBuilderOption();
        var atts = new NullableDictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        var xml = File.ReadAllText(file.GetFullPath());
        var tables = ModelHelper.FromXml(xml, DAL.CreateTable, option, atts);

        Assert.NotNull(tables);
        Assert.NotEmpty(tables);
        Assert.Equal(9, tables.Count);

        Assert.Equal("User", tables[0].Name);
        Assert.Equal("Department", tables[1].Name);
        Assert.Equal("Role", tables[2].Name);
        Assert.Equal("Menu", tables[3].Name);
        Assert.Equal("Parameter", tables[4].Name);
        Assert.Equal("Area", tables[5].Name);
        Assert.Equal("Log", tables[6].Name);
        Assert.Equal("UserOnline", tables[7].Name);
        Assert.Equal("VisitStat", tables[8].Name);

        var user = tables[0];
        Assert.Equal("用户", user.DisplayName);
        Assert.Equal("用户", user.Description);
        Assert.Equal(30, user.Columns.Count);

        Assert.Single(user.Properties);
        Assert.Equal("True", user.Properties["RenderGenEntity"]);

        var column = user.Columns[4];
        Assert.Equal("Sex", column.Name);
        Assert.Equal("性别", column.DisplayName);
        Assert.Equal("性别。未知、男、女", column.Description);
        Assert.Equal(typeof(Int32), column.DataType);
        Assert.Null(column.RawType);
        Assert.Null(column.ItemType);

        Assert.Single(column.Properties);
        Assert.Equal("SexKinds", column.Properties["Type"]);

        var dep = tables[1];
        Assert.Equal("部门", dep.DisplayName);
        Assert.Equal("部门。组织机构，多级树状结构", dep.Description);
    }

    [Fact]
    public void Import2023()
    {
        var file = "Model/Member2023.xml";
        var option = new EntityBuilderOption();
        var atts = new NullableDictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        var xml = File.ReadAllText(file.GetFullPath());

        var tables = ModelHelper.FromXml(xml, DAL.CreateTable, option, atts);
        Assert.NotNull(tables);
        Assert.NotEmpty(tables);
        Assert.Equal(9, tables.Count);

        Assert.Equal("User", tables[0].Name);
        Assert.Equal("Department", tables[1].Name);
        Assert.Equal("Role", tables[2].Name);
        Assert.Equal("Menu", tables[3].Name);
        Assert.Equal("Parameter", tables[4].Name);
        Assert.Equal("Area", tables[5].Name);
        Assert.Equal("Log", tables[6].Name);
        Assert.Equal("Tenant", tables[7].Name);
        Assert.Equal("TenantUser", tables[8].Name);

        var user = tables[0];
        Assert.Equal("用户", user.DisplayName);
        Assert.Equal("用户。用户帐号信息", user.Description);
        Assert.Equal(34, user.Columns.Count);

        //Assert.Single(user.Properties);
        //Assert.Equal("True", user.Properties["RenderGenEntity"]);

        var column = user.Columns[4];
        Assert.Equal("Sex", column.Name);
        Assert.Equal("性别", column.DisplayName);
        Assert.Equal("性别。未知、男、女", column.Description);
        Assert.Equal(typeof(Int32), column.DataType);
        Assert.Null(column.RawType);
        Assert.Null(column.ItemType);

        Assert.Single(column.Properties);
        Assert.Equal("XCode.Membership.SexKinds", column.Properties["Type"]);

        var dep = tables[1];
        Assert.Equal("部门", dep.DisplayName);
        Assert.Equal("部门。组织机构，多级树状结构", dep.Description);
    }
}
