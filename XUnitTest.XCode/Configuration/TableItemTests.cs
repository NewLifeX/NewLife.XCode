using NewLife.Log;
using XCode.Configuration;
using Xunit;
using XUnitTest.XCode.TestEntity;

namespace XUnitTest.XCode.Configuration;

public class TableItemTests
{
    [Fact]
    public void TrimIndex()
    {
        var ti = TableItem.Create(typeof(Log2));
        XTrace.WriteLine(ti.TableName);
        Assert.Equal(4, ti.DataTable.Indexes.Count);
    }

    /// <summary>
    /// 验证设置了 ConnName 时，TableName 应返回 BindTableAttribute.Name，而不是 ConnName。
    /// 修复 issue #90：自动创建表时，表名会错误地变成 ConnName 的名称。
    /// </summary>
    [Fact]
    public void TableName_ShouldNotUseConnName()
    {
        // User2 entity: [BindTable("User2", ConnName = "test")]
        var ti = TableItem.Create(typeof(User2));

        // 表名应为 BindTable 第一个参数 "User2"，而不是连接名 "test"
        Assert.Equal("User2", ti.TableName);
        Assert.Equal("test", ti.ConnName);

        // DataTable 的 TableName 也应正确
        Assert.Equal("User2", ti.DataTable.TableName);
        Assert.Equal("test", ti.DataTable.ConnName);
    }

    /// <summary>
    /// 验证 TableName 来自 BindTableAttribute.Name，ConnName 来自 BindTableAttribute.ConnName，两者相互独立。
    /// </summary>
    [Fact]
    public void TableName_AndConnName_AreDistinct()
    {
        // Log2 entity: [BindTable("Log2", ConnName = "test")]
        var ti = TableItem.Create(typeof(Log2));

        // TableName 来自 BindTableAttribute.Name，ConnName 来自 BindTableAttribute.ConnName
        Assert.NotEqual(ti.TableName, ti.ConnName);
        Assert.Equal("Log2", ti.TableName);
        Assert.Equal("test", ti.ConnName);
    }
}