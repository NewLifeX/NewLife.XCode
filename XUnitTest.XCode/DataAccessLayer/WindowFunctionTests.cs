using System;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>窗口函数测试：SelectBuilder.Window + WindowFunction 助手类</summary>
public class WindowFunctionTests
{
    // ====== SelectBuilder.Window ======

    [Fact]
    public void SelectBuilder_Window_Injected()
    {
        var sb = new SelectBuilder { Table = "Role", Column = "*" };
        sb.Window = "ROW_NUMBER() OVER(ORDER BY ID) AS RowNum";

        var sql = sb.ToString();
        Assert.Contains("ROW_NUMBER() OVER(ORDER BY ID) AS RowNum", sql);
        Assert.StartsWith("Select *,", sql);
    }

    [Fact]
    public void SelectBuilder_Window_WithColumn()
    {
        var sb = new SelectBuilder { Table = "Role", Column = "ID, Name" };
        sb.Window = "RANK() OVER(ORDER BY Name) AS Rank";

        var sql = sb.ToString();
        Assert.Contains("Select ID, Name,", sql);
        Assert.Contains("RANK() OVER(ORDER BY Name) AS Rank", sql);
    }

    [Fact]
    public void SelectBuilder_Window_Null_NoComma()
    {
        var sb = new SelectBuilder { Table = "Role" };
        // Window is null by default

        var sql = sb.ToString();
        Assert.DoesNotContain(", ", sql); // No extra comma
        Assert.Equal("Select * From Role", sql);
    }

    [Fact]
    public void SelectBuilder_Clone_CopiesWindow()
    {
        var sb = new SelectBuilder { Table = "T", Window = "ROW_NUMBER() OVER(ORDER BY ID) AS Rn" };
        var clone = sb.Clone();

        Assert.Equal(sb.Window, clone.Window);
        Assert.Equal(sb.ToString(), clone.ToString());
    }

    // ====== WindowFunction.RowNumber ======

    [Fact]
    public void RowNumber_Basic()
    {
        var result = WindowFunction.RowNumber("ID DESC");
        Assert.Equal("ROW_NUMBER() OVER(ORDER BY ID DESC) AS RowNum", result);
    }

    [Fact]
    public void RowNumber_WithAlias()
    {
        var result = WindowFunction.RowNumber("CreateTime", "Rn");
        Assert.Equal("ROW_NUMBER() OVER(ORDER BY CreateTime) AS Rn", result);
    }

    [Fact]
    public void RowNumber_WithPartition()
    {
        // 三参数重载：partitionBy, orderBy, alias
        var result = WindowFunction.RowNumber(partitionBy: "DeptID", orderBy: "Salary DESC");
        Assert.Equal("ROW_NUMBER() OVER(PARTITION BY DeptID ORDER BY Salary DESC) AS RowNum", result);
    }

    [Fact]
    public void RowNumber_WithPartitionAndAlias()
    {
        var result = WindowFunction.RowNumber("DeptID", "ID", "Row");
        Assert.Equal("ROW_NUMBER() OVER(PARTITION BY DeptID ORDER BY ID) AS Row", result);
    }

    [Fact]
    public void RowNumber_NullOrderBy_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => WindowFunction.RowNumber(null));
    }

    // ====== WindowFunction.Rank ======

    [Fact]
    public void Rank_Basic()
    {
        var result = WindowFunction.Rank("Score DESC");
        Assert.Equal("RANK() OVER(ORDER BY Score DESC) AS Rank", result);
    }

    // ====== WindowFunction.DenseRank ======

    [Fact]
    public void DenseRank_Basic()
    {
        var result = WindowFunction.DenseRank("Score DESC");
        Assert.Equal("DENSE_RANK() OVER(ORDER BY Score DESC) AS DenseRank", result);
    }

    // ====== WindowFunction.Aggregate ======

    [Fact]
    public void Aggregate_Sum_WithPartition()
    {
        var result = WindowFunction.Aggregate("SUM", "Amount", "DeptID");
        Assert.Equal("SUM(Amount) OVER(PARTITION BY DeptID) AS SUM_Amount", result);
    }

    [Fact]
    public void Aggregate_Count_NoPartition()
    {
        var result = WindowFunction.Aggregate("COUNT", "*");
        Assert.Equal("COUNT(*) OVER() AS COUNT_*", result);
    }

    [Fact]
    public void Aggregate_Avg_WithAlias()
    {
        var result = WindowFunction.Aggregate("AVG", "Score", "ClassID", "AvgScore");
        Assert.Equal("AVG(Score) OVER(PARTITION BY ClassID) AS AvgScore", result);
    }
}
