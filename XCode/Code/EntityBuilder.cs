using System.Reflection;
using System.Text;
using NewLife;
using NewLife.Collections;
using NewLife.Log;
using NewLife.Reflection;
using XCode.DataAccessLayer;

namespace XCode.Code;

/// <summary>实体类生成器</summary>
public class EntityBuilder : ClassBuilder
{
    #region 属性
    /// <summary>业务类</summary>
    public Boolean Business { get; set; }

    /// <summary>合并业务类，当业务类已存在时。默认true</summary>
    public Boolean MergeBusiness { get; set; } = true;

    /// <summary>所有表类型名。用于扩展属性</summary>
    public IList<IDataTable> AllTables { get; set; } = new List<IDataTable>();

    /// <summary>实体类生成选型</summary>
    public EntityBuilderOption EntityOption => Option as EntityBuilderOption;
    #endregion 属性

    #region 静态快速

    /// <summary>修正模型文件</summary>
    /// <param name="xmlFile"></param>
    /// <param name="option"></param>
    /// <param name="atts"></param>
    /// <param name="tables"></param>
    /// <param name="log"></param>
    public static void FixModelFile(String xmlFile, BuilderOption option, IDictionary<String, String> atts, IList<IDataTable> tables, ILog? log = null)
    {
        // 保存文件名
        if (xmlFile.IsNullOrEmpty()) xmlFile = atts["ModelFile"];

        // 给默认字段赋值
        var def = option.GetType().CreateInstance() as BuilderOption;
        foreach (var pi in option.GetType().GetProperties(true))
        {
            var val = option.GetValue(pi);
            if (pi.PropertyType == typeof(String) && val is String str)
            {
                if (str.IsNullOrEmpty()) option.SetValue(pi, def.GetValue(pi));
            }
            else
            {
                if (val == null) option.SetValue(pi, def.GetValue(pi));
            }
        }

        // 反哺。确保输出空特性
        //atts["Output"] = option.Output + "";
        //atts["NameSpace"] = option.Namespace + "";
        //atts["ConnName"] = option.ConnName + "";
        //atts["DisplayName"] = option.DisplayName + "";
        //atts["BaseClass"] = option.BaseClass + "";

        // 生成决定是否生成魔方代码
        //atts["CubeOutput"] = option.Items?["CubeOutput"];
        //atts["CubeProject"] = option.Items?["CubeProject"];

        // 清理不再使用的历史配置项
        atts.Remove("NameIgnoreCase");
        atts.Remove("IgnoreNameCase");
        //atts.Remove("ChineseFileName");
        atts.Remove("ModelFile");
        atts.Remove("RenderGenEntity");

        foreach (var item in tables)
        {
            item.Properties.Remove("RenderGenEntity");
        }

        // 格式化处理字段名
        //if (Enum.TryParse<NameFormats>(atts["NameFormat"], true, out var format) && format > NameFormats.Default)
        if (option is EntityBuilderOption opt && opt.NameFormat > NameFormats.Default)
        {
            log?.Info("处理表名字段名为：{0}", opt.NameFormat);

            var resolve = ModelResolver.Current;
            foreach (var dt in tables)
            {
                if (dt.TableName.IsNullOrEmpty() || dt.TableName == dt.Name)
                    dt.TableName = resolve.GetDbName(dt.Name, opt.NameFormat);

                foreach (var col in dt.Columns)
                {
                    if (col.ColumnName.IsNullOrEmpty() || col.ColumnName == col.Name)
                        col.ColumnName = resolve.GetDbName(col.Name, opt.NameFormat);
                }
            }
        }

        // 雪花Id主键，默认设置数据规模DataScale
        foreach (var table in tables)
        {
            if (table.Columns.Any(e => !e.DataScale.IsNullOrEmpty())) continue;

            // 雪花Id主键，默认设置数据规模DataScale
            if (table.PrimaryKeys.Length == 1)
            {
                var column = table.PrimaryKeys[0];
                if (!column.Identity && column.DataType == typeof(Int64))
                {
                    column.DataScale = "time";
                    continue;
                }
            }

            // 只读日志表
            if (table.InsertOnly)
            {
                // 第一个时间日期索引字段
                IDataColumn? column = null;
                foreach (var di in table.Indexes.OrderBy(e => e.Columns.Length).OrderByDescending(e => e.Unique).ThenByDescending(e => e.PrimaryKey))
                {
                    if (di.Columns == null || di.Columns.Length == 0) continue;

                    var col = table.GetColumn(di.Columns[0]);
                    if (col != null && col.DataType == typeof(DateTime))
                    {
                        column = col;
                        break;
                    }
                }

                if (column != null && column.DataType == typeof(DateTime))
                {
                    column.DataScale = "time";
                    continue;
                }
            }
        }

        // 更新xsd
        atts["xmlns"] = "https://newlifex.com/Model202407.xsd";
        atts["xs:schemaLocation"] = "https://newlifex.com https://newlifex.com/Model202407.xsd";

        // 版本和教程
        //var asm = AssemblyX.Create(Assembly.GetExecutingAssembly());
        //atts["Version"] = asm.FileVersion + "";
        atts["Document"] = "https://newlifex.com/xcode/model";
        //if (option is EntityBuilderOption opt2)
        //{
        //    opt2.Version = asm.FileVersion + "";
        //    opt2.Document = "https://newlifex.com/xcode/model";
        //}

        // 保存模型文件
        var xmlContent = File.ReadAllText(xmlFile);
        var xml2 = ModelHelper.ToXml(tables, option, atts);
        if (xmlContent != xml2)
        {
            log?.Info("修正模型：{0}", xmlFile);

            File.WriteAllText(xmlFile, xml2, Encoding.UTF8);
        }
    }

    /// <summary>为Xml模型文件生成实体类</summary>
    /// <param name="tables">模型文件</param>
    /// <param name="option">生成可选项</param>
    /// <param name="log"></param>
    public static Int32 BuildTables(IList<IDataTable> tables, EntityBuilderOption option, ILog? log = null)
    {
        if (tables == null || tables.Count == 0) return 0;

        if (option == null)
            option = new EntityBuilderOption();
        else
            option = (option.Clone() as EntityBuilderOption)!;
        //option.Partial = true;

        var output = option.Output;
        if (output.IsNullOrEmpty()) output = ".";
        log?.Info("生成实体类 {0}", output.GetBasePath());

        var displayNames = new HashSet<String>();

        var count = 0;
        foreach (var item in tables)
        {
            // 跳过排除项
            if (option.Excludes.Contains(item.Name)) continue;
            if (option.Excludes.Contains(item.TableName)) continue;

            var builder = new EntityBuilder
            {
                AllTables = tables,
                Option = option.Clone(),
                Log = log
            };

            // 不能对option赋值，否则所有table的ModelNameForToModel就相同了
            //if (option.ModelNameForToModel.IsNullOrEmpty())
            //{
            //    option.ModelNameForToModel = item.Name;
            //}
            if (builder.EntityOption.ModelNameForToModel.IsNullOrEmpty())
            {
                builder.EntityOption.ModelNameForToModel = item.Name;
            }

            // 如果已存在重复中文名，则使用英文名
            var chinese = option.ChineseFileName;
            if (chinese && !item.DisplayName.IsNullOrEmpty())
            {
                if (displayNames.Contains(item.DisplayName))
                    chinese = false;
                else
                    displayNames.Add(item.DisplayName);
            }

            builder.Load(item);

            builder.Execute();
            builder.Save(null, true, chinese);

            builder.Clear();
            builder.Business = true;
            builder.Execute();
            builder.Save(null, false, chinese);
        }

        return count;
    }

    #endregion 静态快速

    #region 方法

    /// <summary>加载数据表</summary>
    /// <param name="table"></param>
    public override void Load(IDataTable table)
    {
        Table = table;

        var option = EntityOption;

        base.Load(table);

        // 连接名
        var connName = table.ConnName;
        if (!connName.IsNullOrEmpty()) option.ConnName = connName;

        // 基类
        var str = table.Properties["BaseClass"];
        if (!str.IsNullOrEmpty()) option.BaseClass = str;

        // Copy模版
        var modelClass = table.Properties["ModelClass"];
        var modelInterface = table.Properties["ModelInterface"];
        if (!modelInterface.IsNullOrEmpty())
        {
            option.BaseClass = modelInterface;
            option.ModelNameForCopy = modelInterface;
        }
        else if (!modelClass.IsNullOrEmpty())
            option.ModelNameForCopy = modelClass;
    }

    #endregion 方法

    #region 基础
    /// <summary>生成前的准备工作。计算类型以及命名空间等</summary>
    protected override void Prepare()
    {
        // 增加常用命名空间
        AddNameSpace();

        base.Prepare();
    }

    /// <summary>增加常用命名空间</summary>
    protected virtual void AddNameSpace()
    {
        var us = Option.Usings;

        us.Add("NewLife");
        us.Add("NewLife.Data");
        us.Add("XCode");
        us.Add("XCode.Cache");
        us.Add("XCode.Configuration");
        us.Add("XCode.DataAccessLayer");
        //us.Add("XCode.Common");
        if (Business) us.Add("XCode.Shards");

        if (Business)
        {
            //us.Add("System.ComponentModel.DataAnnotations");//属性验证
            us.Add("System.IO");
            us.Add("System.Linq");
            us.Add("System.Reflection");
            us.Add("System.Text");
            us.Add("System.Threading.Tasks");
            us.Add("System.Web");
            us.Add("System.Web.Script.Serialization");
            us.Add("System.Xml.Serialization");
            us.Add("System.Runtime.Serialization");

            us.Add("NewLife");
            us.Add("NewLife.Model");
            us.Add("NewLife.Log");
            us.Add("NewLife.Reflection");
            us.Add("NewLife.Threading");
            us.Add("NewLife.Web");
            us.Add("XCode.Cache");
            us.Add("XCode.Membership");
            us.Add("XCode.Shards");
        }
    }

    /// <summary>获取基类</summary>
    /// <returns></returns>
    protected override String? GetBaseClass()
    {
        var baseClass = Option.BaseClass;
        //if (Option.HasIModel)
        //{
        //    if (!baseClass.IsNullOrEmpty()) baseClass += ", ";
        //    baseClass += "IModel";
        //}

        var bs = baseClass?.Split(',').Select(e => e.Trim()).ToList() ?? [];

        // 数据类的基类只有接口，业务类基类则比较复杂
        var name = "";
        if (Business)
        {
            // 数据类只要实体基类
            name = bs.FirstOrDefault(e => e.Contains("Entity"));
            if (name.IsNullOrEmpty()) name = "Entity";

            name = $"{name}<{ClassName}>";
        }
        else
        {
            // 有可能实现了接口拷贝
            var model = Option.ModelNameForCopy;
            if (!model.IsNullOrEmpty())
            {
                if (model.StartsWith("I")) bs.Add(model);

                bs.Add($"IEntity<{model}>");
            }

            // 数据类不要实体基类
            bs = bs.Where(e => e != "Entity" && !e.StartsWithIgnoreCase("Entity<", "EntityBase<")).ToList();
            if (bs.Count > 0) name = bs.Distinct().Join(", ");
        }

        return name?.Replace("{name}", ClassName);
    }

    /// <summary>保存</summary>
    /// <param name="ext"></param>
    /// <param name="overwrite"></param>
    /// <param name="chineseFileName"></param>
    public override String Save(String? ext = null, Boolean overwrite = true, Boolean chineseFileName = true)
    {
        if (ext.IsNullOrEmpty() && Business)
        {
            ext = ".Biz.cs";
            //overwrite = false;
        }

        // Biz业务文件已存在时，部分覆盖
        if (Business && !overwrite && MergeBusiness)
        {
            var fileName = GetFileName(ext, chineseFileName);
            if (File.Exists(fileName))
            {
                Merge(fileName);
                return fileName;
            }
        }

        return base.Save(ext, overwrite, chineseFileName); ;
    }

    /// <summary>合并当前生成内容到旧文件中</summary>
    /// <param name="fileName"></param>
    public void Merge(String fileName)
    {
        // 新旧代码分组
        var newLines = ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        var oldLines = File.ReadAllLines(fileName).ToList();

        var changed = 0;

        // 合并扩展属性
        {
            var sname = "#region 扩展属性";
            var newNs = Find(newLines, sname, "#endregion");
            var oldNs = Find(oldLines, sname, "#endregion");

            // 两个都有才合并
            if (newNs != null && oldNs != null)
            {
                // endregion 所在行
                var p = oldNs.Start + oldNs.Count - 1;
                foreach (var item in newNs.Sections)
                {
                    // 如果旧文件中不存在，则插入
                    if (!oldNs.Sections.Any(e => e.Name == item.Name))
                    {
                        // 前面有变化，需要插入空行
                        if (changed > 0 || oldNs.Sections.Count > 0) oldLines.Insert(p++, "");

                        foreach (var elm in item.Lines)
                        {
                            oldLines.Insert(p++, elm);
                        }
                        changed++;
                    }
                }
            }
        }

        // 合并扩展查询
        {
            var sname = "#region 扩展查询";
            var newNs = Find(newLines, sname, "#endregion");
            var oldNs = Find(oldLines, sname, "#endregion");

            // 两个都有才合并
            if (newNs != null && oldNs != null)
            {
                // endregion 所在行
                var p = oldNs.Start + oldNs.Count - 1;
                foreach (var item in newNs.Sections)
                {
                    // 如果旧文件中不存在，则插入
                    //if (!oldNs.Sections.Any(e => e.Name == item.Name))
                    // 可能参数名大小写不一致
                    if (!oldNs.Sections.Any(e => e.Name.EqualIgnoreCase(item.Name)))
                    {
                        // 前面有变化，需要插入空行
                        if (changed > 0 || oldNs.Sections.Count > 0) oldLines.Insert(p++, "");

                        foreach (var elm in item.Lines)
                        {
                            oldLines.Insert(p++, elm);
                        }
                        changed++;
                    }
                }
            }
        }

        if (changed > 0) File.WriteAllText(fileName, oldLines.Join(Environment.NewLine).Trim());
    }

    class MyRange
    {
        public Int32 Start { get; set; }
        public Int32 Count { get; set; }
        public IList<MemberSection>? Sections { get; set; }
    }

    MyRange Find(IList<String> lines, String start, String end)
    {
        var s = -1;
        var e = -1;
        var flag = 0;
        for (var i = 0; i < lines.Count && flag < 2; i++)
        {
            if (flag == 0)
            {
                if (lines[i].Contains(start))
                {
                    s = i;
                    flag = 1;
                }
            }
            else if (flag == 1)
            {
                if (lines[i].Contains(end))
                {
                    e = i;
                    flag = 2;
                }
            }
        }

        if (s < 0 || e < 0) return null;

        var ns = lines.Skip(s).Take(e - s + 1).ToArray();
        var list = MemberSection.Parse(ns);
        foreach (var item in list)
        {
            item.StartLine += s;
        }

        return new MyRange { Start = s, Count = e - s + 1, Sections = list };
    }

    /// <summary>生成尾部</summary>
    protected override void OnExecuted()
    {
        // 类接口
        WriteLine("}");

        //if (!Business)
        //{
        //    WriteLine();
        //    BuildInterface();
        //}

        //if (!Option.Namespace.IsNullOrEmpty())
        //{
        //    Writer.Write("}");
        //}
    }

    /// <summary>生成主体</summary>
    protected override void BuildItems()
    {
        if (Business)
        {
            BuildAction();

            WriteLine();
            BuildExtendProperty();

            WriteLine();
            BuildExtendSearch();

            WriteLine();
            BuildSearch();

            WriteLine();
            BuildBusiness();
        }
        else
        {
            base.BuildItems();

            // 生成拷贝函数。需要有基类
            //var bs = Option.BaseClass.Split(",").Select(e => e.Trim()).ToArray();
            //var model = bs.FirstOrDefault(e => e[0] == 'I' && e.Contains("{name}"));
            var model = Option.ModelNameForCopy;
            if (!model.IsNullOrEmpty())
            {
                WriteLine();
                BuildCopy(model.Replace("{name}", Table.Name));
            }

            WriteLine();
            BuildIndexItems();

            WriteLine();
            BuildMap();

            WriteLine();
            BuildFieldName();
        }
    }

    #endregion 基础

    #region 数据类

    /// <summary>实体类头部</summary>
    protected override void BuildAttribute()
    {
        if (Business)
        {
            //WriteLine("/// <summary>{0}</summary>", Table.Description);
            return;
        }

        // 注释
        var des = Table.Description;
        if (!Option.DisplayNameTemplate.IsNullOrEmpty())
        {
            des = Table.Description.TrimStart(Table.DisplayName, "。");
            des = Option.DisplayNameTemplate.Replace("{displayName}", Table.DisplayName) + "。" + des;
        }
        WriteLine("/// <summary>{0}</summary>", des);
        WriteLine("[Serializable]");
        WriteLine("[DataObject]");

        if (!des.IsNullOrEmpty()) WriteLine("[Description(\"{0}\")]", des);

        var dt = Table;
        foreach (var item in dt.Indexes)
        {
            WriteLine("[BindIndex(\"{0}\", {1}, \"{2}\")]", item.Name, item.Unique.ToString().ToLower(), item.Columns.Join());
        }

        var connName = dt.Properties["ConnName"];
        if (connName.IsNullOrEmpty()) connName = EntityOption.ConnName;
        WriteLine("[BindTable(\"{0}\", Description = \"{1}\", ConnName = \"{2}\", DbType = DatabaseType.{3})]", dt.TableName, dt.Description, connName, dt.DbType);
    }

    /// <summary>生成每一项</summary>
    protected override void BuildItem(IDataColumn column)
    {
        var dc = column;

        var type = dc.Properties["Type"];
        if (type.IsNullOrEmpty()) type = dc.DataType?.Name;
        if (type == "String" && Option.Nullable && column.Nullable) type = "String?";

        // 字段
        if (type == "String" && Option.Nullable)
        {
            if (column.Nullable)
                WriteLine("private {0} _{1};", type, dc.Name);
            else
                WriteLine("private {0} _{1} = null!;", type, dc.Name);
        }
        else
            WriteLine("private {0} _{1};", type, dc.Name);

        // 注释
        var des = dc.Description;
        WriteLine("/// <summary>{0}</summary>", des);

        // 附加特性
        if (dc.Properties.TryGetValue("Attribute", out var att))
        {
            // 兼容支持新旧两种格式
            var str = att.Replace("{name}", dc.Name);
            if (str[0] != '[')
                WriteLine("[{0}]", str);
            else
                WriteLine("{0}", str);//lps 2023-07-22 去掉两边的方括号，以便支持多个验证。例如：<Column Name="TestQuantity" DataType="Int32" Description="测试数量" Attribute="[Required(ErrorMessage = &quot;{0}必须填写&quot;)][Range(1, 100,ErrorMessage =&quot;超出范围&quot;)]" />
        }

        // 分类特性
        if (dc.Properties.TryGetValue("Category", out att) && !att.IsNullOrEmpty())
            WriteLine("[Category(\"{0}\")]", att);

        //if (!Option.Pure)
        {
            var dis = dc.DisplayName;
            if (!dis.IsNullOrEmpty()) WriteLine("[DisplayName(\"{0}\")]", dis);

            if (!des.IsNullOrEmpty()) WriteLine("[Description(\"{0}\")]", des);
        }

        WriteLine("[DataObjectField({0}, {1}, {2}, {3})]", dc.PrimaryKey.ToString().ToLower(), dc.Identity.ToString().ToLower(), dc.Nullable.ToString().ToLower(), dc.Length);

        var sb = Pool.StringBuilder.Get();
        sb.AppendFormat("[BindColumn(\"{0}\", \"{1}\", \"{2}\"", dc.ColumnName, dc.Description, dc.RawType);

        // 元素类型
        if (!dc.ItemType.IsNullOrEmpty()) sb.AppendFormat(", ItemType = \"{0}\"", dc.ItemType);

        // 支持生成带精度的特性
        if (dc.Precision > 0 || dc.Scale > 0) sb.AppendFormat(", Precision = {0}, Scale = {1}", dc.Precision, dc.Scale);

        // 默认值
        if (!dc.DefaultValue.IsNullOrEmpty()) sb.AppendFormat(", DefaultValue = \"{0}\"", dc.DefaultValue);

        // 数据规模
        if (!dc.DataScale.IsNullOrEmpty()) sb.AppendFormat(", DataScale = \"{0}\"", dc.DataScale);

        ////添加自定义控件默认值
        //if (!dc.ItemDefaultValue.IsNullOrEmpty()) sb.AppendFormat(", ItemDefaultValue = \"{0}\"", dc.ItemDefaultValue);

        if (dc.Master) sb.Append(", Master = true");

        sb.Append(")]");

        WriteLine(sb.Put(true));

        WriteLine("public {0} {1} {{ get => _{1}; set {{ if (OnPropertyChanging(\"{1}\", value)) {{ _{1} = value; OnPropertyChanged(\"{1}\"); }} }} }}", type, dc.Name);
    }

    /// <summary>生成索引访问器</summary>
    protected override void BuildIndexItems()
    {
        WriteLine("#region 获取/设置 字段值");
        WriteLine("/// <summary>获取/设置 字段值</summary>");
        WriteLine("/// <param name=\"name\">字段名</param>");
        WriteLine("/// <returns></returns>");
        if (Option.Nullable)
            WriteLine("public override Object? this[String name]");
        else
            WriteLine("public override Object this[String name]");
        WriteLine("{");

        // get
        WriteLine("get => name switch");
        WriteLine("{");
        {
            foreach (var column in Table.Columns)
            {
                // 跳过排除项
                if (Option.Excludes.Contains(column.Name)) continue;
                if (Option.Excludes.Contains(column.ColumnName)) continue;

                WriteLine("\"{0}\" => _{0},", column.Name);
            }
            //WriteLine("default: return base[name];");
            WriteLine("_ => base[name]");
        }
        WriteLine("};");

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
                if (Option.Excludes.Contains(column.Name)) continue;
                if (Option.Excludes.Contains(column.ColumnName)) continue;

                var type = column.Properties["Type"];
                if (type.IsNullOrEmpty()) type = column.DataType?.Name;

                if (!type.IsNullOrEmpty())
                {
                    if (!type.Contains(".") && conv.GetMethod("To" + type, [typeof(Object)]) != null)
                    {
                        switch (type)
                        {
                            case "Int32":
                                WriteLine("case \"{0}\": _{0} = value.ToInt(); break;", column.Name);
                                break;

                            case "Int64":
                                WriteLine("case \"{0}\": _{0} = value.ToLong(); break;", column.Name);
                                break;

                            case "Double":
                                WriteLine("case \"{0}\": _{0} = value.ToDouble(); break;", column.Name);
                                break;

                            case "Boolean":
                                WriteLine("case \"{0}\": _{0} = value.ToBoolean(); break;", column.Name);
                                break;

                            case "DateTime":
                                WriteLine("case \"{0}\": _{0} = value.ToDateTime(); break;", column.Name);
                                break;

                            default:
                                WriteLine("case \"{0}\": _{0} = Convert.To{1}(value); break;", column.Name, type);
                                break;
                        }
                    }
                    else
                    {
                        try
                        {
                            // 特殊支持枚举
                            if (column.DataType.IsInt())
                                WriteLine("case \"{0}\": _{0} = ({1})value.ToInt(); break;", column.Name, type);
                            else
                                WriteLine("case \"{0}\": _{0} = ({1})value; break;", column.Name, type);
                        }
                        catch (Exception ex)
                        {
                            XTrace.WriteException(ex);
                            WriteLine("case \"{0}\": _{0} = ({1})value; break;", column.Name, type);
                        }
                    }
                }
            }
            WriteLine("default: base[name] = value; break;");
            WriteLine("}");
        }
        WriteLine("}");

        WriteLine("}");
        WriteLine("#endregion");
    }

    private void BuildFieldName()
    {
        WriteLine("#region 字段名");

        WriteLine("/// <summary>取得{0}字段信息的快捷方式</summary>", Table.DisplayName);
        WriteLine("public partial class _");
        WriteLine("{");
        foreach (var column in Table.Columns)
        {
            // 跳过排除项
            if (Option.Excludes.Contains(column.Name)) continue;
            if (Option.Excludes.Contains(column.ColumnName)) continue;

            WriteLine("/// <summary>{0}</summary>", column.Description);
            WriteLine("public static readonly Field {0} = FindByName(\"{0}\");", column.Name);
            WriteLine();
        }
        WriteLine("static Field FindByName(String name) => Meta.Table.FindByName(name);");
        WriteLine("}");

        WriteLine();

        WriteLine("/// <summary>取得{0}字段名称的快捷方式</summary>", Table.DisplayName);
        WriteLine("public partial class __");
        WriteLine("{");
        var k = Table.Columns.Count;
        foreach (var column in Table.Columns)
        {
            // 跳过排除项
            if (Option.Excludes.Contains(column.Name)) continue;
            if (Option.Excludes.Contains(column.ColumnName)) continue;

            WriteLine("/// <summary>{0}</summary>", column.Description);
            WriteLine("public const String {0} = \"{0}\";", column.Name);
            if (--k > 0) WriteLine();
        }
        WriteLine("}");

        WriteLine("#endregion");
    }

    /// <summary>扩展属性</summary>
    protected virtual void BuildMap()
    {
        WriteLine("#region 关联映射");

        foreach (var column in Table.Columns)
        {
            // 跳过排除项
            if (Option.Excludes.Contains(column.Name)) continue;
            if (Option.Excludes.Contains(column.ColumnName)) continue;

            if (column.Map.IsNullOrEmpty()) continue;

            // 格式：表+主键+显示字段+属性名
            // Role@Id@Name
            // XCode.Membership.Area@Id@Path@AreaPath
            // Tenant@Id@$
            // $表示用ToString()替代显示字段
            var ss = column.Map.Split('@');
            var fullName = ss[0];
            var className = fullName;
            var p = className.LastIndexOf('.');
            if (p > 0) className = className[(p + 1)..];

            // 找到映射表，有可能映射表在别的模型集，mapTable可能为空，此时直接使用类名
            var mapTable = AllTables.FirstOrDefault(e => className.EqualIgnoreCase(e.Name, e.TableName));
            //if (mapTable == null) continue;

            IDataColumn? mapId = null;
            if (mapTable != null)
                mapId = ss.Length > 1 ? mapTable.GetColumn(ss[1]) : mapTable.PrimaryKeys.FirstOrDefault();
            //if (mapId == null) continue;
            var mapIdName = mapId?.Name ?? ss[1];

            IDataColumn? mapName = null;
            if (mapTable != null)
            {
                mapName = ss.Length > 2 && ss[2] != "$" ? mapTable.GetColumn(ss[2]) : null;
                mapName ??= mapTable.Columns.FirstOrDefault(e => e.Master);
                mapName ??= mapTable.GetColumn("Name");
            }
            else
            {
                // 默认字符串类型
                mapName = new XField { Name = ss[2], DataType = typeof(String) };
            }

            // 属性名
            var name = column.Name.TrimEnd("Id", "ID", mapIdName);
            if (Table.Columns.Any(e => e.Name.EqualIgnoreCase(name))) name = "My" + name;

            // 备注
            var dis = column.DisplayName;
            if (dis.IsNullOrEmpty()) dis = mapTable?.DisplayName;

            WriteLine("/// <summary>{0}</summary>", dis);
            WriteLine("[XmlIgnore, IgnoreDataMember, ScriptIgnore]");
            if (Option.Nullable)
                WriteLine("public {0}? {1} => Extends.Get(nameof({1}), k => {0}.FindBy{2}({3}));", fullName, name, mapIdName, column.Name);
            else
                WriteLine("public {0} {1} => Extends.Get(nameof({1}), k => {0}.FindBy{2}({3}));", fullName, name, mapIdName, column.Name);

            var myName = ss.Length > 3 ? ss[3] : null;
            if (myName.IsNullOrEmpty())
            {
                myName = column.Name.TrimEnd("Id", "ID", mapIdName);
                if (mapName != null && mapName.Name != "$") myName += mapName.Name;
            }

            // 扩展属性有可能恰巧跟已有字段同名
            if (!myName.IsNullOrEmpty() && !Table.Columns.Any(e => e.Name.EqualIgnoreCase(myName)))
            {
                var type = Option.Nullable ? "String?" : "String";

                WriteLine();
                WriteLine("/// <summary>{0}</summary>", dis);
                WriteLine("[Map(nameof({0}), typeof({1}), \"{2}\")]", column.Name, fullName, mapIdName);
                if (column.Properties.TryGetValue("Category", out var att) && !att.IsNullOrEmpty())
                    WriteLine("[Category(\"{0}\")]", att);
                if (ss.Length > 2 && ss[2] == "$")
                    WriteLine("public {2} {0} => {1}?.ToString();", myName, name, type);
                else if (mapName != null)
                {
                    if (ss.Length > 2 && mapName.DataType == typeof(String))
                        WriteLine("public {3} {0} => {1}?.{2};", myName, name, ss[2], type);
                    else if (mapName.DataType == typeof(String))
                        WriteLine("public {3} {0} => {1}?.{2};", myName, name, mapName.Name, type);
                    else
                        WriteLine("public {3} {0} => {1} != null ? {1}.{2} : 0;", myName, name, mapName.Name, mapName.DataType.Name);
                }
            }

            WriteLine();
        }

        WriteLine("#endregion");
    }

    #endregion 数据类

    #region 业务类

    /// <summary>对象操作</summary>
    protected virtual void BuildAction()
    {
        WriteLine("#region 对象操作");

        // 静态构造函数
        BuildCctor();

        // 验证函数
        WriteLine();
        BuildValid();

        // 初始化数据
        WriteLine();
        BuildInitData();

        // 重写添删改
        WriteLine();
        BuildOverride();

        WriteLine("#endregion");

        if (Table.Properties["NeedHistory"].ToBoolean())
        {
            WriteLine();
            WriteLine("#region  添加历史记录");
            BuildHistory();
            WriteLine("#endregion");
        }
    }

    /// <summary>生成静态构造函数</summary>
    protected virtual void BuildCctor()
    {
        WriteLine("static {0}()", ClassName);
        WriteLine("{");
        {
            // 只插入日志
            if (Table.InsertOnly)
            {
                WriteLine("Meta.Table.DataTable.InsertOnly = true;");
                WriteLine();
            }

            // 第一个非自增非主键整型字段，生成累加字段代码
            var dc = Table.Columns.FirstOrDefault(e => !e.Identity && !e.PrimaryKey && (e.DataType == typeof(Int32) || e.DataType == typeof(Int64)));
            if (dc != null)
            {
                WriteLine("// 累加字段，生成 Update xx Set Count=Count+1234 Where xxx");
                WriteLine("//var df = Meta.Factory.AdditionalFields;");
                WriteLine("//df.Add(nameof({0}));", dc.Name);
            }

            // 自动分表
            dc = Table.Columns.FirstOrDefault(e => !e.Identity && e.PrimaryKey && e.DataType == typeof(Int64));
            if (dc != null)
            {
                WriteLine("// 按天分表");
                WriteLine("//Meta.ShardPolicy = new TimeShardPolicy(nameof({0}), Meta.Factory)", dc.Name);
                WriteLine("//{");
                WriteLine("//    TablePolicy = \"{0}_{1:yyyyMMdd}\",");
                WriteLine("//    Step = TimeSpan.FromDays(1),");
                WriteLine("//};");
            }

            var ns = new HashSet<String>(Table.Columns.Select(e => e.Name), StringComparer.OrdinalIgnoreCase);
            WriteLine();
            WriteLine("// 过滤器 UserModule、TimeModule、IPModule");
            if (ns.Contains("CreateUserID") || ns.Contains("CreateUser") || ns.Contains("UpdateUserID") || ns.Contains("UpdateUser"))
                WriteLine("Meta.Modules.Add(new UserModule { AllowEmpty = false });");
            if (ns.Contains("CreateTime") || ns.Contains("UpdateTime"))
                WriteLine("Meta.Modules.Add<TimeModule>();");
            if (ns.Contains("CreateIP") || ns.Contains("UpdateIP"))
                WriteLine("Meta.Modules.Add(new IPModule { AllowEmpty = false });");
            if (ns.Contains("TraceId"))
                WriteLine("Meta.Modules.Add<TraceModule>();");
            if (ns.Contains("TenantId"))
                WriteLine("Meta.Modules.Add<TenantModule>();");

            if (!Table.InsertOnly && !Table.Name.EndsWith("Log") && !Table.Name.EndsWith("History") && !Table.Name.EndsWith("Record"))
            {
                // 实体缓存
                {
                    WriteLine();
                    WriteLine("// 实体缓存");
                    WriteLine("// var ec = Meta.Cache;");
                    WriteLine("// ec.Expire = 60;");
                }

                // 唯一索引不是主键，又刚好是Master，使用单对象缓存从键
                var di = Table.Indexes.FirstOrDefault(e => e.Unique && e.Columns.Length == 1 && (Table.GetColumn(e.Columns[0])?.Master ?? false));
                if (di != null)
                {
                    dc = Table.GetColumn(di.Columns[0]);
                    if (dc != null)
                    {
                        WriteLine();
                        WriteLine("// 单对象缓存");
                        WriteLine("var sc = Meta.SingleCache;");
                        WriteLine("// sc.Expire = 60;");
                        WriteLine("sc.FindSlaveKeyMethod = k => Find(_.{0} == k);", dc.Name);
                        WriteLine("sc.GetSlaveKeyMethod = e => e.{0};", dc.Name);
                    }
                }
            }
        }
        WriteLine("}");
    }

    static String[] _validExcludes = ["CreateUser", "CreateUserIP", "UpdateUser", "UpdateUserIP", "TraceId"];
    /// <summary>数据验证</summary>
    protected virtual void BuildValid()
    {
        WriteLine("/// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>");
        WriteLine("/// <param name=\"method\">添删改方法</param>");
        WriteLine("public override Boolean Valid(DataMethod method)");
        WriteLine("{");
        {
            WriteLine("//if (method == DataMethod.Delete) return true;");
            WriteLine("// 如果没有脏数据，则不需要进行任何处理");
            WriteLine("if (!HasDirty) return true;");

            // 非空判断，字符串且没有默认值
            var cs = Table.Columns.Where(e => !e.Nullable && e.DataType == typeof(String) && e.DefaultValue.IsNullOrEmpty()).ToArray();
            // 剔除CreateUser/UpdateUser等特殊字段
            cs = cs.Where(e => !e.Name.EqualIgnoreCase(_validExcludes)).ToArray();
            if (cs.Length > 0)
            {
                // 有索引的字段判断Empty，不允许空字符串（不利于索引），其它判断null
                var ds = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
                foreach (var di in Table.Indexes)
                {
                    foreach (var item in Table.GetColumns(di.Columns))
                    {
                        if (!ds.Contains(item.Name)) ds.Add(item.Name);
                    }
                }
                // 主要字段也判断
                foreach (var item in Table.Columns)
                {
                    if (item.Master && !ds.Contains(item.Name)) ds.Add(item.Name);
                }

                WriteLine();
                WriteLine("// 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框");
                foreach (var item in cs)
                {
                    if (ds.Contains(item.Name))
                        WriteLine("if ({0}.IsNullOrEmpty()) throw new ArgumentNullException({1}, \"{2}不能为空！\");", item.Name, NameOf(item.Name), item.DisplayName ?? item.Name);
                    else
                        WriteLine("if ({0} == null) throw new ArgumentNullException({1}, \"{2}不能为空！\");", item.Name, NameOf(item.Name), item.DisplayName ?? item.Name);
                }
            }

            WriteLine();
            WriteLine("// 建议先调用基类方法，基类方法会做一些统一处理");
            WriteLine("if (!base.Valid(method)) return false;");

            WriteLine();
            WriteLine("// 在新插入数据或者修改了指定字段时进行修正");

            // 保留小数位数
            cs = Table.Columns.Where(e => e.DataType == typeof(Double)).ToArray();
            if (cs.Length > 0)
            {
                WriteLine();
                WriteLine("// 保留2位小数");
                foreach (var item in cs)
                {
                    WriteLine("//{0} = Math.Round({0}, 2);", item.Name);
                }
            }

            // 处理当前已登录用户信息
            cs = Table.Columns.Where(e => e.DataType == typeof(Int32) && e.Name.EqualIgnoreCase("CreateUserID", "UpdateUserID")).ToArray();
            if (cs.Length > 0)
            {
                WriteLine();
                WriteLine("// 处理当前已登录用户信息，可以由UserModule过滤器代劳");
                WriteLine("/*var user = ManageProvider.User;");
                WriteLine("if (user != null)");
                WriteLine("{");
                foreach (var item in cs)
                {
                    if (item.Name.EqualIgnoreCase("CreateUserID"))
                        WriteLine("if (method == DataMethod.Insert && !Dirtys[{0}]) {1} = user.ID;", NameOf(item.Name), item.Name);
                    else
                        WriteLine("if (!Dirtys[{0}]) {1} = user.ID;", NameOf(item.Name), item.Name);
                }
                WriteLine("}*/");
            }

            var dc = Table.Columns.FirstOrDefault(e => e.Name.EqualIgnoreCase("CreateTime"));
            if (dc != null) WriteLine("//if (method == DataMethod.Insert && !Dirtys[{0}]) {1} = DateTime.Now;", NameOf(dc.Name), dc.Name);

            dc = Table.Columns.FirstOrDefault(e => e.Name.EqualIgnoreCase("UpdateTime"));
            if (dc != null) WriteLine("//if (!Dirtys[{0}]) {1} = DateTime.Now;", NameOf(dc.Name), dc.Name);

            dc = Table.Columns.FirstOrDefault(e => e.Name.EqualIgnoreCase("CreateIP"));
            if (dc != null) WriteLine("//if (method == DataMethod.Insert && !Dirtys[{0}]) {1} = ManageProvider.UserHost;", NameOf(dc.Name), dc.Name);

            dc = Table.Columns.FirstOrDefault(e => e.Name.EqualIgnoreCase("UpdateIP"));
            if (dc != null) WriteLine("//if (!Dirtys[{0}]) {1} = ManageProvider.UserHost;", NameOf(dc.Name), dc.Name);

            // 唯一索引检查唯一性
            var dis = Table.Indexes.Where(e => e.Unique).ToArray();
            if (dis.Length > 0)
            {
                WriteLine();
                WriteLine("// 检查唯一索引");
                foreach (var item in dis)
                {
                    //WriteLine("if (!_IsFromDatabase) CheckExist(isNew, {0});", Table.GetColumns(item.Columns).Select(e => "__." + e.Name).Join(", "));
                    WriteLine("// CheckExist(method == DataMethod.Insert, {0});", Table.GetColumns(item.Columns).Select(e => $"nameof({e.Name})").Join(", "));
                }
            }
            WriteLine();
            WriteLine("return true;");
        }
        WriteLine("}");
    }

    /// <summary>初始化数据</summary>
    protected virtual void BuildInitData()
    {
        var name = ClassName;

        WriteLine("///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>");
        WriteLine("//[EditorBrowsable(EditorBrowsableState.Never)]");
        //zilo555 去掉internal
        WriteLine("//protected override void InitData()");
        WriteLine("//{");
        WriteLine("//    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用");
        WriteLine("//    if (Meta.Session.Count > 0) return;");
        WriteLine();
        WriteLine("//    if (XTrace.Debug) XTrace.WriteLine(\"开始初始化{0}[{1}]数据……\");", name, Table.DisplayName);
        WriteLine();
        WriteLine("//    var entity = new {0}();", name);
        foreach (var column in Table.Columns)
        {
            if (column.Identity) continue;

            // 跳过排除项
            if (Option.Excludes.Contains(column.Name)) continue;
            if (Option.Excludes.Contains(column.ColumnName)) continue;

            switch (column.DataType.GetTypeCode())
            {
                case TypeCode.Boolean:
                    WriteLine("//    entity.{0} = true;", column.Name);
                    break;

                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    WriteLine("//    entity.{0} = 0;", column.Name);
                    break;

                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    WriteLine("//    entity.{0} = 0.0;", column.Name);
                    break;

                case TypeCode.DateTime:
                    WriteLine("//    entity.{0} = DateTime.Now;", column.Name);
                    break;

                case TypeCode.String:
                    WriteLine("//    entity.{0} = \"abc\";", column.Name);
                    break;

                default:
                    break;
            }
        }
        WriteLine("//    entity.Insert();");
        WriteLine();
        WriteLine("//    if (XTrace.Debug) XTrace.WriteLine(\"完成初始化{0}[{1}]数据！\");", name, Table.DisplayName);
        WriteLine("//}");
    }

    /// <summary>重写添删改</summary>
    protected virtual void BuildOverride()
    {
        WriteLine("///// <summary>已重载。基类先调用Valid(true)验证数据，然后在事务保护内调用OnInsert</summary>");
        WriteLine("///// <returns></returns>");
        WriteLine("//public override Int32 Insert()");
        WriteLine("//{");
        WriteLine("//    return base.Insert();");
        WriteLine("//}");
        WriteLine();
        WriteLine("///// <summary>已重载。在事务保护范围内处理业务，位于Valid之后</summary>");
        WriteLine("///// <returns></returns>");
        WriteLine("//protected override Int32 OnDelete()");
        WriteLine("//{");
        WriteLine("//    return base.OnDelete();");
        WriteLine("//}");

    }

    /// <summary>添删改添加历史记录</summary>
    protected virtual void BuildHistory()
    {
        //判断生成的表是否包含历史记录表,包含需要添加历史记录信息表
        var needHistory = Table.Properties.FirstOrDefault(x => x.Key.EqualIgnoreCase("NeedHistory"));
        if (Convert.ToBoolean(needHistory.Value))
        {
            WriteLine("/// <summary>重写添加历史记录信息/summary>");
            WriteLine("/// <returns></returns>");
            WriteLine("public override Int32 Insert()");
            WriteLine("{");
            WriteLine($"using var tran = Entity<{Table.Name}>.Meta.CreateTrans(); ");
            WriteLine("var list = new List<IEntity>();  ");
            WriteLine("var ires = base.OnInsert();  ");
            WriteLine("if (ires > 0) AddHistory(this);  ");
            WriteLine("tran.Commit();  ");
            WriteLine("return ires;  ");
            WriteLine("}");
            WriteLine();
            WriteLine("/// <summary>重写删除历史记录信息</summary>");
            WriteLine("/// <returns></returns>");
            WriteLine("protected override Int32 OnDelete()");
            WriteLine("{");
            WriteLine($"using var tran = Entity<{Table.Name}>.Meta.CreateTrans(); ");
            WriteLine("var list = new List<IEntity>();  ");
            WriteLine("var ires = base.OnDelete();  ");
            WriteLine("if (ires > 0) AddHistory(this);  ");
            WriteLine("tran.Commit();  ");
            WriteLine("return ires;  ");
            WriteLine("}");
            WriteLine("/// <summary>重写更新历史记录信息</summary>");
            WriteLine("/// <returns></returns>");
            WriteLine("protected override Int32 OnUpdate()");
            WriteLine("{");
            WriteLine($"using var tran = Entity<{Table.Name}>.Meta.CreateTrans(); ");
            WriteLine("var list = new List<IEntity>();  ");
            WriteLine("var ires = base.OnUpdate();  ");
            WriteLine("if (ires > 0) AddHistory(this);  ");
            WriteLine("tran.Commit();  ");
            WriteLine("return ires;  ");
            WriteLine("}");

            WriteLine("/// <summary>添加历史记录信息</summary>");
            WriteLine("/// <returns></returns>");
            WriteLine($"private int AddHistory({Table.Name} entity) ");
            WriteLine("{");
            {
                var History = Table.Name + "History" + " entityHistory = new " + Table.Name + "History();";
                WriteLine(History);
                var tablePrimaryKey = Table.PrimaryKeys?.Where(o => o.Name.ToUpper().Contains("ID"));
                var primaryKey = tablePrimaryKey.Count() > 0 ? tablePrimaryKey.FirstOrDefault()?.Name : "ID";
                WriteLine("XCode.Common.DataConversion.CopyProperty(entity, entityHistory);");
                WriteLine($"entityHistory.{primaryKey} = 0;  ");
                WriteLine($"entityHistory.{Table.Name}ID = entity.{primaryKey};  ");
                WriteLine("return entityHistory.Insert();  ");
            }
            WriteLine("}");
        }

    }

    /// <summary>扩展属性</summary>
    protected virtual void BuildExtendProperty()
    {
        WriteLine("#region 扩展属性");

        var first = true;
        foreach (var column in Table.Columns)
        {
            // 跳过排除项
            if (Option.Excludes.Contains(column.Name)) continue;
            if (Option.Excludes.Contains(column.ColumnName)) continue;
            if (!column.Map.IsNullOrEmpty()) continue;

            // 找到名字映射
            var dt = AllTables.FirstOrDefault(
                e => e.PrimaryKeys.Length == 1 &&
                e.PrimaryKeys[0].DataType == column.DataType &&
                (e.Name + e.PrimaryKeys[0].Name).EqualIgnoreCase(column.Name));

            if (dt != null)
            {
                // 属性名
                var pname = dt.Name;

                // 备注
                var dis = column.DisplayName;
                if (dis.IsNullOrEmpty()) dis = dt.DisplayName;

                var pk = dt.PrimaryKeys[0];

                if (!first)
                {
                    WriteLine();
                    first = true;
                }

                WriteLine("/// <summary>{0}</summary>", dis);
                WriteLine("[XmlIgnore, IgnoreDataMember, ScriptIgnore]");
                if (Option.Nullable)
                    WriteLine("public {1}? {1} => Extends.Get({0}, k => {1}.FindBy{3}({2}));", NameOf(pname), dt.Name, column.Name, pk.Name);
                else
                    WriteLine("public {1} {1} => Extends.Get({0}, k => {1}.FindBy{3}({2}));", NameOf(pname), dt.Name, column.Name, pk.Name);

                // 主字段
                var master = dt.Master ?? dt.GetColumn("Name");
                // 扩展属性有可能恰巧跟已有字段同名
                if (master != null && !master.PrimaryKey && !Table.Columns.Any(e => e.Name.EqualIgnoreCase(pname + master.Name)))
                {
                    WriteLine();
                    WriteLine("/// <summary>{0}</summary>", dis);
                    WriteLine("[Map(nameof({0}), typeof({1}), \"{2}\")]", column.Name, dt.Name, pk.Name);
                    if (column.Properties.TryGetValue("Category", out var att) && !att.IsNullOrEmpty())
                        WriteLine("[Category(\"{0}\")]", att);
                    if (master.DataType == typeof(String))
                    {
                        if (Option.Nullable)
                            WriteLine("public {2}? {0}{1} => {0}?.{1};", pname, master.Name, master.DataType.Name);
                        else
                            WriteLine("public {2} {0}{1} => {0}?.{1};", pname, master.Name, master.DataType.Name);
                    }
                    else
                        WriteLine("public {2} {0}{1} => {0} != null ? {0}.{1} : 0;", pname, master.Name, master.DataType.Name);
                }

                //WriteLine();
            }
        }

        WriteLine("#endregion");
    }

    /// <summary>扩展查询</summary>
    protected virtual void BuildExtendSearch()
    {
        WriteLine("#region 扩展查询");

        var names = new List<String>();

        // 主键
        IDataColumn? pk = null;
        if (Table.PrimaryKeys.Length == 1)
        {
            pk = Table.PrimaryKeys[0];
            var name = pk.CamelName();

            var type = pk.Properties["Type"];
            if (type.IsNullOrEmpty()) type = pk.DataType?.Name;

            var methodName = $"FindBy{pk.Name}";
            names.Add(methodName);

            WriteLine("/// <summary>根据{0}查找</summary>", pk.DisplayName);
            WriteLine("/// <param name=\"{0}\">{1}</param>", name, pk.DisplayName);
            WriteLine("/// <returns>实体对象</returns>");
            WriteLine("public static {3} {0}({1} {2})", methodName, type, name, ClassName);
            WriteLine("{");
            {
                if (pk.DataType != null && pk.DataType.IsInt())
                    WriteLine("if ({0} <= 0) return null;", name);
                else if (pk.DataType == typeof(String))
                    WriteLine("if ({0}.IsNullOrEmpty()) return null;", name);

                WriteLine();
                WriteLine("// 实体缓存");
                if (pk.DataType == typeof(String))
                    WriteLine("if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.{0}.EqualIgnoreCase({1}));", pk.Name, name);
                else
                    WriteLine("if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.{0} == {1});", pk.Name, name);

                WriteLine();
                WriteLine("// 单对象缓存");
                WriteLine("return Meta.SingleCache[{0}];", name);

                WriteLine();
                WriteLine("//return Find(_.{0} == {1});", pk.Name, name);
            }
            WriteLine("}");
        }

        // 索引
        foreach (var di in Table.Indexes)
        {
            // 跳过主键
            if (di.Columns.Length == 1 && pk != null && di.Columns[0].EqualIgnoreCase(pk.Name, pk.ColumnName)) continue;

            // 超过3字段索引，不要生成查询函数
            if (di.Columns.Length > 3) continue;

            var cs = Table.GetColumns(di.Columns);
            if (cs == null || cs.Length != di.Columns.Length) continue;

            // 索引最后一个字段如果是主键Id，则该不参与生成查询方法
            if (pk != null && cs[^1].ColumnName.EqualIgnoreCase(pk.ColumnName))
            {
                cs = cs.Take(cs.Length - 1).ToArray();
            }

            // 只有整数和字符串能生成查询函数
            var flag = true;
            foreach (var dc in cs)
            {
                if (dc.DataType.IsInt() || dc.DataType == typeof(String))
                {
                    flag = true;
                }
                else if (dc.DataType == typeof(DateTime))
                {
                    if (dc.Name.EqualIgnoreCase("CreateTime", "UpdateTime"))
                    {
                        flag = false;
                    }
                }
                else
                {
                    flag = false;
                }

                if (!flag) break;
            }
            if (!flag) continue;

            WriteLine();
            WriteLine("/// <summary>根据{0}查找</summary>", cs.Select(e => e.DisplayName).Join("、"));
            foreach (var dc in cs)
            {
                WriteLine("/// <param name=\"{0}\">{1}</param>", dc.CamelName(), dc.DisplayName);
            }

            var ps = new Dictionary<String, String>();
            foreach (var dc in cs)
            {
                var type = dc.Properties["Type"];
                if (type.IsNullOrEmpty()) type = dc.DataType?.Name;

                ps[dc.CamelName()] = type!;
            }
            var args = ps.Join(", ", e => $"{e.Value} {e.Key}");

            // 如果方法名已存在，则不生成
            var methodName = cs.Select(e => e.Name).Join("And");
            methodName = di.Unique ? $"FindBy{methodName}" : $"FindAllBy{methodName}";
            if (names.Contains(methodName)) continue;
            names.Add(methodName);

            // 返回类型
            if (di.Unique)
            {
                WriteLine("/// <returns>{0}</returns>", di.Unique ? "实体对象" : "实体列表");
                WriteLine("public static {2} {0}({1})", methodName, args, ClassName);
                WriteLine("{");
                {
                    if (cs.Length == 1)
                    {
                        var dc = cs[0];
                        if (dc.DataType.IsInt())
                            WriteLine("if ({0} <= 0) return null;", dc.CamelName());
                        else if (dc.DataType == typeof(String))
                            WriteLine("if ({0}.IsNullOrEmpty()) return null;", dc.CamelName());
                    }

                    var exp = new StringBuilder();
                    var wh = new StringBuilder();
                    foreach (var dc in cs)
                    {
                        if (exp.Length > 0) exp.Append(" & ");
                        exp.AppendFormat("_.{0} == {1}", dc.Name, dc.CamelName());

                        if (wh.Length > 0) wh.Append(" && ");
                        if (dc.DataType == typeof(String))
                            wh.AppendFormat("e.{0}.EqualIgnoreCase({1})", dc.Name, dc.CamelName());
                        else
                            wh.AppendFormat("e.{0} == {1}", dc.Name, dc.CamelName());
                    }

                    if (cs.Length == 1) WriteLine();
                    WriteLine("// 实体缓存");
                    WriteLine("if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => {0});", wh);

                    // 单对象缓存
                    if (cs.Length == 1 && cs[0].Master)
                    {
                        WriteLine();
                        WriteLine("// 单对象缓存");
                        WriteLine("//return Meta.SingleCache.GetItemWithSlaveKey({0}) as {1};", cs[0].CamelName(), ClassName);
                    }

                    WriteLine();
                    WriteLine("return Find({0});", exp);
                }
                WriteLine("}");
            }
            else
            {
                WriteLine("/// <returns>{0}</returns>", di.Unique ? "实体对象" : "实体列表");
                WriteLine("public static IList<{2}> {0}({1})", methodName, args, ClassName);
                WriteLine("{");
                {
                    if (cs.Length == 1)
                    {
                        var dc = cs[0];
                        if (dc.DataType.IsInt())
                            WriteLine("if ({0} <= 0) return new List<{1}>();", dc.CamelName(), ClassName);
                        else if (dc.DataType == typeof(String))
                            WriteLine("if ({0}.IsNullOrEmpty()) return new List<{1}>();", dc.CamelName(), ClassName);
                    }

                    var exp = new StringBuilder();
                    var wh = new StringBuilder();
                    foreach (var dc in cs)
                    {
                        if (exp.Length > 0) exp.Append(" & ");
                        exp.AppendFormat("_.{0} == {1}", dc.Name, dc.CamelName());

                        if (wh.Length > 0) wh.Append(" && ");
                        if (dc.DataType == typeof(String))
                            wh.AppendFormat("e.{0}.EqualIgnoreCase({1})", dc.Name, dc.CamelName());
                        else
                            wh.AppendFormat("e.{0} == {1}", dc.Name, dc.CamelName());
                    }

                    if (cs.Length == 1) WriteLine();
                    WriteLine("// 实体缓存");
                    WriteLine("if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => {0});", wh);

                    WriteLine();
                    WriteLine("return FindAll({0});", exp);
                }
                WriteLine("}");
            }
        }

        WriteLine("#endregion");
    }

    /// <summary>高级查询</summary>
    protected virtual void BuildSearch()
    {
        // 收集索引信息，索引中的所有字段都参与，构造一个高级查询模板
        var idx = Table.Indexes ?? [];
        var cs = new List<IDataColumn>();
        if (idx != null && idx.Count > 0)
        {
            // 索引中的所有字段，按照表字段顺序
            var dcs = idx.SelectMany(e => e.Columns).Distinct().ToArray();
            foreach (var dc in Table.Columns)
            {
                // 主键和自增，不参与
                if (dc.PrimaryKey || dc.Identity) continue;

                if (dc.Name.EqualIgnoreCase(dcs) || dc.ColumnName.EqualIgnoreCase(dcs)) cs.Add(dc);
            }
        }

        var returnName = ClassName;

        WriteLine("#region 高级查询");
        if (cs.Count > 0)
        {
            // 时间字段。无差别支持UpdateTime/CreateTime
            var dcTime = cs.FirstOrDefault(e => e.DataType == typeof(DateTime));
            dcTime ??= Table.GetColumns(["UpdateTime", "CreateTime"])?.FirstOrDefault();
            var dcSnow = cs.FirstOrDefault(e => e.PrimaryKey && !e.Identity && e.DataType == typeof(Int64));

            if (dcTime != null) cs.Remove(dcTime);
            cs.RemoveAll(e => e.Name.EqualIgnoreCase("key", "page"));
            if (dcSnow != null || dcTime != null)
                cs.RemoveAll(e => e.Name.EqualIgnoreCase("start", "end"));

            // 可用于关键字模糊搜索的字段
            var keys = Table.Columns.Where(e => e.DataType == typeof(String)).ToList();

            // 注释部分
            WriteLine("/// <summary>高级查询</summary>");
            foreach (var dc in cs)
            {
                WriteLine("/// <param name=\"{0}\">{1}</param>", dc.CamelName(), dc.Description);
            }
            if (dcTime != null)
            {
                WriteLine("/// <param name=\"start\">{0}开始</param>", dcTime.DisplayName);
                WriteLine("/// <param name=\"end\">{0}结束</param>", dcTime.DisplayName);
            }
            else if (dcSnow != null)
            {
                WriteLine("/// <param name=\"start\">{0}开始</param>", dcSnow.DisplayName);
                WriteLine("/// <param name=\"end\">{0}结束</param>", dcSnow.DisplayName);
            }
            WriteLine("/// <param name=\"key\">关键字</param>");
            WriteLine("/// <param name=\"page\">分页参数信息。可携带统计和数据权限扩展查询等信息</param>");
            WriteLine("/// <returns>实体列表</returns>");

            // 参数部分
            //var pis = cs.Join(", ", dc => $"{dc.DataType.Name} {dc.CamelName()}");
            var pis = new StringBuilder();
            foreach (var dc in cs)
            {
                if (pis.Length > 0) pis.Append(", ");

                var type = dc.Properties["Type"];
                if (type.IsNullOrEmpty()) type = dc.DataType?.Name;

                if (dc.DataType == typeof(Boolean))
                    pis.Append($"{type}? {dc.CamelName()}");
                else
                    pis.Append($"{type} {dc.CamelName()}");
            }
            var piTime = dcTime == null ? "" : "DateTime start, DateTime end, ";
            if (pis.Length > 0)
                WriteLine("public static IList<{0}> Search({1}, {2}String key, PageParameter page)", returnName, pis, piTime);
            else
                WriteLine("public static IList<{0}> Search({2}String key, PageParameter page)", returnName, pis, piTime);
            WriteLine("{");
            {
                WriteLine("var exp = new WhereExpression();");

                // 构造表达式
                WriteLine();
                foreach (var dc in cs)
                {
                    if (dc.DataType.IsInt())
                        WriteLine("if ({0} >= 0) exp &= _.{1} == {0};", dc.CamelName(), dc.Name);
                    else if (dc.DataType == typeof(Boolean))
                        WriteLine("if ({0} != null) exp &= _.{1} == {0};", dc.CamelName(), dc.Name);
                    else if (dc.DataType == typeof(String))
                        WriteLine("if (!{0}.IsNullOrEmpty()) exp &= _.{1} == {0};", dc.CamelName(), dc.Name);
                }

                if (dcSnow != null)
                    WriteLine("exp &= _.{0}.Between(start, end, Meta.Factory.Snow);", dcSnow.Name);
                else if (dcTime != null)
                    WriteLine("exp &= _.{0}.Between(start, end);", dcTime.Name);

                if (keys.Count > 0)
                    WriteLine("if (!key.IsNullOrEmpty()) exp &= {0};", keys.Join(" | ", k => $"_.{k.Name}.Contains(key)"));

                // 查询返回
                WriteLine();
                WriteLine("return FindAll(exp, page);");
            }
            WriteLine("}");
        }

        // 字段缓存，用于魔方前台下拉选择
        {
            // 主键和时间字段
            var pk = Table.Columns.FirstOrDefault(e => e.Identity);
            var pname = pk?.Name ?? "Id";
            var dcTime = cs.FirstOrDefault(e => e.DataType == typeof(DateTime));
            var tname = dcTime?.Name ?? "CreateTime";

            // 遍历索引，第一个字段是字符串类型，则为其生成下拉选择
            var count = 0;
            var names = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
            foreach (var di in idx)
            {
                if (di.Columns == null || di.Columns.Length == 0) continue;

                // 单字段唯一索引，不需要
                if (di.Unique && di.Columns.Length == 1) continue;

                var dc = Table.GetColumn(di.Columns[0]);
                if (dc == null || dc.DataType != typeof(String) || dc.Master) continue;

                // 有可能多个索引第一字段相同，不需要重复生成
                var name = dc.Name;
                if (names.Contains(name)) continue;
                names.Add(name);

                WriteLine();
                WriteLine($"// Select Count({pname}) as {pname},{name} From {Table.TableName} Where {tname}>'2020-01-24 00:00:00' Group By {name} Order By {pname} Desc limit 20");
                WriteLine($"static readonly FieldCache<{returnName}> _{name}Cache = new FieldCache<{returnName}>(nameof({name}))");
                WriteLine("{");
                {
                    WriteLine($"//Where = _.{tname} > DateTime.Today.AddDays(-30) & Expression.Empty");
                }
                WriteLine("};");
                WriteLine();
                WriteLine($"/// <summary>获取{dc.DisplayName}列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>");
                WriteLine("/// <returns></returns>");
                WriteLine($"public static IDictionary<String, String> Get{name}List() => _{name}Cache.FindAllName();");

                count++;
            }

            // 如果没有输出，则生成一个注释的模板
            if (count == 0)
            {
                WriteLine();
                WriteLine($"// Select Count({pname}) as {pname},Category From {Table.TableName} Where {tname}>'2020-01-24 00:00:00' Group By Category Order By {pname} Desc limit 20");
                WriteLine($"//static readonly FieldCache<{returnName}> _CategoryCache = new FieldCache<{returnName}>(nameof(Category))");
                WriteLine("//{");
                {
                    WriteLine($"//Where = _.{tname} > DateTime.Today.AddDays(-30) & Expression.Empty");
                }
                WriteLine("//};");
                WriteLine();
                WriteLine("///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>");
                WriteLine("///// <returns></returns>");
                WriteLine("//public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();");
            }
        }

        WriteLine("#endregion");
    }

    /// <summary>业务操作</summary>
    protected virtual void BuildBusiness()
    {
        WriteLine("#region 业务操作");
        var toModel = EntityOption.ModelNameForToModel;
        var model = Option.ModelNameForCopy;
        if (!toModel.IsNullOrEmpty() && !model.IsNullOrEmpty())
        {
            BuildToModel(toModel.Replace("{name}", ClassName), model.Replace("{name}", ClassName));
            WriteLine("");
        }
        WriteLine("#endregion");
    }

    #endregion 业务类
}