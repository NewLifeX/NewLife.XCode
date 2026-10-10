using System;
using System.Data;
using System.IO;
using NewLife;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>多结果集测试。一条命令返回多个结果集，填充到 DataSet 的多个 DataTable。</summary>
[Collection("Database")]
public class MultiResultsetTests
{
    private static readonly String _connName = "MultiRs";

    static MultiResultsetTests()
    {
        var dbFile = Path.Combine(Path.GetTempPath(), $"MultiRs_{Guid.NewGuid():n}.db");
        DAL.AddConnStr(_connName, $"Data Source={dbFile}", null, "SQLite");

        var dal = DAL.Create(_connName);
        dal.Execute("CREATE TABLE IF NOT EXISTS MultiRsTest (ID INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL)");
        dal.Execute("DELETE FROM MultiRsTest");
        dal.Execute("INSERT INTO MultiRsTest (Name) VALUES ('one')");
        dal.Execute("INSERT INTO MultiRsTest (Name) VALUES ('two')");
        dal.Execute("INSERT INTO MultiRsTest (Name) VALUES ('three')");
    }

    [Fact(DisplayName = "多结果集：一条命令返回多个结果集填充多表")]
    public void Select_MultipleResultSets()
    {
        var dal = DAL.Create(_connName);

        var ds = dal.Select("Select ID, Name From MultiRsTest Order By ID; Select Count(*) As Cnt From MultiRsTest");

        Assert.Equal(2, ds.Tables.Count);

        var t0 = ds.Tables[0];
        Assert.Equal(3, t0.Rows.Count);
        Assert.Equal("one", t0.Rows[0]["Name"]);
        Assert.Equal("three", t0.Rows[2]["Name"]);

        var t1 = ds.Tables[1];
        Assert.Equal(1, t1.Rows.Count);
        Assert.Equal(3, t1.Rows[0][0].ToInt());
    }

    [Fact(DisplayName = "单结果集：普通查询仍填充单表")]
    public void Select_SingleResultSet()
    {
        var dal = DAL.Create(_connName);

        var ds = dal.Select("Select Name From MultiRsTest Where ID=1");

        Assert.Equal(1, ds.Tables.Count);
        Assert.Equal(1, ds.Tables[0].Rows.Count);
        Assert.Equal("one", ds.Tables[0].Rows[0][0]);
    }

    [Fact(DisplayName = "多结果集：第二个结果集为空时仍有表结构")]
    public void Select_EmptySecondResultSet()
    {
        var dal = DAL.Create(_connName);

        var ds = dal.Select("Select Name From MultiRsTest Where ID=1; Select Name From MultiRsTest Where ID=999");

        Assert.Equal(2, ds.Tables.Count);
        Assert.Equal(1, ds.Tables[0].Rows.Count);
        Assert.Equal(0, ds.Tables[1].Rows.Count);
        Assert.Equal("Name", ds.Tables[1].Columns[0].ColumnName);
    }
}
