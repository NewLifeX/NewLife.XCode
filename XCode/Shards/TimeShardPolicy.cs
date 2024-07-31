using NewLife;
using NewLife.Data;
using XCode.Configuration;
using XCode.Statistics;

namespace XCode.Shards;

/// <summary>时间分表策略</summary>
public class TimeShardPolicy : IShardPolicy
{
    #region 属性
    /// <summary>实体工厂</summary>
    public IEntityFactory? Factory { get; set; }

    /// <summary>字段</summary>
    public FieldItem? Field { get; set; }

    /// <summary>连接名策略。格式化字符串，0位基础连接名，1位时间，如{0}_{1:yyyy}</summary>
    public String? ConnPolicy { get; set; }

    /// <summary>表名策略。格式化字符串，0位基础表名，1位时间，如{0}_{1:yyyyMM}</summary>
    public String? TablePolicy { get; set; }

    /// <summary>时间区间步进。遇到时间区间需要扫描多张表时的时间步进，默认1天</summary>
    public TimeSpan Step { get; set; } = TimeSpan.FromDays(1);

    /// <summary>日期级别。年月日</summary>
    public StatLevels Level { get; set; }

    private readonly String? _fieldName;
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public TimeShardPolicy() { }

    /// <summary>指定字段实例化分表策略</summary>
    /// <param name="field"></param>
    /// <param name="factory"></param>
    public TimeShardPolicy(FieldItem field, IEntityFactory? factory = null)
    {
        Field = field;
        Factory = factory ?? field.Factory;
    }

    /// <summary>指定字段名和工厂实例化分表策略</summary>
    /// <param name="fieldName"></param>
    /// <param name="factory"></param>
    public TimeShardPolicy(String fieldName, IEntityFactory factory)
    {
        _fieldName = fieldName;
        Factory = factory;

        // 异步加载字段
        Task.Run(GetField);
    }

    private FieldItem? GetField() => Field ??= _fieldName == null ? null : Factory?.Table.FindByName(_fieldName);
    #endregion

    /// <summary>为实体对象、时间、雪花Id等计算分表分库</summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public virtual ShardModel? Shard(Object value)
    {
        if (value is IModel entity) return Shard(entity);
        if (value is DateTime dt) return Shard(dt);
        if (value is Int64 id)
        {
            if (Factory == null) throw new XCodeException("分表策略要求指定实体工厂！");

            if (!Factory.Snow.TryParse(id, out var time, out _, out _) || time.Year <= 1970)
                throw new XCodeException("雪花Id解析时间失败，无法用于分表");

            return Shard(time);
        }

        throw new XCodeException($"分表策略无法识别数据[{value}]");
    }

    /// <summary>为实体对象计算分表分库</summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    public virtual ShardModel? Shard(IModel entity)
    {
        var fi = GetField();
        if (fi == null) throw new XCodeException("分表策略要求指定时间字段！");

        if (fi.Type == typeof(DateTime))
        {
            var time = entity[fi.Name].ToDateTime();
            if (time.Year <= 1970) throw new XCodeException("实体对象时间字段为空，无法用于分表");

            return Shard(time);
        }
        else if (fi.Type == typeof(Int64))
        {
            if (Factory == null) throw new XCodeException("分表策略要求指定实体工厂！");

            var id = entity[fi.Name].ToLong();
            if (!Factory.Snow.TryParse(id, out var time, out _, out _) || time.Year <= 1970)
                throw new XCodeException("雪花Id解析时间失败，无法用于分表");

            return Shard(time);
        }

        throw new XCodeException($"时间分表策略不支持[{fi.Type.FullName}]类型字段");
    }

    /// <summary>为时间计算分表分库</summary>
    /// <param name="time"></param>
    /// <returns></returns>
    public virtual ShardModel? Shard(DateTime time)
    {
        if (time.Year <= 1) throw new ArgumentNullException(nameof(time), "分表策略要求指定时间！");

        var fi = GetField() ?? throw new XCodeException("分表策略要求指定时间字段！");

        if (ConnPolicy.IsNullOrEmpty() && TablePolicy.IsNullOrEmpty()) return null;

        if (Factory == null) throw new XCodeException("分表策略要求指定实体工厂！");
        var table = Factory.Table;

        // 这里不区分时区，而是直接使用时间
        var model = new ShardModel();
        if (!ConnPolicy.IsNullOrEmpty()) model.ConnName = String.Format(ConnPolicy, table.ConnName, time);
        if (!TablePolicy.IsNullOrEmpty()) model.TableName = String.Format(TablePolicy, table.TableName, time);

        return model;
    }

    /// <summary>从时间区间中计算多个分表分库，支持倒序。步进由Step指定，默认1天</summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    public virtual ShardModel[] Shards(DateTime start, DateTime end)
    {
        if (start.Year <= 1) throw new ArgumentNullException(nameof(start), "分表策略要求指定时间！");
        if (end.Year <= 1) throw new ArgumentNullException(nameof(end), "分表策略要求指定时间！");

        if (start <= end) return GetModels(start, end);

        var arr = GetModels(end, start);
        Array.Reverse(arr);
        return arr;
    }

    /// <summary>从查询表达式中计算多个分表分库</summary>
    /// <param name="expression"></param>
    /// <returns></returns>
    public virtual ShardModel[] Shards(Expression expression)
    {
        //if (expression is not WhereExpression where) return null;

        // 时间范围查询，用户可能自己写分表查询
        var fi = GetField() ?? throw new XCodeException("分表策略要求指定时间字段！");

        var exps = new List<FieldExpression>();
        if (expression is WhereExpression where)
            exps = where.Where(e => e is FieldExpression fe && fe.Field.Name == fi.Name).Cast<FieldExpression>().ToList();
        else if (expression is FieldExpression fieldExpression && fieldExpression.Field.Name == fi.Name)
            exps.Add(fieldExpression);
        //if (exps.Count == 0) throw new XCodeException($"分表策略要求查询条件包括[{fi}]字段！");
        if (exps.Count == 0) return [];

        if (fi.Type == typeof(DateTime))
        {
            var sf = exps.FirstOrDefault(e => e.Action is ">" or ">=");
            var ef = exps.FirstOrDefault(e => e.Action is "<" or "<=");
            if (sf != null)
            {
                var start = sf.Value.ToDateTime();
                if (start.Year > 1)
                {
                    // 不等于时，增加1秒
                    if (sf.Action == ">") start = start.AddSeconds(1);

                    var end = DateTime.Now;

                    if (ef != null)
                    {
                        var time = ef.Value.ToDateTime();
                        if (time.Year > 1)
                        {
                            end = time;

                            // 不等于时，减少1秒
                            if (ef.Action == "<") end = end.AddSeconds(-1);
                        }
                    }

                    return GetModels(start, end);
                }
            }
        }
        else if (fi.Type == typeof(Int64))
        {
            if (Factory == null) throw new XCodeException("分表策略要求指定实体工厂！");

            var sf = exps.FirstOrDefault(e => e.Action is ">" or ">=");
            var ef = exps.FirstOrDefault(e => e.Action is "<" or "<=");
            if (sf != null)
            {
                var id = sf.Value.ToLong();
                if (Factory.Snow.TryParse(id, out var time, out _, out _))
                {
                    var start = time;
                    var end = DateTime.Now;

                    if (ef != null && Factory.Snow.TryParse(ef.Value.ToLong(), out var time2, out _, out _))
                    {
                        end = time2;
                    }

                    return GetModels(start, end);
                }
            }

            var eq = exps.FirstOrDefault(e => e.Action == "=");
            if (eq != null)
            {
                var id = eq.Value.ToLong();
                if (Factory.Snow.TryParse(id, out var time, out _, out _))
                {
                    var model = Shard(time);
                    if (model != null) return [model];
                }
            }
        }

        throw new XCodeException("分表策略因条件不足无法执行分表查询操作！");
    }

    private ShardModel[] GetModels(DateTime start, DateTime end)
    {
        var models = new List<ShardModel>();

        // 猜测时间步进级别
        var st = Step;
        var level = Level;
        if (level <= 0)
        {
            if (st.TotalDays >= 360)
                level = StatLevels.Year;
            else if (st.TotalDays >= 28 && st.TotalDays <= 31)
                level = StatLevels.Month;
            else if (st.TotalDays == 1)
                level = StatLevels.Day;
            else if (st.TotalHours == 1)
                level = StatLevels.Hour;
        }

        // 根据步进，把start调整到整点
        if (st.TotalDays >= 1)
            start = start.Date;
        else if (start.Hour >= 1)
            start = start.Date.AddHours(start.Hour);

        var hash = new HashSet<String>();
        for (var dt = start; dt < end;)
        {
            var model = Shard(dt);
            if (model != null)
            {
                var key = $"{model.ConnName}#{model.TableName}";
                if (key != "#" && !hash.Contains(key))
                {
                    models.Add(model);
                    hash.Add(key);
                }
            }

            // 根据时间步进级别调整时间，解决每月每年时间不固定的问题
            if (level == StatLevels.Year)
            {
                dt = dt.AddYears(1);
                dt = new DateTime(dt.Year, 1, 1);
            }
            else if (level == StatLevels.Month)
            {
                dt = dt.AddMonths(1);
                dt = new DateTime(dt.Year, dt.Month, 1);
            }
            else if (level == StatLevels.Day)
            {
                dt = dt.AddDays(1).Date;
            }
            else if (level == StatLevels.Hour)
            {
                dt = dt.AddHours(1);
                dt = dt.Date.AddHours(dt.Hour);
            }
            else
                dt = dt.Add(st);
        }

        //// 标准时间区间 start <= @fi < end ，但是要考虑到end有一部分落入新的分片，减一秒判断
        //{
        //    var model = Shard(end.AddSeconds(1));
        //    if (model != null)
        //    {
        //        var key = $"{model.ConnName}#{model.TableName}";
        //        if (key != "#" && !hash.Contains(key))
        //        {
        //            models.Add(model);
        //            hash.Add(key);
        //        }
        //    }
        //}

        return models.ToArray();
    }
}