using System;
using NewLife;
using NewLife.Log;
using NewLife.Security;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Membership;

public class UserTests
{
    [Fact]
    public void TestRoleIds()
    {
        var user = new User
        {
            Name = Rand.NextString(16),
            RoleIds = ",3,2,1,7,4",
        };
        user.Insert();

        Assert.Equal(1, user.RoleID);
        Assert.Equal(4, user.RoleIds.SplitAsInt().Length);
        Assert.Equal(",2,3,4,7,", user.RoleIds);

        var user2 = User.FindByKey(user.ID);
        Assert.Equal(1, user2.RoleID);
        Assert.Equal(4, user2.RoleIds.SplitAsInt().Length);
        Assert.Equal(",2,3,4,7,", user2.RoleIds);

        user2.RoleIds = "5,3,9,2,";
        user2.Update();

        var user3 = User.FindByKey(user.ID);
        Assert.Equal(1, user3.RoleID);
        Assert.Equal(4, user3.RoleIds.SplitAsInt().Length);
        Assert.Equal(",2,3,5,9,", user3.RoleIds);

        var dal = User.Meta.Session.Dal;
        var str = dal.QuerySingle<String>("select roleIds from user where id=@id", new { id = user.ID });
        Assert.Equal(",2,3,5,9,", str);

        //var ids = dal.QuerySingle<Int32[]>("select roleIds from user where id=@id", new { id = user.ID });
        //Assert.Equal(new[] { 2, 3, 5, 9 }, ids);
    }

    [Fact]
    public void StringLength()
    {
        var user = new User { Name = Rand.NextString(64) };
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => user.Insert());
        Assert.Equal("Name", ex.ParamName);
        Assert.Equal("[Name/名称@User]长度限制50字符[ID=0] (Parameter 'Name')", ex.Message);
    }

    [Fact]
    public void GetOrAdd()
    {
        var name = Rand.NextString(8);
        XTrace.WriteLine("GetOrAdd {0}", name);

        var user = User.GetOrAdd(name, k => User.FindByName(k), k => new User { Name = k });
        Assert.NotNull(user);

        XTrace.WriteLine("GetOrAdd2 {0}", name);

        var user2 = User.GetOrAdd(name, k => User.FindByName(k), k => new User { Name = k });
        Assert.NotNull(user2);
        Assert.Equal(user.ID, user2.ID);

        user.Delete();
    }

    [Fact]
    public void GetOrAdd3()
    {
        var name = Rand.NextString(8);

        //var u = new User { ["name"] = name };
        //u.Insert();
        //var u = new User();
        //u.SetItem("Name", name);
        //u.Insert();

        XTrace.WriteLine("GetOrAdd {0}", name);

        var user = User.GetOrAdd("name", name);
        Assert.NotNull(user);

        XTrace.WriteLine("GetOrAdd2 {0}", name);

        var user2 = User.GetOrAdd("name", name);
        Assert.NotNull(user2);
        Assert.Equal(user.ID, user2.ID);

        user.Delete();
    }
}