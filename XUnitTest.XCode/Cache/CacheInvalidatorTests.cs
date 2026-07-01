using System;
using NewLife.Caching;
using XCode.Cache;
using Xunit;

namespace XUnitTest.XCode.Cache;

/// <summary>二级缓存（分布式）测试：CacheInvalidator.Provider 注入</summary>
public class CacheInvalidatorTests
{
    public CacheInvalidatorTests()
    {
        CacheInvalidator.Provider = null;
    }

    [Fact]
    public void WithoutProvider_GetVersion_ReturnsZero()
    {
        var ver = CacheInvalidator.GetVersion(typeof(CacheInvalidatorTests));
        Assert.Equal(0, ver);
    }

    [Fact]
    public void WithoutProvider_Invalidate_DoesNotThrow()
    {
        var ex = Record.Exception(() => CacheInvalidator.Invalidate(typeof(CacheInvalidatorTests), "test"));
        Assert.Null(ex);
    }

    [Fact]
    public void WithMemoryCache_Invalidate_IncrementsVersion()
    {
        CacheInvalidator.Provider = new MemoryCache();
        try
        {
            var type = typeof(CacheInvalidatorTests);

            var v0 = CacheInvalidator.GetVersion(type);
            CacheInvalidator.Invalidate(type, "test1");
            var v1 = CacheInvalidator.GetVersion(type);

            Assert.Equal(1, v1 - v0);

            CacheInvalidator.Invalidate(type, "test2");
            var v2 = CacheInvalidator.GetVersion(type);

            Assert.Equal(2, v2 - v0);
        }
        finally { CacheInvalidator.Provider = null; }
    }

    [Fact]
    public void WithMemoryCache_GetVersion_ReturnsCachedValue()
    {
        CacheInvalidator.Provider = new MemoryCache();
        try
        {
            var type = typeof(CacheInvalidatorTests);
            var v1 = CacheInvalidator.GetVersion(type);
            var v2 = CacheInvalidator.GetVersion(type);
            Assert.Equal(v1, v2);
        }
        finally { CacheInvalidator.Provider = null; }
    }

    [Fact]
    public void DifferentTypes_HaveDifferentVersions()
    {
        CacheInvalidator.Provider = new MemoryCache();
        try
        {
            var typeA = typeof(String);
            var typeB = typeof(Int32);

            CacheInvalidator.Invalidate(typeA, "a");
            var vA = CacheInvalidator.GetVersion(typeA);
            var vB = CacheInvalidator.GetVersion(typeB);

            Assert.Equal(1, vA);
            Assert.Equal(0, vB);
        }
        finally { CacheInvalidator.Provider = null; }
    }

    [Fact]
    public void Provider_CanSetAndRead()
    {
        var cache = new MemoryCache();
        CacheInvalidator.Provider = cache;
        try
        {
            Assert.Same(cache, CacheInvalidator.Provider);
        }
        finally { CacheInvalidator.Provider = null; }
    }
}
