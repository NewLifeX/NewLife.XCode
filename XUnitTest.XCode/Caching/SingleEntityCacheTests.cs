using System;
using System.Linq;
using System.Threading;
using NewLife.Log;
using NewLife.Security;
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
        var id = list.FirstOrDefault().Id;

        XTrace.WriteLine("准备在User上测试单对象缓存，Id={0}    ", id);

        var cache = new SingleEntityCache<Int32, User> { Expire = 1 };

        // 首次访问
        XTrace.WriteLine("首次访问");
        var user = cache[id];
        Assert.Equal(0, cache.Success);

        // 再次访问
        XTrace.WriteLine("再次访问");
        var user2 = cache[id];
        Assert.Equal(1, cache.Success);

        Thread.Sleep(cache.Expire * 1000 + 500);

        // 三次访问
        XTrace.WriteLine("三次访问");
        var user3 = cache[id];
        Assert.Equal(2, cache.Success);
    }

    [Fact(DisplayName = "写入单对象缓存后按主键与从键都能取到，且改名后旧从键失效")]
    public void TestAdd()
    {
        var cache = new SingleEntityCache<Int32, User> { Expire = 60 };
        cache.FindSlaveKeyMethod = k => User.Find(User._.Name == k);
        cache.GetSlaveKeyMethod = e => e.Name;

        // 高频主键，确保不会命中数据库既有记录
        var id = 900_000 + Rand.Next(100_000);
        var name = "cache_" + Rand.NextString(6);
        var user = new User { Id = id, Name = name, Enable = true };

        // 索引器写入后无需数据库即可命中
        cache[id] = user;
        Assert.True(cache.ContainsKey(id));
        Assert.Equal(name, cache[id].Name);
        Assert.True(cache.ContainsSlaveKey(name));
        Assert.Equal(id, cache.GetItemWithSlaveKey(name).Id);

        // 原位替换：改名后旧从键不能继续指向旧对象
        var renamed = new User { Id = id, Name = name + "_new", Enable = true };
        cache[id] = renamed;
        Assert.Equal(renamed.Name, cache[id].Name);
        Assert.False(cache.ContainsSlaveKey(name));
        Assert.True(cache.ContainsSlaveKey(renamed.Name));
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

        Thread.Sleep(cache.Expire * 1000 + 500);

        // 再次访问
        var user3 = cache.GetItemWithSlaveKey("admin");
        Assert.Equal(2, cache.Success);
    }
}