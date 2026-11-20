using System;
using System.IO;
using System.Linq;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>窗口函数表达式与方言渲染测试</summary>
public class WindowExpressionTests
{
    [Fact]
    [System.ComponentModel.Description("ROW_NUMBER 渲染为标准窗口 SQL")]
    public void RowNumber_RendersStandardSql()
    {
        var expression = WindowExpression.RowNumber("ID DESC", null, "RowNum");

        Assert.Equal("ROW_NUMBER() OVER(ORDER BY ID DESC) AS RowNum", expression.Render());
    }

    [Fact]
    [System.ComponentModel.Description("RANK 支持分区与排序")]
    public void Rank_WithPartition_Renders()
    {
        var expression = WindowExpression.Rank("Salary DESC", "DeptID", "Rank");

        Assert.Equal("RANK() OVER(PARTITION BY DeptID ORDER BY Salary DESC) AS Rank", expression.Render());
    }

    [Fact]
    [System.ComponentModel.Description("聚合窗口函数渲染窗口帧")]
    public void Aggregate_WithFrame_Renders()
    {
        var expression = WindowExpression.Aggregate(
            WindowFunctionKind.Sum,
            "Amount",
            "DeptID",
            "ID",
            new WindowFrame
            {
                IsRows = true,
                Start = WindowFrameBoundary.UnboundedPreceding,
                End = WindowFrameBoundary.CurrentRow,
            },
            "RunningTotal");

        Assert.Equal("SUM(Amount) OVER(PARTITION BY DeptID ORDER BY ID ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS RunningTotal", expression.Render());
    }

    [Fact]
    [System.ComponentModel.Description("ROW_NUMBER 必须指定排序字段")]
    public void RowNumber_WithoutOrderBy_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => WindowExpression.RowNumber());
    }

    [Fact]
    [System.ComponentModel.Description("NTILE 渲染分桶参数")]
    public void Ntile_RendersArguments()
    {
        var expression = WindowExpression.Ntile(4, "Score DESC", null, "Bucket");

        Assert.Equal("NTILE(4) OVER(ORDER BY Score DESC) AS Bucket", expression.Render());
    }

    [Fact]
    [System.ComponentModel.Description("LAG 支持偏移量与默认值")]
    public void Lag_RendersOffsetAndDefault()
    {
        var expression = WindowExpression.Lag("Score", 2, "0", "ID", null, "PrevScore");

        Assert.Equal("LAG(Score, 2, 0) OVER(ORDER BY ID) AS PrevScore", expression.Render());
    }

    [Fact]
    [System.ComponentModel.Description("FIRST_VALUE/LAST_VALUE 渲染取值列")]
    public void FirstLastValue_RendersColumn()
    {
        var first = WindowExpression.FirstValue("Score", "ID", null, "FirstScore");
        var last = WindowExpression.LastValue("Score", "ID", "DeptID", "LastScore");

        Assert.Equal("FIRST_VALUE(Score) OVER(ORDER BY ID) AS FirstScore", first.Render());
        Assert.Equal("LAST_VALUE(Score) OVER(PARTITION BY DeptID ORDER BY ID) AS LastScore", last.Render());
    }

    [Fact]
    [System.ComponentModel.Description("窗口帧偏移量不允许为负数")]
    public void WindowFrame_NegativeOffset_Throws()
    {
        var frame = new WindowFrame
        {
            Start = WindowFrameBoundary.Preceding,
            StartOffset = -1,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => frame.Render());
    }

    [Fact]
    [System.ComponentModel.Description("聚合窗口表达式拒绝排名函数")]
    public void Aggregate_WithRankFunction_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowExpression.Aggregate(WindowFunctionKind.Rank, "Score"));
    }

    [Fact]
    [System.ComponentModel.Description("SelectBuilder 按数据库方言渲染窗口表达式")]
    public void SelectBuilder_SetWindow_RendersWithDialect()
    {
        var db = DbFactory.Create(DatabaseType.SQLite);
        Assert.NotNull(db);

        var builder = new SelectBuilder { Table = "Role", Column = "*" };
        builder.SetWindow(WindowExpression.RowNumber("ID DESC", null, "RowNum"), db!);

        var sql = builder.ToString(db!.Dialect);
        Assert.Contains("ROW_NUMBER() OVER(ORDER BY ID DESC) AS RowNum", sql);
    }

    [Fact]
    [System.ComponentModel.Description("不支持窗口函数的数据库在 SetWindow 时抛出明确异常")]
    public void SelectBuilder_SetWindow_UnsupportedProvider_Throws()
    {
        var db = DbFactory.Create(DatabaseType.MongoDB);
        Assert.NotNull(db);

        var builder = new SelectBuilder { Table = "Role", Column = "*" };
        Assert.Throws<NotSupportedException>(() => builder.SetWindow(WindowExpression.RowNumber("ID DESC"), db!));
    }

    [Fact]
    [System.ComponentModel.Description("统计查询不再携带窗口函数")]
    public void SelectCount_RemovesWindow()
    {
        var db = DbFactory.Create(DatabaseType.SQLite);
        Assert.NotNull(db);

        var builder = new SelectBuilder { Table = "Role", Column = "*" };
        builder.SetWindow(WindowExpression.RowNumber("ID DESC"), db!);

        var count = builder.SelectCount().ToString(db!.Dialect);
        Assert.DoesNotContain("ROW_NUMBER", count);
    }

    [Fact]
    [System.ComponentModel.Description("SQLite 真库执行排名窗口查询")]
    public void SQLite_WindowFunction_Executes()
    {
        var file = Path.Combine(Path.GetTempPath(), $"xcode_window_{Guid.NewGuid():N}.db");
        var connName = $"window_{Guid.NewGuid():N}";
        DAL.AddConnStr(connName, $"Data Source={file}", null, "SQLite");

        try
        {
            var dal = DAL.Create(connName);
            dal.Execute("Create Table Score(Id Integer Primary Key, Name Text, Score Integer)");
            dal.Execute("Insert Into Score(Name, Score) Values('A', 90)");
            dal.Execute("Insert Into Score(Name, Score) Values('B', 95)");
            dal.Execute("Insert Into Score(Name, Score) Values('C', 95)");

            var builder = new SelectBuilder { Table = "Score", Column = "Id, Name, Score", OrderBy = "Score Desc" };
            builder.SetWindow(WindowExpression.Rank("Score Desc", null, "Rank"), dal.Db);

            var list = dal.Query<WindowRow>(builder.ToString(dal.Db.Dialect)).ToList();
            Assert.Equal(3, list.Count);
            Assert.Equal(1, list[0].Rank);
            Assert.Equal(1, list[1].Rank);
            Assert.Equal(3, list[2].Rank);
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    private sealed class WindowRow
    {
        public Int32 Id { get; set; }

        public String? Name { get; set; }

        public Int32 Score { get; set; }

        public Int32 Rank { get; set; }
    }
}
