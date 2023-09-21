using System.Reflection;

namespace XCode.Common;

/// <summary>数据转换</summary>
public class DataConversion
{
    private static BindingFlags _bf = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    /// <summary>拷贝</summary>
    /// <param name="objSource"></param>
    /// <param name="objTarjet"></param>
    /// <param name="ignoreTarjetProperties"></param>
    public static void CopyProperty(Object objSource, Object objTarjet, List<String>? ignoreTarjetProperties = null)
    {
        Object? obj = null;
        var empty = String.Empty;
        try
        {
            var properties = GetProperties(objSource);
            var properties2 = GetProperties(objTarjet);
            if (properties2 == null)
            {
                return;
            }

            var array = properties2;
            foreach (var propertyInfo in array)
            {
                if (properties == null)
                {
                    continue;
                }

                var array2 = properties;
                foreach (var propertyInfo2 in array2)
                {
                    if (propertyInfo.Name == "Items")
                    {
                    }

                    if (propertyInfo.Name == "PrintInsideLabelInfo")
                    {
                    }

                    if (!(propertyInfo.Module.Name != "XCode.dll") || !(propertyInfo.Name == propertyInfo2.Name) || !(propertyInfo.Name != "Item") || !(propertyInfo.Name != "Items"))
                    {
                        continue;
                    }

                    empty = propertyInfo.Name;
                    obj = propertyInfo2.GetValue(objSource, null);
                    if (!propertyInfo.CanWrite)
                    {
                        continue;
                    }

                    if (ignoreTarjetProperties != null)
                    {
                        if (!ignoreTarjetProperties.Contains(propertyInfo.Name))
                        {
                            propertyInfo.SetValue(objTarjet, obj, null);
                        }
                    }
                    else
                    {
                        propertyInfo.SetValue(objTarjet, obj, null);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    /// <summary>获取属性</summary>
    /// <param name="obj"></param>
    /// <param name="withBindingFlags"></param>
    /// <returns></returns>
    public static PropertyInfo[] GetProperties(Object obj, Boolean withBindingFlags = false) => withBindingFlags ? obj.GetType().GetProperties(_bf) : obj.GetType().GetProperties();
}
