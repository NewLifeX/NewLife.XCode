using NewLife.Security;
using XCode;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Entity;

public class EntityExtensionTests
{
    [Fact]
    public void TrimExtraLong()
    {
        var user = new User
        {
            Name = "Stone"
        };

        var rs = user.TrimExtraLong();
        Assert.Equal(0, rs);
        Assert.Equal("Stone", user.Name);
    }

    [Fact]
    public void TrimExtraLong2()
    {
        var name = Rand.NextString(99);
        var user = new User
        {
            Name = name
        };

        var rs = user.TrimExtraLong("DisplayName");
        Assert.Equal(0, rs);
        Assert.Equal(name, user.Name);

        rs = user.TrimExtraLong("name");
        Assert.Equal(0, rs);
        Assert.NotEqual(name, user.Name);
        Assert.Equal(name.Substring(0, 50), user.Name);
    }

    [Fact]
    public void TrimExtraLong3()
    {
        var name = Rand.NextString(99);
        var user = new User
        {
            Name = name
        };

        var rs = user.TrimExtraLong();
        Assert.NotEqual(name, user.Name);
        Assert.Equal(name.Substring(0, 50), user.Name);
    }
}
