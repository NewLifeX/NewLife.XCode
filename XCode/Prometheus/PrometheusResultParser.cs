using System.Data;
using System.Globalization;
using NewLife.Serialization;

namespace XCode.Prometheus;

/// <summary>Prometheus 查询结果解析器</summary>
internal static class PrometheusResultParser
{
    private static readonly DateTime _epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>解析查询结果</summary>
    /// <param name="text">响应文本</param>
    /// <returns>结果表</returns>
    internal static DataTable Parse(String text)
    {
        var table = new DataTable();
        if (text.IsNullOrWhiteSpace()) return table;

        if (JsonHelper.Parse(text) is not IDictionary<String, Object?> root) return table;

        if (root["status"] as String != "success")
            throw new Exception(L.T("{0} request failed: {1}", "Prometheus", root["error"] + ""));

        if (root["data"] is not IDictionary<String, Object?> data) return table;

        var type = data["resultType"] as String ?? "vector";
        var result = data["result"];

        if (type == "scalar" || type == "string")
        {
            table.Columns.Add("time", typeof(DateTime));
            table.Columns.Add("value", type == "string" ? typeof(String) : typeof(Double));

            if (result is IList<Object> pair && pair.Count >= 2)
            {
                var row = table.NewRow();
                row[0] = ToTime(pair[0]);
                row[1] = type == "string" ? pair[1] ?? DBNull.Value : ToDouble(pair[1]);
                table.Rows.Add(row);
            }

            return table;
        }

        if (result is not IList<Object> items) return table;

        var labelNames = new SortedSet<String>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<Row>();

        foreach (var item in items)
        {
            if (item is not IDictionary<String, Object?> series) continue;

            var labels = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            if (series["metric"] is IDictionary<String, Object?> metric)
            {
                foreach (var label in metric)
                {
                    labels[label.Key] = label.Value + "";
                    if (!label.Key.EqualIgnoreCase("__name__")) labelNames.Add(label.Key);
                }
            }

            var name = labels.TryGetValue("__name__", out var n) ? n : String.Empty;

            if (type == "matrix")
            {
                if (series["values"] is not IList<Object> values) continue;

                foreach (var sample in values)
                {
                    if (sample is not IList<Object> cells || cells.Count < 2) continue;

                    rows.Add(new Row
                    {
                        Time = ToTime(cells[0]),
                        Metric = name,
                        Labels = labels,
                        Value = ToDouble(cells[1]),
                    });
                }
            }
            else
            {
                if (series["value"] is not IList<Object> cells || cells.Count < 2) continue;

                rows.Add(new Row
                {
                    Time = ToTime(cells[0]),
                    Metric = name,
                    Labels = labels,
                    Value = ToDouble(cells[1]),
                });
            }
        }

        table.Columns.Add("time", typeof(DateTime));
        table.Columns.Add("metric", typeof(String));
        foreach (var label in labelNames) table.Columns.Add(label, typeof(String));
        table.Columns.Add("value", typeof(Double));

        foreach (var item in rows)
        {
            var values = new Object?[table.Columns.Count];
            values[0] = item.Time;
            values[1] = item.Metric;

            var index = 2;
            foreach (var label in labelNames)
            {
                values[index++] = item.Labels.TryGetValue(label, out var value) ? value : DBNull.Value;
            }

            values[index] = item.Value;
            table.Rows.Add(values);
        }

        return table;
    }

    private sealed class Row
    {
        public Object? Time { get; set; }

        public String Metric { get; set; } = String.Empty;

        public IDictionary<String, String> Labels { get; set; } = new Dictionary<String, String>();

        public Object? Value { get; set; }
    }

    private static Object ToTime(Object? value)
    {
        if (value == null) return DBNull.Value;

        var seconds = value.ToDouble();
        return seconds > 0 ? _epoch.AddMilliseconds(seconds * 1000).ToLocalTime() : DBNull.Value;
    }

    private static Object ToDouble(Object? value)
    {
        if (value == null) return DBNull.Value;

        var text = value + "";
        if (text.EqualIgnoreCase("NaN")) return Double.NaN;
        if (text.EqualIgnoreCase("+Inf", "Inf")) return Double.PositiveInfinity;
        if (text.EqualIgnoreCase("-Inf")) return Double.NegativeInfinity;

        return Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : DBNull.Value;
    }
}
