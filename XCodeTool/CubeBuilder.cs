using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NewLife;
using NewLife.Collections;
using NewLife.Log;
using NewLife.Reflection;
using XCode.Code;
using XCode.DataAccessLayer;

namespace XCode;

/// <summary>魔方页面生成器</summary>
public class CubeBuilder : ClassBuilder
{
    #region 属性
    /// <summary>区域名</summary>
    public String AreaName { get; set; }

    /// <summary>根命名空间</summary>
    public String RootNamespace { get; set; }

    /// <summary>菜单排序</summary>
    public Int32 Sort { get; set; }

    /// <summary>区域模版</summary>
    public String AreaTemplate { get; set; } = """
        using System.ComponentModel;
        using NewLife;
        using NewLife.Cube;

        namespace {RootNamespace}.Areas.{Name};
        
        [DisplayName("{DisplayName}")]
        public class {Name}Area : AreaBase
        {
            public {Name}Area() : base(nameof({Name}Area).TrimSuffix("Area")) { }
        }
        """;

    /// <summary>控制器模版</summary>
    public String ControllerTemplate { get; set; } = """
        using Microsoft.AspNetCore.Mvc;
        using {EntityNamespace};
        using NewLife;
        using NewLife.Cube;
        using NewLife.Cube.Extensions;
        using NewLife.Cube.ViewModels;
        using NewLife.Log;
        using NewLife.Web;
        using XCode.Membership;
        using static {EntityNamespace}.{EntityName};
        {EnumList}
        namespace {RootNamespace}.Areas.{Name}.Controllers;
        
        /// <summary>{DisplayName}</summary>
        [Menu({Sort}, true, Icon = "fa-table")]
        [{Name}Area]
        public class {ClassName} : {BaseClass}<{EntityName}>
        {
            static {ClassName}()
            {
                //LogOnChange = true;

                //ListFields.RemoveField("Id", "Creator");
                ListFields.RemoveCreateField().RemoveRemarkField();

                //{
                //    var df = ListFields.GetField("Code") as ListField;
                //    df.Url = "?code={Code}";
                //    df.Target = "_blank";
                //}
                //{
                //    var df = ListFields.AddListField("devices", null, "Onlines");
                //    df.DisplayName = "查看设备";
                //    df.Url = "Device?groupId={Id}";
                //    df.DataVisible = e => (e as {EntityName}).Devices > 0;
                //    df.Target = "_frame";
                //}
                //{
                //    var df = ListFields.GetField("Kind") as ListField;
                //    df.GetValue = e => ((Int32)(e as {EntityName}).Kind).ToString("X4");
                //}
                //ListFields.TraceUrl("TraceId");
            }

            //private readonly ITracer _tracer;

            //public {ClassName}(ITracer tracer)
            //{
            //    _tracer = tracer;
            //}

            /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
            /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
            /// <returns></returns>
            protected override IEnumerable<{EntityName}> Search(Pager p)
            {
                //var deviceId = p["deviceId"].ToInt(-1);
                //var enable = p["enable"]?.ToBoolean();
        
                var start = p["dtStart"].ToDateTime();
                var end = p["dtEnd"].ToDateTime();

                return {EntityName}.Search(start, end, p["Q"], p);
            }
        }
        """;
    #endregion

    #region 静态
    /// <summary>生成魔方区域</summary>
    /// <param name="option">可选项</param>
    /// <returns></returns>
    public static Int32 BuildArea(CubeBuilderOption option, ILog log = null)
    {
        if (option == null)
            option = new CubeBuilderOption();
        else
            option = option.Clone() as CubeBuilderOption;

        // 自动识别并修正区域名（主要是大小写）
        var areaName = FindAreaName(option.Output.GetBasePath());
        if (!areaName.IsNullOrEmpty()) return 0;

        // 优先使用路径最后一段作为区域名，其次再用连接名
        areaName = Path.GetFileNameWithoutExtension(option.Output);
        if (areaName.IsNullOrEmpty())
            areaName = option.ConnName;

        var file = $"{areaName}Area.cs";
        file = option.Output.CombinePath(file);
        file = file.GetBasePath();

        // 文件已存在，不要覆盖
        if (File.Exists(file)) return 0;

        // 根命名空间
        var root = FindProjectRootNamespace(option.Output);
        if (root.IsNullOrEmpty()) root = option.ConnName + "Web";

        log?.Info("生成魔方区域 {0} {1}", areaName, file);

        var builder = new CubeBuilder
        {
            RootNamespace = root
        };

        var code = builder.AreaTemplate;

        //code = code.Replace("{Namespace}", option.Namespace);
        code = code.Replace("{RootNamespace}", builder.RootNamespace);
        code = code.Replace("{Name}", areaName);
        code = code.Replace("{DisplayName}", option.DisplayName);

        // 输出到文件
        file.EnsureDirectory(true);
        File.WriteAllText(file, code, Encoding.UTF8);

        return 1;
    }

    /// <summary>生成控制器</summary>
    /// <param name="tables">表集合</param>
    /// <param name="option">可选项</param>
    /// <returns></returns>
    public static Int32 BuildControllers(IList<IDataTable> tables, CubeBuilderOption option = null, ILog log = null)
    {
        if (option == null)
            option = new CubeBuilderOption();
        else
            option = option.Clone() as CubeBuilderOption;

        // 根命名空间
        var root = FindProjectRootNamespace(option.Output);
        if (root.IsNullOrEmpty()) root = option.ConnName + "Web";

        // 自动识别并修正区域名（主要是大小写）
        var areaName = FindAreaName(option.Output);
        if (areaName.IsNullOrEmpty()) areaName = option.ConnName;

        if (option.ClassNameTemplate.IsNullOrEmpty()) option.ClassNameTemplate = "{name}Controller";

        option.Output = option.Output.CombinePath("Controllers");

        log?.Info("生成控制器 {0}", option.Output.GetBasePath());

        var count = 0;
        var n = tables.Count;
        foreach (var item in tables)
        {
            // 跳过排除项
            if (option.Excludes.Contains(item.Name)) continue;
            if (option.Excludes.Contains(item.TableName)) continue;

            var builder = new CubeBuilder
            {
                Table = item,
                Option = option.Clone(),

                AreaName = areaName,
                RootNamespace = root,
                Sort = n * 10,
                Log = log
            };

            if (builder.Option.BaseClass.IsNullOrEmpty())
            {
                if (item.InsertOnly)
                    builder.Option.BaseClass = "ReadOnlyEntityController";
                else
                    builder.Option.BaseClass = "EntityController";
            }

            builder.Load(item);

            builder.Execute();
            builder.Save(null, false, false);

            count++;
            n--;
        }

        return count;
    }

    static String FindAreaName(String dir)
    {
        var di = dir.GetBasePath().AsDirectory();
        if (!di.Exists) return null;

        foreach (var fi in di.GetFiles("*Area.cs"))
        {
            var txt = File.ReadAllText(fi.FullName);
            var str = txt.Substring("public class", "AreaBase")?.Trim(' ', ':');
            if (!str.IsNullOrEmpty())
            {
                return str.TrimSuffix("Area");
            }
        }

        return null;
    }

    /// <summary>在指定目录中查找项目名</summary>
    /// <param name="dir"></param>
    /// <returns></returns>
    static String FindProjectRootNamespace(String dir)
    {
        var di = dir.GetBasePath().AsDirectory();
        for (var i = 0; i < 3; i++)
        {
            if (di.Exists)
            {
                foreach (var fi in di.GetFiles("*.csproj", SearchOption.TopDirectoryOnly))
                {
                    var ns = Path.GetFileNameWithoutExtension(fi.FullName);

                    var xml = File.ReadAllText(fi.FullName);
                    if (!xml.IsNullOrEmpty())
                    {
                        var str = xml.Substring("<RootNamespace>", "</RootNamespace>");
                        if (!str.IsNullOrEmpty()) ns = str;
                    }

                    if (!ns.IsNullOrEmpty()) return ns;
                }
            }
            di = di.Parent;
        }

        return null;
    }
    #endregion

    #region 方法
    /// <summary>加载数据表</summary>
    /// <param name="table"></param>
    public override void Load(IDataTable table)
    {
        Table = table;

        var option = Option;

        // 命名空间
        var str = table.Properties["Namespace"];
        if (!str.IsNullOrEmpty()) option.Namespace = str;

        // 输出目录
        str = table.Properties["CubeOutput"];
        if (!str.IsNullOrEmpty()) option.Output = str.GetBasePath();
    }

    /// <summary>生成前</summary>
    protected override void OnExecuting()
    {
        var opt = Option;
        var code = ControllerTemplate;

        code = code.Replace("{EntityNamespace}", opt.Namespace);
        code = code.Replace("{ClassName}", ClassName);
        code = code.Replace("{EntityName}", Table.Name);
        code = code.Replace("{RootNamespace}", RootNamespace);
        code = code.Replace("{Name}", AreaName);
        code = code.Replace("{DisplayName}", Table.Description);
        code = code.Replace("{Sort}", Sort + "");

        code = code.Replace("{BaseClass}", GetBaseClass());

        if (Table.Columns.Any(c => c.Name.EqualIgnoreCase("TraceId")))
            code = code.Replace("//ListFields.TraceUrl(", "ListFields.TraceUrl(");

        var ss = BuildSearch(out var enumAliases);
        code = code.Replace("{EnumList}", enumAliases);
        if (!ss.IsNullOrEmpty())
        {
            var p1 = code.IndexOf("        //var deviceId = p[\"deviceId\"].ToInt(-1);");
            var p2 = code.IndexOf("p);", p1);

            code = code.Substring(0, p1) + ss + code.Substring(p2 + "p);".Length);
        }

        Writer.Write(code);
    }

    /// <summary>生成后</summary>
    protected override void OnExecuted() { }

    /// <summary>生成主体</summary>
    protected override void BuildItems() { }
    #endregion

    #region 辅助
    /// <summary>构建搜索参数和枚举引用</summary>
    /// <remarks>该方法用于生成高级查询模板，并收集枚举类型的引用信息，以类型别名方式加入到using位置</remarks>
    /// <returns></returns>
    private String BuildSearch(out String enumAliasStr)
    {
        enumAliasStr = "";
        // 收集索引信息，索引中的所有字段都参与，构造一个高级查询模板
        var builder = new SearchBuilder(Table) { Nullable = Option.Nullable };
        var cs = builder.GetColumns();
        if (cs.Count <= 0) return null;

        var ps = builder.GetParameters(cs);
        var pdic = ps.ToDictionary(e => e.Name);

        // 预解析枚举列的类型引用。同名不同类型时全部退化为全局全名，避免类型别名冲突
        var enumRefs = new Dictionary<String, (String Name, String FullPath)>();
        foreach (var dc in cs)
        {
            if (!dc.DataType.IsInt()) continue;

            var er = ResolveEnumReference(dc);
            if (er != null) enumRefs[dc.Name] = er.Value;
        }
        var conflicts = new HashSet<String>();
        foreach (var gp in enumRefs.Values.Where(e => !e.FullPath.IsNullOrEmpty()).GroupBy(e => e.Name))
        {
            if (gp.Select(e => e.FullPath).Distinct().Count() > 1) conflicts.Add(gp.Key);
        }

        var sb = Pool.StringBuilder.Get();

        var pis = new List<String>();
        var aliases = new List<(String Name, String FullPath)>();
        foreach (var dc in cs)
        {
            //var name = dc.CamelName();
            if (!pdic.TryGetValue(dc.Name, out var pi)) continue;
            var name = pi.ParameterName;

            if (enumRefs.TryGetValue(dc.Name, out var er))
            {
                // 枚举列。完整路径可用类型别名缩短，其余保持原样
                var cast = er.Name;
                if (!er.FullPath.IsNullOrEmpty())
                {
                    if (conflicts.Contains(er.Name))
                        // 内联转换位于类体内，外层命名空间中的同名类型会抢占首段，必须用 global:: 从根解析
                        cast = $"global::{er.FullPath}";
                    else
                        aliases.Add(er);
                }

                sb.AppendLine($"        var {name} = ({cast})p[\"{name}\"].ToInt(-1);");
            }
            else
            {
                var type = dc.DataType;
                var underlying = type == null ? null : Nullable.GetUnderlyingType(type);
                if (underlying != null) type = underlying;

                if (type == typeof(Int32))
                    sb.AppendLine($"        var {name} = p[\"{name}\"].ToInt(-1);");
                else if (type == typeof(Int64))
                    sb.AppendLine($"        var {name} = p[\"{name}\"].ToLong(-1);");
                else if (type == typeof(UInt64))
                    sb.AppendLine($"        var {name} = (UInt64)p[\"{name}\"].ToLong(-1);");
                else if (type != null && type.IsInt())
                    sb.AppendLine($"        var {name} = ({type.Name})p[\"{name}\"].ToInt(-1);");
                else if (type == typeof(Boolean))
                    sb.AppendLine($"        var {name} = p[\"{name}\"]?.ToBoolean();");
                else if (type == typeof(DateTime))
                    sb.AppendLine($"        var {name} = p[\"{name}\"].ToDateTime();");
                else if (type == typeof(Double))
                    sb.AppendLine($"        var {name} = p[\"{name}\"].ToDouble();");
                else if (type == typeof(Single))
                    sb.AppendLine($"        var {name} = (Single)p[\"{name}\"].ToDouble();");
                else if (type == typeof(Decimal))
                    sb.AppendLine($"        var {name} = p[\"{name}\"].ToDecimal();");
                else if (type == typeof(String))
                    sb.AppendLine($"        var {name} = p[\"{name}\"];");
                else
                    // 不支持的类型，跳过（正常情况下已被 SearchBuilder 过滤，这里兜底）
                    continue;
            }

            // 该字段参与查询，必须加入参数列表，顺序与实体 Search 方法保持一致
            pis.Add(name);

        }

        // 枚举类型别名，每个类型一行，自动去重。别名位于文件级，右侧名字优先绑定全局命名空间，无需 global::
        if (aliases.Count > 0)
            enumAliasStr = aliases.DistinctBy(e => e.Name).Select(e => $"using {e.Name} = {e.FullPath};").Join(Environment.NewLine);

        if (builder.DataTime != null)
        {
            sb.AppendLine();
            sb.AppendLine("        var start = p[\"dtStart\"].ToDateTime();");
            sb.AppendLine("        var end = p[\"dtEnd\"].ToDateTime();");

            pis.Add("start");
            pis.Add("end");
        }

        sb.AppendLine();
        sb.AppendLine($"        return {Table.Name}.Search({pis.Join(", ")}, p[\"Q\"], p);");

        return sb.Return(true).TrimEnd();
    }

    /// <summary>解析枚举列的类型引用。有点号的完整路径可用类型别名缩短，无点号的短名保持原样</summary>
    /// <param name="column">数据列</param>
    /// <returns>枚举短名与完整路径；完整路径为空表示无法确定，按原样文本使用；非枚举列返回 null</returns>
    private static (String Name, String FullPath)? ResolveEnumReference(IDataColumn column)
    {
        var type = column.DataType;

        // DataType 直接是枚举类型
        if (type != null && type.IsEnum)
        {
            var full = type.FullName?.Replace('+', '.');
            if (!full.IsNullOrEmpty() && full.Contains('.')) return (type.Name, full);

            return (type.Name, null);
        }

        // XML 模型显式指定的类型字符串
        var str = column.Properties["Type"]?.Trim();
        if (str.IsNullOrEmpty()) return null;

        // 无点号视为短名，原样保留，能否解析取决于生成文件自身的using
        if (!str.Contains('.')) return (str, null);

        // 有点号视为完整路径。最后一个点号之前是命名空间或外层类型，嵌套类型同样适用
        var prefix = str.CutEnd(".");
        return (str.CutStart(prefix + "."), str);
    }
    #endregion
}