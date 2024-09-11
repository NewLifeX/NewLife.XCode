using System.Collections;

namespace XCode;

/// <remarks>
/// Array 部分的实现，先尽量保持简洁。
/// 后续更强大的转换，再补充。
/// </remarks>
public partial class ValidHelper
{
    private static T[]? ToArray<T>(Object? value, Func<object?, T> converter)
    {
        if (value is T[] arr) return arr;
        if (value is null || Convert.IsDBNull(value)) return default;
        if (value is IEnumerable<T> list) return list.ToArray();
        if (value is T v) return new T[] { v };
        if (value is IEnumerable)
        {
            var ret = new List<T>();
            foreach (var item in (IEnumerable)value)
            {
                ret.Add(converter.Invoke(item));
            }
            return ret.ToArray();
        }
        return default;
    }
    public static Int32[]? ToInt32Array(Object? value) => ToArray(value, ToInt32);
    public static Int64[]? ToInt64Array(Object? value) => ToArray(value, ToInt64);
    public static Double[]? ToDoubleArray(Object? value) => ToArray(value, ToDouble);
    public static Boolean[]? ToBooleanArray(Object? value) => ToArray(value, ToBoolean);
    public static DateTime[]? ToDateTimeArray(Object? value) => ToArray(value, ToDateTime);
    public static String[]? ToStringArray(Object? value) => ToArray(value, ToString);
    public static Byte[]? ToByteArray(Object? value) => ToArray(value, ToByte);
    public static Decimal[]? ToDecimalArray(Object? value) => ToArray(value, ToDecimal);
    public static Int16[]? ToInt16Array(Object? value) => ToArray(value, ToInt16);
    public static UInt64[]? ToUInt64Array(Object? value) => ToArray(value, ToUInt64);
    public static T[]? ToEnumArray<T>(Object? value) where T : struct => ToArray(value, ToEnum<T>);
    public static T[]? ToObjectArray<T>(Object? value) where T : class => ToArray(value, ToObject<T>);
}
