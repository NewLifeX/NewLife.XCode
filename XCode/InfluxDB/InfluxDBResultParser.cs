using System.Data;
using NewLife.Serialization;

namespace XCode.InfluxDB;

/// <summary>InfluxDB 查询结果解析器。把 InfluxQL 与 InfluxDB 3.x SQL 的 JSON 结果转换为 DataTable</summary>
internal static class InfluxDBResultParser
{
    /// <summary>解析查询结果</summary>
    /// <param name="text">响应文本</param>
    /// <param name="language">查询语言</param>
    /// <returns>结果表</returns>
    public static DataTable Parse(String text, InfluxQueryLanguage language)
    {
        if (text.IsNullOrWhiteSpace()) return new DataTable();

        return language == InfluxQueryLanguage.Sql ? ParseRows(text) : ParseInfluxQl(text);
    }

    /// <summary>解析 InfluxQL 的 results/series 结构</summary>
    /// <param name="text">响应文本</param>
    /// <returns>结果表</returns>
    internal static DataTable ParseInfluxQl(String text)
    {
        var table = new DataTable();

        if (JsonHelper.Parse(text) is not IDictionary<String, Object?> root) return table;
        if (root.TryGetValue("error", out var error) && error != null) throw new InvalidOperationException(error.ToString());

        if (root["results"] is not IList<Object> results) return table;

        var indexes = new Dictionary<String, Int32>(StringComparer.OrdinalIgnoreCase);
        var hasMeasurement = false;

        foreach (var item in results)
        {
            if (item is not IDictionary<String, Object?> result) continue;

            if (result.TryGetValue("error", out var itemError) && itemError != null) throw new InvalidOperationException(itemError.ToString());

            if (result["series"] is not IList<Object> series) continue;

            foreach (var element in series)
            {
                if (element is not IDictionary<String, Object?> item2) continue;

                var name = item2["name"] as String ?? String.Empty;
                if (!hasMeasurement)
                {
                    table.Columns.Add("_measurement", typeof(Object));
                    indexes["_measurement"] = 0;
                    hasMeasurement = true;
                }

                var columns = new List<String>();
                if (item2["columns"] is IList<Object> cols)
                {
                    foreach (var col in cols)
                    {
                        var column = col + "";
                        if (column.IsNullOrEmpty()) continue;

                        columns.Add(column);
                        if (!indexes.ContainsKey(column))
                        {
                            indexes[column] = table.Columns.Count;
                            table.Columns.Add(column, typeof(Object));
                        }
                    }
                }

                if (item2["values"] is not IList<Object> values) continue;

                foreach (var row in values)
                {
                    if (row is not IList<Object> cells) continue;

                    var values2 = new Object?[table.Columns.Count];
                    values2[0] = name;
                    for (var i = 0; i < columns.Count && i < cells.Count; i++)
                    {
                        values2[indexes[columns[i]]] = cells[i];
                    }

                    table.Rows.Add(values2);
                }
            }
        }

        return table;
    }

    /// <summary>解析 JSONL 或 JSON 数组结构。InfluxDB 3.x SQL 的 format=jsonl 每行一个对象</summary>
    /// <param name="text">响应文本</param>
    /// <returns>结果表</returns>
    internal static DataTable ParseRows(String text)
    {
        var table = new DataTable();

        var list = new List<IDictionary<String, Object?>>();
        var trimmed = text.Trim();
        if (trimmed.StartsWith("["))
        {
            if (JsonHelper.Parse(trimmed) is IList<Object> array)
            {
                foreach (var item in array)
                {
                    if (item is IDictionary<String, Object?> dic) list.Add(dic);
                }
            }
        }
        else
        {
            foreach (var line in trimmed.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (JsonHelper.Parse(line.Trim()) is IDictionary<String, Object?> dic) list.Add(dic);
            }
        }

        foreach (var item in list)
        {
            foreach (var key in item.Keys)
            {
                if (!table.Columns.Contains(key)) table.Columns.Add(key, typeof(Object));
            }

            var row = table.NewRow();
            foreach (var item2 in item) row[item2.Key] = item2.Value ?? DBNull.Value;
            table.Rows.Add(row);
        }

        return table;
    }
}
