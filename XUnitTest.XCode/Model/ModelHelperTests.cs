﻿using System;
using System.IO;
using NewLife.Collections;
using NewLife.Data;
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
        var tables = DAL.ImportFrom(file);

        Assert.NotNull(tables);
        Assert.NotEmpty(tables);
        Assert.Equal(9, tables.Count);

        var option = new EntityBuilderOption();
        var atts = new NullableDictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        var xml = File.ReadAllText(file.GetFullPath());
        tables = ModelHelper.FromXml(xml, DAL.CreateTable, option, atts);

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
        var tables = DAL.ImportFrom(file);

        Assert.NotNull(tables);
        Assert.NotEmpty(tables);
        Assert.Equal(9, tables.Count);

        var option = new EntityBuilderOption();
        var atts = new NullableDictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        var xml = File.ReadAllText(file.GetFullPath());

        tables = ModelHelper.FromXml(xml, DAL.CreateTable, option, atts);
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

    private String ReadTarget(String file, String text)
    {
        //var file2 = @"..\..\XUnitTest.XCode\".CombinePath(file);
        //File.WriteAllText(file2.EnsureDirectory(true), text);

        var target = File.ReadAllText(file.GetFullPath());

        return target;
    }

    [Fact]
    public void ImportCity()
    {
        var file = "Model/City.xml";
        var tables = DAL.ImportFrom(file);

        Assert.NotNull(tables);
        Assert.NotEmpty(tables);
        Assert.Equal(1, tables.Count);

        var column = tables[0].Columns[6];
        Assert.NotNull(column.Name);
        Assert.NotEmpty(column.Name);
        Assert.NotNull(column.ColumnName);
        Assert.NotEmpty(column.ColumnName);

        //column.Fix();

        var option = new EntityBuilderOption();
        var atts = new NullableDictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        var xml = File.ReadAllText(file.GetFullPath());

        tables = ModelHelper.FromXml(xml, DAL.CreateTable, option, atts);
        Assert.NotNull(tables);
        Assert.NotEmpty(tables);
        Assert.Equal(1, tables.Count);

        var xml2 = DAL.Export(tables);
        //Assert.Equal(xml, xml2);

        // 代码生成

        option = new EntityBuilderOption
        {
            ConnName = "MyConn",
            Namespace = "Company.MyName",
            //Partial = true,
            Nullable = true,
        };

        var builder = new EntityBuilder
        {
            Table = tables[0],
            Option = option,
        };

        // 数据类
        builder.Execute();

        var rs = builder.ToString();
        Assert.NotEmpty(rs);

        var target = ReadTarget("Model\\Code\\entity_city.cs", rs);
        Assert.Equal(target, rs);

        // 业务类
        builder.Clear();
        builder.Business = true;
        builder.Execute();

        rs = builder.ToString();
        Assert.NotEmpty(rs);

        target = ReadTarget("Model\\Code\\entity_city_biz.cs", rs);
        Assert.Equal(target, rs);
    }
}
