using NewLife.Reflection;
using XCode.Configuration;
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
                if (dc.Master) continue;

                // 如果显式设置里指定了搜索，则按指定的来
                if (!dc.ShowIn.IsNullOrEmpty())
                {
                    var opt = ShowInOption.Parse(dc.ShowIn);
                    if (opt.Search == TriState.Hide) continue;
                }

                if (dc.Name.EqualIgnoreCase(dcs) || dc.ColumnName.EqualIgnoreCase(dcs)) cs.Add(dc);
            }
        }

        // 特殊字段
        foreach (var dc in Table.Columns)
        {
            if (cs.Contains(dc)) continue;

            // 如果显式设置里指定了搜索，则按指定的来
            if (!dc.ShowIn.IsNullOrEmpty())
            {
                var opt = ShowInOption.Parse(dc.ShowIn);
                if (opt.Search == TriState.Show)
                {
                    cs.Add(dc);
                    continue;
                }
                else if (opt.Search == TriState.Hide)
                {
                    continue;
                }
            }

            // 数据时间字段可用于搜索
            if (!dc.DataScale.IsNullOrEmpty())
                cs.Add(dc);
            // 整型
            else if (dc.DataType.IsInt())
            {
                // 整型枚举
                if (dc.DataType.IsEnum)
                    cs.Add(dc);
                else if (!dc.Properties["Type"].IsNullOrEmpty())
                    cs.Add(dc);
                // 整型有映射
                else if (!dc.Map.IsNullOrEmpty())
                    cs.Add(dc);
            }
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

    ///// <summary>获取参数列表。名称+类型</summary>
    ///// <param name="columns"></param>
    ///// <param name="includeTime"></param>
    ///// <param name="includeKey"></param>
    ///// <param name="includePage"></param>
    ///// <returns></returns>
    //public IDictionary<String, String> GetParameters(IList<IDataColumn> columns, Boolean includeTime = false, Boolean includeKey = false, Boolean includePage = false)
    //{
    //    var ps = new Dictionary<String, String>();
    //    foreach (var dc in columns)
    //    {
    //        var type = dc.Properties["Type"];
    //        if (type.IsNullOrEmpty()) type = dc.DataType?.Name + "";

    //        if (dc.DataType == typeof(Boolean))
    //            type += "?";
    //        else if (dc.DataType == typeof(String))
    //        {
    //            if (Nullable && dc.Nullable)
    //            {
    //                type += "?";
    //            }
    //        }

    //        //var p = type.LastIndexOf('.');
    //        //if (p > 0) type = type[(p + 1)..];
    //        ps[dc.CamelName()] = type;
    //    }

    //    if (includeTime && DataTime != null)
    //    {
    //        ps["start"] = "DateTime";
    //        ps["end"] = "DateTime";
    //    }
    //    if (includeKey)
    //        ps["key"] = "String";
    //    if (includePage)
    //        ps["page"] = "PageParameter";

    //    return ps;
    //}

    private static readonly HashSet<String> _reservedNames = new(StringComparer.Ordinal)
    {
        // 关键字
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while",
        // 上下文关键字
        "add", "alias", "and", "ascending", "async", "await", "by", "descending", "dynamic",
        "equals", "file", "from", "get", "global", "group", "init", "into", "join", "let",
        "nameof", "nint", "not", "notnull", "nuint", "on", "or", "orderby", "partial", "record",
        "remove", "required", "scoped", "select", "set", "unmanaged", "value", "var", "when",
        "where", "with", "yield"
    };
    /// <summary>获取参数列表。名称+类型(全名+简名)</summary>
    /// <param name="columns"></param>
    /// <param name="extend"></param>
    /// <returns></returns>
    public IList<ParameterModel> GetParameters(IList<IDataColumn> columns, Boolean extend = false)
    {
        var ps = new List<ParameterModel>();
        foreach (var dc in columns)
        {
            var type = dc.Properties["Type"];
            if (type.IsNullOrEmpty()) type = dc.DataType?.Name + "";

            if (dc.DataType == typeof(Boolean))
                type += "?";
            else if (dc.DataType == typeof(String) && Nullable && dc.Nullable)
                type += "?";

            var model = new ParameterModel
            {
                Name = dc.Name,
                ParameterName = dc.CamelName(),
                TypeName = type,
                TypeFullName = type,
                DisplayName = dc.DisplayName,
                Description = dc.Description,
            };

            // 处理保留字
            if (_reservedNames.Contains(model.ParameterName)) model.ParameterName = "@" + model.ParameterName;

            var p = type.LastIndexOf('.');
            if (p > 0) model.TypeName = type[(p + 1)..];

            ps.Add(model);
        }

        if (extend)
        {
            if (DataTime != null)
            {
                ps.Add(new ParameterModel { Name = "start", ParameterName = "start", TypeName = "DateTime", TypeFullName = "DateTime", Extend = true });
                ps.Add(new ParameterModel { Name = "end", ParameterName = "end", TypeName = "DateTime", TypeFullName = "DateTime", Extend = true });
            }
            ps.Add(new ParameterModel { Name = "key", ParameterName = "key", TypeName = "String", TypeFullName = "String", Extend = true });
            ps.Add(new ParameterModel { Name = "page", ParameterName = "page", TypeName = "PageParameter", TypeFullName = "PageParameter", Extend = true });
        }

        return ps;
    }
    #endregion
}
