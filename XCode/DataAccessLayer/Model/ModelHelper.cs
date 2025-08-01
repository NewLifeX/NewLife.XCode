﻿using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using NewLife.Log;
using NewLife.Reflection;
using XCode.Code;

namespace XCode.DataAccessLayer;

/// <summary>数据模型扩展</summary>
public static class ModelHelper
{
    #region 模型扩展方法
    /// <summary>根据字段名获取字段</summary>
    /// <param name="table"></param>
    /// <param name="name">名称</param>
    /// <returns></returns>
    public static IDataColumn? GetColumn(this IDataTable table, String name) => name.IsNullOrEmpty() ? null : table.Columns.FirstOrDefault(c => c.Is(name));

    /// <summary>根据字段名数组获取字段数组</summary>
    /// <param name="table"></param>
    /// <param name="names"></param>
    /// <returns></returns>
    public static IDataColumn[] GetColumns(this IDataTable table, String[] names)
    {
        if (names == null || names.Length <= 0) return [];

        //return table.Columns.Where(c => names.Any(n => c.Is(n))).ToArray();
        var dcs = new List<IDataColumn>();
        foreach (var name in names)
        {
            var dc = table.GetColumn(name);
            if (dc != null) dcs.Add(dc);
        }

        return dcs.ToArray();
    }

    /// <summary>获取全部字段，包括继承的父类</summary>
    /// <param name="table"></param>
    /// <param name="tables">在该表集合里面找父类</param>
    /// <param name="baseFirst">是否父类字段在前</param>
    /// <returns></returns>
    public static List<IDataColumn> GetAllColumns(this IDataTable table, IEnumerable<IDataTable> tables, Boolean baseFirst = true)
    {
        var list = new List<List<IDataColumn>>();

        var dt = table;
        while (dt != null)
        {
            list.Add(dt.Columns);

            var baseType = dt.BaseType;
            if (baseType.IsNullOrWhiteSpace()) break;

            dt = tables.FirstOrDefault(e => baseType.EqualIgnoreCase(e.Name, e.TableName));
        }

        if (baseFirst) list.Reverse();

        var dts = new List<IDataColumn>();
        foreach (var item in list)
        {
            dts.AddRange(item);
        }
        return dts;
    }

    /// <summary>判断表是否等于指定名字</summary>
    /// <param name="table"></param>
    /// <param name="name">名称</param>
    /// <returns></returns>
    public static Boolean Is(this IDataTable table, String name) => !String.IsNullOrEmpty(name) && name.EqualIgnoreCase(table.TableName, table.Name);

    /// <summary>判断字段是否等于指定名字</summary>
    /// <param name="column"></param>
    /// <param name="name">名称</param>
    /// <returns></returns>
    public static Boolean Is(this IDataColumn column, String name) => !String.IsNullOrEmpty(name) && name.EqualIgnoreCase(column.ColumnName, column.Name);

    //private static Boolean EqualIgnoreCase(this String[] src, String[] des)
    //{
    //    if (src == null || src.Length == 0) return des == null || des.Length == 0;
    //    if (des == null || des.Length == 0) return false;

    //    if (src.Length != des.Length) return false;

    //    //return !src.Except(des, StringComparer.OrdinalIgnoreCase).Any();
    //    return src.SequenceEqual(des, StringComparer.OrdinalIgnoreCase);
    //}

    /// <summary>根据字段名找索引</summary>
    /// <param name="table"></param>
    /// <param name="columnNames"></param>
    /// <returns></returns>
    public static IDataIndex? GetIndex(this IDataTable table, params String[] columnNames)
    {
        var dis = table.Indexes;
        if (dis == null || dis.Count <= 0 || columnNames == null || columnNames.Length <= 0) return null;

        //var di = dis.FirstOrDefault(e => e != null && e.Columns.EqualIgnoreCase(columnNames));
        //if (di != null) return di;

        //// 用别名再试一次
        //var columns = table.GetColumns(columnNames);
        //if (columns.Length != columnNames.Length) return null;

        //var names = columns.Select(e => e.Name).ToArray();
        //di = dis.FirstOrDefault(e => e.Columns.EqualIgnoreCase(names));
        //if (di != null) return di;

        //names = columns.Select(e => e.ColumnName).ToArray();
        //return dis.FirstOrDefault(e => e.Columns.EqualIgnoreCase(names));

        foreach (var di in dis)
        {
            if (di.Columns == null || di.Columns.Length != columnNames.Length) continue;

            // 把索引计算为标准字段，再逐一对比
            var dcs = table.GetColumns(di.Columns);
            if (dcs.Length != columnNames.Length) continue;

            var flag = true;
            for (var i = 0; i < dcs.Length; i++)
            {
                if (!dcs[i].Is(columnNames[i]))
                {
                    flag = false;
                    break;
                }
            }

            if (flag) return di;
        }

        return null;
    }

    /// <summary>驼峰变量名</summary>
    /// <param name="column"></param>
    /// <returns></returns>
    public static String CamelName(this IDataColumn column)
    {
        var name = column.Name;
        if (name.IsNullOrEmpty()) name = column.ColumnName;
        if (name.IsNullOrEmpty()) return name;

        if (name.EqualIgnoreCase("id")) return "id";

        // 全小写，直接返回
        if (name == name.ToLower()) return name;

        // 全大写，可能是专有名词，整体转小写
        if (name == name.ToUpper()) return name.ToLower();

        // 首字母小写
        name = Char.ToLower(name[0]) + name[1..];

        // 特殊处理ID结尾，改为Id，否则难看
        if (name.Length > 3 && name.EndsWith("ID") && Char.IsLower(name[^3])) name = name[0..^2] + "Id";

        return name;
    }
    #endregion

    #region 序列化扩展
    /// <summary>导出模型</summary>
    /// <param name="tables"></param>
    /// <param name="option">写在前面的扩展对象，一般用于存储配置</param>
    /// <param name="atts">附加属性</param>
    /// <returns></returns>
    public static String ToXml(IEnumerable<IDataTable> tables, Object? option = null, IDictionary<String, String>? atts = null)
    {
        var ms = new MemoryStream();

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true
        };

        var writer = XmlWriter.Create(ms, settings);
        writer.WriteStartDocument();

        var hasAttr = atts != null && atts.Count > 0;
        // 如果含有命名空间则添加
        if (hasAttr && atts.TryGetValue("xmlns", out var xmlns))
            writer.WriteStartElement("EntityModel", xmlns);
        else
            writer.WriteStartElement("EntityModel");

        // 写入版本
        //writer.WriteAttributeString("Version", Assembly.GetExecutingAssembly().GetName().Version.ToString());
        if (hasAttr)
        {
            foreach (var item in atts)
            {
                // 处理命名空间
                if (item.Key.EqualIgnoreCase("xmlns")) continue;
                if (item.Key.Contains(':'))
                {
                    var keys = item.Key.Split(':');
                    if (keys.Length != 2) continue;

                    writer.WriteAttributeString(keys[0], keys[1], null, item.Value);
                }
                else /*if (!item.Key.EqualIgnoreCase("Version"))*/
                    writer.WriteAttributeString(item.Key, item.Value);
                //if (!String.IsNullOrEmpty(item.Value)) writer.WriteElementString(item.Key, item.Value);
                //writer.WriteElementString(item.Key, item.Value);
            }
        }
        if (option != null)
        {
            writer.WriteStartElement("Option");
            foreach (var pi in option.GetType().GetProperties(true))
            {
                if (pi.PropertyType.GetTypeCode() == TypeCode.Object) continue;

                var des = pi.GetDescription() ?? pi.GetDisplayName();
                if (!des.IsNullOrEmpty()) writer.WriteComment(des);

                var v = pi.GetValue(option, null);
                writer.WriteElementString(pi.Name, v + "");
            }
            writer.WriteEndElement();
        }

        var nameFormat = option is EntityBuilderOption opt ? opt.NameFormat : NameFormats.Default;
        {
            writer.WriteStartElement("Tables");

            // 回写xml模型,排除IsHistory=true的表单，仅仅保留原始表单
            foreach (var table in tables.Where(x => x is not XTable xt || !xt.IsHistory))
            {
                writer.WriteStartElement("Table");
                table.WriteXml(writer, nameFormat);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>导入模型</summary>
    /// <param name="xml"></param>
    /// <param name="createTable">用于创建<see cref="IDataTable"/>实例的委托</param>
    /// <param name="option">写在前面的扩展对象，一般用于存储配置</param>
    /// <param name="atts">附加属性</param>
    /// <returns></returns>
    public static IList<IDataTable> FromXml(String xml, Func<IDataTable> createTable, Object? option = null, IDictionary<String, String>? atts = null)
    {
        if (xml.IsNullOrEmpty()) return [];
        if (createTable == null) throw new ArgumentNullException(nameof(createTable));

        var settings = new XmlReaderSettings
        {
            IgnoreWhitespace = true,
            IgnoreComments = true
        };

        var reader = XmlReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(xml)), settings);
        while (reader.NodeType != XmlNodeType.Element) { if (!reader.Read()) return []; }

        // 读取根节点特性
        if (atts != null && reader.HasAttributes)
        {
            reader.MoveToFirstAttribute();
            do
            {
                atts[reader.Name] = reader.Value;
            } while (reader.MoveToNextAttribute());
        }
        reader.ReadStartElement();

        var list = new List<IDataTable>();
        while (reader.IsStartElement())
        {
            // 202309起，新版让Tables作为第二层
            if (reader.Name.EqualIgnoreCase("Tables"))
            {
                while (reader.IsStartElement())
                {
                    if (reader.Name.EqualIgnoreCase("Table"))
                    {
                        ReadTable(reader, createTable, list);
                    }
                    else
                    {
                        // 这里必须处理，否则加载特殊Xml文件时将会导致死循环
                        reader.Read();
                    }
                }
            }
            // 2012版和2023版，Table都放在第二层
            else if (reader.Name.EqualIgnoreCase("Table"))
            {
                ReadTable(reader, createTable, list);
            }
            // 2023版和202309版，Option放在第二层
            else if (reader.Name.EqualIgnoreCase("Option"))
            {
                if (option != null)
                {
                    //if (option is IXmlSerializable xml2)
                    //    xml2.ReadXml(reader);
                    //else
                    ReadXml(reader, option);
                }
                else
                {
                    reader.Skip();
                }
            }
            // 2012版，顶级元素带有特性
            else if (atts != null)
            {
                var name = reader.Name;
                reader.ReadStartElement();
                if (reader.NodeType == XmlNodeType.Text)
                {
                    atts[name] = reader.ReadContentAsString();
                }
                if (reader.NodeType == XmlNodeType.EndElement) reader.ReadEndElement();
            }
            else
            {
                // 这里必须处理，否则加载特殊Xml文件时将会导致死循环
                reader.Read();
            }
        }
        return list;
    }

    static void ReadTable(XmlReader reader, Func<IDataTable> createTable, IList<IDataTable> list)
    {
        var table = createTable();
        table.ReadXml(reader);

        // 判断是否存在属性NeedHistory设置且为true
        var needHistory = table.Properties.FirstOrDefault(x => x.Key.EqualIgnoreCase("NeedHistory"));
        if (Convert.ToBoolean(needHistory.Value))
        {
            // 将标准映射添加到
            var historydataTable = ProcessNeedHistory(table);
            list.Add(historydataTable);
        }

        list.Add(table);
    }

    static IDataTable ProcessNeedHistory(IDataTable table)
    {
        //设置是历史表,用于标识,不用反写生成相关xml
        var historydataTable = (table.Clone() as XTable)!;
        historydataTable.IsHistory = true;

        //获取最后出现"。"字符串,返回其位置,无返回字符串长度---
        var des = table.Description + "";
        var p = des.LastIndexOf("。");
        if (p < 0) p = des.Length;

        historydataTable.Description = des.Substring(0, p) + "历史" + des.Substring(p, des.Length - p);
        historydataTable.Name = table.Name + "History";
        historydataTable.TableName = table.Name + "History";
        //历史表的所有index都必须允许重复
        historydataTable.Indexes?.ForEach(k =>
        {
            k.Unique = false;
        });
        historydataTable.Properties.Remove("NeedHistory");

        var col = table.CreateColumn();
        col.Description = des.Substring(0, p) + "信息";
        col.ColumnName = table.Name + "ID";
        col.DataType = typeof(DateTime);
        col.Name = table.Name + "ID";
        col.DataType = typeof(Int32);
        col.Map = $"{table.Name}@ID@$@{table.Name}Info";
        historydataTable.Columns.Insert(1, col);

        return historydataTable;
    }

    /// <summary>读取</summary>
    /// <param name="table"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public static IDataTable ReadXml(this IDataTable table, XmlReader reader)
    {
        // 读属性
        if (reader.HasAttributes)
        {
            reader.MoveToFirstAttribute();
            ReadXmlAttribute(reader, table);
        }

        reader.ReadStartElement();

        // 读字段
        reader.MoveToElement();
        // 有些数据表模型没有字段
        if (reader.NodeType == XmlNodeType.Element && reader.Name.EqualIgnoreCase("Table")) return table;

        while (reader.NodeType != XmlNodeType.EndElement)
        //while (reader.NodeType == XmlNodeType.Element)
        {
            switch (reader.Name)
            {
                case "Columns":
                    reader.ReadStartElement();
                    while (reader.IsStartElement())
                    {
                        var dc = table.CreateColumn();
                        var v = reader.GetAttribute("DataType");
                        if (v != null)
                        {
                            dc.DataType = v.GetTypeEx();
                            v = reader.GetAttribute("Length");
                            if (v != null && Int32.TryParse(v, out var len)) dc.Length = len;

                            dc = FixDefaultByType(dc, null);
                            // 清空默认的原始类型，让其从xml读取
                            dc.RawType = null;
                        }
                        ReadXmlAttribute(reader, dc);
                        // 跳过当前节点
                        reader.Skip();

                        // 未指定DataType，但指定了Type，修正为枚举整型
                        if (dc.DataType == null && dc.Properties.ContainsKey("Type")) dc.DataType = typeof(Int32);

                        table.Columns.Add(dc);
                    }
                    reader.ReadEndElement();

                    // 修正可能的主字段
                    if (!table.Columns.Any(e => e.Master))
                    {
                        var f = table.Columns.FirstOrDefault(e => e.Name.EqualIgnoreCase("Name", "Title"));
                        if (f != null) f.Master = true;
                    }
                    break;
                case "Indexes":
                    reader.ReadStartElement();
                    while (reader.IsStartElement())
                    {
                        var di = table.CreateIndex();
                        ReadXmlAttribute(reader, di);
                        // 跳过当前节点
                        reader.Skip();

                        di.Fix();
                        table.Indexes.Add(di);
                    }
                    reader.ReadEndElement();
                    break;
                case "Relations":
                    reader.ReadStartElement();
                    reader.Skip();
                    reader.ReadEndElement();
                    break;
                default:
                    // 这里必须处理，否则加载特殊Xml文件时将会导致死循环
                    reader.Read();
                    break;
            }
        }

        if (reader.NodeType == XmlNodeType.EndElement) reader.ReadEndElement();

        // 修正
        table.Fix();

        return table;
    }

    /// <summary>写入</summary>
    /// <param name="table"></param>
    /// <param name="writer"></param>
    /// <param name="nameFormat"></param>
    public static IDataTable WriteXml(this IDataTable table, XmlWriter writer, NameFormats nameFormat = NameFormats.Default)
    {
        var ignoreNameCase = nameFormat <= NameFormats.Default;

        WriteXml(writer, table, false, ignoreNameCase);

        // 写字段
        if (table.Columns.Count > 0)
        {
            writer.WriteStartElement("Columns");
            foreach (var dc in table.Columns)
            {
                writer.WriteStartElement("Column");
                WriteXml(writer, dc, false, ignoreNameCase);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        if (table.Indexes.Count > 0)
        {
            writer.WriteStartElement("Indexes");
            foreach (var di in table.Indexes)
            {
                writer.WriteStartElement("Index");
                WriteXml(writer, di, false, true);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        return table;
    }

    /// <summary>读取</summary>
    /// <param name="reader"></param>
    /// <param name="value">数值</param>
    public static void ReadXml(XmlReader reader, Object value)
    {
        reader.ReadStartElement();

        var pis = value.GetType().GetProperties(true);
        while (reader.IsStartElement())
        {
            var pi = pis.FirstOrDefault(e => e.Name.EqualIgnoreCase(reader.Name));
            if (pi != null)
            {
                var val = reader.ReadElementContentAsString();
                value.SetValue(pi, val);
            }
            else
            {
                // 这里必须处理，否则加载特殊Xml文件时将会导致死循环
                reader.Skip();
            }
        }

        if (reader.NodeType == XmlNodeType.EndElement) reader.ReadEndElement();
    }

    /// <summary>读取</summary>
    /// <param name="reader"></param>
    /// <param name="value">数值</param>
    public static void ReadXmlAttribute(XmlReader reader, Object value)
    {
        var pis = value.GetType().GetProperties(true);
        var names = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        foreach (var pi in pis)
        {
            if (!pi.CanRead || !pi.CanWrite) continue;
            if (pi.GetCustomAttribute<IgnoreDataMemberAttribute>(false) != null) continue;
            if (pi.GetCustomAttribute<XmlIgnoreAttribute>(false) != null) continue;

            // 已处理的特性
            names.Add(pi.Name);

            var v = reader.GetAttribute(pi.Name);
            if (v.IsNullOrEmpty()) continue;

            if (pi.PropertyType == typeof(String[]))
            {
                var ss = v.Split([","], StringSplitOptions.RemoveEmptyEntries);
                // 去除前后空格，因为手工修改xml的时候，可能在逗号后加上空格
                for (var i = 0; i < ss.Length; i++)
                {
                    ss[i] = ss[i].Trim();
                }
                value.SetValue(pi, ss);
            }
            else
                value.SetValue(pi, v.ChangeType(pi.PropertyType));
        }
        var pi1 = pis.FirstOrDefault(e => e.Name == "Name");
        var pi2 = pis.FirstOrDefault(e => e.Name is "TableName" or "ColumnName");
        if (pi1 != null && pi2 != null)
        {
            // 写入的时候省略了相同的TableName/ColumnName
            var v2 = (String?)value.GetValue(pi2);
            if (String.IsNullOrEmpty(v2))
            {
                value.SetValue(pi2, value.GetValue(pi1));
            }
        }
        // 自增字段非空
        if (value is IDataColumn dc)
        {
            if (dc.Identity) dc.Nullable = false;

            // 优化字段名
            //dc.Fix();
            if (dc.ColumnName.IsNullOrEmpty()) dc.ColumnName = dc.Name;
            if (dc.Name.IsNullOrEmpty() || dc.ColumnName == dc.Name)
            {
                var name = ModelResolver.Current.GetName(dc.ColumnName);

                // 检查该名字是否已存在，可能两个字段名差异只是多了个下划线
                if (dc.Table == null || !dc.Table.Columns.Any(e => e.Name.EqualIgnoreCase(name)))
                    dc.Name = name;
                else
                    dc.Name = dc.ColumnName;
            }
        }
        //reader.Skip();

        // 剩余特性作为扩展属性
        if (reader.MoveToFirstAttribute())
        {
            if (value is IDataTable dt)
            {
                do
                {
                    if (!names.Contains(reader.Name))
                    {
                        dt.Properties[reader.Name] = reader.Value;
                    }
                } while (reader.MoveToNextAttribute());
            }
            else if (value is IDataColumn dc3)
            {
                do
                {
                    if (!names.Contains(reader.Name))
                    {
                        dc3.Properties[reader.Name] = reader.Value;
                    }
                } while (reader.MoveToNextAttribute());
            }
        }
    }

    /// <summary>写入</summary>
    /// <param name="writer"></param>
    /// <param name="value">数值</param>
    /// <param name="writeDefaultValueMember">是否写数值为默认值的成员。为了节省空间，默认不写。</param>
    /// <param name="ignoreNameCase">忽略名称大小写</param>
    public static void WriteXml(XmlWriter writer, Object value, Boolean writeDefaultValueMember = false, Boolean ignoreNameCase = true)
    {
        var type = value.GetType();
        var def = GetDefault(type);
        //var ignoreNameCase = true;
        if (value is IDataColumn value2)
        {
            //var dc2 = def as IDataColumn;
            //var value2 = value as IDataColumn;
            // 需要重新创建，因为GetDefault带有缓存
            var dc2 = (type.CreateInstance() as IDataColumn)!;
            dc2.DataType = value2.DataType;
            dc2.Length = value2.Length;
            def = FixDefaultByType(dc2, value2);
            //ignoreNameCase = (value2.Table.IgnoreNameCase).ToBoolean(true);
        }
        else if (value is IDataTable value3)
        {
            //ignoreNameCase = (value3.IgnoreNameCase).ToBoolean(true);
        }

        String? name = null;

        // 基本类型，输出为特性
        foreach (var pi in type.GetProperties(true))
        {
            if (!pi.CanWrite) continue;
            //if (pi.GetCustomAttribute<XmlIgnoreAttribute>(false) != null) continue;
            // 忽略ID
            if (pi.Name == "ID") continue;
            // IDataIndex跳过默认Name
            if (value is IDataIndex di && pi.Name.EqualIgnoreCase("Name"))
            {
                if (di.Name.EqualIgnoreCase(ModelResolver.Current.GetName(di))) continue;
            }

            var code = Type.GetTypeCode(pi.PropertyType);

            var obj = value.GetValue(pi);
            // 默认值不参与序列化，节省空间
            if (!writeDefaultValueMember)
            {
                var dobj = def.GetValue(pi);
                if (Equals(obj, dobj)) continue;
                if (code == TypeCode.String && "" + obj == "" + dobj) continue;
            }

            if (code == TypeCode.String && obj is String str)
            {
                // 如果别名与名称相同，则跳过，不区分大小写
                // 改为区分大小写，避免linux环境下 mysql 数据库存在
                if (pi.Name == "Name")
                    name = str;
                else if (pi.Name is "TableName" or "ColumnName")
                {
                    if (name == str) continue;
                    if (ignoreNameCase)
                    {
                        if (name.EqualIgnoreCase(str)) continue;
                    }
                    else
                    {
                        // 如果全小写或者全大写，也不缺分大小写比较
                        if ((str == str.ToLower() || str == str.ToUpper()) && name.EqualIgnoreCase(str)) continue;
                    }
                }
            }
            else if (code == TypeCode.Object)
            {
                var ptype = pi.PropertyType;
                if (ptype.IsArray || ptype.As<IEnumerable>() || obj is IEnumerable)
                {
                    var sb = new StringBuilder();
                    var arr = obj as IEnumerable;
                    foreach (var elm in arr)
                    {
                        if (sb.Length > 0) sb.Append(',');
                        sb.Append(elm);
                    }
                    obj = sb.ToString();
                }
                else if (pi.PropertyType == typeof(Type))
                {
                    obj = (obj as Type).Name;
                }
                else
                {
                    // 其它的不支持，跳过
                    if (XTrace.Debug) XTrace.WriteLine("不支持的类型[{0} {1}]！", pi.PropertyType.Name, pi.Name);

                    continue;
                }
                //if (item.Type == typeof(Type)) obj = (obj as Type).Name;
            }
            if (obj != null) writer.WriteAttributeString(pi.Name, obj + "");
        }

        if (value is IDataTable table)
        {
            // 写入扩展属性作为特性
            if (table.Properties.Count > 0)
            {
                foreach (var item in table.Properties)
                {
                    writer.WriteAttributeString(item.Key, item.Value);
                }
            }
        }
        else if (value is IDataColumn column)
        {
            // 写入扩展属性作为特性
            if (column.Properties.Count > 0)
            {
                foreach (var item in column.Properties)
                {
                    if (!item.Key.EqualIgnoreCase("DisplayName", "NumOfByte")) writer.WriteAttributeString(item.Key, item.Value);
                }
            }
        }
    }

    private static readonly ConcurrentDictionary<Type, Object> cache = new();

    private static Object GetDefault(Type type) => cache.GetOrAdd(type, item => item.CreateInstance()!);
    #endregion

    #region 修正数据列
    /// <summary>根据类型修正字段的一些默认值</summary>
    /// <param name="dc"></param>
    /// <param name="oridc"></param>
    /// <returns></returns>
    public static IDataColumn FixDefaultByType(this IDataColumn dc, IDataColumn? oridc)
    {
        if (dc.DataType == null) return dc;

        switch (dc.DataType.GetTypeCode())
        {
            case TypeCode.Boolean:
                dc.RawType = "bit";
                dc.Nullable = false;
                dc.Precision = 0;
                dc.Scale = 0;
                break;
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.SByte:
                dc.RawType = "tinyint";
                dc.Nullable = false;
                dc.Precision = 3;
                dc.Scale = 0;
                break;
            case TypeCode.DateTime:
                dc.RawType = "datetime";
                dc.Nullable = true;
                dc.Precision = 0;
                dc.Scale = 0;
                break;
            case TypeCode.Int16:
            case TypeCode.UInt16:
                dc.RawType = "smallint";
                dc.Nullable = false;
                dc.Precision = 5;
                dc.Scale = 0;
                break;
            case TypeCode.Int32:
            case TypeCode.UInt32:
                dc.RawType = "int";
                dc.Nullable = false;
                dc.Precision = 10;
                dc.Scale = 0;
                break;
            case TypeCode.Int64:
            case TypeCode.UInt64:
                dc.RawType = "bigint";
                dc.Nullable = false;
                dc.Precision = oridc != null && oridc.RawType == "bigint unsigned" ? 20 : 19;
                dc.Scale = 0;
                break;
            case TypeCode.Single:
                dc.RawType = "real";
                dc.Nullable = false;
                dc.Precision = 12;
                dc.Scale = 2;
                break;
            case TypeCode.Double:
                dc.RawType = "float";
                dc.Nullable = false;
                dc.Precision = 22;
                dc.Scale = 4;
                break;
            case TypeCode.Decimal:
                dc.RawType = "money";
                dc.Nullable = false;
                //dc.Precision = 20;
                //dc.Scale = 4;
                dc.Precision = 0;
                dc.Scale = 0;
                break;
            case TypeCode.String:
                // 原来就是普通字符串，或者非ntext字符串，一律转nvarchar
                if (dc.Length >= 0 && dc.Length < 4000 || oridc != null && oridc != dc && oridc.RawType != "ntext")
                {
                    var len = dc.Length;
                    if (len == 0) len = 50;
                    dc.RawType = $"nvarchar({len})";

                    // 新建默认长度50，写入忽略50的长度，其它长度不能忽略
                    dc.Length = len == 50 ? 50 : 0;
                }
                else
                {
                    // 新建默认长度-1，写入忽略所有长度
                    dc.Length = -1;
                    if (oridc == null || oridc == dc)
                    {
                        dc.RawType = "ntext";
                        //dc.Length = -1;
                    }
                    else
                    {
                        // 强制写入长度-1
                        //dc.Length = 0;
                        oridc.Length = 0;

                        // 不写RawType
                        dc.RawType = oridc.RawType;
                    }
                }
                dc.Nullable = true;
                dc.Precision = 0;
                dc.Scale = 0;
                break;
            default:
                if (dc.DataType == typeof(Byte[]))
                {
                    dc.Length = 0;
                    dc.Nullable = true;
                }
                break;
        }

        // 默认值里面不要设置数据类型，否则写入模型文件的时候会漏掉数据类型
        dc.DataType = null!;
        if (dc.RawType.IsNullOrEmpty()) dc.RawType = null;

        return dc;
    }
    #endregion
}