using System.Text;
using NewLife;

namespace XCode.DataAccessLayer;

/// <summary>窗口函数类型</summary>
public enum WindowFunctionKind
{
    /// <summary>行号</summary>
    RowNumber,

    /// <summary>排名，并列时跳过后续名次</summary>
    Rank,

    /// <summary>密集排名，并列时不跳过名次</summary>
    DenseRank,

    /// <summary>分桶</summary>
    Ntile,

    /// <summary>前一行</summary>
    Lag,

    /// <summary>后一行</summary>
    Lead,

    /// <summary>首值</summary>
    FirstValue,

    /// <summary>末值</summary>
    LastValue,

    /// <summary>求和</summary>
    Sum,

    /// <summary>平均</summary>
    Avg,

    /// <summary>计数</summary>
    Count,

    /// <summary>最小值</summary>
    Min,

    /// <summary>最大值</summary>
    Max,
}

/// <summary>窗口帧边界</summary>
public enum WindowFrameBoundary
{
    /// <summary>无界前</summary>
    UnboundedPreceding,

    /// <summary>前 N 行/值</summary>
    Preceding,

    /// <summary>当前行</summary>
    CurrentRow,

    /// <summary>后 N 行/值</summary>
    Following,

    /// <summary>无界后</summary>
    UnboundedFollowing,
}

/// <summary>窗口帧。ROWS/RANGE BETWEEN ... AND ...</summary>
public sealed class WindowFrame
{
    /// <summary>是否使用 ROWS。false 表示 RANGE</summary>
    public Boolean IsRows { get; set; } = true;

    /// <summary>起始边界</summary>
    public WindowFrameBoundary Start { get; set; } = WindowFrameBoundary.UnboundedPreceding;

    /// <summary>起始偏移</summary>
    public Int32 StartOffset { get; set; }

    /// <summary>结束边界</summary>
    public WindowFrameBoundary End { get; set; } = WindowFrameBoundary.CurrentRow;

    /// <summary>结束偏移</summary>
    public Int32 EndOffset { get; set; }

    /// <summary>渲染窗口帧</summary>
    /// <returns>ROWS/RANGE BETWEEN ... AND ...</returns>
    public String Render()
    {
        var sb = new StringBuilder();
        sb.Append(IsRows ? "ROWS" : "RANGE");
        sb.Append(" BETWEEN ");
        sb.Append(RenderBoundary(Start, StartOffset));
        sb.Append(" AND ");
        sb.Append(RenderBoundary(End, EndOffset));
        return sb.ToString();
    }

    private static String RenderBoundary(WindowFrameBoundary boundary, Int32 offset)
    {
        if ((boundary is WindowFrameBoundary.Preceding or WindowFrameBoundary.Following) && offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "窗口帧偏移量不能为负数");

        return boundary switch
        {
            WindowFrameBoundary.UnboundedPreceding => "UNBOUNDED PRECEDING",
            WindowFrameBoundary.Preceding => $"{offset} PRECEDING",
            WindowFrameBoundary.CurrentRow => "CURRENT ROW",
            WindowFrameBoundary.Following => $"{offset} FOLLOWING",
            WindowFrameBoundary.UnboundedFollowing => "UNBOUNDED FOLLOWING",
            _ => "CURRENT ROW",
        };
    }
}

/// <summary>窗口规格。PARTITION BY / ORDER BY / 窗口帧</summary>
public sealed class WindowSpec
{
    /// <summary>分区字段</summary>
    public String? PartitionBy { get; set; }

    /// <summary>排序字段</summary>
    public String? OrderBy { get; set; }

    /// <summary>窗口帧</summary>
    public WindowFrame? Frame { get; set; }

    /// <summary>渲染窗口规格内容</summary>
    /// <returns>PARTITION BY ... ORDER BY ... FRAME</returns>
    public String Render()
    {
        var sb = new StringBuilder();
        if (!PartitionBy.IsNullOrEmpty())
        {
            sb.Append("PARTITION BY ");
            sb.Append(PartitionBy);
        }
        if (!OrderBy.IsNullOrEmpty())
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append("ORDER BY ");
            sb.Append(OrderBy);
        }
        if (Frame != null)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(Frame.Render());
        }
        return sb.ToString();
    }
}

/// <summary>窗口函数表达式。函数 + 参数 + 窗口规格 + 别名</summary>
public sealed class WindowExpression
{
    /// <summary>函数类型</summary>
    public WindowFunctionKind Function { get; set; }

    /// <summary>函数参数，例如列名或排序字段</summary>
    public String? Arguments { get; set; }

    /// <summary>窗口规格</summary>
    public WindowSpec? Spec { get; set; }

    /// <summary>别名</summary>
    public String? Alias { get; set; }

    /// <summary>渲染标准 SQL 窗口表达式</summary>
    /// <returns>FUNC(...) OVER(...) AS alias</returns>
    public String Render()
    {
        var sb = new StringBuilder();
        sb.Append(GetFunctionName(Function));
        sb.Append('(');
        if (!Arguments.IsNullOrEmpty()) sb.Append(Arguments);
        sb.Append(") OVER(");
        if (Spec != null) sb.Append(Spec.Render());
        sb.Append(')');
        if (!Alias.IsNullOrEmpty())
        {
            sb.Append(" AS ");
            sb.Append(Alias);
        }
        return sb.ToString();
    }

    /// <summary>创建 ROW_NUMBER 表达式</summary>
    /// <param name="orderBy">排序字段</param>
    /// <param name="partitionBy">分区字段</param>
    /// <param name="alias">别名</param>
    /// <returns>窗口表达式</returns>
    public static WindowExpression RowNumber(String? orderBy = null, String? partitionBy = null, String? alias = "RowNum") => new()
    {
        Function = WindowFunctionKind.RowNumber,
        Spec = new WindowSpec { OrderBy = orderBy, PartitionBy = partitionBy },
        Alias = alias,
    };

    /// <summary>创建 RANK 表达式</summary>
    /// <param name="orderBy">排序字段</param>
    /// <param name="partitionBy">分区字段</param>
    /// <param name="alias">别名</param>
    /// <returns>窗口表达式</returns>
    public static WindowExpression Rank(String orderBy, String? partitionBy = null, String? alias = "Rank")
    {
        if (orderBy.IsNullOrEmpty()) throw new ArgumentNullException(nameof(orderBy), "RANK 必须指定排序字段");

        return new()
        {
            Function = WindowFunctionKind.Rank,
            Spec = new WindowSpec { OrderBy = orderBy, PartitionBy = partitionBy },
            Alias = alias,
        };
    }

    /// <summary>创建 DENSE_RANK 表达式</summary>
    /// <param name="orderBy">排序字段</param>
    /// <param name="partitionBy">分区字段</param>
    /// <param name="alias">别名</param>
    /// <returns>窗口表达式</returns>
    public static WindowExpression DenseRank(String orderBy, String? partitionBy = null, String? alias = "DenseRank")
    {
        if (orderBy.IsNullOrEmpty()) throw new ArgumentNullException(nameof(orderBy), "DENSE_RANK 必须指定排序字段");

        return new()
        {
            Function = WindowFunctionKind.DenseRank,
            Spec = new WindowSpec { OrderBy = orderBy, PartitionBy = partitionBy },
            Alias = alias,
        };
    }

    /// <summary>创建聚合窗口表达式</summary>
    /// <param name="function">聚合函数</param>
    /// <param name="column">聚合列</param>
    /// <param name="partitionBy">分区字段</param>
    /// <param name="orderBy">排序字段</param>
    /// <param name="frame">窗口帧</param>
    /// <param name="alias">别名</param>
    /// <returns>窗口表达式</returns>
    public static WindowExpression Aggregate(WindowFunctionKind function, String column, String? partitionBy = null, String? orderBy = null, WindowFrame? frame = null, String? alias = null)
    {
        if (function is not (WindowFunctionKind.Sum or WindowFunctionKind.Avg or WindowFunctionKind.Count or WindowFunctionKind.Min or WindowFunctionKind.Max))
            throw new ArgumentOutOfRangeException(nameof(function), function, "仅聚合函数可创建聚合窗口表达式");
        if (column.IsNullOrEmpty()) throw new ArgumentNullException(nameof(column), "聚合窗口必须指定列");

        return new()
        {
            Function = function,
            Arguments = column,
            Spec = new WindowSpec { PartitionBy = partitionBy, OrderBy = orderBy, Frame = frame },
            Alias = alias ?? $"{function}_{column}",
        };
    }

    private static String GetFunctionName(WindowFunctionKind function) => function switch
    {
        WindowFunctionKind.RowNumber => "ROW_NUMBER",
        WindowFunctionKind.Rank => "RANK",
        WindowFunctionKind.DenseRank => "DENSE_RANK",
        WindowFunctionKind.Ntile => "NTILE",
        WindowFunctionKind.Lag => "LAG",
        WindowFunctionKind.Lead => "LEAD",
        WindowFunctionKind.FirstValue => "FIRST_VALUE",
        WindowFunctionKind.LastValue => "LAST_VALUE",
        WindowFunctionKind.Sum => "SUM",
        WindowFunctionKind.Avg => "AVG",
        WindowFunctionKind.Count => "COUNT",
        WindowFunctionKind.Min => "MIN",
        WindowFunctionKind.Max => "MAX",
        _ => "ROW_NUMBER",
    };
}
