using System;
using System.Linq;
using NewLife.Data;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>窗口函数内存回退测试</summary>
public class WindowFallbackTests
{
    private static DbTable CreateTable()
    {
        var table = new DbTable
        {
            Columns = ["Id", "Dept", "Score"],
            Types = [typeof(Int32), typeof(String), typeof(Int32)],
        };

        // 故意打乱行顺序，验证排序
        table.Rows.Add([3, "B", 95]);
        table.Rows.Add([1, "A", 90]);
        table.Rows.Add([4, "A", 95]);
        table.Rows.Add([2, "B", 80]);
        table.Rows.Add([5, "A", 95]);

        return table;
    }

    [Fact]
    [System.ComponentModel.Description("内存回退按排序生成 ROW_NUMBER")]
    public void RowNumber_InMemory()
    {
        var table = CreateTable();
        var expression = WindowExpression.RowNumber("Score DESC", null, "RowNum");

        WindowEvaluator.Apply(expression, table);

        var idx = table.GetColumn("RowNum");
        Assert.True(idx >= 0);

        // 结果集行序为 Id 3/1/4/2/5；按 Score 倒序后依次是 Id3、Id4、Id5、Id1、Id2
        Assert.Equal("1,4,2,5,3", String.Join(",", table.Rows.Select(e => (Int32)e[idx]!).ToArray()));
    }

    [Fact]
    [System.ComponentModel.Description("内存回退按分区生成 ROW_NUMBER")]
    public void RowNumber_WithPartition()
    {
        var table = CreateTable();
        var expression = WindowExpression.RowNumber("Score DESC", "Dept", "RowNum");

        WindowEvaluator.Apply(expression, table);

        var idx = table.GetColumn("RowNum");

        // A 分区：Id 4(95)、5(95)、1(90)；B 分区：Id 3(95)、2(80)
        Assert.Equal(1, table.Rows.First(e => (Int32)e[0]! == 4)[idx]);
        Assert.Equal(2, table.Rows.First(e => (Int32)e[0]! == 5)[idx]);
        Assert.Equal(3, table.Rows.First(e => (Int32)e[0]! == 1)[idx]);
        Assert.Equal(1, table.Rows.First(e => (Int32)e[0]! == 3)[idx]);
        Assert.Equal(2, table.Rows.First(e => (Int32)e[0]! == 2)[idx]);
    }

    [Fact]
    [System.ComponentModel.Description("内存回退的 RANK 并列后跳号，DENSE_RANK 不跳号")]
    public void Rank_InMemory()
    {
        var table = CreateTable();
        var rank = WindowExpression.Rank("Score DESC", null, "Rank");
        var dense = WindowExpression.DenseRank("Score DESC", null, "DenseRank");

        WindowEvaluator.Apply(rank, table);
        WindowEvaluator.Apply(dense, table);

        var ri = table.GetColumn("Rank");
        var di = table.GetColumn("DenseRank");
        var score95 = table.Rows.Where(e => (Int32)e[2]! == 95).ToArray();
        var score90 = table.Rows.First(e => (Int32)e[2]! == 90);

        Assert.All(score95, e => Assert.Equal(1, e[ri]));
        Assert.All(score95, e => Assert.Equal(1, e[di]));
        Assert.Equal(4, score90[ri]);
        Assert.Equal(2, score90[di]);
    }

    [Fact]
    [System.ComponentModel.Description("内存回退的 ROWS 累计聚合")]
    public void Sum_WithRowsFrame()
    {
        var table = CreateTable();
        var expression = WindowExpression.Aggregate(
            WindowFunctionKind.Sum,
            "Score",
            null,
            "Id",
            new WindowFrame
            {
                IsRows = true,
                Start = WindowFrameBoundary.UnboundedPreceding,
                End = WindowFrameBoundary.CurrentRow,
            },
            "Running");

        WindowEvaluator.Apply(expression, table);

        var idx = table.GetColumn("Running");
        var rows = table.Rows.OrderBy(e => (Int32)e[0]!).ToArray();

        Assert.Equal(90, Convert.ToInt32(rows[0][idx]));
        Assert.Equal(170, Convert.ToInt32(rows[1][idx]));
        Assert.Equal(265, Convert.ToInt32(rows[2][idx]));
        Assert.Equal(360, Convert.ToInt32(rows[3][idx]));
        Assert.Equal(455, Convert.ToInt32(rows[4][idx]));
    }

    [Fact]
    [System.ComponentModel.Description("内存回退的 LAG/LEAD 取相邻值")]
    public void LagLead_InMemory()
    {
        var table = CreateTable();
        var lag = WindowExpression.Lag("Score", 1, null, "Id", null, "Prev");
        var lead = WindowExpression.Lead("Score", 1, null, "Id", null, "Next");

        WindowEvaluator.Apply(lag, table);
        WindowEvaluator.Apply(lead, table);

        var li = table.GetColumn("Prev");
        var ni = table.GetColumn("Next");

        var first = table.Rows.First(e => (Int32)e[0]! == 1);
        var last = table.Rows.First(e => (Int32)e[0]! == 5);

        Assert.Null(first[li]);
        Assert.Equal(80, first[ni]);
        Assert.Equal(95, last[li]);
        Assert.Null(last[ni]);
    }

    [Fact]
    [System.ComponentModel.Description("内存回退的 NTILE 分桶")]
    public void Ntile_InMemory()
    {
        var table = CreateTable();
        var expression = WindowExpression.Ntile(2, "Id", null, "Bucket");

        WindowEvaluator.Apply(expression, table);

        var idx = table.GetColumn("Bucket");
        var buckets = table.Rows.OrderBy(e => (Int32)e[0]!).Select(e => (Int32)e[idx]!).ToArray();

        Assert.Equal("1,1,1,2,2", String.Join(",", buckets));
    }

    [Fact]
    [System.ComponentModel.Description("窗口列缺失时明确报错，提示把列加入查询字段")]
    public void MissingColumn_Throws()
    {
        var table = CreateTable();
        var expression = WindowExpression.RowNumber("Missing DESC", null, "RowNum");

        var ex = Assert.Throws<NotSupportedException>(() => WindowEvaluator.Apply(expression, table));
        Assert.Contains("Missing", ex.Message);
    }

    [Fact]
    [System.ComponentModel.Description("不支持窗口函数的数据库可显式开启内存回退")]
    public void SetWindow_WithInMemoryFallback()
    {
        var db = DbFactory.Create(DatabaseType.MongoDB);
        Assert.NotNull(db);

        var builder = new SelectBuilder { Table = "Role", Column = "Id, Score" };
        var expression = WindowExpression.RowNumber("Score DESC", null, "RowNum");

        // 默认策略仍然明确抛出
        Assert.Throws<NotSupportedException>(() => builder.SetWindow(expression, db!));

        // 显式降级后通过
        builder.SetWindow(expression, db!, WindowFallback.InMemory);
        var sql = builder.BuildSql(db, out var fallback);

        Assert.DoesNotContain("ROW_NUMBER", sql);
        Assert.Same(expression, fallback);
    }

    [Fact]
    [System.ComponentModel.Description("支持窗口函数的数据库不使用内存回退")]
    public void BuildSql_SupportedProvider_NoFallback()
    {
        var db = DbFactory.Create(DatabaseType.SQLite);
        Assert.NotNull(db);

        var builder = new SelectBuilder { Table = "Role", Column = "Id, Score" };
        builder.SetWindow(WindowExpression.RowNumber("Score DESC", null, "RowNum"), db!, WindowFallback.InMemory);

        var sql = builder.BuildSql(db, out var fallback);

        Assert.Contains("ROW_NUMBER", sql);
        Assert.Null(fallback);
    }
}
