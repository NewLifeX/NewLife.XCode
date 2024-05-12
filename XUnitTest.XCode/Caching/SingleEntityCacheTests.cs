using System;
using System.Linq;
using System.Threading;
using NewLife.Log;
using XCode.Cache;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Caching;

[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class SingleEntityCacheTests
{
    static SingleEntityCacheTests() => CacheBase.Debug = true;

    public SingleEntityCacheTests()
    {
    }

    [Fact]
    public void Test1()
    {
        var cache = new SingleEntityCache<Int32, User>();
        Assert.Equal(10, cache.Expire);
        Assert.Equal(60, cache.ClearPeriod);
        Assert.Equal(10000, cache.MaxEntity);
        Assert.False(cache.Using);
        Assert.NotNull(cache.GetKeyMethod);
        Assert.NotNull(cache.FindKeyMethod);
        Assert.Equal(0, cache.Total);
        Assert.Equal(0, cache.Success);
    }

    [Fact]
    public void TestKey()
    {
        var list = User.FindAll(null, null, null, 0, 1);
        var id = list.FirstOrDefault().ID;

        XTrace.WriteLine("准备在User上测试单对象缓存，ID={0}    ", id);

        var cache = new SingleEntityCache<Int32, User> { Expire = 1 };

        // 首次访问
        XTrace.WriteLine("首次访问");
        var user = cache[id];
        Assert.Equal(0, cache.Success);

        // 再次访问
        XTrace.WriteLine("再次访问");
        var user2 = cache[id];
        Assert.Equal(1, cache.Success);

        Thread.Sleep(cache.Expire * 1000 + 10);

        // 三次访问
        XTrace.WriteLine("三次访问");
        var user3 = cache[id];
        Assert.Equal(2, cache.Success);
    }

    [Fact]
    public void TestSlave()
    {
        //var list = User.FindAll(null, null, null, 0, 100);
        //var entity = User.Find(User._.Name == "admin");
        //if (entity == null)
        //{
        //    entity = new User { Name = "admin" };
        //    entity.Insert();
        //}

        var cache = new SingleEntityCache<Int32, User> { Expire = 1 };
        cache.FindSlaveKeyMethod = k => User.Find(User._.Name == k);
        cache.GetSlaveKeyMethod = e => e.Name;

        // 首次访问
        var user = cache.GetItemWithSlaveKey("admin");
        Assert.Equal(0, cache.Success);

        // 再次访问
        var user2 = cache.GetItemWithSlaveKey("admin");
        Assert.Equal(1, cache.Success);

        Thread.Sleep(cache.Expire * 1000 + 10);

        // 再次访问
        var user3 = cache.GetItemWithSlaveKey("admin");
        Assert.Equal(2, cache.Success);
    }
}