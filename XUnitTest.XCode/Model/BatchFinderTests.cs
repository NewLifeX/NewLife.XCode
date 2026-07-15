using System;
using System.Linq;
using XCode.Model;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Model;

/// <summary>批量主键查找器测试</summary>
public class BatchFinderTests
{
    [Fact(DisplayName = "BatchFinder_创建实例")]
    public void CreateInstance()
    {
        var bf = new BatchFinder<Int32, Role>();
        Assert.NotNull(bf);
        Assert.NotNull(bf.Factory);
        Assert.NotNull(bf.Keys);
    }

    [Fact(DisplayName = "BatchFinder_默认值")]
    public void DefaultValues()
    {
        var bf = new BatchFinder<Int32, Role>();
        Assert.Equal(500, bf.BatchSize);
    }

    [Fact(DisplayName = "BatchFinder_添加键")]
    public void AddKeys()
    {
        var bf = new BatchFinder<Int32, Role>();
        bf.Keys.Add(1);
        bf.Keys.Add(2);
        bf.Keys.Add(3);
        Assert.Equal(3, bf.Keys.Count);
        Assert.Equal([1, 2, 3], bf.Keys);
    }
}
