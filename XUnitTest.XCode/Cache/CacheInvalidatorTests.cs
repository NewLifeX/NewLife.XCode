using System;
using NewLife.Caching;
using XCode;
using XCode.Cache;
using Xunit;

namespace XUnitTest.XCode.Cache;

/// <summary>二级缓存（分布式）测试：CacheInvalidator + XCodeSetting.CacheProvider</summary>
public class CacheInvalidatorTests
{
    public CacheInvalidatorTests()
    {
        // 确保测试前没有设置分布式缓存
        XCodeSetting.Current.CacheProvider = null;
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
        var cache = new MemoryCache();
        XCodeSetting.Current.CacheProvider = cache;

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
        finally
        {
            XCodeSetting.Current.CacheProvider = null;
        }
    }

    [Fact]
    public void WithMemoryCache_GetVersion_ReturnsCachedValue()
    {
        var cache = new MemoryCache();
        XCodeSetting.Current.CacheProvider = cache;

        try
        {
            var type = typeof(CacheInvalidatorTests);

            var v1 = CacheInvalidator.GetVersion(type);
            var v2 = CacheInvalidator.GetVersion(type);

            Assert.Equal(v1, v2); // 多次读取版本号不变
        }
        finally
        {
            XCodeSetting.Current.CacheProvider = null;
        }
    }

    [Fact]
    public void Provider_IsFromSetting()
    {
        var cache = new MemoryCache();
        XCodeSetting.Current.CacheProvider = cache;

        try
        {
            Assert.Same(cache, CacheInvalidator.Provider);
        }
        finally
        {
            XCodeSetting.Current.CacheProvider = null;
        }
    }

    [Fact]
    public void DifferentTypes_HaveDifferentVersions()
    {
        var cache = new MemoryCache();
        XCodeSetting.Current.CacheProvider = cache;

        try
        {
            var typeA = typeof(String);
            var typeB = typeof(Int32);

            CacheInvalidator.Invalidate(typeA, "a");
            var vA = CacheInvalidator.GetVersion(typeA);
            var vB = CacheInvalidator.GetVersion(typeB);

            Assert.Equal(1, vA);
            Assert.Equal(0, vB); // B未被影响
        }
        finally
        {
            XCodeSetting.Current.CacheProvider = null;
        }
    }
}
