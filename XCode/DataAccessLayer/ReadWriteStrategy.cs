using System.Diagnostics.CodeAnalysis;
using NewLife;

namespace XCode.DataAccessLayer;

/// <summary>时间区间</summary>
public struct TimeRegion
{
    /// <summary>开始时间</summary>
    public TimeSpan Start;

    /// <summary>结束时间</summary>
    public TimeSpan End;
}

/// <summary>读写分离策略。忽略时间区间和表名</summary>
public class ReadWriteStrategy
{
    /// <summary>要忽略的时间区间</summary>
    public IList<TimeRegion> IgnoreTimes { get; set; } = [];

    /// <summary>要忽略的表名</summary>
    public ICollection<String> IgnoreTables { get; set; } = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

    private Int32 _index;

    /// <summary>设置不走读写分离的时间段，如00:30-00:50，多段区间逗号分开</summary>
    /// <param name="regions"></param>
    public void AddIgnoreTimes(String regions)
    {
        var rs = regions.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var item in rs)
        {
            var ss = item.Split('-');
            if (ss.Length == 2)
            {
                if (TimeSpan.TryParse(ss[0], out var start) &&
                    TimeSpan.TryParse(ss[1], out var end) &&
                    start < end)
                {
                    IgnoreTimes.Add(new TimeRegion { Start = start, End = end });
                }
            }
        }
    }

    /// <summary>检查是否支持读写分离</summary>
    /// <param name="dal"></param>
    /// <param name="sql"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public virtual Boolean Validate(DAL dal, String sql, String action)
    {
        // 事务中不支持分离
        //if (dal.ReadOnly == null) return false;
        if (dal.Session.Transaction != null) return false;

        if (!action.EqualIgnoreCase("Select", "SelectCount", "Query")) return false;
        if (action == "ExecuteScalar" && !sql.TrimStart().StartsWithIgnoreCase("select ")) return false;

        // 判断是否忽略的时间区间
        var span = DateTime.Now - DateTime.Today;
        foreach (var item in IgnoreTimes)
        {
            if (span >= item.Start && span < item.End) return false;
        }

        // 是否忽略的表名
        if (!sql.IsNullOrEmpty() && IgnoreTables != null && IgnoreTables.Count > 0)
        {
            var tables = DAL.GetTables(sql, false);
            foreach (var item in tables)
            {
                if (IgnoreTables.Contains(item)) return false;
            }
        }

        return true;
    }

    /// <summary>尝试获取一个只读库</summary>
    /// <param name="dal"></param>
    /// <param name="sql"></param>
    /// <param name="action"></param>
    /// <param name="readonly"></param>
    /// <returns></returns>
    public virtual Boolean TryGet(DAL dal, String sql, String action, out DAL? @readonly)
    {
        @readonly = null;

        var rs = dal.Reads;
        if (rs == null || rs.Count == 0) return false;

        if (!Validate(dal, sql, action)) return false;

        // 轮询从库
        @readonly = rs[Interlocked.Increment(ref _index) % rs.Count];

        return true;
    }
}