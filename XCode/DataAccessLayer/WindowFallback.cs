using NewLife;
using NewLife.Data;

namespace XCode.DataAccessLayer;

/// <summary>窗口函数降级策略</summary>
public enum WindowFallback
{
    /// <summary>不支持时抛出异常。默认行为</summary>
    None = 0,

    /// <summary>在内存中对查询结果计算窗口列。只适合小结果集</summary>
    InMemory = 1,
}

/// <summary>窗口函数内存计算器。数据库不支持窗口函数时，对结果集在内存中计算</summary>
/// <remarks>
/// 内存回退只处理已经返回的结果集，因此分页发生在窗口计算之前；
/// 需要跨页排名时应先查询完整结果再分页。
/// </remarks>
public static class WindowEvaluator
{
    #region 主入口
    /// <summary>对结果集追加窗口列</summary>
    /// <param name="expression">窗口表达式</param>
    /// <param name="table">查询结果</param>
    public static void Apply(WindowExpression expression, DbTable table)
    {
        if (expression == null) throw new ArgumentNullException(nameof(expression));
        if (table == null) throw new ArgumentNullException(nameof(table));

        var rows = table.Rows;
        var count = rows.Count;
        var alias = expression.Alias.IsNullOrEmpty() ? "WindowValue" : expression.Alias;
        var values = new Object?[count];

        if (count > 0)
        {
            var partitions = BuildPartitions(expression, table, out var order);
            foreach (var part in partitions)
            {
                var ordered = SortPartition(part, order, table);
                Evaluate(expression, table, ordered, values);
            }
        }

        SetColumn(table, alias, values);
    }
    #endregion

    #region 分组与排序
    private sealed class OrderItem(String column, Boolean desc)
    {
        public String Column { get; } = column;

        public Boolean Desc { get; } = desc;

        /// <summary>在结果集中的列序号，排序前解析</summary>
        public Int32 Index { get; set; } = -1;
    }

    private static OrderItem[] ResolveOrder(DbTable table, OrderItem[] order)
    {
        // 提前解析列序号，避免在排序比较委托中抛异常被包装成 InvalidOperationException
        foreach (var item in order) item.Index = ResolveColumn(table, item.Column);

        return order;
    }

    private static List<List<Int32>> BuildPartitions(WindowExpression expression, DbTable table, out OrderItem[] order)
    {
        var partitionColumns = ParseColumns(expression.Spec?.PartitionBy);
        order = ResolveOrder(table, ParseOrder(expression.Spec?.OrderBy));

        var indexes = new List<Int32>(table.Rows.Count);
        for (var i = 0; i < table.Rows.Count; i++) indexes.Add(i);

        if (partitionColumns.Length == 0) return [indexes];

        var groups = new Dictionary<String, List<Int32>>(StringComparer.Ordinal);
        var keys = new List<String>();
        foreach (var idx in indexes)
        {
            var key = BuildKey(table, idx, partitionColumns);
            if (!groups.TryGetValue(key, out var list))
            {
                groups[key] = list = [];
                keys.Add(key);
            }
            list.Add(idx);
        }

        var parts = new List<List<Int32>>(keys.Count);
        foreach (var key in keys) parts.Add(groups[key]);

        return parts;
    }

    private static String BuildKey(DbTable table, Int32 rowIndex, String[] columns)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var column in columns)
        {
            var idx = ResolveColumn(table, column);
            var value = table.Rows[rowIndex][idx];
            sb.Append(value?.ToString() ?? "\0");
            sb.Append('\u0001');
        }
        return sb.ToString();
    }

    private static List<Int32> SortPartition(List<Int32> part, OrderItem[] order, DbTable table)
    {
        if (order.Length == 0 || part.Count < 2) return part;

        var list = new List<Int32>(part);
        list.Sort((x, y) =>
        {
            foreach (var item in order)
            {
                var rs = CompareValue(table.Rows[x][item.Index], table.Rows[y][item.Index]);
                if (rs != 0) return item.Desc ? -rs : rs;
            }

            return x.CompareTo(y);
        });

        return list;
    }

    private static Int32 CompareValue(Object? left, Object? right)
    {
        if (left == null) return right == null ? 0 : -1;
        if (right == null) return 1;

        if (left is IComparable cmp && left.GetType() == right.GetType()) return cmp.CompareTo(right);

        var d1 = left.ToDouble();
        var d2 = right.ToDouble();
        var rs = d1.CompareTo(d2);

        return rs != 0 ? rs : String.CompareOrdinal(left.ToString(), right.ToString());
    }

    private static OrderItem[] ParseOrder(String? orderBy)
    {
        if (orderBy.IsNullOrWhiteSpace()) return [];

        var list = new List<OrderItem>();
        foreach (var item in orderBy.Split(','))
        {
            var text = item.Trim();
            if (text.IsNullOrEmpty()) continue;

            var desc = false;
            if (text.EndsWithIgnoreCase(" DESC"))
            {
                desc = true;
                text = text[..^5].Trim();
            }
            else if (text.EndsWithIgnoreCase(" ASC"))
            {
                text = text[..^4].Trim();
            }

            list.Add(new OrderItem(CleanName(text), desc));
        }

        return list.ToArray();
    }

    private static String[] ParseColumns(String? columns)
    {
        if (columns.IsNullOrWhiteSpace()) return [];

        var list = new List<String>();
        foreach (var item in columns.Split(','))
        {
            var text = CleanName(item.Trim());
            if (!text.IsNullOrEmpty()) list.Add(text);
        }

        return list.ToArray();
    }

    private static String CleanName(String name)
    {
        if (name.IsNullOrEmpty()) return name;

        // 去掉表别名前缀，例如 T0.Score
        var p = name.LastIndexOf('.');
        if (p >= 0 && p < name.Length - 1) name = name[(p + 1)..];

        return name.Trim('"', '[', ']', '`', '\'');
    }

    private static Int32 ResolveColumn(DbTable table, String name)
    {
        if (table.Columns == null) throw new NotSupportedException(L.T("内存窗口回退要求结果集包含列 [{0}]，但当前结果集没有列信息", name));

        var idx = table.GetColumn(name);
        if (idx < 0)
        {
            foreach (var column in table.Columns)
            {
                if (CleanName(column).EqualIgnoreCase(name))
                {
                    idx = table.GetColumn(column);
                    break;
                }
            }
        }

        if (idx < 0) throw new NotSupportedException(L.T("内存窗口回退要求结果集包含列 [{0}]，请把该列加入查询字段", name));

        return idx;
    }
    #endregion

    #region 计算
    private static void Evaluate(WindowExpression expression, DbTable table, List<Int32> ordered, Object?[] values)
    {
        var kind = expression.Function;
        switch (kind)
        {
            case WindowFunctionKind.RowNumber:
                for (var i = 0; i < ordered.Count; i++) values[ordered[i]] = i + 1;
                break;
            case WindowFunctionKind.Rank:
            case WindowFunctionKind.DenseRank:
                EvaluateRank(expression, table, ordered, values, kind == WindowFunctionKind.DenseRank);
                break;
            case WindowFunctionKind.Ntile:
                EvaluateNtile(expression, ordered, values);
                break;
            case WindowFunctionKind.Lag:
            case WindowFunctionKind.Lead:
                EvaluateOffset(expression, table, ordered, values, kind == WindowFunctionKind.Lead);
                break;
            case WindowFunctionKind.FirstValue:
            case WindowFunctionKind.LastValue:
                EvaluateValue(expression, table, ordered, values, kind == WindowFunctionKind.LastValue);
                break;
            default:
                EvaluateAggregate(expression, table, ordered, values);
                break;
        }
    }

    private static void EvaluateRank(WindowExpression expression, DbTable table, List<Int32> ordered, Object?[] values, Boolean dense)
    {
        var order = ResolveOrder(table, ParseOrder(expression.Spec?.OrderBy));
        var rank = 0;
        var denseRank = 0;
        Object?[]? previous = null;

        for (var i = 0; i < ordered.Count; i++)
        {
            var current = ReadOrderValues(table, ordered[i], order);
            if (previous == null || !SameValues(previous, current))
            {
                rank = i + 1;
                denseRank++;
            }

            values[ordered[i]] = dense ? denseRank : rank;
            previous = current;
        }
    }

    private static void EvaluateNtile(WindowExpression expression, List<Int32> ordered, Object?[] values)
    {
        if (!Int32.TryParse(expression.Arguments, out var buckets) || buckets <= 0)
            throw new NotSupportedException(L.T("NTILE 的内存回退需要正整数分桶数量，当前为 [{0}]", expression.Arguments));

        var count = ordered.Count;
        var baseSize = count / buckets;
        var remainder = count % buckets;
        var pos = 0;
        for (var b = 1; b <= buckets; b++)
        {
            var size = baseSize + (b <= remainder ? 1 : 0);
            for (var i = 0; i < size && pos < count; i++) values[ordered[pos++]] = b;
        }
    }

    private static void EvaluateOffset(WindowExpression expression, DbTable table, List<Int32> ordered, Object?[] values, Boolean lead)
    {
        var args = ParseArguments(expression.Arguments);
        if (args.Length == 0) throw new NotSupportedException(L.T("LAG/LEAD 的内存回退需要取值列"));

        var idx = ResolveColumn(table, args[0]);
        var offset = args.Length > 1 ? args[1].ToInt(1) : 1;
        Object? defaultValue = args.Length > 2 ? args[2] : null;

        for (var i = 0; i < ordered.Count; i++)
        {
            var target = lead ? i + offset : i - offset;
            values[ordered[i]] = target >= 0 && target < ordered.Count ? table.Rows[ordered[target]][idx] : defaultValue;
        }
    }

    private static void EvaluateValue(WindowExpression expression, DbTable table, List<Int32> ordered, Object?[] values, Boolean last)
    {
        var args = ParseArguments(expression.Arguments);
        if (args.Length == 0) throw new NotSupportedException(L.T("FIRST_VALUE/LAST_VALUE 的内存回退需要取值列"));

        var idx = ResolveColumn(table, args[0]);
        var frame = ResolveFrame(expression);
        var frameIndexes = BuildFrameIndexes(expression, table, ordered, frame);

        for (var i = 0; i < ordered.Count; i++)
        {
            var (start, end) = frameIndexes[i];
            values[ordered[i]] = start > end || end < start ? null : table.Rows[ordered[last ? end : start]][idx];
        }
    }

    private static void EvaluateAggregate(WindowExpression expression, DbTable table, List<Int32> ordered, Object?[] values)
    {
        var args = ParseArguments(expression.Arguments);
        if (args.Length == 0) throw new NotSupportedException(L.T("聚合窗口的内存回退需要聚合列"));

        var idx = ResolveColumn(table, args[0]);
        var frame = ResolveFrame(expression);
        var frameIndexes = BuildFrameIndexes(expression, table, ordered, frame);
        var type = table.Types != null && table.Types.Length > idx ? table.Types[idx] : typeof(Double);

        for (var i = 0; i < ordered.Count; i++)
        {
            var (start, end) = frameIndexes[i];
            values[ordered[i]] = Aggregate(expression.Function, table, ordered, idx, start, end, type);
        }
    }

    private static WindowFrame ResolveFrame(WindowExpression expression)
    {
        // SQL 默认帧：有排序时为 RANGE UNBOUNDED PRECEDING 到 CURRENT ROW
        var frame = expression.Spec?.Frame;
        if (frame != null) return frame;

        var hasOrder = ParseOrder(expression.Spec?.OrderBy).Length > 0;

        return new WindowFrame
        {
            IsRows = !hasOrder,
            Start = WindowFrameBoundary.UnboundedPreceding,
            End = hasOrder ? WindowFrameBoundary.CurrentRow : WindowFrameBoundary.UnboundedFollowing,
        };
    }

    private static (Int32 Start, Int32 End)[] BuildFrameIndexes(WindowExpression expression, DbTable table, List<Int32> ordered, WindowFrame frame)
    {
        var count = ordered.Count;
        var result = new (Int32, Int32)[count];
        if (count == 0) return result;

        var order = ParseOrder(expression.Spec?.OrderBy);

        for (var i = 0; i < count; i++)
        {
            Int32 start;
            Int32 end;

            if (frame.IsRows)
            {
                start = ResolveRowsBoundary(frame.Start, frame.StartOffset, i, count, true);
                end = ResolveRowsBoundary(frame.End, frame.EndOffset, i, count, false);
            }
            else
            {
                start = ResolveRangeBoundary(frame.Start, frame.StartOffset, i, ordered, table, order, true);
                end = ResolveRangeBoundary(frame.End, frame.EndOffset, i, ordered, table, order, false);
            }

            result[i] = (start, end);
        }

        return result;
    }

    private static Int32 ResolveRowsBoundary(WindowFrameBoundary boundary, Int32 offset, Int32 i, Int32 count, Boolean start) => boundary switch
    {
        WindowFrameBoundary.UnboundedPreceding => 0,
        WindowFrameBoundary.Preceding => Math.Max(0, i - offset),
        WindowFrameBoundary.CurrentRow => i,
        WindowFrameBoundary.Following => Math.Min(count - 1, i + offset),
        WindowFrameBoundary.UnboundedFollowing => count - 1,
        _ => start ? 0 : count - 1,
    };

    private static Int32 ResolveRangeBoundary(WindowFrameBoundary boundary, Int32 offset, Int32 i, List<Int32> ordered, DbTable table, OrderItem[] order, Boolean start)
    {
        var count = ordered.Count;
        switch (boundary)
        {
            case WindowFrameBoundary.UnboundedPreceding:
                return 0;
            case WindowFrameBoundary.UnboundedFollowing:
                return count - 1;
            case WindowFrameBoundary.CurrentRow:
                {
                    // RANGE 的 CURRENT ROW 以排序值相同的一组行（peer）为单位
                    if (order.Length == 0) return i;

                    var current = ReadOrderValues(table, ordered[i], order);
                    if (start)
                    {
                        var pos = i;
                        while (pos > 0 && SameValues(ReadOrderValues(table, ordered[pos - 1], order), current)) pos--;
                        return pos;
                    }
                    else
                    {
                        var pos = i;
                        while (pos < count - 1 && SameValues(ReadOrderValues(table, ordered[pos + 1], order), current)) pos++;
                        return pos;
                    }
                }
            default:
                throw new NotSupportedException(L.T("内存窗口回退不支持带偏移量的 RANGE 帧 [{0}]，请改用 ROWS 帧", boundary));
        }
    }

    private static Object? Aggregate(WindowFunctionKind kind, DbTable table, List<Int32> ordered, Int32 column, Int32 start, Int32 end, Type type)
    {
        if (start > end) return kind == WindowFunctionKind.Count ? 0 : null;

        var counting = kind == WindowFunctionKind.Count;
        var count = 0;
        var useFloat = type == typeof(Single) || type == typeof(Double);
        Double sumFloat = 0;
        Decimal sumDecimal = 0;
        Object? best = null;

        for (var i = start; i <= end; i++)
        {
            var value = table.Rows[ordered[i]][column];
            if (value == null || value == DBNull.Value) continue;

            count++;
            switch (kind)
            {
                case WindowFunctionKind.Count:
                    break;
                case WindowFunctionKind.Min:
                    if (best == null || CompareValue(value, best) < 0) best = value;
                    break;
                case WindowFunctionKind.Max:
                    if (best == null || CompareValue(value, best) > 0) best = value;
                    break;
                case WindowFunctionKind.Avg:
                    if (useFloat) sumFloat += value.ToDouble();
                    else sumDecimal += value.ToDecimal();
                    break;
                default:
                    if (useFloat) sumFloat += value.ToDouble();
                    else sumDecimal += value.ToDecimal();
                    break;
            }
        }

        if (counting) return count;

        return kind switch
        {
            WindowFunctionKind.Min or WindowFunctionKind.Max => best,
            WindowFunctionKind.Avg => count == 0 ? null : useFloat ? sumFloat / count : (Object)(sumDecimal / count),
            _ => count == 0 ? null : useFloat ? sumFloat : (Object)sumDecimal,
        };
    }

    private static String[] ParseArguments(String? arguments)
    {
        if (arguments.IsNullOrWhiteSpace()) return [];

        return arguments.Split(',').Select(e => CleanName(e.Trim())).Where(e => !e.IsNullOrEmpty()).ToArray();
    }

    private static Object?[] ReadOrderValues(DbTable table, Int32 rowIndex, OrderItem[] order)
    {
        var values = new Object?[order.Length];
        for (var i = 0; i < order.Length; i++) values[i] = table.Rows[rowIndex][order[i].Index];

        return values;
    }

    private static Boolean SameValues(Object?[] left, Object?[] right)
    {
        if (left.Length != right.Length) return false;

        for (var i = 0; i < left.Length; i++)
        {
            if (CompareValue(left[i], right[i]) != 0) return false;
        }

        return true;
    }
    #endregion

    #region 辅助
    /// <summary>把计算好的窗口列写入结果集</summary>
    /// <param name="table">查询结果</param>
    /// <param name="alias">列名</param>
    /// <param name="values">按原始行索引排列的数值</param>
    public static void SetColumn(DbTable table, String alias, Object?[] values)
    {
        var columns = table.Columns ?? [];
        var types = table.Types;
        var index = table.GetColumn(alias);

        if (index < 0)
        {
            index = columns.Length;

            var newColumns = new String[columns.Length + 1];
            Array.Copy(columns, newColumns, columns.Length);
            newColumns[index] = alias;
            table.Columns = newColumns;

            var newTypes = new Type[index + 1];
            if (types != null) Array.Copy(types, newTypes, Math.Min(types.Length, index));
            newTypes[index] = typeof(Object);
            table.Types = newTypes;
        }

        var rows = table.Rows;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Length > index)
            {
                row[index] = i < values.Length ? values[i] : null;
                continue;
            }

            var newRow = new Object[index + 1];
            Array.Copy(row, newRow, row.Length);
            newRow[index] = i < values.Length ? values[i] : null;
            rows[i] = newRow;
        }
    }
    #endregion
}
