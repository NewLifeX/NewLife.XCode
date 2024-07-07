using System.Text.RegularExpressions;
using NewLife;

namespace XCode.Code;

/// <summary>成员段。一个属性或方法的代码段</summary>
internal class MemberSection
{
    #region 属性
    /// <summary>名称。成员名或方法签名</summary>
    public String Name { get; set; } = null!;

    /// <summary>全名</summary>
    public String FullName { get; set; } = null!;

    /// <summary>开始行行号</summary>
    public Int32 StartLine { get; set; }

    /// <summary>代码行</summary>
    public String[] Lines { get; set; } = [];
    #endregion

    #region 方法
    /// <summary>从来源分解出多个成员段</summary>
    /// <param name="lines"></param>
    /// <returns></returns>
    public static IList<MemberSection> Parse(IList<String> lines)
    {
        var list = new List<MemberSection>();

        // 以空行为界，分解代码段
        var name = "";
        var p = -1;
        var status = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i]?.Trim();
            if (status == -1)
            {
                // 遇到空行，代码段开始
                if (line.IsNullOrEmpty() || IsStart(line))
                {
                    status = 0;
                }
            }
            else if (status == 0)
            {
                // 空行后的#，不认为是开始
                if (line != null && line.StartsWith("#region"))
                    status = 0;
                else
                {
                    var pre = lines[i - 1]?.Trim();
                    if (pre.IsNullOrEmpty() || pre.StartsWith("#region"))
                        p = i;
                    else
                        p = i - 1;
                    status = 1;

                    if (line != null) name = GetName(line);
                }
            }
            else if (status >= 1)
            {
                // 遇到大括号，控制缩进
                if (line == "{")
                    status++;
                // 右大括号，减少缩进。有些代码风格并没有把左大括号放在独立一行
                else if (line != null && line.StartsWith("}") && status > 1)
                    status--;
                // 遇到空行，代码段结束
                else if (status == 1 && (line.IsNullOrEmpty() || IsStart(line) || line.EndsWith("#endregion")))
                {
                    var ms = Create(name, p, lines.Skip(p).Take(i - p).ToArray());
                    if (ms != null) list.Add(ms);

                    status = line.IsNullOrEmpty() || IsStart(line) ? 0 : -1;
                    name = null;
                }
                // 最后一行，也要结束
                else if (status == 1 && i == lines.Count - 1)
                {
                    var ms = Create(name, p, lines.Skip(p).Take(i - p + 1).ToArray());
                    if (ms != null) list.Add(ms);

                    status = -1;
                    name = null;
                }
                else
                {
                    if (name.IsNullOrEmpty() && line != null) name = GetName(line);
                }
            }
        }

        return list;
    }

    static Boolean IsStart(String line) => line.StartsWith("/// <summary>") || line.StartsWith("///// <summary>") || line.StartsWith("#region");

    static MemberSection? Create(String? name, Int32 p, String[] lines)
    {
        // 名称为空，可能被注释了
        if (name.IsNullOrEmpty())
        {
            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("//"))
                {
                    name = GetName(line.Trim().Substring(2));
                    if (!name.IsNullOrEmpty()) break;
                }
            }
        }
        if (name.IsNullOrEmpty()) return null;

        var key = name;
        var pKey = key.IndexOf('(');
        if (pKey > 0) key = key.Substring(0, pKey);

        var ms = new MemberSection
        {
            Name = key,
            FullName = name,
            StartLine = p,
            Lines = lines
        };

        return ms;
    }

    static String? GetName(String line)
    {
        if (!line.StartsWithIgnoreCase("public ", "protected ", "private ", "internal ", "static "))
            return null;

        var str = line;

        // 去掉修饰符
        var p2 = str.IndexOf(' ');
        if (p2 > 0) str = str.Substring(p2 + 1);
        str = str.TrimStart("static ", "readonly ");

        // 去掉参数
        p2 = str.IndexOfAny(['{', '=']);
        str = p2 > 0 ? str.Substring(0, p2).Trim() : str;

        // 去掉返回值
        p2 = str.IndexOf(' ');
        str = p2 > 0 ? str.Substring(p2 + 1).Trim() : str;

        return str;
    }

    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String ToString() => $"{Name} [{Lines?.Length}]";
    #endregion

    #region 辅助
    /// <summary>获取方法以及签名</summary>
    /// <param name="txt"></param>
    /// <returns></returns>
    public static IList<MemberSection> GetMethods(String txt)
    {
        var rs = new List<MemberSection>();
        if (txt.IsNullOrEmpty()) return rs;

        var reg = new Regex(@"([\w\<\>,]+)\s(\w+)\(([^\)]*)\)\s*(?://)*(?:{|=>)");
        foreach (Match item in reg.Matches(txt))
        {
            var types = new List<String>();
            foreach (var elm in item.Groups[3].Value.Split(","))
            {
                var str = elm.Trim();
                var p = str.IndexOf(' ');
                if (p > 0)
                {
                    str = str[..p];
                    p = str.LastIndexOf('.');
                    if (p > 0) str = str[(p + 1)..];

                    types.Add(str);
                }
            }
            rs.Add(new MemberSection
            {
                Name = item.Groups[2].Value,
                FullName = $"{item.Groups[2].Value}({types.Join(",")})",
            });
        }

        return rs;
    }
    #endregion
}