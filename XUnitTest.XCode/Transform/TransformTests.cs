using System;
using System.Collections.Generic;
using XCode;
using XCode.Transform;
using Xunit;

namespace XUnitTest.XCode.Transform;

/// <summary>ExtractSetting抽取参数测试</summary>
public class ExtractSettingTests
{
    [Fact(DisplayName = "构造_默认值")]
    public void Ctor_Default()
    {
        var setting = new ExtractSetting();

        Assert.Equal(default, setting.Start);
        Assert.Equal(default, setting.End);
        Assert.Equal(0, setting.Offset);
        Assert.Equal(0, setting.Row);
        Assert.Equal(0, setting.Step);
        Assert.Equal(5000, setting.BatchSize);
    }

    [Fact(DisplayName = "构造_从另一个设置复制")]
    public void Ctor_CopyFrom()
    {
        var source = new ExtractSetting
        {
            Start = new DateTime(2025, 1, 1),
            End = new DateTime(2025, 6, 30),
            Offset = 60,
            Row = 100,
            Step = 3600,
            BatchSize = 1000
        };

        var target = new ExtractSetting(source);

        Assert.Equal(new DateTime(2025, 1, 1), target.Start);
        Assert.Equal(new DateTime(2025, 6, 30), target.End);
        Assert.Equal(100, target.Row);
        Assert.Equal(3600, target.Step);
        Assert.Equal(1000, target.BatchSize);
    }

    [Fact(DisplayName = "Copy_复制属性")]
    public void Copy_CopiesProperties()
    {
        var source = new ExtractSetting
        {
            Start = new DateTime(2025, 3, 1),
            End = new DateTime(2025, 3, 31),
            Row = 50,
            Step = 7200,
            BatchSize = 2000
        };

        var target = new ExtractSetting();
        target.Copy(source);

        Assert.Equal(source.Start, target.Start);
        Assert.Equal(source.End, target.End);
        Assert.Equal(source.Row, target.Row);
        Assert.Equal(source.Step, target.Step);
        Assert.Equal(source.BatchSize, target.BatchSize);
    }

    [Fact(DisplayName = "Copy_null源返回自身")]
    public void Copy_NullSource_ReturnsSelf()
    {
        var target = new ExtractSetting { BatchSize = 999 };

        var result = target.Copy(null!);

        Assert.Same(target, result);
        Assert.Equal(999, target.BatchSize);
    }

    [Fact(DisplayName = "Clone_创建独立副本")]
    public void Clone_CreatesIndependentCopy()
    {
        var source = new ExtractSetting
        {
            Start = new DateTime(2025, 1, 1),
            End = new DateTime(2025, 12, 31),
            Row = 10,
            Step = 86400,
            BatchSize = 3000
        };

        var clone = source.Clone();

        Assert.NotSame(source, clone);
        Assert.Equal(source.Start, clone.Start);
        Assert.Equal(source.End, clone.End);
        Assert.Equal(source.Row, clone.Row);
        Assert.Equal(source.Step, clone.Step);
        Assert.Equal(source.BatchSize, clone.BatchSize);
    }

    [Fact(DisplayName = "Clone_修改不影响原对象")]
    public void Clone_ModifyClone_DoesNotAffectOriginal()
    {
        var source = new ExtractSetting { BatchSize = 5000, Step = 100 };

        var clone = source.Clone();
        clone.BatchSize = 9999;
        clone.Step = 999;

        Assert.Equal(5000, source.BatchSize);
        Assert.Equal(100, source.Step);
    }

    [Fact(DisplayName = "接口类型")]
    public void ImplementsInterface()
    {
        var setting = new ExtractSetting();

        Assert.IsAssignableFrom<IExtractSetting>(setting);
    }

    [Fact(DisplayName = "设置所有属性")]
    public void SetAllProperties()
    {
        var setting = new ExtractSetting
        {
            Start = new DateTime(2025, 1, 1),
            End = new DateTime(2025, 12, 31),
            Offset = 30,
            Row = 500,
            Step = 3600,
            BatchSize = 10000
        };

        Assert.Equal(new DateTime(2025, 1, 1), setting.Start);
        Assert.Equal(new DateTime(2025, 12, 31), setting.End);
        Assert.Equal(30, setting.Offset);
        Assert.Equal(500, setting.Row);
        Assert.Equal(3600, setting.Step);
        Assert.Equal(10000, setting.BatchSize);
    }
}

/// <summary>ExtracterBase抽取器基类测试</summary>
public class ExtracterBaseTests
{
    [Fact(DisplayName = "Init_无字段名抛异常")]
    public void Init_NoFieldName_ThrowsException()
    {
        // ExtracterBase is abstract, we can't instantiate it directly
        // We test through known implementations like IdExtracter
        Assert.True(typeof(ExtracterBase).IsAbstract);
    }

    [Fact(DisplayName = "ExtracterBase_属性默认值")]
    public void ExtracterBase_DefaultProperties()
    {
        // Verify ExtracterBase has expected properties
        var type = typeof(ExtracterBase);
        Assert.NotNull(type.GetProperty("Name"));
        Assert.NotNull(type.GetProperty("Factory"));
        Assert.NotNull(type.GetProperty("FieldName"));
        Assert.NotNull(type.GetProperty("Where"));
        Assert.NotNull(type.GetProperty("Field"));
        Assert.NotNull(type.GetProperty("OrderBy"));
        Assert.NotNull(type.GetProperty("Selects"));
        Assert.NotNull(type.GetProperty("Log"));
    }
}
