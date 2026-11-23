using System.Data;

namespace XCode.DataAccessLayer;

/// <summary>SQL 元数据辅助。按标准化的 tables/columns 查询结果构建表模型</summary>
/// <remarks>
/// 各数据库方言的 tables/columns 查询需要把列别名统一为：
/// table_schema、table_name、table_type、column_name、ordinal_position、data_type、is_nullable、
/// column_default、character_maximum_length、numeric_precision、numeric_scale。
/// is_identity 可选，缺省表示不支持自增。
/// </remarks>
internal static class SqlMetaDataHelper
{
    /// <summary>根据 tables/columns 查询构造表模型</summary>
    /// <param name="db">数据库</param>
    /// <param name="tablesSql">表查询语句</param>
    /// <param name="columnsSql">字段查询语句</param>
    /// <param name="getDataType">原始类型到 .NET 类型的映射</param>
    /// <returns></returns>
    public static List<IDataTable> GetTables(IDatabase db, String tablesSql, String columnsSql, Func<IDataColumn, Type?> getDataType)
    {
        var list = new List<IDataTable>();

        var ss = db.CreateSession();
        var old = ss.ShowSQL;
        ss.ShowSQL = false;
        try
        {
            var ds = ss.Query(tablesSql, CommandType.Text);
            if (ds.Tables.Count == 0) return list;

            var dt = ds.Tables[0];
            if (dt.Rows.Count == 0) return list;

            // 按 模式.表名 分组，避免同名表跨模式串数据
            var dic = new Dictionary<String, List<DataRow>>(StringComparer.OrdinalIgnoreCase);
            var cds = ss.Query(columnsSql, CommandType.Text);
            if (cds.Tables.Count > 0)
            {
                foreach (DataRow dr in cds.Tables[0].Rows)
                {
                    var key = $"{dr["table_schema"]}.{dr["table_name"]}";
                    if (!dic.TryGetValue(key, out var rows)) dic[key] = rows = [];
                    rows.Add(dr);
                }

                // 按 ordinal_position 还原字段顺序。部分方言（如开启 strict mode 的 Presto）不允许在元数据查询中排序
                foreach (var rows in dic.Values)
                {
                    if (rows.Count > 1) rows.Sort((x, y) => GetInt32(x, "ordinal_position").CompareTo(GetInt32(y, "ordinal_position")));
                }
            }

            foreach (DataRow dr in dt.Rows)
            {
                var table = DAL.CreateTable();
                table.TableName = dr["table_name"] + "";
                table.Owner = dr["table_schema"] + "";
                table.IsView = (dr["table_type"] + "").EqualIgnoreCase("VIEW");
                table.DbType = db.Type;

                if (dic.TryGetValue($"{dr["table_schema"]}.{dr["table_name"]}", out var rows))
                {
                    foreach (var cr in rows)
                    {
                        var field = table.CreateColumn();
                        field.ColumnName = cr["column_name"] + "";
                        field.RawType = cr["data_type"] + "";
                        field.Length = GetInt32(cr, "character_maximum_length");
                        field.Precision = GetInt32(cr, "numeric_precision");
                        field.Scale = GetInt32(cr, "numeric_scale");
                        field.Nullable = (cr["is_nullable"] + "").EqualIgnoreCase("YES");

                        if (cr.Table.Columns.Contains("column_default") && cr["column_default"] is not DBNull)
                            field.DefaultValue = cr["column_default"] + "";

                        // 自增与主键约束均为信息性；只有明确返回 is_identity 时才标记
                        if (cr.Table.Columns.Contains("is_identity"))
                            field.Identity = (cr["is_identity"] + "").EqualIgnoreCase("YES");

                        field.DataType = getDataType(field) ?? typeof(String);

                        field.Fix();
                        table.Columns.Add(field);
                    }
                }

                table.Fix();

                list.Add(table);
            }
        }
        finally
        {
            ss.ShowSQL = old;
            ss.Dispose();
        }

        return list;
    }

    private static Int32 GetInt32(DataRow dr, String name)
    {
        if (dr.Table.Columns.Contains(name) && !dr.IsNull(name) && Int32.TryParse(dr[name] + "", out var n)) return n;

        return 0;
    }

    /// <summary>构造 information_schema 查询条件</summary>
    /// <param name="schema">模式名，为空时不按模式过滤</param>
    /// <param name="names">表名集合，为空时不按表名过滤</param>
    /// <returns>以 where 开头的条件，没有条件时返回空字符串</returns>
    public static String BuildWhere(String? schema, String[]? names)
    {
        var filters = new List<String>();
        if (!schema.IsNullOrEmpty()) filters.Add($"table_schema = '{schema.Replace("'", "''")}'");

        var list = EscapeNames(names);
        if (!list.IsNullOrEmpty()) filters.Add($"table_name in ({list})");

        return filters.Count > 0 ? " where " + String.Join(" and ", filters) : "";
    }

    /// <summary>把名称集合转成 SQL 值列表，自动转义单引号</summary>
    /// <param name="names">名称集合</param>
    /// <returns>逗号分隔的带引号列表；没有有效名称时返回空字符串</returns>
    public static String EscapeNames(String[]? names)
    {
        if (names == null || names.Length == 0) return "";

        var ns = names.Where(e => !e.IsNullOrEmpty()).Select(e => "'" + e.Replace("'", "''") + "'");

        return String.Join(",", ns);
    }

    /// <summary>取字符串字段类型。长度未知或超过长文本阈值时使用长文本类型</summary>
    /// <param name="field">字段</param>
    /// <param name="longTextLength">长文本阈值</param>
    /// <param name="longType">长文本类型</param>
    /// <param name="shortType">短文本类型</param>
    /// <returns></returns>
    public static String GetStringFieldType(IDataColumn field, Int32 longTextLength, String longType, String shortType = "VARCHAR")
        => field.Length <= 0 || field.Length >= longTextLength ? longType : $"{shortType}({field.Length})";
}
