using System.Text.RegularExpressions;

namespace XCode.Configuration;

/// <summary>三态枚举</summary>
public enum TriState
{
    /// <summary>自动</summary>
    Auto = 0,
    /// <summary>显示</summary>
    Show = 1,
    /// <summary>隐藏</summary>
    Hide = 2,
}

/// <summary>显示位置选项。五区三态组合解析器</summary>
/// <remarks>
/// 这里给出一个“单字段 + 多种友好语法 + 三态语义”的方案，满足“充分表达 + 简单易用 + 兼容默认自动”的目标。
/// 目标
/// •	在 Column 上新增一个 ShowIn 属性（单字段），表达该列在以下 5 个区域的显示策略：
/// •	List、Search、AddForm、EditForm、Detail
/// •	三态语义：Show/Hide/Auto（显示/不显示/系统自动）
/// •	未指定的部分均为 Auto，不改变现有“系统自动决定”的行为
/// 属性设计
/// •	新增属性：ShowIn（String，可选）
/// •	默认值：未设置时等同于“全部 Auto”，保留原系统自动判断
/// •	语义：每个区域为三态之一（Y/N/A；或 Show/Hide/Auto）
/// 推荐的三种等价输入语法（任意选择）
/// 1.	具名列表（最直观，推荐）
/// •	规则：逗号分隔，支持别名；无前缀=显式显示，- 前缀=显式隐藏；未出现=自动
/// •	支持宏：All、None、Auto（先应用宏，再按顺序应用后续项实现覆盖）
/// •	区域别名：List(L)、Detail(D)、AddForm(Add)、EditForm(Edit)、Search(S)
/// •	示例：
/// •	ShowIn="List,Search" → List=Show, Search=Show，其它=Auto
/// •	ShowIn="-EditForm,-Detail" → EditForm=Hide, Detail=Hide，其它=Auto
/// •	ShowIn="All,-Detail" → 先全部 Show，再将 Detail 设为 Hide
/// •	ShowIn="None,Search,Add" → 先全部 Hide，再将 Search、AddForm 设为 Show
/// •	ShowIn="Auto" → 全部 Auto（等同不写）
/// 2.	管道 5 段（紧凑、可读）
/// •	规则：Y/N/A 分别为 显示/不显示/自动；空白等同 A
/// •	顺序：List|Detail|AddForm|EditForm|Search
/// •	示例：
/// •	ShowIn="Y|Y|N||A" → 列表/明细 显示；添加 不显示；表单 自动；搜索 自动
/// •	ShowIn="|||N|" → 仅 EditForm=Hide，其它 Auto
/// 3.	5 字符掩码（最简）
/// •	规则：5 字符，顺序同上；1=Show，0=Hide，A/?/-=Auto
/// •	示例：
/// •	ShowIn="110A?" → List=Show, Detail=Auto, AddForm=Hide, EditForm=Auto, Search=Show
/// •	ShowIn="-----" 或 "AAAAA" → 全 Auto
/// 解析与优先级
/// •	ShowIn 为空：全部 Auto
/// •	若包含 |：按“管道 5 段”解析
/// •	否则若长度=5 且匹配 [10A?-]{5}：按“5 字符掩码”解析
/// •	否则按“具名列表 + 宏”解析（顺序覆盖）
/// •	未指定的部分均为 Auto，由系统原有规则决定
/// </remarks>
public struct ShowInOption
{
    /// <summary>列表页</summary>
    public TriState List { get; set; }

    /// <summary>明细页</summary>
    public TriState Detail { get; set; }

    /// <summary>添加表单</summary>
    public TriState AddForm { get; set; }

    /// <summary>编辑表单</summary>
    public TriState EditForm { get; set; }

    /// <summary>搜索区</summary>
    public TriState Search { get; set; }

    /// <summary>默认全 Auto</summary>
    public static ShowInOption AutoAll => new()
    {
        List = TriState.Auto,
        Detail = TriState.Auto,
        AddForm = TriState.Auto,
        EditForm = TriState.Auto,
        Search = TriState.Auto,
    };

    /// <summary>解析字符串</summary>
    public static ShowInOption Parse(String? text)
    {
        if (text.IsNullOrWhiteSpace()) return AutoAll;

        text = text.Trim();

        // 管道 5 段：Y/N/A/空
        if (text.Contains("|"))
        {
            var segs = (text.Split('|').Select(s => s.Trim()).ToArray());
            Array.Resize(ref segs, 5);
            return new ShowInOption
            {
                List = ParseYN(segs[0]),
                Detail = ParseYN(segs[1]),
                AddForm = ParseYN(segs[2]),
                EditForm = ParseYN(segs[3]),
                Search = ParseYN(segs[4]),
            };
        }

        // 5 字符掩码：1/0/A/?/-
        if (Regex.IsMatch(text, "^[10A\\?\\-]{5}$", RegexOptions.IgnoreCase))
        {
            return new ShowInOption
            {
                List = ParseMask(text[0]),
                Detail = ParseMask(text[1]),
                AddForm = ParseMask(text[2]),
                EditForm = ParseMask(text[3]),
                Search = ParseMask(text[4]),
            };
        }

        // 具名列表 + 宏。顺序覆盖
        var opt = AutoAll;
        var tokens = text.Split(',').Select(e => e.Trim()).Where(e => e.Length > 0);
        foreach (var tk in tokens)
        {
            // 宏
            if (tk.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                opt = AutoAll;
                continue;
            }
            if (tk.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                opt = new ShowInOption
                {
                    List = TriState.Show,
                    Detail = TriState.Show,
                    AddForm = TriState.Show,
                    EditForm = TriState.Show,
                    Search = TriState.Show,
                }; continue;
            }
            if (tk.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                opt = new ShowInOption
                {
                    List = TriState.Hide,
                    Detail = TriState.Hide,
                    AddForm = TriState.Hide,
                    EditForm = TriState.Hide,
                    Search = TriState.Hide,
                }; continue;
            }

            // 单项：前缀
            var hide = tk.StartsWith("-") || tk.StartsWith("!");
            var name = hide || tk.StartsWith("+") ? tk.Substring(1) : tk;

            var state = hide ? TriState.Hide : TriState.Show;
            switch (name.ToLowerInvariant())
            {
                case "list":
                case "l": opt.List = state; break;
                case "detail":
                case "d": opt.Detail = state; break;
                case "add":
                case "addform":
                case "a": opt.AddForm = state; break;
                case "edit":
                case "editform":
                case "e": opt.EditForm = state; break;
                case "form":
                case "f": opt.AddForm = state; opt.EditForm = state; break;
                case "search":
                case "s": opt.Search = state; break;
            }
        }
        return opt;

        static TriState ParseYN(String? s)
        {
            if (s.IsNullOrEmpty()) return TriState.Auto;
            return s.Equals("Y", StringComparison.OrdinalIgnoreCase) ? TriState.Show :
                   s.Equals("N", StringComparison.OrdinalIgnoreCase) ? TriState.Hide : TriState.Auto;
        }

        static TriState ParseMask(Char c)
        {
            return c is '1' or 'Y' or 'y' ? TriState.Show :
                   c is '0' or 'N' or 'n' ? TriState.Hide : TriState.Auto;
        }
    }
}