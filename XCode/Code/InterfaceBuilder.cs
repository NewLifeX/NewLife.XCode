using NewLife;
using NewLife.Log;
using XCode.DataAccessLayer;

namespace XCode.Code;

/// <summary>接口生成器</summary>
public class InterfaceBuilder : ClassBuilder
{
    #region 静态快速
    /// <summary>生成简易版实体接口</summary>
    /// <param name="tables">表集合</param>
    /// <param name="option">可选项</param>
    /// <param name="log"></param>
    /// <returns></returns>
    public static Int32 BuildInterfaces(IList<IDataTable> tables, BuilderOption? option = null, ILog? log = null)
    {
        if (option == null)
            option = new BuilderOption();
        else
            option = option.Clone();

        //option.Partial = true;

        log?.Info("生成简易接口 {0}", option.Output?.GetBasePath());

        var count = 0;
        foreach (var item in tables)
        {
            // 跳过排除项
            if (option.Excludes.Contains(item.Name)) continue;
            if (option.Excludes.Contains(item.TableName)) continue;

            var builder = new InterfaceBuilder
            {
                Table = item,
                Option = option.Clone(),
                //Log = log
            };
            if (log != null) builder.Log = log;

            builder.Load(item);

            // 模型类使用全局输出路径
            if (!option.Output.IsNullOrEmpty()) builder.Option.Output = option.Output;

            //// 自定义模型
            //var modelInterface = option.ModelInterface;
            //if (!modelInterface.IsNullOrEmpty()) builder.Option.ClassNameTemplate = modelInterface;

            // 自定义模型
            var modelInterface = item.Properties["ModelInterface"];
            if (!modelInterface.IsNullOrEmpty())
            {
                builder.Option.ClassNameTemplate = modelInterface;
            }

            builder.Execute();
            builder.Save(null, true, false);

            count++;
        }

        return count;
    }
    #endregion 静态快速

    /// <summary>生成前的准备工作。计算类型以及命名空间等</summary>
    protected override void Prepare()
    {
        var option = Option;
        if (ClassName.IsNullOrEmpty())
        {
            if (!option.ClassNameTemplate.IsNullOrEmpty())
                ClassName = option.ClassNameTemplate.Replace("{name}", Table.Name);
            else
                ClassName = "I" + Table.Name;
        }

        base.Prepare();
    }

    /// <summary>实体类头部</summary>
    protected override void BuildClassHeader()
    {
        // 头部
        BuildAttribute();

        // 基类
        var baseClass = GetBaseClass();
        if (!baseClass.IsNullOrEmpty()) baseClass = " : " + baseClass;

        // 分部类
        var partialClass = " partial";

        // 类接口
        WriteLine("public{2} interface {0}{1}", ClassName, baseClass, partialClass);

        WriteLine("{");
    }

    /// <summary>生成每一项</summary>
    protected override void BuildItem(IDataColumn column)
    {
        var dc = column;

        // 注释
        var des = dc.Description;
        WriteLine("/// <summary>{0}</summary>", des);

        var type = dc.Properties["Type"];
        if (type.IsNullOrEmpty()) type = dc.DataType?.Name;
        if (type == "String")
        {
            if (Option.Nullable)
            {
                if (column.Nullable)
                    WriteLine("String? {0} {{ get; set; }}", dc.Name);
                else
                    WriteLine("String {0} {{ get; set; }}", dc.Name);
            }
            else
            {
                WriteLine("String {0} {{ get; set; }}", dc.Name);
            }
        }
        else
        {
            WriteLine("{0} {1} {{ get; set; }}", type, dc.Name);
        }
    }

    /// <summary>验证字段是否可用于生成</summary>
    /// <param name="column"></param>
    /// <param name="validModel"></param>
    /// <returns></returns>
    protected override Boolean ValidColumn(IDataColumn column, Boolean validModel = false)
    {
        if (column.Properties["Model"] == "False") return false;

        return base.ValidColumn(column, validModel);
    }

    /// <summary>获取文件名</summary>
    /// <param name="ext"></param>
    /// <param name="chineseFileName"></param>
    /// <returns></returns>
    protected override String GetFileName(String ext = null, Boolean chineseFileName = true)
    {
        var p = Option.Output;
        if (ext.IsNullOrEmpty())
            ext = ".cs";
        else if (!ext.Contains("."))
            ext += ".cs";

        p = p.CombinePath(ClassName + ext);

        p = p.GetBasePath();

        return p;
    }
}
