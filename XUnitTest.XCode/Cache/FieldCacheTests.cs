using System;
using XCode.Cache;
using XCode.Configuration;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Cache;

/// <summary>字段缓存测试</summary>
public class FieldCacheTests
{
    [Fact(DisplayName = "FieldCache_创建实例")]
    public void CreateInstance()
    {
        // FieldCache 要求传入字段名，这里使用 Name 作为测试字段
        var fc = new FieldCache<Role>("Name");
        Assert.NotNull(fc);

        // 默认最大行数 50
        Assert.Equal(50, fc.MaxRows);
    }

    [Fact(DisplayName = "FieldCache_排序默认值")]
    public void DefaultOrderBy()
    {
        var fc = new FieldCache<Role>("Name");
        Assert.Equal("group_count desc", fc.OrderBy);
    }
}
