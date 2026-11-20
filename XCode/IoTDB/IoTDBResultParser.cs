using System.Data;
using NewLife.Serialization;

namespace XCode.IoTDB;

/// <summary>IoTDB REST v2 结果解析器</summary>
internal static class IoTDBResultParser
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

        var code = root["code"].ToInt();
        if (code != 0 && code != 200) throw new Exception(L.T("{0} request failed: {1}", "IoTDB", root["message"] + ""));

        var expressions = root["expressions"] as IList<Object>;
        var columnNames = root["column_names"] as IList<Object>;
        var timestamps = root["timestamps"] as IList<Object>;
        var values = root["values"] as IList<Object>;

        // 数据查询返回 expressions + timestamps，元数据查询返回 column_names
        var isData = expressions != null;
        var columns = isData ? expressions : columnNames;
        if (columns == null || values == null) return table;

        if (isData) table.Columns.Add("Time", typeof(DateTime));
        foreach (var column in columns)
        {
            var name = column + "";
            if (name.IsNullOrEmpty()) continue;

            table.Columns.Add(name, typeof(Object));
        }

        var rowCount = 0;
        foreach (var column in values)
        {
            if (column is IList<Object> cells && cells.Count > rowCount) rowCount = cells.Count;
        }

        var offset = isData ? 1 : 0;
        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var row = table.NewRow();

            if (isData) row[0] = timestamps != null && rowIndex < timestamps.Count ? ToTime(timestamps[rowIndex]) : DBNull.Value;

            for (var columnIndex = 0; columnIndex < values.Count && columnIndex < columns.Count; columnIndex++)
            {
                if (values[columnIndex] is not IList<Object> cells || rowIndex >= cells.Count) continue;

                row[columnIndex + offset] = cells[rowIndex] ?? DBNull.Value;
            }

            table.Rows.Add(row);
        }

        return table;
    }

    private static Object ToTime(Object? value)
    {
        if (value == null) return DBNull.Value;

        var ms = value.ToLong();
        return ms > 0 ? _epoch.AddMilliseconds(ms).ToLocalTime() : DBNull.Value;
    }
}
