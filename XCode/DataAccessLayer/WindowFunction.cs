using NewLife;

namespace XCode.DataAccessLayer;

/// <summary>窗口函数构建器。生成 ROW_NUMBER/RANK/DENSE_RANK 等标准 SQL 窗口函数</summary>
/// <remarks>
/// 用法：
/// <code>
/// var sb = new SelectBuilder { Table = "Role", Column = "*" };
/// sb.Window = WindowFunction.RowNumber("ID DESC", "RowNum");
/// // 生成: Select *, ROW_NUMBER() OVER(ORDER BY ID DESC) AS RowNum From Role
/// 
/// sb.Window = WindowFunction.Rank("DeptID", "Salary DESC", "Rank");
/// // 生成: Select *, RANK() OVER(PARTITION BY DeptID ORDER BY Salary DESC) AS Rank From Role
/// </code>
/// </remarks>
public static class WindowFunction
{
    /// <summary>ROW_NUMBER() OVER (ORDER BY orderBy) AS alias</summary>
    /// <param name="orderBy">排序字段</param>
    /// <param name="alias">别名</param>
    /// <returns></returns>
    public static String RowNumber(String orderBy, String alias = "RowNum")
    {
        if (orderBy.IsNullOrEmpty()) throw new ArgumentNullException(nameof(orderBy));

        return $"ROW_NUMBER() OVER(ORDER BY {orderBy}) AS {alias}";
    }

    /// <summary>ROW_NUMBER() OVER (PARTITION BY partitionBy ORDER BY orderBy) AS alias</summary>
    /// <param name="partitionBy">分区字段</param>
    /// <param name="orderBy">排序字段</param>
    /// <param name="alias">别名</param>
    /// <returns></returns>
    public static String RowNumber(String partitionBy, String orderBy, String alias = "RowNum")
    {
        if (partitionBy.IsNullOrEmpty()) return RowNumber(orderBy, alias);
        if (orderBy.IsNullOrEmpty()) throw new ArgumentNullException(nameof(orderBy));

        return $"ROW_NUMBER() OVER(PARTITION BY {partitionBy} ORDER BY {orderBy}) AS {alias}";
    }

    /// <summary>RANK() OVER (ORDER BY orderBy) AS alias</summary>
    /// <param name="orderBy">排序字段</param>
    /// <param name="alias">别名</param>
    /// <returns></returns>
    public static String Rank(String orderBy, String alias = "Rank")
    {
        if (orderBy.IsNullOrEmpty()) throw new ArgumentNullException(nameof(orderBy));

        return $"RANK() OVER(ORDER BY {orderBy}) AS {alias}";
    }

    /// <summary>DENSE_RANK() OVER (ORDER BY orderBy) AS alias</summary>
    /// <param name="orderBy">排序字段</param>
    /// <param name="alias">别名</param>
    /// <returns></returns>
    public static String DenseRank(String orderBy, String alias = "DenseRank")
    {
        if (orderBy.IsNullOrEmpty()) throw new ArgumentNullException(nameof(orderBy));

        return $"DENSE_RANK() OVER(ORDER BY {orderBy}) AS {alias}";
    }

    /// <summary>聚合窗口函数 SUM/AVG/COUNT/MAX/MIN OVER (PARTITION BY partitionBy) AS alias</summary>
    /// <param name="func">聚合函数名（SUM/AVG/COUNT/MAX/MIN）</param>
    /// <param name="column">聚合列</param>
    /// <param name="partitionBy">分区字段（可选）</param>
    /// <param name="alias">别名</param>
    /// <returns></returns>
    public static String Aggregate(String func, String column, String? partitionBy = null, String? alias = null)
    {
        if (func.IsNullOrEmpty()) throw new ArgumentNullException(nameof(func));
        if (column.IsNullOrEmpty()) throw new ArgumentNullException(nameof(column));

        alias ??= $"{func}_{column}";

        if (partitionBy.IsNullOrEmpty())
            return $"{func}({column}) OVER() AS {alias}";

        return $"{func}({column}) OVER(PARTITION BY {partitionBy}) AS {alias}";
    }
}
