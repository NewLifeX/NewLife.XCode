using NewLife;
using NewLife.Data;
using XCode.Configuration;

namespace XCode.Shards;

/// <summary>字段值分表策略。按指定业务字段的值分表，如按UserId字段分表，UserId=1000时表名为Log_1000</summary>
/// <remarks>
/// 适用于多租户等场景，整张分表的字段值固定一致。
/// 示例：UserId=1000 → Log_1000
/// </remarks>
public class FieldShardPolicy : IShardPolicy
{
    #region 属性
    /// <summary>实体工厂</summary>
    public IEntityFactory? Factory { get; set; }

    /// <summary>字段</summary>
    public FieldItem? Field { get; set; }

    /// <summary>连接名策略。格式化字符串，0位基础连接名，1位字段值，如{0}_{1}</summary>
    public String? ConnPolicy { get; set; }

    /// <summary>表名策略。格式化字符串，0位基础表名，1位字段值，如{0}_{1}</summary>
    public String? TablePolicy { get; set; } = "{0}_{1}";

    private readonly String? _fieldName;
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public FieldShardPolicy() { }

    /// <summary>指定字段实例化字段分表策略</summary>
    /// <param name="field">分表字段</param>
    /// <param name="factory">实体工厂</param>
    public FieldShardPolicy(FieldItem field, IEntityFactory? factory = null)
    {
        Field = field;
        Factory = factory ?? field.Factory;
    }

    /// <summary>指定字段名和工厂实例化字段分表策略</summary>
    /// <param name="fieldName">字段名</param>
    /// <param name="factory">实体工厂</param>
    public FieldShardPolicy(String fieldName, IEntityFactory factory)
    {
        _fieldName = fieldName;
        Factory = factory;

        // 异步加载字段
        Task.Run(GetField);
    }

    private FieldItem? GetField() => Field ??= _fieldName == null ? null : Factory?.Table.FindByName(_fieldName);
    #endregion

    #region 分表
    /// <summary>为实体对象或字段值计算分表分库</summary>
    /// <param name="value">实体对象或字段直接值（Int32/Int64/String 等）</param>
    /// <returns>分表模型，策略未配置时返回 null</returns>
    public virtual ShardModel? Shard(Object value)
    {
        if (value is IModel entity) return ShardByEntity(entity);
        return ShardByValue(value);
    }

    /// <summary>从实体对象中提取分表字段值并计算分表分库</summary>
    /// <param name="entity">实体对象</param>
    /// <returns>分表模型</returns>
    protected virtual ShardModel? ShardByEntity(IModel entity)
    {
        var fi = GetField() ?? throw new XCodeException("字段分表策略要求指定分表字段！");

        var value = entity[fi.Name];
        if (value == null) throw new XCodeException($"实体对象字段[{fi.Name}]为空，无法用于字段分表");

        return ShardByValue(value);
    }

    /// <summary>按字段值计算分表分库</summary>
    /// <param name="fieldValue">字段值</param>
    /// <returns>分表模型，策略未配置时返回 null</returns>
    public virtual ShardModel? ShardByValue(Object fieldValue)
    {
        if (ConnPolicy.IsNullOrEmpty() && TablePolicy.IsNullOrEmpty()) return null;

        if (Factory == null) throw new XCodeException("字段分表策略要求指定实体工厂！");
        var table = Factory.Table;

        var connName = table.ConnName;
        var tableName = table.TableName;
        if (!ConnPolicy.IsNullOrEmpty()) connName = String.Format(ConnPolicy, connName, fieldValue);
        if (!TablePolicy.IsNullOrEmpty()) tableName = String.Format(TablePolicy, tableName, fieldValue);

        return new(connName, tableName);
    }

    /// <summary>字段值分表策略不支持时间区间查询，直接返回空数组</summary>
    /// <param name="start">开始时间（不使用）</param>
    /// <param name="end">结束时间（不使用）</param>
    /// <returns>空数组</returns>
    public virtual ShardModel[] Shards(DateTime start, DateTime end) => [];

    /// <summary>从查询表达式中提取等值条件计算分表分库</summary>
    /// <param name="expression">查询表达式</param>
    /// <returns>分表模型数组；条件中没有分表字段时返回空数组</returns>
    public virtual ShardModel[] Shards(Expression expression)
    {
        var fi = GetField() ?? throw new XCodeException("字段分表策略要求指定分表字段！");

        var exps = new List<FieldExpression>();
        if (expression is WhereExpression where)
            exps = where.Where(e => e is FieldExpression fe && fe.Field.Name == fi.Name).Cast<FieldExpression>().ToList();
        else if (expression is FieldExpression fe2 && fe2.Field.Name == fi.Name)
            exps.Add(fe2);

        if (exps.Count == 0) return [];

        // 仅支持等值条件
        var eq = exps.FirstOrDefault(e => e.Action == "=");
        if (eq != null)
        {
            var model = ShardByValue(eq.Value);
            if (model != null) return [model];
        }

        return [];
    }
    #endregion
}
