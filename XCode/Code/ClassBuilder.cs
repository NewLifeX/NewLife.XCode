using System.Text;
using NewLife;
using NewLife.Collections;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Serialization;
using XCode.DataAccessLayer;

namespace XCode.Code;

/// <summary>类代码生成器</summary>
public class ClassBuilder
{
    #region 属性

    /// <summary>写入器</summary>
    public TextWriter Writer { get; set; } = null!;

    /// <summary>数据表</summary>
    public IDataTable Table { get; set; } = null!;

    /// <summary>类名。默认Table.Name</summary>
    public String? ClassName { get; set; }

    /// <summary>生成器选项</summary>
    public BuilderOption Option { get; set; } = new BuilderOption();

    #endregion 属性

    #region 静态快速
    /// <summary>加载模型文件</summary>
    /// <param name="xmlFile">Xml模型文件</param>
    /// <param name="option">生成可选项</param>
    /// <param name="atts">扩展属性字典</param>
    /// <param name="log"></param>
    /// <returns></returns>
    public static IList<IDataTable> LoadModels(String? xmlFile, BuilderOption option, out IDictionary<String, String> atts, ILog? log = null)
    {
        if (xmlFile.IsNullOrEmpty())
        {
            var di = ".".GetBasePath().AsDirectory();
            //XTrace.WriteLine("未指定模型文件，准备从目录中查找第一个xml文件 {0}", di.FullName);
            // 选当前目录第一个
            xmlFile = di.GetFiles("*.xml", SearchOption.TopDirectoryOnly).FirstOrDefault()?.FullName;
        }

        if (xmlFile.IsNullOrEmpty()) throw new Exception("找不到任何模型文件！");

        var dir = Path.GetDirectoryName(xmlFile);
        xmlFile = xmlFile.GetBasePath();
        if (!File.Exists(xmlFile)) throw new FileNotFoundException("指定模型文件不存在！", xmlFile);

        // 导入模型
        var xmlContent = File.ReadAllText(xmlFile);
        atts = new NullableDictionary<String, String>(StringComparer.OrdinalIgnoreCase)
        {
            ["xmlns"] = "https://newlifex.com/Model202309.xsd",
            ["xmlns:xs"] = "http://www.w3.org/2001/XMLSchema-instance",
            ["xs:schemaLocation"] = "https://newlifex.com https://newlifex.com/Model202309.xsd"
        };

        log?.Info("导入模型：{0}", xmlFile);

        // 导入模型
        var tables = ModelHelper.FromXml(xmlContent, DAL.CreateTable, option, atts);

        if (option != null)
        {
            //option.Output = atts["Output"] ?? Path.GetDirectoryName(xmlFile);
            //option.Namespace = atts["NameSpace"] ?? Path.GetFileNameWithoutExtension(xmlFile);
            //option.ConnName = atts["ConnName"];
            //option.DisplayName = atts["DisplayName"];
            //option.BaseClass = atts["BaseClass"];

            //if (atts.TryGetValue("ChineseFileName", out str) && !str.IsNullOrEmpty())
            //    option.ChineseFileName = str.ToBoolean();
            //if (atts.TryGetValue("CreateCustomBizFile", out str) && !str.IsNullOrEmpty())
            //    option.CreateCustomBizFile = str.ToBoolean();
            //if (atts.TryGetValue("OverwriteBizFile", out str) && !str.IsNullOrEmpty())
            //    option.OverwriteBizFile = str.ToBoolean();

            option.Items = atts;

            // 反射去掉option中已有设置，改用头部配置对象
            foreach (var pi in option.GetType().GetProperties())
            {
                if (atts.TryGetValue(pi.Name, out var val))
                {
                    if (pi.PropertyType.IsEnum)
                        option.SetValue(pi, Enum.Parse(pi.PropertyType, val, true));
                    else
                        option.SetValue(pi, val);
                    atts.Remove(pi.Name);
                }
            }

            // 去掉空属性
            foreach (var item in atts.ToKeyArray())
            {
                if (atts.TryGetValue(item, out var val) && val.IsNullOrEmpty())
                {
                    atts.Remove(item);
                }
            }

            if (option.Output.IsNullOrEmpty() && !dir.EqualIgnoreCase(".".GetBasePath())) option.Output = dir;
            if (option.Namespace.IsNullOrEmpty()) option.Namespace = Path.GetFileNameWithoutExtension(xmlFile);
        }

        // 保存文件名
        atts["ModelFile"] = xmlFile;

        return tables;
    }
    #endregion 静态快速

    #region 方法

    /// <summary>加载数据表</summary>
    /// <param name="table"></param>
    public virtual void Load(IDataTable table)
    {
        Table = table;

        var option = Option;

        // 命名空间
        var str = table.Properties["Namespace"];
        if (!str.IsNullOrEmpty()) option.Namespace = str;

        // 输出目录
        str = table.Properties["Output"];
        if (!str.IsNullOrEmpty()) option.Output = str.GetBasePath();
    }

    #endregion 方法

    #region 主方法

    /// <summary>执行生成</summary>
    public virtual void Execute()
    {
        // 参数检查
        var dt = Table ?? throw new ArgumentNullException(nameof(Table));
        if (dt.Columns == null || dt.Columns.Count == 0) throw new ArgumentOutOfRangeException(nameof(Table));

        foreach (var dc in dt.Columns)
        {
            if (dc.DataType == null && dc.Properties["Type"].IsNullOrEmpty())
                throw new XCodeException($"表[{dt.Name}]的字段[{dc.Name}]未指定DataType类型");
        }

        var option = Option;
        WriteLog("生成 {0} {1} {2}", Table.Name, Table.DisplayName, new { option.ClassNameTemplate, option.BaseClass, option.ModelNameForCopy, option.Namespace }.ToJson(false, false, false));

        //Clear();
        Writer ??= new StringWriter();

        Prepare();

        OnExecuting();

        BuildItems();

        OnExecuted();
    }

    /// <summary>生成前的准备工作。计算类型以及命名空间等</summary>
    protected virtual void Prepare()
    {
        var option = Option;
        if (ClassName.IsNullOrEmpty())
        {
            if (!option.ClassNameTemplate.IsNullOrEmpty())
                ClassName = option.ClassNameTemplate.Replace("{name}", Table.Name);
            else
                ClassName = Table.Name;
        }
    }

    /// <summary>生成头部</summary>
    protected virtual void OnExecuting()
    {
        // 引用命名空间
        var us = Option.Usings;
        //if (Option.HasIModel)
        //{
        //    if (!us.Contains("NewLife.Data")) us.Add("NewLife.Data");
        //    if (!us.Contains("NewLife.Reflection")) us.Add("NewLife.Reflection");
        //}

        us = us.Distinct().OrderBy(e => e.StartsWith("System") ? 0 : 1).ThenBy(e => e).ToArray();
        foreach (var item in us)
        {
            WriteLine("using {0};", item);
        }
        WriteLine();

        var ns = Option.Namespace;
        if (!ns.IsNullOrEmpty())
        {
            WriteLine("namespace {0};", ns);
            WriteLine();
            //WriteLine("{");
        }

        BuildClassHeader();
    }

    /// <summary>实体类头部</summary>
    protected virtual void BuildClassHeader()
    {
        // 头部
        BuildAttribute();

        // 基类
        var baseClass = GetBaseClass();
        if (!baseClass.IsNullOrEmpty()) baseClass = " : " + baseClass;

        // 分部类
        var partialClass = " partial";

        // 类接口
        WriteLine("public{2} class {0}{1}", ClassName, baseClass, partialClass);
        WriteLine("{");
    }

    /// <summary>获取基类</summary>
    /// <returns></returns>
    protected virtual String? GetBaseClass()
    {
        var baseClass = Option.BaseClass?.Replace("{name}", Table.Name);
        //if (Option.HasIModel)
        //{
        //    if (!baseClass.IsNullOrEmpty()) baseClass += ", ";
        //    baseClass += "IModel";
        //}

        return baseClass;
    }

    /// <summary>实体类头部</summary>
    protected virtual void BuildAttribute()
    {
        // 注释
        var des = Table.Description;
        if (!Option.DisplayNameTemplate.IsNullOrEmpty())
        {
            var dis = Table.DisplayName;
            if (!des.IsNullOrEmpty())
            {
                if (!dis.IsNullOrEmpty()) des = des.TrimStart(dis);
                des = des.TrimStart("。");
            }
            des = Option.DisplayNameTemplate.Replace("{displayName}", dis) + "。" + des;
        }
        WriteLine("/// <summary>{0}</summary>", des);
    }

    /// <summary>生成尾部</summary>
    protected virtual void OnExecuted()
    {
        // 类接口
        WriteLine("}");

        //if (!Option.Namespace.IsNullOrEmpty())
        //{
        //    Writer.Write("}");
        //}
    }

    /// <summary>生成主体</summary>
    protected virtual void BuildItems()
    {
        WriteLine("#region 属性");
        for (var i = 0; i < Table.Columns.Count; i++)
        {
            var column = Table.Columns[i];

            // 跳过排除项
            if (!ValidColumn(column)) continue;

            if (i > 0) WriteLine();
            BuildItem(column);
        }
        WriteLine("#endregion");

        //if (Option.HasIModel)
        //{
        //    WriteLine();
        //    BuildIndexItems();
        //}
    }

    /// <summary>生成每一项</summary>
    protected virtual void BuildItem(IDataColumn column)
    {
        var dc = column;

        // 注释
        var des = dc.Description;
        WriteLine("/// <summary>{0}</summary>", des);

        //if (!Option.Pure)
        //{
        //    if (!des.IsNullOrEmpty()) WriteLine("[Description(\"{0}\")]", des);

        //    var dis = dc.DisplayName;
        //    if (!dis.IsNullOrEmpty()) WriteLine("[DisplayName(\"{0}\")]", dis);
        //}

        var type = dc.Properties["Type"];
        if (type.IsNullOrEmpty()) type = dc.DataType?.Name;
        if (type == "String")
        {
            if (Option.Nullable)
            {
                if (column.Nullable)
                    WriteLine("public String? {0} {{ get; set; }}", dc.Name);
                else
                    WriteLine("public String {0} {{ get; set; }} = null!;", dc.Name);
            }
            else
            {
                WriteLine("public String {0} {{ get; set; }}", dc.Name);
            }
        }
        else
        {
            WriteLine("public {0} {1} {{ get; set; }}", type, dc.Name);
        }
    }

    /// <summary>生成索引访问器</summary>
    protected virtual void BuildIndexItems()
    {
        WriteLine("#region 获取/设置 字段值");
        WriteLine("/// <summary>获取/设置 字段值</summary>");
        WriteLine("/// <param name=\"name\">字段名</param>");
        WriteLine("/// <returns></returns>");
        if (Option.Nullable)
            WriteLine("public virtual Object? this[String name]");
        else
            WriteLine("public virtual Object this[String name]");
        WriteLine("{");

        // get
        WriteLine("get");
        WriteLine("{");
        {
            WriteLine("return name switch");
            WriteLine("{");
            foreach (var column in Table.Columns)
            {
                // 跳过排除项
                if (!ValidColumn(column)) continue;

                WriteLine("\"{0}\" => {0},", column.Name);
            }
            //WriteLine("default: throw new KeyNotFoundException($\"{name} not found\");");
            WriteLine("_ => this.GetValue(name, false),");
            WriteLine("};");
        }
        WriteLine("}");

        // set
        WriteLine("set");
        WriteLine("{");
        {
            WriteLine("switch (name)");
            WriteLine("{");
            var conv = typeof(Convert);
            foreach (var column in Table.Columns)
            {
                // 跳过排除项
                if (!ValidColumn(column)) continue;

                var type = column.Properties["Type"];
                if (type.IsNullOrEmpty()) type = column.DataType?.Name;

                if (!type.IsNullOrEmpty())
                {
                    if (!type.Contains(".") && conv.GetMethod("To" + type, [typeof(Object)]) != null)
                    {
                        switch (type)
                        {
                            case "Int32":
                                WriteLine("case \"{0}\": {0} = value.ToInt(); break;", column.Name);
                                break;

                            case "Int64":
                                WriteLine("case \"{0}\": {0} = value.ToLong(); break;", column.Name);
                                break;

                            case "Double":
                                WriteLine("case \"{0}\": {0} = value.ToDouble(); break;", column.Name);
                                break;

                            case "Boolean":
                                WriteLine("case \"{0}\": {0} = value.ToBoolean(); break;", column.Name);
                                break;

                            case "DateTime":
                                WriteLine("case \"{0}\": {0} = value.ToDateTime(); break;", column.Name);
                                break;

                            default:
                                WriteLine("case \"{0}\": {0} = Convert.To{1}(value); break;", column.Name, type);
                                break;
                        }
                    }
                    else
                    {
                        try
                        {
                            // 特殊支持枚举
                            var type2 = type.GetTypeEx();
                            if (type2 != null && type2.IsEnum)
                            {
                                var enumType = Enum.GetUnderlyingType(type2);
                                switch (enumType.Name)
                                {
                                    case "Int32":
                                        WriteLine("case \"{0}\": {0} = ({1})value.ToInt(); break;", column.Name, type);
                                        break;
                                    case "Int64":
                                        WriteLine("case \"{0}\": {0} = ({1})value.ToLong(); break;", column.Name, type);
                                        break;
                                    default:
                                        WriteLine("case \"{0}\": {0} = ({1})value.ToInt(); break;", column.Name, type);
                                        break;
                                }
                            }
                            else
                                WriteLine("case \"{0}\": {0} = ({1})value; break;", column.Name, type);
                        }
                        catch (Exception ex)
                        {
                            XTrace.WriteException(ex);
                            WriteLine("case \"{0}\": {0} = ({1})value; break;", column.Name, type);
                        }
                    }
                }
            }
            //WriteLine("default: throw new KeyNotFoundException($\"{name} not found\");");
            WriteLine("default: this.SetValue(name, value); break;");
            WriteLine("}");
        }
        WriteLine("}");

        WriteLine("}");
        WriteLine("#endregion");
    }

    /// <summary>生成拷贝函数</summary>
    /// <param name="model">模型类</param>
    protected virtual void BuildCopy(String model)
    {
        WriteLine("#region 拷贝");
        WriteLine("/// <summary>拷贝模型对象</summary>");
        WriteLine("/// <param name=\"model\">模型</param>");
        WriteLine("public void Copy({0} model)", model);
        WriteLine("{");
        foreach (var column in Table.Columns)
        {
            // 跳过排除项
            if (!ValidColumn(column, true)) continue;

            WriteLine("{0} = model.{0};", column.Name);
        }
        WriteLine("}");
        WriteLine("#endregion");
    }

    /// <summary>生成实体转模型函数</summary>
    /// <param name="modelClass"></param>
    /// <param name="modelInterface"></param>
    protected virtual void BuildToModel(String modelClass, String modelInterface)
    {
        WriteLine($"public {modelInterface} ToModel()");
        WriteLine("{");
        WriteLine($"var model = new {modelClass}();");
        WriteLine("model.Copy(this);");
        WriteLine("");
        WriteLine("return model;");
        WriteLine("}");
    }

    #endregion 主方法

    #region 写入缩进方法

    private String? _Indent;

    /// <summary>设置缩进</summary>
    /// <param name="add"></param>
    protected virtual void SetIndent(Boolean add)
    {
        if (add)
            _Indent += "    ";
        else if (!_Indent.IsNullOrEmpty())
            _Indent = _Indent[0..^4];
    }

    /// <summary>写入</summary>
    /// <param name="value"></param>
    protected virtual void WriteLine(String? value = null)
    {
        if (value.IsNullOrEmpty())
        {
            Writer.WriteLine();
            return;
        }

        if (value[0] == '}') SetIndent(false);

        var v = value;
        if (!_Indent.IsNullOrEmpty()) v = _Indent + v;

        Writer.WriteLine(v);

        if (value == "{") SetIndent(true);
    }

    /// <summary>写入</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    protected virtual void WriteLine(String format, params Object?[] args)
    {
        if (!_Indent.IsNullOrEmpty()) format = _Indent + format;

        Writer.WriteLine(format, args);
    }

    /// <summary>清空，重新生成</summary>
    public virtual void Clear()
    {
        _Indent = null;

        if (Writer is StringWriter sw)
        {
            sw.GetStringBuilder().Clear();
        }
    }

    /// <summary>输出结果</summary>
    /// <returns></returns>
    public override String ToString() => Writer.ToString();

    #endregion 写入缩进方法

    #region 保存
    /// <summary>获取文件名</summary>
    /// <param name="ext"></param>
    /// <param name="chineseFileName"></param>
    /// <returns></returns>
    protected virtual String GetFileName(String? ext = null, Boolean chineseFileName = true)
    {
        var p = Option.Output;
        if (ext.IsNullOrEmpty())
            ext = ".cs";
        else if (!ext.Contains("."))
            ext += ".cs";

        if (chineseFileName && !Table.DisplayName.IsNullOrEmpty())
            p = p.CombinePath(Table.DisplayName + ext);
        else
            p = p.CombinePath(ClassName + ext);

        p = p.GetBasePath();

        return p;
    }

    /// <summary>保存文件，返回文件路径</summary>
    /// <param name="ext">扩展名，默认.cs</param>
    /// <param name="overwrite">是否覆盖目标文件</param>
    /// <param name="chineseFileName">是否使用中文名</param>
    public virtual String Save(String? ext = null, Boolean overwrite = true, Boolean chineseFileName = true)
    {
        var p = GetFileName(ext, chineseFileName);

        if (!File.Exists(p) || overwrite) File.WriteAllText(p.EnsureDirectory(true), ToString(), Encoding.UTF8);

        return p;
    }

    #endregion 保存

    #region 辅助

    /// <summary>验证字段是否可用于生成</summary>
    /// <param name="column"></param>
    /// <param name="validModel"></param>
    /// <returns></returns>
    protected virtual Boolean ValidColumn(IDataColumn column, Boolean validModel = false)
    {
        if (Option.Excludes.Contains(column.Name)) return false;
        if (Option.Excludes.Contains(column.ColumnName)) return false;
        if (validModel && column.Properties["Model"] == "False")
            return false;

        return true;
    }

    /// <summary>C#版本</summary>
    public Version? CSharp { get; set; }

    /// <summary>nameof</summary>
    /// <param name="name"></param>
    /// <returns></returns>
    protected String NameOf(String name)
    {
        var v = CSharp;
        if (v == null || v.Major == 0 || v.Major > 5) return $"nameof({name})";

        return "\"" + name + "\"";
    }

    /// <summary>驼峰命名，首字母小写</summary>
    /// <param name="name"></param>
    /// <returns></returns>
    protected static String GetCamelCase(String name)
    {
        if (name.EqualIgnoreCase("id")) return "id";

        return Char.ToLower(name[0]) + name[1..];
    }

    ///// <summary>是否调试</summary>
    //public static Boolean Debug { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args) => Log?.Info(format, args);

    #endregion 辅助
}