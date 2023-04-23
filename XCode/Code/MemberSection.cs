using NewLife;

namespace XCode.Code;

/// <summary>成员段。一个属性或方法的代码段</summary>
internal class MemberSection
{
    #region 属性
    /// <summary>名称。成员名或方法签名</summary>
    public String Name { get; set; }

    /// <summary>开始行行号</summary>
    public Int32 StartLine { get; set; }

    /// <summary>代码行</summary>
    public String[] Lines { get; set; }
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
                if (line.IsNullOrEmpty() || line.StartsWith("/// <summary>"))
                {
                    status = 0;
                }
            }
            else if (status == 0)
            {
                // 空行后的#，不认为是开始
                if (line.StartsWith("#region"))
                    status = -1;
                else
                {
                    p = i - 1;
                    status = 1;
                }
            }
            else if (status >= 1)
            {
                // 遇到大括号，控制缩进
                if (line == "{")
                    status++;
                // 右大括号，减少缩进。有些代码风格并没有把左大括号放在独立一行
                else if (line == "}" && status > 1)
                    status--;
                // 遇到空行，代码段结束
                else if (status == 1 && (line.IsNullOrEmpty() || line.StartsWith("/// <summary>") || line.EndsWith("#endregion")))
                {
                    var ms = new MemberSection
                    {
                        Name = name,
                        StartLine = p,
                        Lines = lines.Skip(p).Take(i - p).ToArray()
                    };
                    list.Add(ms);

                    status = line.StartsWith("/// <summary>") ? 0 : -1;
                    name = null;
                }
                // 最后一行，也要结束
                else if (status == 1 && i == lines.Count - 1)
                {
                    var ms = new MemberSection
                    {
                        Name = name,
                        StartLine = p,
                        Lines = lines.Skip(p).Take(i - p + 1).ToArray()
                    };
                    list.Add(ms);

                    status = -1;
                    name = null;
                }
                else
                {
                    if (line.StartsWithIgnoreCase("public ", "protect ", "private ", "internal ", "static "))
                    {
                        var str = line;

                        // 去掉修饰符
                        var p2 = str.IndexOf(' ');
                        if (p2 > 0) str = str.Substring(p2 + 1);
                        str = str.TrimStart("static ");

                        // 去掉参数
                        p2 = str.IndexOfAny(new[] { '{', '=' });
                        str = p2 > 0 ? str.Substring(0, p2).Trim() : str;

                        // 去掉返回值
                        p2 = str.IndexOf(' ');
                        str = p2 > 0 ? str.Substring(p2 + 1).Trim() : str;

                        name = str;
                    }
                }
            }
        }

        return list;
    }

    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String ToString() => $"{Name} [{Lines?.Length}]";
    #endregion
}