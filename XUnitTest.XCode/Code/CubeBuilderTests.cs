using System;
using System.Collections.Generic;
using System.ComponentModel;
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

    [Fact]
    [DisplayName("枚举搜索参数使用全限定名时，生成类型别名与短类型名")]
    public void BuildEnumSearchWithFullName()
    {
        var option = _option.Clone() as CubeBuilderOption;

        var table = _tables.FirstOrDefault(e => e.Name == "User");
        option.BaseClass = "EntityController";

        var builder = new CubeBuilder
        {
            Table = table,
            Option = option,
            RootNamespace = $"{option.ConnName}.Web",
            AreaName = "Admin",
        };

        builder.Execute();

        var rs = builder.ToString();
        Assert.Contains("using SexKinds = XCode.Membership.SexKinds;", rs);
        Assert.Contains("(SexKinds)p[\"sex\"]", rs);
        Assert.DoesNotContain("using SexKinds;", rs);
        Assert.DoesNotContain("global::", rs);
    }

    [Fact]
    [DisplayName("枚举搜索参数使用短名时，不生成非法的 using 类型名")]
    public void BuildEnumSearchWithShortName()
    {
        var table = DAL.CreateTable();
        table.Name = "Order";
        table.TableName = "Order";
        table.Description = "订单";

        var col = table.CreateColumn();
        col.Name = "Sex";
        col.ColumnName = "Sex";
        col.DataType = typeof(Int32);
        col.Properties["Type"] = "SexKinds";
        table.Columns.Add(col);

        var builder = new CubeBuilder
        {
            Table = table,
            Option = new CubeBuilderOption { Namespace = "Zero.Data.Entities" },
            RootNamespace = "Zero.Web",
            AreaName = "Admin",
        };

        builder.Execute();

        var rs = builder.ToString();
        Assert.NotEmpty(rs);
        Assert.Contains("(SexKinds)p[\"sex\"]", rs);
        Assert.DoesNotContain("using SexKinds", rs);
    }

    [Fact]
    [DisplayName("枚举作为 DataType 时，生成类型别名与短类型名而非 global:: 短名前缀")]
    public void BuildEnumDataTypeSearch()
    {
        var table = DAL.CreateTable();
        table.Name = "Order";
        table.TableName = "Order";
        table.Description = "订单";

        var col = table.CreateColumn();
        col.Name = "Sex";
        col.ColumnName = "Sex";
        col.DataType = typeof(global::XCode.Membership.SexKinds);
        table.Columns.Add(col);

        var builder = new CubeBuilder
        {
            Table = table,
            Option = new CubeBuilderOption { Namespace = "Zero.Data.Entities" },
            RootNamespace = "Zero.Web",
            AreaName = "Admin",
        };

        builder.Execute();

        var rs = builder.ToString();
        Assert.NotEmpty(rs);
        Assert.Contains("using SexKinds = XCode.Membership.SexKinds;", rs);
        Assert.Contains("(SexKinds)p[\"sex\"]", rs);
        Assert.DoesNotContain("(global::SexKinds)", rs);
    }

    [Fact]
    [DisplayName("索引中的数值列生成与实体侧签名匹配的转换")]
    public void BuildIndexedNumericColumns()
    {
        var table = DAL.CreateTable();
        table.Name = "PowerMeter";
        table.TableName = "power_meter";
        table.Description = "电能指标";

        var col = table.CreateColumn();
        col.Name = "Current";
        col.ColumnName = "Current";
        col.DataType = typeof(Single);
        table.Columns.Add(col);

        var col2 = table.CreateColumn();
        col2.Name = "Ex3";
        col2.ColumnName = "Ex3";
        col2.DataType = typeof(Double);
        table.Columns.Add(col2);

        var col3 = table.CreateColumn();
        col3.Name = "Amount";
        col3.ColumnName = "Amount";
        col3.DataType = typeof(Decimal);
        table.Columns.Add(col3);

        var col4 = table.CreateColumn();
        col4.Name = "Level";
        col4.ColumnName = "Level";
        col4.DataType = typeof(Int16);
        table.Columns.Add(col4);

        var col5 = table.CreateColumn();
        col5.Name = "Big";
        col5.ColumnName = "Big";
        col5.DataType = typeof(UInt64);
        table.Columns.Add(col5);

        var di = table.CreateIndex();
        di.Columns = ["Current", "Ex3", "Amount", "Level", "Big"];
        di.Fix();
        table.Indexes.Add(di);

        var builder = new CubeBuilder
        {
            Table = table,
            Option = new CubeBuilderOption { Namespace = "Zero.Data.Entities" },
            RootNamespace = "Zero.Web",
            AreaName = "Admin",
        };

        builder.Execute();

        var rs = builder.ToString();
        Assert.NotEmpty(rs);
        Assert.Contains("var current = (Single)p[\"current\"].ToDouble();", rs);
        Assert.Contains("var ex3 = p[\"ex3\"].ToDouble();", rs);
        Assert.Contains("var amount = p[\"amount\"].ToDecimal();", rs);
        Assert.Contains("var level = (Int16)p[\"level\"].ToInt(-1);", rs);
        Assert.Contains("var big = (UInt64)p[\"big\"].ToLong(-1);", rs);
        Assert.Contains("PowerMeter.Search(current, ex3, amount, level, big", rs);
    }

    /// <summary>嵌套枚举类型。用于验证类型别名对嵌套类型的支持</summary>
    public enum NestedKinds
    {
        /// <summary>甲</summary>
        A = 1,

        /// <summary>乙</summary>
        B = 2,
    }

    [Fact]
    [DisplayName("嵌套枚举类型生成点号路径的类型别名")]
    public void BuildEnumNestedType()
    {
        var table = DAL.CreateTable();
        table.Name = "Order";
        table.TableName = "Order";
        table.Description = "订单";

        var col = table.CreateColumn();
        col.Name = "Kind";
        col.ColumnName = "Kind";
        col.DataType = typeof(Int32);
        col.Properties["Type"] = "XUnitTest.XCode.Code.CubeBuilderTests.NestedKinds";
        table.Columns.Add(col);

        var builder = new CubeBuilder
        {
            Table = table,
            Option = new CubeBuilderOption { Namespace = "Zero.Data.Entities" },
            RootNamespace = "Zero.Web",
            AreaName = "Admin",
        };

        builder.Execute();

        var rs = builder.ToString();
        Assert.NotEmpty(rs);
        Assert.Contains("using NestedKinds = XUnitTest.XCode.Code.CubeBuilderTests.NestedKinds;", rs);
        Assert.Contains("(NestedKinds)p[\"kind\"]", rs);
    }

    [Fact]
    [DisplayName("同名不同枚举冲突时退化为全局全名")]
    public void BuildEnumAliasConflict()
    {
        var table = DAL.CreateTable();
        table.Name = "Order";
        table.TableName = "Order";
        table.Description = "订单";

        var col = table.CreateColumn();
        col.Name = "Sex1";
        col.ColumnName = "Sex1";
        col.DataType = typeof(Int32);
        col.Properties["Type"] = "XCode.Membership.SexKinds";
        table.Columns.Add(col);

        var col2 = table.CreateColumn();
        col2.Name = "Sex2";
        col2.ColumnName = "Sex2";
        col2.DataType = typeof(Int32);
        col2.Properties["Type"] = "XUnitTest.XCode.TestEntity.SexKinds";
        table.Columns.Add(col2);

        var builder = new CubeBuilder
        {
            Table = table,
            Option = new CubeBuilderOption { Namespace = "Zero.Data.Entities" },
            RootNamespace = "Zero.Web",
            AreaName = "Admin",
        };

        builder.Execute();

        var rs = builder.ToString();
        Assert.NotEmpty(rs);
        Assert.Contains("(global::XCode.Membership.SexKinds)p[\"sex1\"]", rs);
        Assert.Contains("(global::XUnitTest.XCode.TestEntity.SexKinds)p[\"sex2\"]", rs);
        Assert.DoesNotContain("using SexKinds =", rs);
    }
}
