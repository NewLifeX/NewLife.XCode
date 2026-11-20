using System;
using System.IO;
using System.Linq;
using NewLife;
using NewLife.NovaDb.Client;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>窗口函数内存回退的端到端测试。使用不声明窗口函数的 NovaDb 嵌入库</summary>
[Collection("Database")]
public class WindowFallbackIntegrationTests
{
    private const String ConnName = "novaEmbed_window";
    private const String Table = "xw_score";

    private static DAL Prepare()
    {
        var dataDir = "Data\\nova_windowfallback";
        var fullDir = dataDir.GetFullPath();
        NovaConnection.DisposeSharedEngines();
        if (Directory.Exists(fullDir)) Directory.Delete(fullDir, true);

        DAL.AddConnStr(ConnName, $"Data Source={dataDir}", null, "NovaDb");
        var dal = DAL.Create(ConnName);

        var table = DAL.CreateTable();
        table.TableName = Table;

        var id = table.CreateColumn();
        id.Name = "Id";
        id.ColumnName = "Id";
        id.DataType = typeof(Int32);
        id.PrimaryKey = true;
        table.Columns.Add(id);

        var name = table.CreateColumn();
        name.Name = "Name";
        name.ColumnName = "Name";
        name.DataType = typeof(String);
        name.Length = 50;
        table.Columns.Add(name);

        var score = table.CreateColumn();
        score.Name = "Score";
        score.ColumnName = "Score";
        score.DataType = typeof(Int32);
        table.Columns.Add(score);

        dal.Db.CreateMetaData().CreateTable(table);

        dal.Execute($"insert into {Table} (Id, Name, Score) values (1, 'A', 90)");
        dal.Execute($"insert into {Table} (Id, Name, Score) values (2, 'B', 95)");
        dal.Execute($"insert into {Table} (Id, Name, Score) values (3, 'C', 95)");
        dal.Execute($"insert into {Table} (Id, Name, Score) values (4, 'D', 80)");

        return dal;
    }

    private static SelectBuilder CreateBuilder()
    {
        var builder = new SelectBuilder { Table = Table, Column = "Id, Name, Score", OrderBy = "Score Desc" };
        builder.SetWindow(WindowExpression.Rank("Score Desc", null, "Rank"), DAL.Create(ConnName).Db, WindowFallback.InMemory);

        return builder;
    }

    [Fact]
    [System.ComponentModel.Description("NovaDb 不声明窗口函数，DAL 查询走内存回退并返回排名列")]
    public void Query_InMemoryRank()
    {
        var dal = Prepare();
        Assert.False(dal.Db.Supports(DatabaseCapability.WindowFunctions));

        var builder = CreateBuilder();
        var table = dal.Query(builder, 0, 0);

        var idx = table.GetColumn("Rank");
        Assert.True(idx >= 0, "内存回退应补充 Rank 列");

        var rows = table.Rows.OrderBy(e => e[0].ToInt()).ToArray();
        Assert.Equal(4, rows.Length);
        Assert.Equal(3, rows[0][idx].ToInt());
        Assert.Equal(1, rows[1][idx].ToInt());
        Assert.Equal(1, rows[2][idx].ToInt());
        Assert.Equal(4, rows[3][idx].ToInt());

        // 执行的 SQL 不应包含窗口函数
        var sql = "";
        DAL.LocalFilter = s => sql = s;
        try
        {
            dal.Query(CreateBuilder(), 0, 0);
        }
        finally
        {
            DAL.LocalFilter = null;
        }
        Assert.DoesNotContain("RANK", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [System.ComponentModel.Description("DAL.Select 返回的数据集同样应用内存回退")]
    public void Select_InMemoryRank()
    {
        var dal = Prepare();

        var ds = dal.Select(CreateBuilder(), 0, 0);

        Assert.Single(ds.Tables);
        var dt = ds.Tables[0];
        Assert.True(dt.Columns.Contains("Rank"));

        var ranks = dt.Rows.Cast<System.Data.DataRow>().Select(e => e["Rank"].ToInt()).OrderBy(e => e).ToArray();
        Assert.Equal("1,1,3,4", String.Join(",", ranks));
    }

    [Fact]
    [System.ComponentModel.Description("异步查询同样应用内存回退")]
    public void QueryAsync_InMemoryRank()
    {
        var dal = Prepare();

        var table = dal.QueryAsync(CreateBuilder(), 0, 0).GetAwaiter().GetResult();

        var idx = table.GetColumn("Rank");
        Assert.True(idx >= 0);

        var ranks = table.Rows.Select(e => e[idx].ToInt()).OrderBy(e => e).ToArray();
        Assert.Equal("1,1,3,4", String.Join(",", ranks));
    }

    [Fact]
    [System.ComponentModel.Description("默认策略下不支持的数据库仍然明确抛错")]
    public void Query_WithoutFallback_Throws()
    {
        var dal = Prepare();

        var builder = new SelectBuilder { Table = Table, Column = "Id, Name, Score" };
        var expression = WindowExpression.RowNumber("Score Desc", null, "RowNum");
        builder.WindowExpression = expression;

        Assert.Throws<NotSupportedException>(() => dal.Query(builder, 0, 0));
    }
}
