using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace XCode.Common
{
    /// <summary>数据转换</summary>
    public class DataConversion
    {
        public static BindingFlags bf = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        public static void CopyProperty(object objSource, object objTarjet, List<string> ignoreTarjetProperties = null)
        {
            object obj = null;
            string empty = string.Empty;
            try
            {
                PropertyInfo[] properties = GetProperties(objSource);
                PropertyInfo[] properties2 = GetProperties(objTarjet);
                if (properties2 == null)
                {
                    return;
                }

                PropertyInfo[] array = properties2;
                foreach (PropertyInfo propertyInfo in array)
                {
                    if (properties == null)
                    {
                        continue;
                    }

                    PropertyInfo[] array2 = properties;
                    foreach (PropertyInfo propertyInfo2 in array2)
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


        public static PropertyInfo[] GetProperties(object obj, bool withBindingFlags = false)
        {
            return withBindingFlags ? obj.GetType().GetProperties(bf) : obj.GetType().GetProperties();
        }
    }
}
