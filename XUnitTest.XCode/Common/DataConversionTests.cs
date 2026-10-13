using System;
using XCode.Common;
using Xunit;

namespace XUnitTest.XCode.Common;

/// <summary>数据转换测试</summary>
public class DataConversionTests
{
    private class Source
    {
        public Int32 Id { get; set; }
        public String Name { get; set; }
        public String OnlySource { get; set; }
        public String ReadOnly => "ro";
    }

    private class Target
    {
        public Int32 Id { get; set; }
        public String Name { get; set; }
        public String OnlyTarget { get; set; }
        public String ReadOnly => "ro";
    }

    [Fact(DisplayName = "CopyProperty_拷贝同名公开属性")]
    public void CopyProperties()
    {
        var src = new Source { Id = 3, Name = "n1", OnlySource = "x" };
        var dst = new Target { Id = 1, Name = "old", OnlyTarget = "keep" };

        DataConversion.CopyProperty(src, dst);

        Assert.Equal(3, dst.Id);
        Assert.Equal("n1", dst.Name);
        Assert.Equal("keep", dst.OnlyTarget);
        Assert.Equal("ro", dst.ReadOnly);
    }

    [Fact(DisplayName = "CopyProperty_忽略指定属性")]
    public void IgnoreProperties()
    {
        var src = new Source { Id = 3, Name = "n1" };
        var dst = new Target { Id = 1, Name = "old" };

        DataConversion.CopyProperty(src, dst, ["Name"]);

        Assert.Equal(3, dst.Id);
        Assert.Equal("old", dst.Name);
    }

    [Fact(DisplayName = "CopyProperty_泛型重载与对象重载行为一致")]
    public void GenericOverload()
    {
        var src = new Source { Id = 5, Name = "g" };
        var dst1 = new Target();
        var dst2 = new Target();

        DataConversion.CopyProperty(src, dst1);
        DataConversion.CopyProperty((Object)src, (Object)dst2);

        Assert.Equal(dst2.Id, dst1.Id);
        Assert.Equal(dst2.Name, dst1.Name);
    }
}
