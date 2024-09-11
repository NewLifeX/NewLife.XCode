using System.Collections;

namespace XCode;

/// <remarks>
/// Array 部分的实现，先尽量保持简洁。
/// 后续更强大的转换，再补充。
/// </remarks>
public partial class ValidHelper
{
    /// <summary>
    /// 将给定的对象转换为字符串数组。
    /// </summary>
    /// <param name="value">要转换的对象。</param>
    public static String[]? ToStringArray(object? value)
    {
        if (value is String[] arr) return arr;
        if (value == null || Convert.IsDBNull(value)) return default;
        if (value is IEnumerable<string> list) return list.ToArray();
        if (value is string str) return new string[] { str };
        if (value is IEnumerable)
        {
            var ret = new List<String>();
            foreach (var item in (IEnumerable)value)
            {
                ret.Add(Convert.ToString(item));
            }
            return ret.ToArray();
        }
        return default;
    }
}
