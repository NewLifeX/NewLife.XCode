using System.Collections;

namespace XCode;

public partial class ValidHelper
{
    /// <summary>
    /// 将给定的对象转换为字符串数组。
    /// </summary>
    /// <param name="value">要转换的对象。</param>
    public static String[] ToStringArray(object? value)
    {
        if (value == null) return Empty<String>();
        if (Convert.IsDBNull(value)) return Empty<String>();
        if (value is String[] arr) return arr;
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

        return Empty<String>();
    }
}
