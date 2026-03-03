using System;
using System.Collections.Generic;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>网络虚拟数据库单元测试。覆盖NetworkDb/NetworkSession/NetworkMetaData的纯逻辑路径，无需真实网络连接</summary>
public class NetworkTests
{
    [Fact(DisplayName = "初始化网络数据库")]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.Network);
        Assert.NotNull(db);
        Assert.Equal(DatabaseType.Network, db.Type);
    }

    [Fact(DisplayName = "测试Support方法识别network/net提供者")]
    public void SupportTest()
    {
        var db = DbFactory.Create(DatabaseType.Network);

        Assert.True(db.Support("network"));
        Assert.True(db.Support("Network"));
        Assert.True(db.Support("net"));
        Assert.False(db.Support("mysql"));
        Assert.False(db.Support("sqlite"));
    }

    [Fact(DisplayName = "无Server时长文本长度默认4000")]
    public void LongTextLength_NoServer_Returns4000()
    {
        var db = DbFactory.Create(DatabaseType.Network);

        Assert.Equal(4000, db.LongTextLength);
    }

    [Fact(DisplayName = "无Server时格式化时间为标准SQL格式")]
    public void FormatDateTime_NoServer_DefaultFormat()
    {
        var db = DbFactory.Create(DatabaseType.Network);
        var dt = new DateTime(2024, 6, 15, 12, 30, 45);

        var result = db.FormatDateTime(dt);

        Assert.Equal("'2024-06-15 12:30:45'", result);
    }

    [Fact(DisplayName = "无Server时格式化名称原样返回")]
    public void FormatName_NoServer_ReturnsOriginal()
    {
        var db = DbFactory.Create(DatabaseType.Network);

        Assert.Equal("TestTable", db.FormatName("TestTable"));
        Assert.Equal("user_id", db.FormatName("user_id"));
    }

    [Fact(DisplayName = "无Server时格式化参数名添加@前缀")]
    public void FormatParameterName_NoServer_AppendsAt()
    {
        var db = DbFactory.Create(DatabaseType.Network);

        Assert.Equal("@name", db.FormatParameterName("name"));
        Assert.Equal("@userId", db.FormatParameterName("userId"));
    }

    [Fact(DisplayName = "无Server时字符串连接使用加号")]
    public void StringConcat_NoServer_UsesPlus()
    {
        var db = DbFactory.Create(DatabaseType.Network);

        Assert.Equal("a+b", db.StringConcat("a", "b"));
    }

    [Fact(DisplayName = "无Server时创建参数返回正确IDataParameter")]
    public void CreateParameter_ReturnsParameter()
    {
        var db = DbFactory.Create(DatabaseType.Network);

        var p = db.CreateParameter("name", "hello", (IDataColumn?)null);

        Assert.NotNull(p);
        Assert.Equal("@name", p.ParameterName);
        Assert.Equal("hello", p.Value);
    }

    [Fact(DisplayName = "无Server时创建参数null值使用DBNull")]
    public void CreateParameter_NullValue_UsesDbnull()
    {
        var db = DbFactory.Create(DatabaseType.Network);

        var p = db.CreateParameter("col", (Object?)null, (IDataColumn?)null);

        Assert.NotNull(p);
        Assert.Equal(DBNull.Value, p.Value);
    }

    [Fact(DisplayName = "CreateParameters传null返回空数组")]
    public void CreateParameters_Null_ReturnsEmpty()
    {
        var db = DbFactory.Create(DatabaseType.Network);

        var ps = db.CreateParameters((IDictionary<String, Object>?)null);

        Assert.NotNull(ps);
        Assert.Empty(ps);
    }

    [Fact(DisplayName = "CreateParameters传字典返回参数数组")]
    public void CreateParameters_Dict_ReturnsParameters()
    {
        var db = DbFactory.Create(DatabaseType.Network);
        var dict = new Dictionary<String, Object> { ["id"] = 42 };

        var ps = db.CreateParameters(dict);

        Assert.NotNull(ps);
        Assert.Single(ps);
        Assert.Equal("@id", ps[0].ParameterName);
        Assert.Equal(42, ps[0].Value);
    }

    [Fact(DisplayName = "分页SQL - 无分页参数原样返回SQL")]
    public void PageSplitString_NoPaging_ReturnsOriginalSql()
    {
        var db = DbFactory.Create(DatabaseType.Network);
        var sql = "SELECT * FROM user";

        var result = db.PageSplit(sql, 0, 0, null);

        Assert.Equal(sql, result);
    }

    [Fact(DisplayName = "分页SQL - 仅最大行数追加LIMIT子句")]
    public void PageSplitString_LimitOnly_AppendsLimit()
    {
        var db = DbFactory.Create(DatabaseType.Network);
        var sql = "SELECT * FROM user";

        var result = db.PageSplit(sql, 0, 10, null);

        Assert.Equal($"{sql} limit 10", result);
    }

    [Fact(DisplayName = "分页SQL - 有起始行和最大行数追加偏移LIMIT")]
    public void PageSplitString_OffsetAndLimit_AppendsOffsetLimit()
    {
        var db = DbFactory.Create(DatabaseType.Network);
        var sql = "SELECT * FROM user";

        var result = db.PageSplit(sql, 5, 10, null);

        Assert.Equal($"{sql} limit 5, 10", result);
    }

    [Fact(DisplayName = "分页SQL - 有起始行无最大行数时抛出NotSupportedException")]
    public void PageSplitString_StartRowWithoutMaxRows_ThrowsNotSupported()
    {
        var db = DbFactory.Create(DatabaseType.Network);
        var sql = "SELECT * FROM user";

        Assert.Throws<NotSupportedException>(() => db.PageSplit(sql, 5, 0, null));
    }

    [Fact(DisplayName = "Builder分页 - 仅最大行数设置Limit属性")]
    public void PageSplitBuilder_LimitOnly_SetsLimit()
    {
        var db = DbFactory.Create(DatabaseType.Network);
        var builder = new SelectBuilder("SELECT * FROM user");

        var result = db.PageSplit(builder, 0, 10);

        Assert.Equal("limit 10", result.Limit);
    }

    [Fact(DisplayName = "Builder分页 - 有起始行和最大行数设置偏移Limit属性")]
    public void PageSplitBuilder_OffsetAndLimit_SetsOffsetLimit()
    {
        var db = DbFactory.Create(DatabaseType.Network);
        var builder = new SelectBuilder("SELECT * FROM user");

        var result = db.PageSplit(builder, 5, 10);

        Assert.Equal("limit 5, 10", result.Limit);
    }

    [Fact(DisplayName = "Builder分页 - 无分页参数时Limit属性为空")]
    public void PageSplitBuilder_NoPaging_LimitIsNull()
    {
        var db = DbFactory.Create(DatabaseType.Network);
        var builder = new SelectBuilder("SELECT * FROM user");

        var result = db.PageSplit(builder, 0, 0);

        Assert.Null(result.Limit);
    }

    [Fact(DisplayName = "解析连接字符串后DatabaseName正确设置")]
    public void ConnectionString_DatabaseNameParsed()
    {
        var db = DbFactory.Create(DatabaseType.Network);
        db.ConnName = "test_net_dbname";

        db.ConnectionString = "Server=http://127.0.0.1:3305;Database=TestDb;Password=tok123;Provider=Network";

        Assert.Equal("TestDb", db.DatabaseName);
    }

    [Fact(DisplayName = "NetworkMetaData.SetTables为空操作不抛异常")]
    public void NetworkMetaData_SetTables_IsNoOp()
    {
        var connName = "net_meta_" + Guid.NewGuid().ToString("N")[..8];
        DAL.AddConnStr(connName, "Server=http://127.0.0.1:3305;Database=TestDb;Password=tok", null, "Network");
        var dal = DAL.Create(connName);

        // NetworkMetaData.OnSetTables是no-op，无论有无实际网络连接都不应抛出异常
        var ex = Record.Exception(() => dal.SetTables());
        Assert.Null(ex);
    }
}
