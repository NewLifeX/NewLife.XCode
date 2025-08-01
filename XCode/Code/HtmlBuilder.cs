using System.Text;
using NewLife;
using NewLife.Log;
using XCode.DataAccessLayer;

namespace XCode.Code;

/// <summary>Html数据字典生成器</summary>
public class HtmlBuilder : ClassBuilder
{
    #region 属性
    /// <summary>样式</summary>
    public String Style { get; set; } = @"table {
        border-collapse: collapse;
        border: 1px solid;
        border-color: rgb(211, 202, 221);
    }

    table thead,
    table tr {
        border-top-width: 1px;
        border-top-style: solid;
        border-top-color: rgb(211, 202, 221);
    }

    table {
        border-bottom-width: 1px;
        border-bottom-style: solid;
        border-bottom-color: rgb(211, 202, 221);
    }

    table td,
    table th {
        padding: 5px 10px;
        font-size: 14px;
        font-family: Verdana;
        color: rgb(95, 74, 121);
    }

    table tr:nth-child(even) {
        background: rgb(223, 216, 232)
    }

    table tr:nth-child(odd) {
        background: #FFF
    }";
    #endregion

    #region 静态
    /// <summary>生成数据字典</summary>
    /// <param name="tables">表集合</param>
    /// <param name="option">可选项</param>
    /// <param name="log"></param>
    /// <returns></returns>
    public static Int32 BuildDataDictionary(IList<IDataTable> tables, BuilderOption option = null, ILog log = null)
    {
        if (option == null)
            option = new BuilderOption();
        else
            option = option.Clone();

        var file = (option as EntityBuilderOption)?.ConnName;
        if (file.IsNullOrEmpty()) file = "Model";
        file += ".htm";
        file = file.GetBasePath();

        log?.Info("生成数据字典 {0}", file);

        var count = 0;
        var writer = new StringWriter();

        // 样式
        {
            var builder = new HtmlBuilder { Writer = writer };
            if (!builder.Style.IsNullOrEmpty())
            {
                builder.WriteLine("<style>");
                builder.WriteLine(builder.Style);
                builder.WriteLine("</style>");
            }
        }

        foreach (var item in tables)
        {
            // 跳过排除项
            if (option.Excludes.Contains(item.Name)) continue;
            if (option.Excludes.Contains(item.TableName)) continue;

            var builder = new HtmlBuilder
            {
                Writer = writer,
                Table = item,
                Option = option.Clone(),
                Log = log
            };

            builder.Load(item);

            // 执行生成
            builder.Execute();
            //builder.Save(null, true, false);

            count++;
        }

        // 输出到文件
        File.WriteAllText(file, writer.ToString(), Encoding.UTF8);

        return count;
    }
    #endregion

    #region 方法
    /// <summary>生成前</summary>
    protected override void OnExecuting()
    {
        if (Table.DisplayName.IsNullOrEmpty())
            WriteLine("<h3>{0}</h3>", Table.TableName);
        else
            WriteLine("<h3>{0}（{1}）</h3>", Table.DisplayName, Table.TableName);

        WriteLine("<table>");
        {
            WriteLine("<thead>");
            WriteLine("<tr>");
            WriteLine("<th>名称</th>");
            WriteLine("<th>显示名</th>");
            WriteLine("<th>类型</th>");
            WriteLine("<th>长度</th>");
            WriteLine("<th>精度</th>");
            WriteLine("<th>主键</th>");
            WriteLine("<th>允许空</th>");
            WriteLine("<th>备注</th>");
            WriteLine("</tr>");
            WriteLine("</thead>");
        }
        WriteLine("<tbody>");
    }

    /// <summary>生成后</summary>
    protected override void OnExecuted()
    {
        WriteLine("</tbody>");
        WriteLine("</table>");

        WriteLine("<br></br>");
    }

    /// <summary>生成主体</summary>
    protected override void BuildItems()
    {
        for (var i = 0; i < Table.Columns.Count; i++)
        {
            var column = Table.Columns[i];

            // 跳过排除项
            if (!ValidColumn(column)) continue;

            if (i > 0) WriteLine();
            BuildItem(column);
        }
    }

    /// <summary>生成项</summary>
    /// <param name="column"></param>
    protected override void BuildItem(IDataColumn column)
    {
        WriteLine("<tr>");
        {
            WriteLine("<td>{0}</td>", column.ColumnName);
            WriteLine("<td>{0}</td>", column.DisplayName);
            WriteLine("<td>{0}</td>", column.RawType ?? column.DataType?.FullName.TrimStart("System."));

            if (column.Length > 0)
                WriteLine("<td>{0}</td>", column.Length);
            else
                WriteLine("<td></td>");

            var def = ModelHelper.FixDefaultByType(column.Clone(column.Table), column);
            if (column.Precision > 0 && column.Precision != def.Precision || column.Scale > 0 && column.Scale != def.Scale)
                WriteLine("<td>({0}, {1})</td>", column.Precision, column.Scale);
            else
                WriteLine("<td></td>");

            if (column.Identity)
                WriteLine("<td title=\"自增\">AI</td>");
            else if (column.PrimaryKey)
                WriteLine("<td title=\"主键\">PK</td>");
            else if (Table.Indexes.Any(e => e.Unique && e.Columns.Length == 1 && e.Columns[0].EqualIgnoreCase(column.Name, column.ColumnName)))
                WriteLine("<td title=\"唯一索引\">UQ</td>");
            else
                WriteLine("<td></td>");

            WriteLine("<td>{0}</td>", column.Nullable ? "" : "N");
            WriteLine("<td>{0}</td>", column.Description?.TrimStart(column.DisplayName).TrimStart("。", "，"));
        }
        WriteLine("</tr>");
    }
    #endregion

    #region 辅助
    /// <summary>写入</summary>
    /// <param name="value"></param>
    protected override void WriteLine(String value = null)
    {
        if (!value.IsNullOrEmpty() && value.Length > 2 && value[0] == '<' && value[1] == '/') SetIndent(false);

        base.WriteLine(value);

        if (!value.IsNullOrEmpty() && value.Length > 2 && value[0] == '<' && value[1] != '/' && !value.Contains("</")) SetIndent(true);
    }
    #endregion
}