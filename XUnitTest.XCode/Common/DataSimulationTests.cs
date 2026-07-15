using System;
using System.Linq;
using XCode.Common;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Common;

/// <summary>数据模拟压测测试</summary>
public class DataSimulationTests
{
    [Fact(DisplayName = "DataSimulation_创建实例")]
    public void CreateInstance()
    {
        var ds = new DataSimulation<Role>();
        Assert.NotNull(ds);
        Assert.NotNull(ds.Factory);
    }

    [Fact(DisplayName = "DataSimulation_默认值")]
    public void DefaultValues()
    {
        var ds = new DataSimulation();
        Assert.Equal(1000, ds.BatchSize);
        Assert.Equal(1, ds.Threads);
    }
}
