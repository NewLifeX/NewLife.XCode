using System;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Membership;

public class PermissionFlagsTests
{
    [Fact]
    public void Test1()
    {
        var pm = (PermissionFlags)0xFFFFFFFF;
        Assert.Equal(PermissionFlags.All, pm);
        Assert.Equal(0xFFFFFFFF, (UInt32)pm);
        Assert.Equal(-1, (Int32)pm);

        var v = -1;
        pm = (PermissionFlags)v;
        Assert.Equal(PermissionFlags.All, pm);
        Assert.Equal(0xFFFFFFFF, (UInt32)pm);
        Assert.Equal(-1, (Int32)pm);
    }
}
