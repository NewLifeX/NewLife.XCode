using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.DataAccessLayer;
using XCode.Transform;
using Xunit;

namespace XUnitTest.XCode.Transform;

/// <summary>抽取器测试实体</summary>
[Serializable]
[BindTable("ExtractTestEntity", Description = "抽取器测试", ConnName = "ExtractTest", DbType = DatabaseType.SQLite)]
public class ExtractTestEntity : Entity<ExtractTestEntity>
{
    #region 属性
    private Int64 _ID;
    /// <summary>编号</summary>
    [DataObjectField(true, true, false, 0)]
    [BindColumn("ID", "编号", "")]
    public Int64 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private DateTime _LogTime;
    /// <summary>日志时间</summary>
    [DataObjectField(false, false, false, 0)]
    [BindColumn("LogTime", "日志时间", "")]
    public DateTime LogTime { get => _LogTime; set { if (OnPropertyChanging("LogTime", value)) { _LogTime = value; OnPropertyChanged("LogTime"); } } }
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object this[String name]
    {
        get => name switch
        {
            "ID" => _ID,
            "Name" => _Name,
            "LogTime" => _LogTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "ID": _ID = value.ToLong(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "LogTime": _LogTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion
}

/// <summary>ETL 数据抽取器测试（M7-1）。SQLite 临时库验证主键步进、分页、时间分片三种抽取算法。</summary>
[Collection("Database")]
public class ExtracterTests
{
    private const String _connName = "ExtractTest";
    private const String _table = "ExtractTestEntity";

    static ExtracterTests()
    {
        var dbFile = Path.Combine(Path.GetTempPath(), $"ExtractTest_{Guid.NewGuid():n}.db");
        DAL.AddConnStr(_connName, $"Data Source={dbFile}", null, "SQLite");
        DAL.Create(_connName).Execute($"CREATE TABLE IF NOT EXISTS {_table} (ID INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT, LogTime DATETIME)");
    }

    private static DAL Dal => DAL.Create(_connName);

    private static IDataColumn IdColumn => ExtractTestEntity.Meta.Table.DataTable.Columns.First(e => e.Name == "ID");

    private static IDataColumn TimeColumn => ExtractTestEntity.Meta.Table.DataTable.Columns.First(e => e.Name == "LogTime");

    private static void ResetRows(params DateTime[] times)
    {
        var dal = Dal;
        dal.Execute($"Delete From {_table}");
        dal.Execute($"Delete From sqlite_sequence Where name='{_table}'");
        for (var i = 0; i < times.Length; i++)
        {
            dal.Execute($"Insert Into {_table} (Name, LogTime) Values ('n{i + 1}', '{times[i].ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}')");
        }
    }

    private static DateTime[] RecentFlags(Int32 count, Int32 startSeconds)
    {
        var now = DateTime.Now;
        var list = new List<DateTime>();
        for (var i = 0; i < count; i++) list.Add(now.AddSeconds(-(startSeconds - i * 30)));
        return list.ToArray();
    }

    private static Int64[] IdsOf(DbTable dt)
    {
        var list = new List<Int64>();
        for (var i = 0; i < dt.Rows.Count; i++) list.Add(dt.Get<Int64>(i, "ID"));
        return list.ToArray();
    }

    [Fact(DisplayName = "主键步进抽取：分批取完且可断点续抽")]
    public void IdExtracter_Fetch()
    {
        ResetRows(RecentFlags(5, 180));

        var ex = new IdExtracter(Dal, _table, IdColumn) { BatchSize = 2 };
        var batches = ex.Fetch().ToList();

        Assert.Equal(3, batches.Count);
        Assert.Equal(5, batches.Sum(b => b.Rows.Count));
        Assert.Equal(5, ex.TotalCount);
        Assert.Equal(new[] { 1L, 2L }, IdsOf(batches[0]));
        Assert.Equal(5L, ex.Row);

        // 无新增数据时续抽为空
        Assert.Empty(ex.Fetch().ToList());

        // 指定起点续抽
        var ex2 = new IdExtracter(Dal, _table, IdColumn) { BatchSize = 5, Row = 3 };
        var batches2 = ex2.Fetch().ToList();
        Assert.Single(batches2);
        Assert.Equal(new[] { 4L, 5L }, IdsOf(batches2[0]));
    }

    [Fact(DisplayName = "分页抽取：按页取完且可指定偏移续抽")]
    public void PagingExtracter_Fetch()
    {
        ResetRows(RecentFlags(5, 180));

        var ex = new PagingExtracter(Dal, _table, "ID asc") { BatchSize = 2 };
        var batches = ex.Fetch().ToList();

        Assert.Equal(3, batches.Count);
        Assert.Equal(5, batches.Sum(b => b.Rows.Count));
        Assert.Equal(new[] { 1L, 2L, 3L, 4L, 5L }, batches.SelectMany(IdsOf).ToArray());

        // 从偏移4继续
        var ex2 = new PagingExtracter(Dal, _table, "ID asc") { BatchSize = 2, Row = 4 };
        var batches2 = ex2.Fetch().ToList();
        Assert.Single(batches2);
        Assert.Equal(new[] { 5L }, IdsOf(batches2[0]));
    }

    [Fact(DisplayName = "时间分片抽取：显式起点分批取完")]
    public void TimeExtracter_Fetch_WithStartTime()
    {
        var times = RecentFlags(5, 180);
        ResetRows(times);

        var ex = new TimeExtracter(Dal, _table, TimeColumn) { BatchSize = 2, StartTime = times[0].AddSeconds(-1) };
        var batches = ex.Fetch().ToList();

        Assert.Equal(5, ex.TotalCount);
        Assert.Equal(5, batches.Sum(b => b.Rows.Count));
        Assert.Equal(new[] { 1L, 2L, 3L, 4L, 5L }, batches.SelectMany(IdsOf).OrderBy(e => e).ToArray());
    }

    [Fact(DisplayName = "时间分片抽取：未指定起点时自动从首行时间开始")]
    public void TimeExtracter_Fetch_AutoStartTime()
    {
        ResetRows(RecentFlags(3, 120));

        var ex = new TimeExtracter(Dal, _table, TimeColumn) { BatchSize = 10 };
        var batches = ex.Fetch().ToList();

        Assert.Equal(3, ex.TotalCount);
        Assert.Equal(3, batches.Sum(b => b.Rows.Count));

        // 起点已被推进到首行时间之后
        Assert.True(ex.StartTime > DateTime.Now.AddMinutes(-3));
    }

    [Fact(DisplayName = "时间分片抽取：历史数据（尾部间隔超过1分钟）也能抽取完毕并终止", Timeout = 60000)]
    public async Task TimeExtracter_Fetch_HistoricalDataTerminates()
    {
        var now = DateTime.Now;
        ResetRows(now.AddHours(-3), now.AddHours(-3).AddSeconds(30), now.AddHours(-2.5));

        var ex = new TimeExtracter(Dal, _table, TimeColumn) { BatchSize = 10 };
        var batches = await Task.Run(() => ex.Fetch().ToList());

        Assert.Equal(3, ex.TotalCount);
        Assert.Equal(new[] { 1L, 2L, 3L }, batches.SelectMany(IdsOf).OrderBy(e => e).ToArray());
    }

    [Fact(DisplayName = "实体主键抽取：分批取完且不重复")]
    public void EntityIdExtracter_Fetch()
    {
        ResetRows(RecentFlags(5, 180));

        var field = ExtractTestEntity.Meta.Table.Fields.First(e => e.Name == "ID");
        var ex = new EntityIdExtracter(ExtractTestEntity.Meta.Factory, field) { BatchSize = 2 };
        var batches = ex.Fetch().ToList();

        Assert.Equal(3, batches.Count);
        var ids = batches.SelectMany(b => b.Select(e => (e as ExtractTestEntity).ID)).ToArray();
        Assert.Equal(new[] { 1L, 2L, 3L, 4L, 5L }, ids);

        // 无新增数据时续抽为空
        Assert.Empty(ex.Fetch().ToList());
    }

    [Fact(DisplayName = "空表抽取：各抽取器立即结束不卡死", Timeout = 30000)]
    public async Task EmptyTable_ReturnsImmediately()
    {
        ResetRows();

        await Task.Run(() =>
        {
            Assert.Empty(new IdExtracter(Dal, _table, IdColumn).Fetch().ToList());
            Assert.Empty(new PagingExtracter(Dal, _table, "ID asc").Fetch().ToList());
            Assert.Empty(new TimeExtracter(Dal, _table, TimeColumn).Fetch().ToList());
            Assert.Empty(new EntityIdExtracter(ExtractTestEntity.Meta.Factory, ExtractTestEntity.Meta.Table.Fields.First(e => e.Name == "ID")).Fetch().ToList());
        });
    }
}
