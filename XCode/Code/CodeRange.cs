using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XCode.Code;

internal class CodeRange
{
    public Int32 Start { get; set; }

    public Int32 Count { get; set; }

    public IList<MemberSection> Sections { get; set; } = [];

    public static CodeRange? Find(IList<String> lines, String start, String end)
    {
        var s = -1;
        var e = -1;
        var flag = 0;
        for (var i = 0; i < lines.Count && flag < 2; i++)
        {
            if (flag == 0)
            {
                if (lines[i].Contains(start))
                {
                    s = i;
                    flag = 1;
                }
            }
            else if (flag == 1)
            {
                if (lines[i].Contains(end))
                {
                    e = i;
                    flag = 2;
                }
            }
        }

        if (s < 0 || e < 0) return null;

        var ns = lines.Skip(s).Take(e - s + 1).ToArray();
        var list = MemberSection.Parse(ns);
        foreach (var item in list)
        {
            item.StartLine += s;
        }

        return new CodeRange { Start = s, Count = e - s + 1, Sections = list };
    }
}
