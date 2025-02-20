using System;
using System.IO;
using NewLife;
using XCode.Exceptions;
using Xunit;
using XUnitTest.XCode.TestEntity;

namespace XUnitTest.XCode.EntityTests;

public class TimeShardTests
{
    [Fact]
    public void Test1()
    {
        var file = Setting.Current.DataPath.CombinePath("test.db");
        if (File.Exists(file)) File.Delete(file);

        //var dal = ExpressLogs.Meta.Session.Dal;
        //var table = ExpressLogs.Meta.Table.DataTable;

        //dal.SetTables(table);

        // 对分表查行数，报错
        Assert.Throws<XSqlException>(() => ExpressLogs.FindCount());

        // 分表查询，没有数据
        var time = DateTime.Now.AddHours(1);
        var list = ExpressLogs.Search(time.Date, time, null, null);
        Assert.Empty(list);

        // 写入数据
        var entity = new ExpressLogs
        {
            Code = "123456",
        };
        entity.Insert();

        list = ExpressLogs.Search(time.Date, time, null, null);
        Assert.Single(list);

        //var n = ExpressLogs.Meta.Count;
        //Assert.Equal(0, n);
    }
}
