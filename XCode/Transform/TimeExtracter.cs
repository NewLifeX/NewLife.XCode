using NewLife.Data;
using XCode.DataAccessLayer;

namespace XCode.Transform;

/// <summary>时间索引数据抽取器</summary>
/// <remarks>
/// 适用于带有时间索引字段的数据抽取器，速度飞快。
/// </remarks>
public class TimeExtracter : IExtracter<DbTable>
{
    #region 属性
    /// <summary>数据层</summary>
    public DAL Dal { get; set; }

    /// <summary>查询表达式</summary>
    public SelectBuilder Builder { get; set; }

    /// <summary>时间字段</summary>
    public IDataColumn Field { get; set; }

    /// <summary>开始行。分页时表示偏移行数，自增时表示下一个编号，默认0</summary>
    public Int64 Row { get; set; }

    /// <summary>开始抽取时间</summary>
    public DateTime StartTime { get; set; }

    /// <summary>批大小。默认5000</summary>
    public Int32 BatchSize { get; set; } = 5000;

    /// <summary>总行数</summary>
    public Int32 TotalCount { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化时间索引数据抽取器</summary>
    public TimeExtracter() { }

    /// <summary>实例化时间索引数据抽取器</summary>
    /// <param name="dal"></param>
    /// <param name="tableName"></param>
    /// <param name="field"></param>
    public TimeExtracter(DAL dal, String tableName, IDataColumn field)
    {
        Dal = dal;
        Builder = new SelectBuilder { Table = tableName, OrderBy = field.ColumnName + " asc" };
        Field = field;
        BatchSize = dal.GetBatchSize();
    }
    #endregion

    #region 抽取数据
    /// <summary>迭代抽取数据</summary>
    /// <remarks>
    /// 从StartTime开始，分区加分页抽取数据。每次抽取完成后，将StartTime设置为最后一行的时间，然后加上1秒作为下一次的开始时间。
    /// 逼近当前时间时停止，下次再调用Fetch时，将从上次停止的地方继续抽取。
    /// </remarks>
    /// <returns></returns>
    public virtual IEnumerable<DbTable> Fetch()
    {
        var field = Field;
        var db = Dal.Db;
        var name = db.FormatName(field);

        // 第一次查询，不带时间条件，目的是为了找到第一个时间
        if (StartTime.Year < 2000)
        {
            // 查询数据
            var sb = Builder.Clone();
            var dt = Dal.Query(sb, 0, 1);

            // 第一页都没有数据，直接返回
            if (dt == null || dt.Rows == null || dt.Rows.Count <= 0)
                yield break;

            StartTime = dt.Get<DateTime>(0, field.ColumnName);

            if (StartTime.Year < 2000) yield break;
        }

        // 时间步进，分页查询
        var minStep = TimeSpan.FromSeconds(60);
        var maxStep = TimeSpan.FromDays(1);
        var step = minStep;
        while (true)
        {
            var now = DateTime.Now;
            var end = StartTime.Add(step);
            if (end > now) end = now;

            // 按时间分片查询。如果有多页，则分片内使用分页查询
            var sb = Builder.Clone().AppendWhereAnd($"{name}>={db.FormatValue(field, StartTime)} And {name}<{db.FormatValue(field, end)}");

            var startRow = 0;
            while (true)
            {
                // 分页查询数据
                var dt = Dal.Query(sb, startRow, BatchSize);
                if (dt == null || dt.Rows == null) break;

                var count = dt.Rows.Count;
                if (count == 0) break;

                // 返回数据
                yield return dt;

                // 取最后一行的时间，加上1秒作为下一次分片的开始时间。多次赋值，最后一页为准
                StartTime = dt.Get<DateTime>(count - 1, field.ColumnName).AddSeconds(1);

                startRow += count;
                TotalCount += count;
                if (count == BatchSize) break;
            }

            // 踏空，加大步进
            if (startRow == 0 && step < maxStep)
                step += step;

            // 止步于当前时间
            if (StartTime.Add(minStep) >= DateTime.Now) break;
        }
    }
    #endregion
}