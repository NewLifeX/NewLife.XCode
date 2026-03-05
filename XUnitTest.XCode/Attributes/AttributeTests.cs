using System;
using System.Collections.Generic;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.Attributes;

/// <summary>BindColumnAttribute测试</summary>
public class BindColumnAttributeTests
{
    [Fact(DisplayName = "构造_无参")]
    public void Ctor_Default()
    {
        var attr = new BindColumnAttribute();

        Assert.Null(attr.Name);
        Assert.Null(attr.Description);
        Assert.Null(attr.RawType);
        Assert.Null(attr.ItemType);
        Assert.Null(attr.ShowIn);
        Assert.Equal(0, attr.Precision);
        Assert.Equal(0, attr.Scale);
        Assert.Null(attr.DefaultValue);
        Assert.False(attr.Master);
        Assert.Null(attr.DataScale);
    }

    [Fact(DisplayName = "构造_名称")]
    public void Ctor_Name()
    {
        var attr = new BindColumnAttribute("UserName");

        Assert.Equal("UserName", attr.Name);
    }

    [Fact(DisplayName = "构造_三参数")]
    public void Ctor_NameDescriptionRawType()
    {
        var attr = new BindColumnAttribute("Id", "主键", "int");

        Assert.Equal("Id", attr.Name);
        Assert.Equal("主键", attr.Description);
        Assert.Equal("int", attr.RawType);
    }

    [Fact(DisplayName = "设置属性")]
    public void SetProperties()
    {
        var attr = new BindColumnAttribute
        {
            Name = "Price",
            Description = "价格",
            RawType = "decimal(18,2)",
            ItemType = "Number",
            ShowIn = "list",
            Precision = 18,
            Scale = 2,
            DefaultValue = "0",
            Master = true,
            DataScale = "time"
        };

        Assert.Equal("Price", attr.Name);
        Assert.Equal("价格", attr.Description);
        Assert.Equal("decimal(18,2)", attr.RawType);
        Assert.Equal("Number", attr.ItemType);
        Assert.Equal("list", attr.ShowIn);
        Assert.Equal(18, attr.Precision);
        Assert.Equal(2, attr.Scale);
        Assert.Equal("0", attr.DefaultValue);
        Assert.True(attr.Master);
        Assert.Equal("time", attr.DataScale);
    }

    [Fact(DisplayName = "GetCustomAttribute_无标记返回null")]
    public void GetCustomAttribute_NoAttribute_ReturnsNull()
    {
        var prop = typeof(TestClassNoAttr).GetProperty("Name");

        var attr = BindColumnAttribute.GetCustomAttribute(prop!);

        Assert.Null(attr);
    }

    [Fact(DisplayName = "GetCustomAttribute_有标记返回特性")]
    public void GetCustomAttribute_HasAttribute_ReturnsAttribute()
    {
        var prop = typeof(TestClassWithAttr).GetProperty("Name");

        var attr = BindColumnAttribute.GetCustomAttribute(prop!);

        Assert.NotNull(attr);
        Assert.Equal("user_name", attr!.Name);
    }

    private class TestClassNoAttr
    {
        public String? Name { get; set; }
    }

    private class TestClassWithAttr
    {
        [BindColumn("user_name", "用户名", "nvarchar(50)")]
        public String? Name { get; set; }
    }
}

/// <summary>BindTableAttribute测试</summary>
public class BindTableAttributeTests
{
    [Fact(DisplayName = "构造_名称")]
    public void Ctor_Name()
    {
        var attr = new BindTableAttribute("User");

        Assert.Equal("User", attr.Name);
        Assert.Null(attr.Description);
        Assert.Null(attr.ConnName);
        Assert.False(attr.IsView);
    }

    [Fact(DisplayName = "构造_名称和描述")]
    public void Ctor_NameDescription()
    {
        var attr = new BindTableAttribute("User", "用户表");

        Assert.Equal("User", attr.Name);
        Assert.Equal("用户表", attr.Description);
    }

    [Fact(DisplayName = "构造_完整参数")]
    public void Ctor_FullParams()
    {
        var attr = new BindTableAttribute("User", "用户表", "Membership", DatabaseType.SQLite, false);

        Assert.Equal("User", attr.Name);
        Assert.Equal("用户表", attr.Description);
        Assert.Equal("Membership", attr.ConnName);
        Assert.Equal(DatabaseType.SQLite, attr.DbType);
        Assert.False(attr.IsView);
    }

    [Fact(DisplayName = "视图标记")]
    public void IsView_Set()
    {
        var attr = new BindTableAttribute("vw_UserInfo", "用户视图", "Default", DatabaseType.SqlServer, true);

        Assert.True(attr.IsView);
    }
}

/// <summary>BindIndexAttribute测试</summary>
public class BindIndexAttributeTests
{
    [Fact(DisplayName = "构造_基本属性")]
    public void Ctor_BasicProperties()
    {
        var attr = new BindIndexAttribute("IX_User_Name", false, "Name");

        Assert.Equal("IX_User_Name", attr.Name);
        Assert.False(attr.Unique);
        Assert.Equal("Name", attr.Columns);
    }

    [Fact(DisplayName = "唯一索引")]
    public void UniqueIndex()
    {
        var attr = new BindIndexAttribute("UK_User_Email", true, "Email");

        Assert.True(attr.Unique);
    }

    [Fact(DisplayName = "复合列索引")]
    public void CompositeColumns()
    {
        var attr = new BindIndexAttribute("IX_User_NameAge", false, "Name,Age");

        Assert.Equal("IX_User_NameAge", attr.Name);
        Assert.Equal("Name,Age", attr.Columns);
    }
}

/// <summary>ModelCheckMode测试</summary>
public class ModelCheckModeAttributeTests
{
    [Fact(DisplayName = "CheckAllTablesWhenInit模式")]
    public void CheckAllTablesWhenInit()
    {
        var attr = new ModelCheckModeAttribute(ModelCheckModes.CheckAllTablesWhenInit);

        Assert.Equal(ModelCheckModes.CheckAllTablesWhenInit, attr.Mode);
    }

    [Fact(DisplayName = "CheckTableWhenFirstUse模式")]
    public void CheckTableWhenFirstUse()
    {
        var attr = new ModelCheckModeAttribute(ModelCheckModes.CheckTableWhenFirstUse);

        Assert.Equal(ModelCheckModes.CheckTableWhenFirstUse, attr.Mode);
    }

    [Fact(DisplayName = "枚举值")]
    public void EnumValues()
    {
        Assert.Equal(0, (Int32)ModelCheckModes.CheckAllTablesWhenInit);
        Assert.Equal(1, (Int32)ModelCheckModes.CheckTableWhenFirstUse);
    }
}

/// <summary>ModelSortMode测试</summary>
public class ModelSortModeAttributeTests
{
    [Fact(DisplayName = "BaseFirst模式")]
    public void BaseFirst()
    {
        var attr = new ModelSortModeAttribute(ModelSortModes.BaseFirst);

        Assert.Equal(ModelSortModes.BaseFirst, attr.Mode);
    }

    [Fact(DisplayName = "DerivedFirst模式")]
    public void DerivedFirst()
    {
        var attr = new ModelSortModeAttribute(ModelSortModes.DerivedFirst);

        Assert.Equal(ModelSortModes.DerivedFirst, attr.Mode);
    }

    [Fact(DisplayName = "枚举值")]
    public void EnumValues()
    {
        Assert.Equal(0, (Int32)ModelSortModes.BaseFirst);
        Assert.Equal(1, (Int32)ModelSortModes.DerivedFirst);
    }
}
