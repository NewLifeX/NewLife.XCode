using System;
using System.Collections.Generic;
using NewLife.Model;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>DbSession.BuildUpdateSql 方法的纯逻辑单元测试（仅测试 SQL 构造，不触发数据库连接）</summary>
public class DbSession_BuildUpdateSqlTests
{
    private const String ConnName = "_unit_test_build_update_sql";
    private static readonly IDbSession _session;

    static DbSession_BuildUpdateSqlTests()
    {
        DAL.AddConnStr(ConnName, "Data Source=:memory:", null, "SQLite");
        _session = DAL.Create(ConnName).Session;
    }

    /// <summary>构建测试表，列类型均为 Int32 以简化测试</summary>
    /// <param name="tableName">表名</param>
    /// <param name="cols">列定义：(名称, 是否主键, 是否自增)</param>
    /// <returns></returns>
    private static IDataTable BuildTable(String tableName, params (String name, Boolean pk, Boolean identity)[] cols)
    {
        var table = ObjectContainer.Current.Resolve<IDataTable>();
        table.Name = tableName;
        table.TableName = tableName;
        foreach (var (name, pk, identity) in cols)
        {
            var dc = table.CreateColumn();
            dc.Name = name;
            dc.ColumnName = name;
            dc.DataType = typeof(Int32);
            dc.PrimaryKey = pk;
            dc.Identity = identity;
            table.Columns.Add(dc);
        }
        return table;
    }

    /// <summary>返回默认测试表：Id(PK+Identity)、Amount、Score、Name</summary>
    private static IDataTable DefaultTable() => BuildTable("StockItem",
        ("Id", true, true),
        ("Amount", false, false),
        ("Score", false, false),
        ("Name", false, false));

    #region 边界：两个集合均为空

    [Fact]
    [System.ComponentModel.Description("updateColumns 和 addColumns 均为 null 时返回 null，且 ps 为空")]
    public void BothCollectionsNull_ReturnsNull()
    {
        var table = DefaultTable();
        var ps = new List<String>();

        var sql = _session.BuildUpdateSql(table, [.. table.Columns], null, null, ps);

        Assert.Null(sql);
        Assert.Empty(ps);
    }

    [Fact]
    [System.ComponentModel.Description("updateColumns 和 addColumns 均为空集合时返回 null，且 ps 为空")]
    public void BothCollectionsEmpty_ReturnsNull()
    {
        var table = DefaultTable();
        var ps = new List<String>();

        var sql = _session.BuildUpdateSql(table, [.. table.Columns], [], [], ps);

        Assert.Null(sql);
        Assert.Empty(ps);
    }

    #endregion

    #region 边界：缺少主键或列名不匹配

    [Fact]
    [System.ComponentModel.Description("columns 中无主键时抛出 InvalidOperationException")]
    public void NoPrimaryKey_ThrowsInvalidOperationException()
    {
        var table = DefaultTable();
        // 仅传入非主键列
        IDataColumn[] noPkCols = [.. table.Columns.FindAll(c => !c.PrimaryKey)];
        var ps = new List<String>();

        Assert.Throws<InvalidOperationException>(() =>
            _session.BuildUpdateSql(table, noPkCols, ["Amount"], null, ps));
    }

    [Fact]
    [System.ComponentModel.Description("传入的列名与 columns 均不匹配时返回 null")]
    public void UnmatchedColumnNames_ReturnsNull()
    {
        var table = DefaultTable();
        var ps = new List<String>();

        var sql = _session.BuildUpdateSql(table, [.. table.Columns], ["NotExist"], null, ps);

        Assert.Null(sql);
    }

    #endregion

    #region 正常路径：单类型集合

    [Fact]
    [System.ComponentModel.Description("仅 updateColumns 生成标准赋值 SET 子句")]
    public void UpdateColumnsOnly_GeneratesCorrectSql()
    {
        var table = DefaultTable();
        var ps = new List<String>();

        var sql = _session.BuildUpdateSql(table, [.. table.Columns], ["Amount", "Name"], null, ps);

        Assert.Equal("Update StockItem Set Amount=@Amount,Name=@Name Where Id=@Id", sql);
        Assert.Contains("Amount", ps);
        Assert.Contains("Name", ps);
        Assert.Contains("Id", ps);
        Assert.DoesNotContain("Score", ps);
    }

    [Fact]
    [System.ComponentModel.Description("仅 addColumns 生成累加 SET 子句（字段=字段+@参数 形式）")]
    public void AddColumnsOnly_GeneratesIncrementalSql()
    {
        var table = DefaultTable();
        var ps = new List<String>();

        var sql = _session.BuildUpdateSql(table, [.. table.Columns], null, ["Score"], ps);

        Assert.Equal("Update StockItem Set Score=Score+@Score Where Id=@Id", sql);
        Assert.Contains("Score", ps);
        Assert.Contains("Id", ps);
        Assert.DoesNotContain("Amount", ps);
    }

    #endregion

    #region 正常路径：混合集合

    [Fact]
    [System.ComponentModel.Description("updateColumns 与 addColumns 混合时生成正确 SQL，addColumns 优先于 updateColumns")]
    public void MixedColumns_GeneratesCombinedSql()
    {
        var table = DefaultTable();
        var ps = new List<String>();

        // Amount 在 updateColumns，Score 在 addColumns
        var sql = _session.BuildUpdateSql(table, [.. table.Columns], ["Amount"], ["Score"], ps);

        Assert.Equal("Update StockItem Set Amount=@Amount,Score=Score+@Score Where Id=@Id", sql);
        Assert.Contains("Amount", ps);
        Assert.Contains("Score", ps);
        Assert.Contains("Id", ps);
    }

    #endregion

    #region ps 参数集合填充验证

    [Fact]
    [System.ComponentModel.Description("ps 集合精确包含 SET 字段与 WHERE 主键字段，无多余项")]
    public void PsCollectionFilledCorrectly()
    {
        var table = DefaultTable();
        var ps = new List<String>();

        _session.BuildUpdateSql(table, [.. table.Columns], ["Amount", "Score"], null, ps);

        // SET 部分：Amount、Score；WHERE 部分：Id —— 共 3 项
        Assert.Equal(3, ps.Count);
        Assert.Contains("Amount", ps);
        Assert.Contains("Score", ps);
        Assert.Contains("Id", ps);
        Assert.DoesNotContain("Name", ps);
    }

    #endregion

    #region 多主键

    [Fact]
    [System.ComponentModel.Description("多主键时 WHERE 子句用 And 连接所有主键字段")]
    public void MultiplePrimaryKeys_WhereClauseContainsAllKeys()
    {
        var table = BuildTable("Composite",
            ("Key1", true, false),
            ("Key2", true, false),
            ("Amount", false, false));
        var ps = new List<String>();

        var sql = _session.BuildUpdateSql(table, [.. table.Columns], ["Amount"], null, ps);

        Assert.Equal("Update Composite Set Amount=@Amount Where Key1=@Key1 And Key2=@Key2", sql);
        Assert.Contains("Key1", ps);
        Assert.Contains("Key2", ps);
        Assert.Contains("Amount", ps);
    }

    #endregion

    #region Identity 列过滤

    [Fact]
    [System.ComponentModel.Description("Identity 列即使在 updateColumns 中也不出现在 SET 子句")]
    public void IdentityColumn_NotIncludedInSetClause()
    {
        var table = DefaultTable(); // Id 是 PrimaryKey + Identity
        var ps = new List<String>();

        // 显式把 Id 放入 updateColumns，应被过滤
        var sql = _session.BuildUpdateSql(table, [.. table.Columns], ["Id", "Amount"], null, ps);

        Assert.NotNull(sql);
        Assert.DoesNotContain("Set Id=", sql);
        Assert.Contains("Amount=@Amount", sql);
        Assert.Contains("Where Id=@Id", sql);
    }

    #endregion
}
