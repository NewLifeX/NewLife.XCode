using System.Collections;

namespace XCode;

/// <summary>验证助手 - Array 部分</summary>
/// <remarks>
/// Array 部分的实现，先尽量保持简洁。
/// 后续更强大的转换，再补充。
/// </remarks>
public partial class ValidHelper
{
    /// <summary>将对象转换为指定类型的数组</summary>
    /// <typeparam name="T">目标元素类型</typeparam>
    /// <param name="value">要转换的对象</param>
    /// <param name="converter">元素转换器</param>
    /// <returns>转换后的数组，若转换失败返回 null</returns>
    private static T[]? ToArray<T>(Object? value, Func<Object?, T> converter)
    {
        if (value is T[] arr) return arr;
        if (value is null || Convert.IsDBNull(value)) return default;
        if (value is IEnumerable<T> list) return list.ToArray();
        if (value is T v) return [v];
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
    /// <summary>将对象转换为 Int32 数组</summary>
    /// <param name="value">要转换的对象</param>
    /// <returns>Int32 数组，若转换失败返回 null</returns>
    public static Int32[]? ToInt32Array(Object? value) => ToArray(value, ToInt32);

    /// <summary>将对象转换为 Int64 数组</summary>
    /// <param name="value">要转换的对象</param>
    /// <returns>Int64 数组，若转换失败返回 null</returns>
    public static Int64[]? ToInt64Array(Object? value) => ToArray(value, ToInt64);

    /// <summary>将对象转换为 Double 数组</summary>
    /// <param name="value">要转换的对象</param>
    /// <returns>Double 数组，若转换失败返回 null</returns>
    public static Double[]? ToDoubleArray(Object? value) => ToArray(value, ToDouble);

    /// <summary>将对象转换为 Boolean 数组</summary>
    /// <param name="value">要转换的对象</param>
    /// <returns>Boolean 数组，若转换失败返回 null</returns>
    public static Boolean[]? ToBooleanArray(Object? value) => ToArray(value, ToBoolean);

    /// <summary>将对象转换为 DateTime 数组</summary>
    /// <param name="value">要转换的对象</param>
    /// <returns>DateTime 数组，若转换失败返回 null</returns>
    public static DateTime[]? ToDateTimeArray(Object? value) => ToArray(value, ToDateTime);

    /// <summary>将对象转换为 String 数组</summary>
    /// <param name="value">要转换的对象</param>
    /// <returns>String 数组，若转换失败返回 null</returns>
    public static String[]? ToStringArray(Object? value) => ToArray(value, ToString);

    /// <summary>将对象转换为 Byte 数组</summary>
    /// <param name="value">要转换的对象</param>
    /// <returns>Byte 数组，若转换失败返回 null</returns>
    public static Byte[]? ToByteArray(Object? value) => ToArray(value, ToByte);

    /// <summary>将对象转换为 Decimal 数组</summary>
    /// <param name="value">要转换的对象</param>
    /// <returns>Decimal 数组，若转换失败返回 null</returns>
    public static Decimal[]? ToDecimalArray(Object? value) => ToArray(value, ToDecimal);

    /// <summary>将对象转换为 Int16 数组</summary>
    /// <param name="value">要转换的对象</param>
    /// <returns>Int16 数组，若转换失败返回 null</returns>
    public static Int16[]? ToInt16Array(Object? value) => ToArray(value, ToInt16);

    /// <summary>将对象转换为 UInt64 数组</summary>
    /// <param name="value">要转换的对象</param>
    /// <returns>UInt64 数组，若转换失败返回 null</returns>
    public static UInt64[]? ToUInt64Array(Object? value) => ToArray(value, ToUInt64);

    /// <summary>将对象转换为指定枚举类型的数组</summary>
    /// <typeparam name="T">目标枚举类型</typeparam>
    /// <param name="value">要转换的对象</param>
    /// <returns>枚举类型数组，若转换失败返回 null</returns>
    public static T[]? ToEnumArray<T>(Object? value) where T : struct => ToArray(value, ToEnum<T>);

    /// <summary>将对象转换为指定引用类型的数组</summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="value">要转换的对象</param>
    /// <returns>引用类型数组，若转换失败返回 null</returns>
    public static T[]? ToObjectArray<T>(Object? value) where T : class => ToArray(value, ToObject<T>);
}
