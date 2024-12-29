using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XCode;
using XCode.Code;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.Code;

public class CubeBuilderTests
{
    private IList<IDataTable> _tables;
    private CubeBuilderOption _option;

    public CubeBuilderTests()
    {
        _option = new CubeBuilderOption();
        _tables = ClassBuilder.LoadModels(@"..\..\XCode\Membership\Member.xml", _option, out _);
    }

    private String ReadTarget(String file, String text)
    {
        var target = "";
        var file2 = @"..\..\XUnitTest.XCode\".CombinePath(file);
        if (File.Exists(file2)) target = File.ReadAllText(file2.GetFullPath());

        file2.EnsureDirectory(true);
        File.WriteAllText(file2, text);

        //if (!File.Exists(file)) return null;
        //var target = File.ReadAllText(file.GetFullPath());

        return target;
    }

    [Fact]
    public void BuildUser()
    {
        var option = _option.Clone() as CubeBuilderOption;

        var table = _tables.FirstOrDefault(e => e.Name == "User");
        if (table.InsertOnly)
            option.BaseClass = "ReadOnlyEntityController";
        else
            option.BaseClass = "EntityController";

        var builder = new CubeBuilder
        {
            Table = table,
            Option = option,
            RootNamespace = $"{option.ConnName}.Web",
            AreaName = "Admin",
        };

        // 数据类
        builder.Execute();

        var rs = builder.ToString();
        Assert.NotEmpty(rs);

        var target = ReadTarget($"Code\\Controllers\\controller_{table.Name.ToLower()}.cs", rs);
        Assert.Equal(target, rs);
    }

    [Fact]
    public void BuildLog()
    {
        var option = _option.Clone() as CubeBuilderOption;

        var table = _tables.FirstOrDefault(e => e.Name == "Log");
        if (table.InsertOnly)
            option.BaseClass = "ReadOnlyEntityController";
        else
            option.BaseClass = "EntityController";

        var builder = new CubeBuilder
        {
            Table = table,
            Option = option,
            RootNamespace = $"{option.ConnName}.Web",
            AreaName = "Admin",
        };

        // 数据类
        builder.Execute();

        var rs = builder.ToString();
        Assert.NotEmpty(rs);

        var target = ReadTarget($"Code\\Controllers\\controller_{table.Name.ToLower()}.cs", rs);
        Assert.Equal(target, rs);
    }

    [Fact]
    public void BuildRole()
    {
        var option = _option.Clone() as CubeBuilderOption;

        var table = _tables.FirstOrDefault(e => e.Name == "Role");
        if (table.InsertOnly)
            option.BaseClass = "ReadOnlyEntityController";
        else
            option.BaseClass = "EntityController";

        var builder = new CubeBuilder
        {
            Table = table,
            Option = option,
            RootNamespace = $"{option.ConnName}.Web",
            AreaName = "Admin",
        };

        // 数据类
        builder.Execute();

        var rs = builder.ToString();
        Assert.NotEmpty(rs);

        var target = ReadTarget($"Code\\Controllers\\controller_{table.Name.ToLower()}.cs", rs);
        Assert.Equal(target, rs);
    }

    [Fact]
    public void BuildMenu()
    {
        var option = _option.Clone() as CubeBuilderOption;

        var table = _tables.FirstOrDefault(e => e.Name == "Menu");
        if (table.InsertOnly)
            option.BaseClass = "ReadOnlyEntityController";
        else
            option.BaseClass = "EntityController";

        var builder = new CubeBuilder
        {
            Table = table,
            Option = option,
            RootNamespace = $"{option.ConnName}.Web",
            AreaName = "Admin",
        };

        // 数据类
        builder.Execute();

        var rs = builder.ToString();
        Assert.NotEmpty(rs);

        var target = ReadTarget($"Code\\Controllers\\controller_{table.Name.ToLower()}.cs", rs);
        Assert.Equal(target, rs);
    }

    [Fact]
    public void BuildDepartment()
    {
        var option = _option.Clone() as CubeBuilderOption;

        var table = _tables.FirstOrDefault(e => e.Name == "Department");
        if (table.InsertOnly)
            option.BaseClass = "ReadOnlyEntityController";
        else
            option.BaseClass = "EntityController";

        var builder = new CubeBuilder
        {
            Table = table,
            Option = option,
            RootNamespace = $"{option.ConnName}.Web",
            AreaName = "Admin",
        };

        // 数据类
        builder.Execute();

        var rs = builder.ToString();
        Assert.NotEmpty(rs);

        var target = ReadTarget($"Code\\Controllers\\controller_{table.Name.ToLower()}.cs", rs);
        Assert.Equal(target, rs);
    }

    [Fact]
    public void BuildParameter()
    {
        var option = _option.Clone() as CubeBuilderOption;

        var table = _tables.FirstOrDefault(e => e.Name == "Parameter");
        if (table.InsertOnly)
            option.BaseClass = "ReadOnlyEntityController";
        else
            option.BaseClass = "EntityController";

        var builder = new CubeBuilder
        {
            Table = table,
            Option = option,
            RootNamespace = $"{option.ConnName}.Web",
            AreaName = "Admin",
        };

        // 数据类
        builder.Execute();

        var rs = builder.ToString();
        Assert.NotEmpty(rs);

        var target = ReadTarget($"Code\\Controllers\\controller_{table.Name.ToLower()}.cs", rs);
        Assert.Equal(target, rs);
    }

    [Fact]
    public void BuildArea()
    {
        var option = _option.Clone() as CubeBuilderOption;

        var table = _tables.FirstOrDefault(e => e.Name == "Area");
        if (table.InsertOnly)
            option.BaseClass = "ReadOnlyEntityController";
        else
            option.BaseClass = "EntityController";

        var builder = new CubeBuilder
        {
            Table = table,
            Option = option,
            RootNamespace = $"{option.ConnName}.Web",
            AreaName = "Admin",
        };

        // 数据类
        builder.Execute();

        var rs = builder.ToString();
        Assert.NotEmpty(rs);

        var target = ReadTarget($"Code\\Controllers\\controller_{table.Name.ToLower()}.cs", rs);
        Assert.Equal(target, rs);
    }

    [Fact]
    public void BuildTenant()
    {
        var option = _option.Clone() as CubeBuilderOption;

        var table = _tables.FirstOrDefault(e => e.Name == "Tenant");
        if (table.InsertOnly)
            option.BaseClass = "ReadOnlyEntityController";
        else
            option.BaseClass = "EntityController";

        var builder = new CubeBuilder
        {
            Table = table,
            Option = option,
            RootNamespace = $"{option.ConnName}.Web",
            AreaName = "Admin",
        };

        // 数据类
        builder.Execute();

        var rs = builder.ToString();
        Assert.NotEmpty(rs);

        var target = ReadTarget($"Code\\Controllers\\controller_{table.Name.ToLower()}.cs", rs);
        Assert.Equal(target, rs);
    }

    [Fact]
    public void BuildTenantUser()
    {
        var option = _option.Clone() as CubeBuilderOption;

        var table = _tables.FirstOrDefault(e => e.Name == "TenantUser");
        if (table.InsertOnly)
            option.BaseClass = "ReadOnlyEntityController";
        else
            option.BaseClass = "EntityController";

        var builder = new CubeBuilder
        {
            Table = table,
            Option = option,
            RootNamespace = $"{option.ConnName}.Web",
            AreaName = "Admin",
        };

        // 数据类
        builder.Execute();

        var rs = builder.ToString();
        Assert.NotEmpty(rs);

        var target = ReadTarget($"Code\\Controllers\\controller_{table.Name.ToLower()}.cs", rs);
        Assert.Equal(target, rs);
    }
}
