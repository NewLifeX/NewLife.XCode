using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.Cache;

/// <summary>字段缓存测试</summary>
/// <remarks>
/// 使用专用测试实体 FieldCacheRole（BindTable 固定 ConnName=FieldCacheTest），
/// 避免运行时切换 Meta.ConnName 的 AsyncLocal 线程上下文问题。
/// </remarks>
[Collection("Database")]
public class FieldCacheTests : IDisposable
{
    private static readonly String _dbFile;

    static FieldCacheTests()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"FieldCacheTest_{Guid.NewGuid():n}.db");

        // 独立 SQLite 连接，自动迁移建表，避免污染默认 Membership 库
        DAL.AddConnStr("FieldCacheTest", $"Data Source={_dbFile}", null, "SQLite");
    }

    public FieldCacheTests()
    {
        // 清掉可能残留的字段缓存，保证每次测试从零开始
        DataCache.Current.FieldCache.Remove("FieldCacheRole_Name");
    }

    public void Dispose()
    {
        // 清理测试数据
        foreach (var entity in FieldCacheRole.FindAll())
        {
            entity.Delete();
        }
    }

    [Fact(DisplayName = "FieldCache_创建实例")]
    public void CreateInstance()
    {
        // FieldCache 要求传入字段名，这里使用 Name 作为测试字段
        var fc = new FieldCache<FieldCacheRole>("Name");
        Assert.NotNull(fc);

        // 默认最大行数 50
        Assert.Equal(50, fc.MaxRows);
    }

    [Fact(DisplayName = "FieldCache_排序默认值")]
    public void DefaultOrderBy()
    {
        var fc = new FieldCache<FieldCacheRole>("Name");
        Assert.Equal("group_count desc", fc.OrderBy);
    }

    [Fact(DisplayName = "FieldCache_并发FindAllName_结果一致且单次查询")]
    public void FindAllName_Concurrent_Consistent()
    {
        // 准备测试数据：Admin/User/Guest 各 2/1/2 条
        var names = new[] { "Admin", "User", "Guest", "Admin", "Guest" };
        foreach (var name in names)
        {
            new FieldCacheRole { Name = name }.Insert();
        }

        var fc = new FieldCache<FieldCacheRole>("Name")
        {
            WaitFirst = true,
            Expire = 600,
        };

        // 统计缓存填充次数，验证并发下单次查询（单飞行）
        var queryCount = 0;
        var fill = fc.FillListMethod;
        fc.FillListMethod = () =>
        {
            Interlocked.Increment(ref queryCount);
            return fill();
        };

        // 多线程并发首次调用
        const Int32 n = 8;
        var dicts = new ConcurrentDictionary<Int32, IDictionary<String, String>>();
        var tasks = new Task[n];
        for (var i = 0; i < n; i++)
        {
            var idx = i;
            tasks[idx] = Task.Run(() => dicts[idx] = fc.FindAllName());
        }

        Task.WaitAll(tasks);

        // 所有线程都返回非空结果且内容一致
        Assert.Equal(n, dicts.Count);
        var first = dicts[0];
        Assert.NotNull(first);
        Assert.NotEmpty(first);

        foreach (var kv in dicts)
        {
            Assert.NotNull(kv.Value);
            Assert.Equal(first.Count, kv.Value.Count);
            Assert.True(first.All(e => kv.Value.TryGetValue(e.Key, out var v) && v == e.Value));
        }

        // 分组统计正确：Admin=2, User=1, Guest=2（默认格式 "{0} ({1:n0})"）
        Assert.Equal("Admin (2)", first["Admin"]);
        Assert.Equal("User (1)", first["User"]);
        Assert.Equal("Guest (2)", first["Guest"]);

        // 并发首次访问只填充一次
        Assert.Equal(1, queryCount);

        // 二次调用直接命中缓存，不再查询
        var second = fc.FindAllName();
        Assert.Equal(first.Count, second.Count);
        Assert.True(first.All(e => second.TryGetValue(e.Key, out var v) && v == e.Value));
        Assert.Equal(1, queryCount);
    }
}
