using Xunit;

using XUnitTest.XCode.TestEntity;

namespace XUnitTest.XCode;

public class TimeShardTests
{
    [Fact]
    public void Test1()
    {
        var n = ExpressLogs.Meta.Count;

        Assert.Equal(0, n);
    }
}
