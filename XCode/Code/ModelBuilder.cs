using NewLife;
using NewLife.Log;
using XCode.DataAccessLayer;

namespace XCode.Code;

/// <summary>模型类生成器</summary>
public class ModelBuilder : ClassBuilder
{
    #region 属性
    /// <summary>纯净类。去除属性上的Description等特性</summary>
    public Boolean Pure { get; set; } = true;
    #endregion

    #region 静态快速
    /// <summary>生成简易版模型</summary>
    /// <param name="tables">表集合</param>
    /// <param name="option">可选项</param>
    /// <param name="log"></param>
    /// <returns></returns>
    public static Int32 BuildModels(IList<IDataTable> tables, BuilderOption option = null, ILog log = null)
    {
        if (option == null)
            option = new BuilderOption();
        else
            option = option.Clone();

        //option.Pure = true;
        //option.Partial = true;

        log?.Info("生成简易模型类 {0}", option.Output.GetBasePath());

        var count = 0;
        foreach (var item in tables)
        {
            // 跳过排除项
            if (option.Excludes.Contains(item.Name)) continue;
            if (option.Excludes.Contains(item.TableName)) continue;

            var builder = new ModelBuilder
            {
                Table = item,
                Pure = true,
                Option = option.Clone(),
                Log = log
            };

            builder.Load(item);

            // 自定义模型
            var modelClass = item.Properties["ModelClass"];
            var modelInterface = item.Properties["ModelInterface"];
            if (!modelClass.IsNullOrEmpty()) builder.Option.ClassNameTemplate = modelClass;
            if (!modelInterface.IsNullOrEmpty())
            {
                builder.Option.BaseClass = modelInterface;
                builder.Option.ModelNameForCopy = modelInterface;
            }

            builder.Execute();
            builder.Save(null, true, false);

            count++;
        }

        return count;
    }
    #endregion 静态快速

    /// <summary>执行前，生成头部</summary>
    protected override void OnExecuting()
    {
        var option = Option;
        if (ClassName.IsNullOrEmpty())
        {
            if (!option.ClassNameTemplate.IsNullOrEmpty())
                ClassName = option.ClassNameTemplate.Replace("{name}", Table.Name);
            else
                ClassName = Table.Name;
        }

        base.OnExecuting();
    }

    /// <summary>实体类头部</summary>
    protected override void BuildAttribute()
    {
        // 注释
        var des = Table.Description;
        if (!Option.DisplayNameTemplate.IsNullOrEmpty())
        {
            des = Table.Description.TrimStart(Table.DisplayName, "。");
            des = Option.DisplayNameTemplate.Replace("{displayName}", Table.DisplayName) + "。" + des;
        }
        WriteLine("/// <summary>{0}</summary>", des);

        if (!Pure)
        {
            WriteLine("[Serializable]");
            WriteLine("[DataObject]");

            if (!des.IsNullOrEmpty()) WriteLine("[Description(\"{0}\")]", des);
        }
    }

    /// <summary>生成主体</summary>
    protected override void BuildItems()
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
    }

    /// <summary>生成每一项</summary>
    protected override void BuildItem(IDataColumn column)
    {
        var dc = column;

        // 注释
        var des = dc.Description;
        WriteLine("/// <summary>{0}</summary>", des);

        if (!Pure)
        {
            if (!des.IsNullOrEmpty()) WriteLine("[Description(\"{0}\")]", des);

            var dis = dc.DisplayName;
            if (!dis.IsNullOrEmpty()) WriteLine("[DisplayName(\"{0}\")]", dis);
        }

        var type = dc.Properties["Type"];
        if (type.IsNullOrEmpty()) type = dc.DataType?.Name;

        WriteLine("public {0} {1} {{ get; set; }}", type, dc.Name);
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
}
