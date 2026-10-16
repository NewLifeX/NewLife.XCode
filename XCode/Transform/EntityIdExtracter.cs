using XCode.Configuration;

namespace XCode.Transform;

/// <summary>自增数据抽取器</summary>
/// <remarks>
/// 适用于带有自增字段或雪花Id字段的数据抽取器，速度飞快。
/// </remarks>
public class EntityIdExtracter
{
    #region 属性
    /// <summary>实体工厂</summary>
    public IEntityFactory Factory { get; set; }

    /// <summary>Id字段</summary>
    public FieldItem IdField { get; set; }

    /// <summary>开始行。默认0</summary>
    public Int64 Row { get; set; }

    /// <summary>批大小。默认5000</summary>
    public Int32 BatchSize { get; set; } = 5000;
    #endregion

    #region 构造
    ///// <summary>实例化数据抽取器</summary>
    //public EntityIdExtracter() { }

    /// <summary>实例化数据抽取器</summary>
    /// <param name="factory"></param>
    /// <param name="idField"></param>
    public EntityIdExtracter(IEntityFactory factory, FieldItem idField)
    {
        Factory = factory;
        IdField = idField;
        BatchSize = factory.Session.Dal.GetBatchSize();
    }
    #endregion

    #region 抽取数据
    /// <summary>迭代抽取数据</summary>
    /// <returns></returns>
    public virtual IEnumerable<IList<IEntity>> Fetch()
    {
        while (true)
        {
            // 分割数据页，自增。严格大于上一页最后编号，避免重复抽取
            var exp = IdField > Row;

            // 查询数据
            var list = Factory.FindAll(exp, IdField.Asc(), null, 0, BatchSize);
            if (list.Count == 0) break;

            // 返回数据
            yield return list;

            // 自增分割时，取最后一行。必须在退出前推进，避免下次抽取重复最后一页
            var rs = list.Last()[IdField.Name];
            if (rs == null) break;

            Row = (Int64)rs;

            // 下一页
            if (list.Count < BatchSize) break;
        }
    }
    #endregion
}