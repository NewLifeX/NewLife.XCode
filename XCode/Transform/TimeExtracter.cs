using System;
using System.Collections.Generic;
using NewLife;
using NewLife.Data;
using XCode.DataAccessLayer;

namespace XCode.Transform
{
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
        /// <summary>实例化自增抽取器</summary>
        public TimeExtracter() { }

        /// <summary>实例化自增抽取器</summary>
        /// <param name="dal"></param>
        /// <param name="tableName"></param>
        /// <param name="field"></param>
        public TimeExtracter(DAL dal, String tableName, IDataColumn field)
        {
            Dal = dal;
            Builder = new SelectBuilder { Table = tableName, OrderBy = field + " asc" };
            Field = field;
            BatchSize = dal.Db.BatchSize;
        }
        #endregion

        #region 抽取数据
        /// <summary>迭代抽取数据</summary>
        /// <returns></returns>
        public virtual IEnumerable<DbTable> Fetch()
        {
            var field = Field;
            var db = Dal.Db;
            var name = db.FormatName(field);
            while (true)
            {
                // 分割数据页
                var sb = Builder.Clone();
                if (!sb.Where.IsNullOrEmpty()) sb.Where += " And ";
                sb.Where += $"{name}.{db.FormatValue(field, StartTime)}";

                // 查询数据
                var dt = Dal.Query(sb, 0, BatchSize);
                if (dt == null) break;

                var count = dt.Rows.Count;
                if (count == 0) break;

                // 返回数据
                yield return dt;

                // 分割时，取最后一行
                StartTime = dt.Get<DateTime>(count - 1, field.ColumnName);

                // 如果满一页，则再查一次该时间，避免该时间的数据同时落入多页
                if (count == BatchSize)
                {
                    sb = Builder.Clone();
                    if (!sb.Where.IsNullOrEmpty()) sb.Where += " And ";
                    sb.Where += $"{name}>={db.FormatValue(field, StartTime)} And {name}<{db.FormatValue(field, StartTime.AddSeconds(1))}";

                    // 查询数据，该时间点数据也可能有多页
                    var startRow = 0;
                    while (true)
                    {
                        dt = Dal.Query(sb, startRow, BatchSize);
                        if (dt == null) break;

                        var count2 = dt.Rows.Count;
                        if (count2 == 0) break;

                        yield return dt;

                        startRow += count2;
                        count += count2;
                        if (count2 < BatchSize) break;
                    }
                }

                // 下一页
                TotalCount += count;
                if (count < BatchSize) break;
            }
        }
        #endregion
    }
}