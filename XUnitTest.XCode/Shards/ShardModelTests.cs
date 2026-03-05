using System;
using XCode.Shards;
using Xunit;

namespace XUnitTest.XCode.Shards;

/// <summary>ShardModel分表模型测试</summary>
public class ShardModelTests
{
    [Fact(DisplayName = "构造_基本属性")]
    public void Ctor_BasicProperties()
    {
        var model = new ShardModel("conn1", "User_202501");

        Assert.Equal("conn1", model.ConnName);
        Assert.Equal("User_202501", model.TableName);
    }

    [Fact(DisplayName = "Record相等比较_值相等")]
    public void Equals_SameValues_ReturnsTrue()
    {
        var m1 = new ShardModel("conn", "table");
        var m2 = new ShardModel("conn", "table");

        Assert.Equal(m1, m2);
        Assert.True(m1 == m2);
    }

    [Fact(DisplayName = "Record相等比较_值不等")]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        var m1 = new ShardModel("conn1", "table");
        var m2 = new ShardModel("conn2", "table");

        Assert.NotEqual(m1, m2);
        Assert.True(m1 != m2);
    }

    [Fact(DisplayName = "Record_GetHashCode_相同值相同散列")]
    public void GetHashCode_SameValues_SameHash()
    {
        var m1 = new ShardModel("conn", "table");
        var m2 = new ShardModel("conn", "table");

        Assert.Equal(m1.GetHashCode(), m2.GetHashCode());
    }

    [Fact(DisplayName = "Record_ToString")]
    public void ToString_ContainsProperties()
    {
        var model = new ShardModel("MyConn", "User_202501");

        var str = model.ToString();

        Assert.Contains("MyConn", str);
        Assert.Contains("User_202501", str);
    }

    [Fact(DisplayName = "Record解构")]
    public void Deconstruct_Works()
    {
        var model = new ShardModel("conn1", "table1");

        var (connName, tableName) = model;

        Assert.Equal("conn1", connName);
        Assert.Equal("table1", tableName);
    }

}
