using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewLife;
using NewLife.Log;
using XCode.Code;
using XCode.DataAccessLayer;

namespace XCode;

/// <summary>魔方页面生成器</summary>
public class CubeBuilder : ClassBuilder
{
    #region 属性
    /// <summary>项目名</summary>
    public String Project { get; set; }

    /// <summary>区域模版</summary>
    public String AreaTemplate { get; set; } = @"using System.ComponentModel;
using NewLife;
using NewLife.Cube;

namespace {Project}.Areas.{Name}
{
    [DisplayName(""{DisplayName}"")]
    public class {Name}Area : AreaBase
    {
        public {Name}Area() : base(nameof({Name}Area).TrimEnd(""Area"")) { }
    }
}";

    /// <summary>控制器模版</summary>
    public String ControllerTemplate { get; set; } = @"using {Namespace};
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using XCode.Membership;

namespace {Project}.Areas.{Name}.Controllers
{
    /// <summary>{DisplayName}</summary>
    [Menu(10, true, Icon = ""fa-table"")]
    [{Name}Area]
    public class {ClassName}Controller : {ControllerBase}<{ClassName}>
    {
        static {ClassName}Controller()
        {
            //LogOnChange = true;

            //ListFields.RemoveField(""Id"", ""Creator"");
            ListFields.RemoveCreateField();

            //{
            //    var df = ListFields.GetField(""Code"") as ListField;
            //    df.Url = ""?code={Code}"";
            //}
            //{
            //    var df = ListFields.AddListField(""devices"", null, ""Onlines"");
            //    df.DisplayName = ""查看设备"";
            //    df.Url = ""Device?groupId={Id}"";
            //    df.DataVisible = e => (e as {ClassName}).Devices > 0;
            //}
            //{
            //    var df = ListFields.GetField(""Kind"") as ListField;
            //    df.GetValue = e => ((Int32)(e as {ClassName}).Kind).ToString(""X4"");
            //}
            //ListFields.TraceUrl(""TraceId"");
        }

        /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
        /// <param name=""p"">分页器。包含分页排序参数，以及Http请求参数</param>
        /// <returns></returns>
        protected override IEnumerable<{ClassName}> Search(Pager p)
        {
            //var deviceId = p[""deviceId""].ToInt(-1);

            var start = p[""dtStart""].ToDateTime();
            var end = p[""dtEnd""].ToDateTime();

            return {ClassName}.Search(start, end, p[""Q""], p);
        }
    }
}";
    #endregion

    #region 静态
    /// <summary>生成魔方区域</summary>
    /// <param name="option">可选项</param>
    /// <returns></returns>
    public static Int32 BuildArea(BuilderOption option)
    {
        if (option == null)
            option = new BuilderOption();
        else
            option = option.Clone();

        var file = $"{option.ConnName}Area.cs";
        file = option.Output.CombinePath(file);
        file = file.GetBasePath();

        // 文件已存在，不要覆盖
        if (File.Exists(file)) return 0;

        if (Debug) XTrace.WriteLine("生成魔方区域 {0}", file);

        var builder = new CubeBuilder();
        if (option.Items != null && option.Items.TryGetValue("CubeProject", out var project))
            builder.Project = project;
        else
            builder.Project = option.ConnName + "Web";

        var code = builder.AreaTemplate;

        //code = code.Replace("{Namespace}", option.Namespace);
        code = code.Replace("{Project}", builder.Project);
        code = code.Replace("{Name}", option.ConnName);
        code = code.Replace("{DisplayName}", option.DisplayName);

        // 输出到文件
        file.EnsureDirectory(true);
        File.WriteAllText(file, code);

        return 1;
    }

    /// <summary>生成控制器</summary>
    /// <param name="tables">表集合</param>
    /// <param name="option">可选项</param>
    /// <returns></returns>
    public static Int32 BuildControllers(IList<IDataTable> tables, BuilderOption option = null)
    {
        if (option == null)
            option = new BuilderOption();
        else
            option = option.Clone();

        var project = "";
        if (option.Items != null && option.Items.TryGetValue("CubeProject", out var str))
            project = str;
        else
            project = option.ConnName + "Web";

        if (Debug) XTrace.WriteLine("生成控制器 {0}", option.Output.GetBasePath());

        var count = 0;
        foreach (var item in tables)
        {
            // 跳过排除项
            if (option.Excludes.Contains(item.Name)) continue;
            if (option.Excludes.Contains(item.TableName)) continue;

            var builder = new CubeBuilder
            {
                Table = item,
                Option = option.Clone(),
                Project = project,
            };
            if (Debug) builder.Log = XTrace.Log;

            builder.Load(item);

            builder.Execute();
            builder.Save("Controller.cs", false, false);

            count++;
        }

        return count;
    }
    #endregion

    #region 方法
    /// <summary>生成前</summary>
    protected override void OnExecuting()
    {
        var opt = Option;
        var code = ControllerTemplate;

        code = code.Replace("{Namespace}", opt.Namespace);
        code = code.Replace("{ClassName}", ClassName);
        code = code.Replace("{Project}", Project);
        code = code.Replace("{Name}", opt.ConnName);
        code = code.Replace("{DisplayName}", Table.Description);

        code = code.Replace("{ControllerBase}", Table.InsertOnly ? "ReadOnlyEntityController" : "EntityController");

        if (Table.Columns.Any(c => c.Name.EqualIgnoreCase("TraceId")))
            code = code.Replace("//ListFields.TraceUrl(", "ListFields.TraceUrl(");

        Writer.Write(code);
    }

    /// <summary>生成后</summary>
    protected override void OnExecuted() { }

    /// <summary>生成主体</summary>
    protected override void BuildItems() { }
    #endregion

    #region 辅助
    ///// <summary>写入</summary>
    ///// <param name="value"></param>
    //protected override void WriteLine(String value = null)
    //{
    //    if (!value.IsNullOrEmpty() && value.Length > 2 && value[0] == '<' && value[1] == '/') SetIndent(false);

    //    base.WriteLine(value);

    //    if (!value.IsNullOrEmpty() && value.Length > 2 && value[0] == '<' && value[1] != '/' && !value.Contains("</")) SetIndent(true);
    //}
    #endregion
}