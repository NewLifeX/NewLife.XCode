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

    [Fact(DisplayName = "实体列表包含空元素时应忽略并继续生成字典与参数")]
    public void IgnoreNullItems_ForDictionaryAndParameters()
    {
        var list = new User[]
        {
            null!,
            new User { ID = 123, Name = "Stone" },
            null!,
            new User { ID = 456, Name = "NewLife" }
        };

        var dic = list.ToDictionary(nameof(User.Name));
        Assert.Equal(2, dic.Count);

        var dps = list.CreateParameters(User.Meta.Session);
        Assert.NotEmpty(dps);
    }

    [Fact(DisplayName = "实体列表包含空元素时应忽略并继续转换表结构与脏字段")]
    public void IgnoreNullItems_ForTableAndDirtyColumns()
    {
        var user1 = new User { ID = 123, Name = "Stone" };
        var user2 = new User { ID = 456, Name = "NewLife" };
        var list = new User[] { null!, user1, null!, user2 };

        var dt = list.ToDataTable();
        Assert.Equal(2, dt.Rows.Count);
        Assert.True(dt.Columns.Contains(nameof(User.Name)));

        var columns = User.Meta.Factory.GetDirtyColumns(new IEntity[] { null!, user1, null!, user2 });
        Assert.NotEmpty(columns);
    }
}
