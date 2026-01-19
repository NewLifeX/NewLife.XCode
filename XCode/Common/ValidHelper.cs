using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NewLife;

namespace XCode;

/// <summary>用户数据类型转换</summary>
public static partial class ValidHelper
{
    /// <summary>转为整数，转换失败时返回默认值。支持字符串、全角、字节数组（小端）、时间（Unix秒不转UTC）</summary>
    /// <remarks>Int16/UInt32/Int64等，可以先转为最常用的Int32后再二次处理</remarks>
    /// <param name="value">待转换对象</param>
    public static Int32 ToInt32(Object? value)
    {
        return value.ToInt();
    }

    /// <summary>转为长整数，转换失败时返回默认值。支持字符串、全角、字节数组（小端）、时间（Unix毫秒不转UTC）</summary>
    /// <param name="value">待转换对象</param>
    public static Int64 ToInt64(Object? value)
    {
        return value.ToLong();
    }

    /// <summary>转为浮点数，转换失败时返回默认值。支持字符串、全角、字节数组（小端）</summary>
    /// <remarks>Single可以先转为最常用的Double后再二次处理</remarks>
    /// <param name="value">待转换对象</param>
    public static Double ToDouble(Object? value)
    {
        return value.ToDouble();
    }

    /// <summary>转为布尔型，转换失败时返回默认值。支持大小写True/False、0和非零</summary>
    /// <param name="value">待转换对象</param>
    public static Boolean ToBoolean(Object? value)
    {
        return value.ToBoolean();
    }

    /// <summary>转为时间日期，转换失败时返回最小时间。支持字符串、整数（Unix秒不考虑UTC转本地）</summary>
    /// <remarks>
    /// 整数转时间日期时，取1970-01-01加上指定秒数，不考虑UTC时间和本地时间。
    /// 长整数转时间日期时，取1970-01-01加上指定毫秒数，不考虑UTC时间和本地时间。
    /// 在网络中传输时间日期时，特别是物联网设备到云平台的通信，一般取客户端本地UTC时间，转为长整型传输，服务端再转为本地时间。
    /// 因为设备和服务端可能不在同一时区，甚至多个设备也没有处于同一个时区。
    /// </remarks>
    /// <param name="value">待转换对象</param>
    public static DateTime ToDateTime(Object? value)
    {
        return value.ToDateTime();
    }

    /// <summary>
    /// 将指定的值转换为其等效的字符串表示形式。
    /// </summary>
    public static String ToString(Object? value)
    {
        return Convert.ToString(value);
    }

    /// <summary>
    /// Convert.ToByte
    /// </summary>
    public static Byte ToByte(Object? value)
    {
        return Convert.ToByte(value);
    }

    /// <summary>
    /// Convert.ToDecimal
    /// </summary>
    public static Decimal ToDecimal(Object? value)
    {
        return Convert.ToDecimal(value);
    }

    /// <summary>
    /// Convert.ToInt16
    /// </summary>
    public static Int16 ToInt16(Object? value)
    {
        return Convert.ToInt16(value);
    }

    /// <summary>
    /// Convert.ToUInt64
    /// </summary>
    public static UInt64 ToUInt64(Object? value)
    {
        return Convert.ToUInt64(value);
    }

    /// <summary>
    /// 转换为枚举
    /// </summary>
    public static T ToEnum<T>(Object? value) where T : struct
    {
        if (value is T t) return t;
        if (value is null || Convert.IsDBNull(value)) return default;
        if (typeof(T).IsEnum)
        {
            if (value is String str) return (T)Enum.Parse(typeof(T), str, true);
        }
        return (T)value;
    }

    /// <summary>转为目标对象</summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <returns></returns>
    public static T? ToObject<T>(Object? value) where T : class
    {
        if (value is T t) return t;
        if (value is null || Convert.IsDBNull(value)) return default;
        //这里怎么实现呢？
        return default;
    }
}
