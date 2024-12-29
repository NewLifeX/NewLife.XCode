using NewLife;
using NewLife.Reflection;
using XCode.DataAccessLayer;

namespace XCode.Code;

/// <summary>搜索功能代码生成器</summary>
public class SearchBuilder(IDataTable table)
{
    #region 属性
    /// <summary>数据表</summary>
    public IDataTable Table { get; set; } = table;

    /// <summary>可为null上下文。生成String?等</summary>
    public Boolean Nullable { get; set; }

    /// <summary>数据时间字段</summary>
    public IDataColumn? DataTime { get; set; }
    #endregion

    #region 构造
    #endregion

    #region 方法
    /// <summary>获取可用于搜索的字段列表</summary>
    /// <returns></returns>
    public IList<IDataColumn> GetColumns()
    {
        // 收集索引信息，索引中的所有字段都参与，构造一个高级查询模板
        var idx = Table.Indexes ?? [];
        var cs = new List<IDataColumn>();
        if (idx != null && idx.Count > 0)
        {
            // 索引中的所有字段，按照表字段顺序
            var dcs = idx.SelectMany(e => e.Columns).Distinct().ToArray();
            foreach (var dc in Table.Columns)
            {
                // 主键和自增，不参与
                if (dc.PrimaryKey || dc.Identity) continue;

                if (dc.Name.EqualIgnoreCase(dcs) || dc.ColumnName.EqualIgnoreCase(dcs)) cs.Add(dc);
            }
        }

        // 特殊字段
        foreach (var dc in Table.Columns)
        {
            if (cs.Contains(dc)) continue;

            // 数据时间字段可用于搜索
            if (!dc.DataScale.IsNullOrEmpty())
                cs.Add(dc);
            // 整型枚举
            if (dc.DataType.IsInt() && dc.DataType.IsEnum)
                cs.Add(dc);
            // 整型有映射
            else if (dc.DataType.IsInt() && !dc.Map.IsNullOrEmpty())
                cs.Add(dc);
            // 布尔型
            else if (dc.DataType == typeof(Boolean) && !dc.Name.EqualIgnoreCase("enable", "isDeleted"))
                cs.Add(dc);
        }
        // enable和isDeleted字段放在最后
        foreach (var dc in Table.Columns)
        {
            if (cs.Contains(dc)) continue;

            if (dc.Name.EqualIgnoreCase("enable", "isDeleted") && dc.DataType == typeof(Boolean))
                cs.Add(dc);
        }

        if (cs.Count == 0) return [];

        // 时间字段。无差别支持UpdateTime/CreateTime
        var dcTime = cs.FirstOrDefault(e => e.DataScale.StartsWithIgnoreCase("time"));
        dcTime ??= cs.FirstOrDefault(e => e.DataType == typeof(DateTime));
        dcTime ??= Table.GetColumns(["UpdateTime", "CreateTime"])?.FirstOrDefault();
        var dcSnow = cs.FirstOrDefault(e => e.PrimaryKey && !e.Identity && e.DataType == typeof(Int64));

        if (dcTime != null) cs.Remove(dcTime);
        cs.RemoveAll(e => e.Name.EqualIgnoreCase("key", "page"));
        if (dcSnow != null || dcTime != null)
            cs.RemoveAll(e => e.Name.EqualIgnoreCase("start", "end"));

        DataTime = dcSnow ?? dcTime;

        return cs;
    }

    /// <summary>获取参数列表。名称+类型</summary>
    /// <param name="columns"></param>
    /// <param name="includeTime"></param>
    /// <param name="includeKey"></param>
    /// <param name="includePage"></param>
    /// <returns></returns>
    public IDictionary<String, String> GetParameters(IList<IDataColumn> columns, Boolean includeTime = false, Boolean includeKey = false, Boolean includePage = false)
    {
        var ps = new Dictionary<String, String>();
        foreach (var dc in columns)
        {
            var type = dc.Properties["Type"];
            if (type.IsNullOrEmpty()) type = dc.DataType?.Name + "";

            if (dc.DataType == typeof(Boolean))
                type += "?";
            else if (dc.DataType == typeof(String))
            {
                if (Nullable && dc.Nullable)
                {
                    type += "?";
                }
            }

            var p = type.LastIndexOf('.');
            if (p > 0) type = type[(p + 1)..];
            ps[dc.CamelName()] = type;
        }

        if (includeTime && DataTime != null)
        {
            ps["start"] = "DateTime";
            ps["end"] = "DateTime";
        }
        if (includeKey)
            ps["key"] = "String";
        if (includePage)
            ps["page"] = "PageParameter";

        return ps;
    }
    #endregion
}
