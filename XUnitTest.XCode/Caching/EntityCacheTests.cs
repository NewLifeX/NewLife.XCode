using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Log;
using XCode.Cache;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Caching
{
    [Collection("Database")]
    [TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
    public class EntityCacheTests
    {
        static EntityCacheTests()
        {
            CacheBase.Debug = true;
        }

        public EntityCacheTests()
        {
        }

        [Fact]
        public void Test1()
        {
            var cache = new EntityCache<Role>();
            Assert.Equal(0, cache.Times);
            Assert.Equal(10, cache.Expire);
            Assert.False(cache.Using);
            Assert.NotNull(cache.FillListMethod);
            Assert.Equal(cache.FillListMethod, Role.FindAll);
            Assert.True(cache.WaitFirst);
            Assert.Equal(0, cache.Total);
            Assert.Equal(0, cache.Success);

            // 尝试访问
            cache.Expire = 2;
            var list = cache.Entities;

            Assert.Equal(1, cache.Times);
            Assert.True(cache.Using);
            Assert.Equal(1, cache.Total);
            Assert.Equal(0, cache.Success);

            // 再次访问
            var list2 = cache.Entities;

            Assert.Equal(1, cache.Times);
            Assert.Equal(2, cache.Total);
            Assert.Equal(1, cache.Success);

            // 等待超时后，再次访问
            Thread.Sleep(cache.Expire * 1000 + 500);
            var list3 = cache.Entities;

            Assert.Equal(1, cache.Times);
            Assert.Equal(3, cache.Total);
            Assert.Equal(1, cache.Success);
        }

        [Fact]
        public void TestUpdateCacheAsync()
        {
            var cache = new EntityCache<Role>
            {
                Expire = 2
            };

            // 尝试访问
            var list = cache.Entities;
            Assert.Equal(1, cache.Times);

            // 等待超时后，再次访问
            Thread.Sleep(cache.Expire * 1000 + 500);
            var list3 = cache.Entities;

            Assert.Equal(1, cache.Times);

            // 等待更新完成，CI环境较慢，增加等待时间
            Thread.Sleep(3000);
            Assert.Equal(2, cache.Times);
        }

        [Fact]
        public void TestUpdateCacheSync()
        {
            var cache = new EntityCache<Role>
            {
                Expire = 2
            };

            // 尝试访问
            var list = cache.Entities;
            Assert.Equal(1, cache.Times);

            // 2倍超时后，再次访问走同步更新
            Thread.Sleep(cache.Expire * 1000 * 2 + 500);
            var list3 = cache.Entities;

            Assert.Equal(2, cache.Times);
        }

        [Fact]
        public void TestClear()
        {
            var cache = new EntityCache<Role>
            {
                Expire = 2
            };

            // 尝试访问
            var list = cache.Entities;
            Assert.Equal(1, cache.Times);

            cache.Clear("TestClear", false);

            Thread.Sleep(2000);
            Assert.Equal(1, cache.Times);

            // 再次访问
            var list2 = cache.Entities;

            Assert.Equal(1, cache.Times);
            Thread.Sleep(2000);
            Assert.Equal(2, cache.Times);

            cache.Clear("TestClear", true);

            // 再次访问
            list2 = cache.Entities;

            Assert.Equal(3, cache.Times);

            // 等待更新完成
            Thread.Sleep(2000);
            Assert.Equal(3, cache.Times);
        }

        [Fact]
        public void TestAddRemove()
        {
            var cache = new EntityCache<Role>
            {
                Expire = 2
            };

            var list = cache.Entities;

            var role = new Role { Name = "test" };

            // 添加实体对象
            cache.Add(role);
            var list2 = cache.Entities;
            Assert.NotEqual(list, list2);
            Assert.Equal(list.Count + 1, list2.Count);

            // 删除实体对象，来自内部
            cache.Remove(role);
            var list3 = cache.Entities;
            Assert.True(list != list3);
            Assert.True(list2 != list3);
            Assert.Equal(list.Count, list3.Count);

            // 删除实体对象，来自外部
            cache.Add(role);
            var role2 = new Role { Name = "test" };
            cache.Remove(role2);
            var list4 = cache.Entities;
            Assert.True(list != list4);
            Assert.True(list2 != list4);
            Assert.Equal(list.Count, list4.Count);
        }

        [Fact(DisplayName = "刷新期间新增实体不被旧快照覆盖")]
        public void Add_DuringRefresh_NotLost()
        {
            var entities = new List<Role>
            {
                new() { ID = 1, Name = "cache_race_1" },
                new() { ID = 2, Name = "cache_race_2" },
            };

            var blockNext = false;
            var loading = new ManualResetEventSlim();
            var proceed = new ManualResetEventSlim();

            var cache = new EntityCache<Role>
            {
                Expire = 2,
                FillListMethod = () =>
                {
                    var snapshot = entities.ToList();
                    if (blockNext)
                    {
                        // 模拟读库期间发生并发新增
                        loading.Set();
                        proceed.Wait(5000);
                    }
                    return snapshot;
                },
            };

            // 首次加载
            var list = cache.Entities;
            Assert.Equal(2, list.Count);

            // 强制过期，下一次访问同步刷新；刷新线程读取旧快照后阻塞
            blockNext = true;
            cache.Clear("测试刷新期间新增", true);
            var task = Task.Run(() => cache.Entities);
            Assert.True(loading.Wait(5000), "刷新未按预期启动");

            // 刷新进行中新增实体（模拟数据库写入后更新缓存）
            var added = new Role { ID = 100, Name = "cache_race_added" };
            cache.Add(added);

            proceed.Set();
            Assert.True(task.Wait(5000), "刷新未按预期完成");

            // 旧快照不包含新增实体，刷新完成后仍须保留
            var list2 = cache.Entities;
            Assert.Contains(list2, e => e.ID == added.ID);
        }
    }
}