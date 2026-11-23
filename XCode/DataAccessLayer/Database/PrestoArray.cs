using System.Collections;
using System.Data;
using NewLife;
using NewLife.Serialization;

namespace XCode.DataAccessLayer;

/// <summary>Presto/Trino 数组类型解析与值转换</summary>
/// <remarks>
/// 只支持一层数组与有限元素类型，嵌套数组、map、row 继续按 JSON 文本处理。
/// 元素类型白名单与 <see cref="ValidHelper"/> 的数组转换方法保持一致。
/// </remarks>
internal static class PrestoArray
{
    #region 属性
    /// <summary>元素类型白名单</summary>
    private static readonly Dictionary<String, Type> _elementTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["varchar"] = typeof(String),
        ["char"] = typeof(String),
        ["smallint"] = typeof(Int16),
        ["integer"] = typeof(Int32),
        ["int"] = typeof(Int32),
        ["bigint"] = typeof(Int64),
        ["double"] = typeof(Double),
        ["decimal"] = typeof(Decimal),
        ["numeric"] = typeof(Decimal),
        ["boolean"] = typeof(Boolean),
    };
    #endregion

    #region 类型解析
    /// <summary>解析 array(...) 的元素原始类型。嵌套数组、map、row 返回 false</summary>
    /// <param name="rawType">原始类型，如 array(varchar)、array(decimal(10,2))</param>
    /// <param name="elementType">元素原始类型</param>
    /// <returns>是否为受支持的一层数组</returns>
    public static Boolean TryParseArray(String? rawType, out String elementType)
    {
        elementType = String.Empty;
        if (rawType.IsNullOrEmpty()) return false;

        var text = rawType.Trim();
        if (!text.StartsWith("array", StringComparison.OrdinalIgnoreCase)) return false;

        var start = text.IndexOf('(');
        if (start < 0 || !text[5..start].Trim().IsNullOrEmpty()) return false;
        var end = text.LastIndexOf(')');
        if (end <= start) return false;

        var inner = text[(start + 1)..end].Trim();
        if (inner.IsNullOrEmpty()) return false;

        var name = GetBaseName(inner);
        if (name.EqualIgnoreCase("array", "map", "row", "json")) return false;

        elementType = inner;
        return true;
    }

    /// <summary>解析数组元素对应的 CLR 类型。只支持白名单内的元素类型</summary>
    /// <param name="rawType">原始类型</param>
    /// <param name="elementType">元素 CLR 类型</param>
    /// <returns>是否解析成功</returns>
    public static Boolean TryGetElementType(String? rawType, out Type elementType)
    {
        elementType = null!;
        if (!TryParseArray(rawType, out var element)) return false;
        if (!_elementTypes.TryGetValue(GetBaseName(element), out var type)) return false;

        elementType = type!;
        return true;
    }

    /// <summary>是否复杂类型（array/map/row）</summary>
    /// <param name="rawType">原始类型</param>
    /// <returns>复杂类型返回 true</returns>
    public static Boolean IsComplex(String? rawType)
    {
        if (rawType.IsNullOrEmpty()) return false;

        return GetBaseName(rawType).EqualIgnoreCase("array", "map", "row");
    }

    /// <summary>取基础类型名，去掉参数与首尾空白</summary>
    /// <param name="type">类型签名</param>
    /// <returns>基础类型名</returns>
    public static String GetBaseName(String? type)
    {
        if (type.IsNullOrEmpty()) return String.Empty;

        var text = type.Trim();
        var index = text.IndexOf('(');
        if (index > 0) text = text[..index].Trim();

        return text;
    }
    #endregion

    #region 值转换
    /// <summary>把驱动返回值转换为强类型数组</summary>
    /// <param name="value">驱动返回值，可能是数组、列表或 JSON 文本</param>
    /// <param name="elementType">元素类型</param>
    /// <param name="result">转换结果</param>
    /// <returns>是否转换成功</returns>
    public static Boolean TryConvert(Object? value, Type elementType, out Object? result)
    {
        result = value;
        if (value == null || value == DBNull.Value) return true;
        if (value.GetType() == elementType.MakeArrayType()) return true;

        // 驱动解析失败时会回退原始 JSON 文本，这里再尝试一次
        if (value is String text)
        {
            try
            {
                value = new JsonParser(text).Decode();
            }
            catch
            {
                return false;
            }
        }

        if (value is not IEnumerable || value is IDictionary) return false;

        if (elementType == typeof(String))
            result = ValidHelper.ToStringArray(value);
        else if (elementType == typeof(Int16))
            result = ValidHelper.ToInt16Array(value);
        else if (elementType == typeof(Int32))
            result = ValidHelper.ToInt32Array(value);
        else if (elementType == typeof(Int64))
            result = ValidHelper.ToInt64Array(value);
        else if (elementType == typeof(Double))
            result = ValidHelper.ToDoubleArray(value);
        else if (elementType == typeof(Decimal))
            result = ValidHelper.ToDecimalArray(value);
        else if (elementType == typeof(Boolean))
            result = ValidHelper.ToBooleanArray(value);
        else
            return false;

        return result != null;
    }

    /// <summary>把结构化复杂类型还原为 JSON 文本；标量保持原样</summary>
    /// <param name="value">驱动返回值</param>
    /// <returns>JSON 文本或原始标量</returns>
    public static Object? ToJson(Object? value)
    {
        if (value == null || value == DBNull.Value) return value;
        if (value is String) return value;
        if (value is IDictionary || value is IEnumerable && value is not Byte[]) return JsonWriter.ToJson(value, new JsonOptions());

        return value;
    }
    #endregion
}
