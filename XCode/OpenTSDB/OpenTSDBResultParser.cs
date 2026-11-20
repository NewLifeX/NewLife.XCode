using System.Data;
using NewLife.Serialization;

namespace XCode.OpenTSDB;

/// <summary>OpenTSDB 查询结果解析器。把 series/dps 结构展开为行式 DataTable</summary>
internal static class OpenTSDBResultParser
{
    private static readonly DateTime _epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>解析查询结果</summary>
    /// <param name="text">响应文本</param>
    /// <returns>结果表</returns>
    internal static DataTable Parse(String text)
    {
        var table = new DataTable();
        if (text.IsNullOrWhiteSpace()) return table;

        if (JsonHelper.Parse(text) is not IList<Object> list) return table;

        var tagNames = new SortedSet<String>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<Row>();

        foreach (var item in list)
        {
            if (item is not IDictionary<String, Object?> series) continue;

            var metric = series["metric"] as String ?? String.Empty;
            var tags = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            if (series["tags"] is IDictionary<String, Object?> tagDic)
            {
                foreach (var tag in tagDic)
                {
                    var value = tag.Value + "";
                    tags[tag.Key] = value;
                    tagNames.Add(tag.Key);
                }
            }

            if (series["dps"] is not IDictionary<String, Object?> dps) continue;

            foreach (var dp in dps)
            {
                var timestamp = ParseTimestamp(dp.Key);
                if (timestamp == 0) continue;

                rows.Add(new Row
                {
                    Time = _epoch.AddMilliseconds(timestamp).ToLocalTime(),
                    Metric = metric,
                    Tags = tags,
                    Value = dp.Value,
                });
            }
        }

        table.Columns.Add("time", typeof(DateTime));
        table.Columns.Add("metric", typeof(String));
        foreach (var tag in tagNames) table.Columns.Add(tag, typeof(String));
        table.Columns.Add("value", IsInteger(rows) ? typeof(Int64) : typeof(Double));

        foreach (var row in rows)
        {
            var values = new Object?[table.Columns.Count];
            values[0] = row.Time;
            values[1] = row.Metric;

            var index = 2;
            foreach (var tag in tagNames)
            {
                values[index++] = row.Tags.TryGetValue(tag, out var value) ? value : DBNull.Value;
            }

            values[index] = row.Value == null ? DBNull.Value : ConvertValue(row.Value, table.Columns[index].DataType);
            table.Rows.Add(values);
        }

        return table;
    }

    private sealed class Row
    {
        public DateTime Time { get; set; }

        public String Metric { get; set; } = String.Empty;

        public IDictionary<String, String> Tags { get; set; } = new Dictionary<String, String>();

        public Object? Value { get; set; }
    }

    /// <summary>解析时间戳。13 位按毫秒，其余按秒</summary>
    /// <param name="text">时间戳文本</param>
    /// <returns>毫秒时间戳</returns>
    private static Int64 ParseTimestamp(String text)
    {
        if (!Int64.TryParse(text, out var value) || value <= 0) return 0;

        return text.Length >= 13 ? value : value * 1000;
    }

    private static Boolean IsInteger(List<Row> rows)
    {
        foreach (var row in rows)
        {
            if (row.Value == null) continue;

            if (row.Value is Double or Single or Decimal or String) return false;
            if (row.Value is not (SByte or Byte or Int16 or UInt16 or Int32 or UInt32 or Int64 or UInt64)) return false;
        }

        return true;
    }

    private static Object ConvertValue(Object value, Type type)
    {
        if (type == typeof(Int64)) return value.ToLong();
        if (type == typeof(Double)) return value.ToDouble();

        return value;
    }
}
