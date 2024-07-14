using System;
using NewLife.Security;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Entity;

public class EntityTests
{
    [Fact]
    public void LongFieldTest()
    {
        var user = new User
        {
            Name = "StoneXXX",
            DisplayName = Rand.NextString(99),
        };
        //user.Insert();
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => user.Insert());
        Assert.Contains("[Name=StoneXXX]", ex.Message);
    }
}
