using System.Collections;
using System.Data;
using System.IO.Compression;
using NewLife.Data;
using NewLife.IO;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Serialization;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Model;

namespace XCode;

/// <summary>实体扩展方法</summary>
public static class EntityExtension
{
    #region 泛型实例列表扩展
    /// <summary>实体列表转为字典。主键为Key</summary>
    /// <param name="list">实体列表</param>
    /// <param name="valueField">作为Value部分的字段，默认为空表示整个实体对象为值</param>
    /// <returns></returns>
    //[Obsolete("将来不再支持实体列表，请改用Linq")]
    public static IDictionary ToDictionary<T>(this IEnumerable<T> list, String? valueField = null) where T : IEntity
    {
        if (list == null || !list.Any()) return new Dictionary<String, String>();

        var type = list.First().GetType();
        var fact = type.AsFactory();

        // 构造主键类型和值类型
        var key = fact.Unique;
        var ktype = key.Type;

        if (!valueField.IsNullOrEmpty())
        {
            if (fact.Table.FindByName(valueField) is not FieldItem fi) throw new XException("无法找到名为{0}的字段", valueField);

            type = fi.Type;
        }

        // 创建字典
        var dic = typeof(Dictionary<,>).MakeGenericType(ktype, type).CreateInstance() as IDictionary;
        if (dic == null) throw new InvalidOperationException();

        foreach (var item in list)
        {
            var k = item[key.Name];
            if (!dic.Contains(k))
            {
                if (!valueField.IsNullOrEmpty())
                    dic.Add(k, item[valueField]);
                else
                    dic.Add(k, item);
            }
        }

        return dic;
    }

    /// <summary>从实体对象创建参数</summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="entity">实体对象</param>
    /// <param name="session">指定会话，分表分库时必用</param>
    /// <returns></returns>
    public static IDataParameter[] CreateParameter<T>(this T entity, IEntitySession session) where T : IEntity
    {
        if (session == null) throw new ArgumentNullException(nameof(session));

        var dps = new List<IDataParameter>();
        if (entity == null) return dps.ToArray();

        var fact = entity.GetType().AsFactory();
        //session ??= fact.Session;
        var db = session.Dal.Db;

        foreach (var item in fact.Fields)
        {
            if (item.Field != null)
                dps.Add(db.CreateParameter(item.ColumnName ?? item.Name, entity[item.Name], item.Field));
        }

        return dps.ToArray();
    }

    /// <summary>从实体列表创建参数</summary>
    /// <param name="list">实体列表</param>
    /// <param name="session">指定会话，分表分库时必用</param>
    /// <returns></returns>
    public static IDataParameter[] CreateParameters<T>(this IEnumerable<T> list, IEntitySession session) where T : IEntity
    {
        if (session == null) throw new ArgumentNullException(nameof(session));

        var dps = new List<IDataParameter>();
        if (list == null || !list.Any()) return dps.ToArray();

        var fact = list.First().GetType().AsFactory();
        //session ??= fact.Session;
        var db = session.Dal.Db;

        foreach (var item in fact.Fields)
        {
            if (item.Field != null)
            {
                var vs = list.Select(e => e[item.Name]).ToArray();
                dps.Add(db.CreateParameter(item.ColumnName ?? item.Name, vs, item.Field));
            }
        }

        return dps.ToArray();
    }
    #endregion

    #region 对象操作
    /// <summary>把整个集合插入到数据库</summary>
    /// <param name="list">实体列表</param>
    /// <param name="useTransition">是否使用事务保护</param>
    /// <param name="session">指定会话，分表分库时必用</param>
    /// <returns></returns>
    public static Int32 Insert<T>(this IEnumerable<T> list, Boolean? useTransition = null, IEntitySession? session = null) where T : IEntity
    {
        // 避免列表内实体对象为空
        var list2 = list.AsList();
        var entity = list2.FirstOrDefault(e => e != null);
        if (entity == null) return 0;

        var fact = entity.GetType().AsFactory();
        var session2 = session ?? fact.Session;

        // Oracle/MySql批量插入
        if (session2.Dal.SupportBatch && list2.Count() > 1)
        {
            //DefaultSpan.Current?.AppendTag("SupportBatch");

            //if (list is not IList<T> es) es = list.ToList();
            for (var i = list2.Count - 1; i >= 0; i--)
            {
                if (list2[i] is EntityBase entity2)
                {
                    if (!entity2.Valid(DataMethod.Insert)) list2.RemoveAt(i);
                }
                //if (!fact.Modules.Valid(item, item.IsNullKey)) es.Remove((T)item);
            }

            // 如果未指定会话，需要支持自动分表，并且需要考虑实体列表可能落入不同库表
            if (session == null && fact.ShardPolicy != null)
            {
                DefaultSpan.Current?.AppendTag($"ShardPolicy: {fact.ShardPolicy.Field}");

                // 提前计算分表，按库表名分组
                var dic = list2.GroupBy(e =>
                {
                    var shard = fact.ShardPolicy.Shard(e);
                    return fact.GetSession(shard?.ConnName ?? session2.ConnName, shard?.TableName ?? session2.TableName);
                });
                // 按库表分组执行批量插入
                var rs = 0;
                foreach (var item in dic)
                {
                    rs += BatchInsert(item.ToList(), option: null, item.Key);
                }
                return rs;
            }

            return BatchInsert(list2, option: null, session2);
        }

        return DoAction(list2, useTransition, e => e.Insert(), session2);
    }

    /// <summary>把整个集合更新到数据库</summary>
    /// <param name="list">实体列表</param>
    /// <param name="useTransition">是否使用事务保护</param>
    /// <param name="session">指定会话，分表分库时必用</param>
    /// <returns></returns>
    public static Int32 Update<T>(this IEnumerable<T> list, Boolean? useTransition = null, IEntitySession? session = null) where T : IEntity
    {
        // 避免列表内实体对象为空
        var list2 = list.AsList();
        var entity = list2.FirstOrDefault(e => e != null);
        if (entity == null) return 0;

        var fact = entity.GetType().AsFactory();
        session ??= fact.Session;

        // Oracle批量更新
        return session.Dal.DbType == DatabaseType.Oracle && list2.Count() > 1
            ? BatchUpdate(list2.Valid(false), null, session)
            : DoAction(list2, useTransition, e => e.Update(), session);
    }

    /// <summary>把整个保存更新到数据库</summary>
    /// <param name="list">实体列表</param>
    /// <param name="useTransition">是否使用事务保护</param>
    /// <param name="session">指定会话，分表分库时必用</param>
    /// <returns></returns>
    public static Int32 Save<T>(this IEnumerable<T> list, Boolean? useTransition = null, IEntitySession? session = null) where T : IEntity
    {
        /*
       * Save的几个场景：
       * 1，Find, Update()
       * 2，new, Insert()
       * 3，new, Upsert()
       */

        // 避免列表内实体对象为空
        var list2 = list.AsList();
        var entity = list2.FirstOrDefault(e => e != null);
        if (entity == null) return 0;

        var rs = 0;
        var fact = entity.GetType().AsFactory();
        session ??= fact.Session;

        // Oracle/MySql批量插入
        if (session.Dal.SupportBatch && list2.Count() > 1)
        {
            // 根据是否来自数据库，拆分为两组
            var ts = Split(list2);
            list2 = ts.Item1;
            rs += BatchSave(session, ts.Item2.Valid(true));
        }

        return rs + DoAction(list2, useTransition, e => e.Save(), session);
    }

    /// <summary>把整个保存更新到数据库，保存时不需要验证</summary>
    /// <param name="list">实体列表</param>
    /// <param name="useTransition">是否使用事务保护</param>
    /// <param name="session">指定会话，分表分库时必用</param>
    /// <returns></returns>
    public static Int32 SaveWithoutValid<T>(this IEnumerable<T> list, Boolean? useTransition = null, IEntitySession? session = null) where T : IEntity
    {
        // 避免列表内实体对象为空
        var list2 = list.AsList();
        var entity = list2.FirstOrDefault(e => e != null);
        if (entity == null) return 0;

        var rs = 0;
        var fact = entity.GetType().AsFactory();
        session ??= fact.Session;

        // Oracle/MySql批量插入
        if (session.Dal.SupportBatch && list2.Count() > 1)
        {
            // 根据是否来自数据库，拆分为两组
            var ts = Split(list2);
            list2 = ts.Item1;
            rs += BatchSave(session, ts.Item2);
        }

        return rs + DoAction(list2, useTransition, e => e.SaveWithoutValid(), session);
    }

    private static Tuple<IList<T>, IList<T>> Split<T>(IEnumerable<T> list) where T : IEntity
    {
        var updates = new List<T>();
        var others = new List<T>();
        foreach (var item in list)
        {
            if (item.IsFromDatabase)
                updates.Add(item);
            else
                others.Add(item);
        }

        return new Tuple<IList<T>, IList<T>>(updates, others);
    }

    private static Int32 BatchSave<T>(IEntitySession session, IEnumerable<T> list) where T : IEntity
    {
        // 没有其它唯一索引，且主键为空时，走批量插入
        var rs = 0;
        if (!session.DataTable.Indexes.Any(di => di.Unique))
        {
            var inserts = new List<T>();
            var updates = new List<T>();
            var upserts = new List<T>();
            foreach (var item in list)
            {
                // 来自数据库，更新
                if (item.IsFromDatabase)
                    updates.Add(item);
                // 空主键，插入
                else if (item.IsNullKey)
                    inserts.Add(item);
                // 其它 Upsert
                else
                    upserts.Add(item);
            }
            list = upserts;

            if (inserts.Count > 0) rs += BatchInsert(inserts, option: null, session);
            if (updates.Count > 0)
            {
                // 只有Oracle支持批量Update
                if (session.Dal.DbType == DatabaseType.Oracle)
                    rs += BatchUpdate(updates, null, session);
                else
                    upserts.AddRange(upserts);
            }
        }

        if (list.Any()) rs += BatchUpsert(list, null, session);

        return rs;
    }

    /// <summary>把整个集合从数据库中删除</summary>
    /// <param name="list">实体列表</param>
    /// <param name="useTransition">是否使用事务保护</param>
    /// <param name="session">指定会话，分表分库时必用</param>
    /// <returns></returns>
    public static Int32 Delete<T>(this IEnumerable<T> list, Boolean? useTransition = null, IEntitySession? session = null) where T : IEntity
    {
        // 避免列表内实体对象为空
        var list2 = list.AsList();
        var entity = list2.FirstOrDefault(e => e != null);
        if (entity == null) return 0;

        var fact = entity.GetType().AsFactory();
        session ??= fact.Session;

        // 单一主键，采用批量操作
        var pks = fact.Table.PrimaryKeys;
        if (pks != null && pks.Length == 1 && list2.Count() > 1)
        {
            var pk = pks[0];
            var count = 0;
            var rs = 0;
            var ks = new List<Object>();
            var sql = $"Delete From {session.FormatedTableName} Where ";
            foreach (var item in list2)
            {
                var val = item[pk.Name];
                if (val == null) continue;

                ks.Add(val);
                count++;

                // 分批执行
                if (count >= 1000)
                {
                    rs += session.Execute(sql + pk.In(ks));

                    ks.Clear();
                    count = 0;
                }
            }
            if (count > 0)
            {
                rs += session.Execute(sql + pk.In(ks));
            }

            return rs;
        }

        return DoAction(list2, useTransition, e => e.Delete(), session);
    }

    private static Int32 DoAction<T>(this IEnumerable<T> list, Boolean? useTransition, Func<T, Int32> func, IEntitySession session) where T : IEntity
    {
        var list2 = list.AsList();
        if (!list2.Any()) return 0;

        // 避免列表内实体对象为空
        var entity = list2.First(e => e != null);
        if (entity == null) return 0;

        //var fact = entity.GetType().AsFactory();

        //!!! SQLite 默认使用事务将会导致实体队列批量更新时大范围锁数据行，回归到由外部控制增加事务
        //// SQLite 批操作默认使用事务，其它数据库默认不使用事务
        //if (useTransition == null)
        //{
        //    //session ??= fact.Session;
        //    useTransition = session.Dal.DbType == DatabaseType.SQLite;
        //}

        var count = 0;
        if (useTransition != null && useTransition.Value)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            using var trans = session.CreateTrans();
            count = DoAction(list2, func, count);

            trans.Commit();
        }
        else
        {
            count = DoAction(list2, func, count);
        }

        return count;
    }

    private static Int32 DoAction<T>(this IEnumerable<T> list, Func<T, Int32> func, Int32 count) where T : IEntity
    {
        // 加锁拷贝，避免遍历时出现多线程冲突
        var arr = list is ICollection<T> cs ? cs.ToArray() : list.ToArray();
        foreach (var item in arr)
        {
            if (item != null) count += func(item);
        }
        return count;
    }

    /// <summary>批量验证对象</summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <param name="isNew"></param>
    /// <returns></returns>
    public static IList<T> Valid<T>(this IEnumerable<T> list, Boolean isNew) where T : IEntity
    {
        var rs = new List<T>();

        var list2 = list.AsList();
        var entity = list2.FirstOrDefault(e => e != null);
        if (entity == null) return rs;

        var fact = entity.GetType().AsFactory();
        var modules = fact.Modules;

        // 验证对象
        foreach (IEntity item in list2)
        {
            if (item is EntityBase entity2)
            {
                if (entity2.Valid(isNew ? DataMethod.Insert : DataMethod.Update)) rs.Add((T)item);
            }
            else
                rs.Add((T)item);
        }

        return rs;
    }
    #endregion

    #region 批量更新
    /// <summary>批量插入</summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="list">实体列表</param>
    /// <param name="columns">要插入的字段，默认所有字段</param>
    /// <param name="session">指定会话，分表分库时必用</param>
    /// <returns>
    /// Oracle：当批量插入操作中有一条记录无法正常写入，则本次写入的所有数据都不会被写入（可以理解为自带事务）
    /// MySQL：当批量插入操作中有一条记录无法正常写入，则本次写入的所有数据都不会被写入（可以理解为自带事务）
    /// </returns>
    public static Int32 BatchInsert<T>(this IEnumerable<T> list, IDataColumn[] columns, IEntitySession? session = null) where T : IEntity
    {
        var option = new BatchOption(columns, null, null);
        return BatchInsert(list, option, session);
    }

    /// <summary>批量插入</summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="list">实体列表</param>
    /// <param name="option">批操作选项</param>
    /// <param name="session">指定会话，分表分库时必用</param>
    /// <returns>
    /// Oracle：当批量插入操作中有一条记录无法正常写入，则本次写入的所有数据都不会被写入（可以理解为自带事务）
    /// MySQL：当批量插入操作中有一条记录无法正常写入，则本次写入的所有数据都不会被写入（可以理解为自带事务）
    /// </returns>
    public static Int32 BatchInsert<T>(this IEnumerable<T> list, BatchOption? option = null, IEntitySession? session = null) where T : IEntity
    {
        var list2 = list.AsList();
        if (list2 == null || !list2.Any()) return 0;

        option ??= new BatchOption();

        var entity = list2.First();
        var fact = entity.GetType().AsFactory();
        session ??= fact.Session;
        session.InitData();

        var dal = session.Dal;
        if (option.BatchSize <= 0) option.BatchSize = dal.GetBatchSize();

        if (option.Columns == null)
        {
            var columns = fact.Fields.Where(e => e.Field != null).Select(e => e.Field!).ToArray();

            // 第一列数据包含非零自增，表示要插入自增值
            var id = columns.FirstOrDefault(e => e.Identity);
            if (id != null)
            {
                // 如果自增列数据为0，则提出自增列，让数据库填充自增值
                if (entity[id.Name].ToLong() == 0) columns = columns.Where(e => !e.Identity).ToArray();
            }

            // 每个列要么有脏数据，要么允许空。不允许空又没有脏数据的字段插入没有意义
            //var dirtys = GetDirtyColumns(fact, list.Cast<IEntity>());
            //if (fact.FullInsert)
            //    columns = columns.Where(e => e.Nullable || dirtys.Contains(e.Name)).ToArray();
            //else
            //    columns = columns.Where(e => dirtys.Contains(e.Name)).ToArray();
            if (!option.FullInsert && !fact.FullInsert)
            {
                var dirtys = GetDirtyColumns(fact, list2.Cast<IEntity>());
                columns = columns.Where(e => dirtys.Contains(e)).ToArray();
            }

            option.Columns = columns;
        }

        var total = list2.Count();
        var tracer = dal.Tracer ?? DAL.GlobalTracer;
        using var span = tracer?.NewSpan($"db:{dal.ConnName}:BatchInsert:{fact.Table.TableName}", $"{session.TableName}[{total}]", total);
        span?.AppendTag($"BatchSize: {option.BatchSize}");
        span?.AppendTag($"Columns: {option.Columns.Join(",", e => e.Name)}");
        try
        {
            var rs = 0;
            for (var i = 0; i < total; i += option.BatchSize)
            {
                var tmp = list2.Skip(i).Take(option.BatchSize).ToList();
                rs += dal.Session.Insert(session.DataTable, option.Columns, tmp.Cast<IModel>());

                // 清除脏数据，避免重复提交保存
                foreach (var item in tmp)
                {
                    item.Dirtys.Clear();
                }
            }

            return rs;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, list2);
            throw;
        }
    }

    /// <summary>批量忽略插入</summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="list">实体列表</param>
    /// <param name="columns">要插入的字段，默认所有字段</param>
    /// <param name="session">指定会话，分表分库时必用</param>
    /// <returns>
    /// Oracle：当批量插入操作中有一条记录无法正常写入，则本次写入的所有数据都不会被写入（可以理解为自带事务）
    /// MySQL：当批量插入操作中有一条记录无法正常写入，则本次写入的所有数据都不会被写入（可以理解为自带事务）
    /// </returns>
    public static Int32 BatchInsertIgnore<T>(this IEnumerable<T> list, IDataColumn[] columns, IEntitySession? session = null) where T : IEntity
    {
        var option = new BatchOption(columns, null, null);
        return BatchInsertIgnore(list, option, session);
    }

    /// <summary>批量忽略插入</summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="list">实体列表</param>
    /// <param name="option">批操作选项</param>
    /// <param name="session">指定会话，分表分库时必用</param>
    /// <returns>
    /// Oracle：当批量插入操作中有一条记录无法正常写入，则本次写入的所有数据都不会被写入（可以理解为自带事务）
    /// MySQL：当批量插入操作中有一条记录无法正常写入，则本次写入的所有数据都不会被写入（可以理解为自带事务）
    /// </returns>
    public static Int32 BatchInsertIgnore<T>(this IEnumerable<T> list, BatchOption? option = null, IEntitySession? session = null) where T : IEntity
    {
        var list2 = list.AsList();
        if (list2 == null || !list2.Any()) return 0;

        option ??= new BatchOption();

        var entity = list2.First();
        var fact = entity.GetType().AsFactory();
        session ??= fact.Session;
        session.InitData();

        var dal = session.Dal;
        if (option.BatchSize <= 0) option.BatchSize = dal.GetBatchSize();

        if (option.Columns == null)
        {
            var columns = fact.Fields.Where(e => e.Field != null).Select(e => e.Field!).ToArray();

            // 第一列数据包含非零自增，表示要插入自增值
            var id = columns.FirstOrDefault(e => e.Identity);
            if (id != null)
            {
                // 如果自增列数据为0，则提出自增列，让数据库填充自增值
                if (entity[id.Name].ToLong() == 0) columns = columns.Where(e => !e.Identity).ToArray();
            }

            // 每个列要么有脏数据，要么允许空。不允许空又没有脏数据的字段插入没有意义
            if (!option.FullInsert && !fact.FullInsert)
            {
                var dirtys = GetDirtyColumns(fact, list2.Cast<IEntity>());
                columns = columns.Where(e => dirtys.Contains(e)).ToArray();
            }

            option.Columns = columns;
        }

        var total = list2.Count();
        var tracer = dal.Tracer ?? DAL.GlobalTracer;
        using var span = tracer?.NewSpan($"db:{dal.ConnName}:InsertIgnore:{fact.Table.TableName}", $"{session.TableName}[{total}]", total);
        span?.AppendTag($"BatchSize: {option.BatchSize}");
        span?.AppendTag($"Columns: {option.Columns.Join(",", e => e.Name)}");
        try
        {
            var rs = 0;
            for (var i = 0; i < total; i += option.BatchSize)
            {
                var tmp = list2.Skip(i).Take(option.BatchSize).ToList();
                rs += dal.Session.InsertIgnore(session.DataTable, option.Columns, tmp.Cast<IModel>());

                // 清除脏数据，避免重复提交保存
                foreach (var item in tmp)
                {
                    item.Dirtys.Clear();
                }
            }

            return rs;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, list2);
            throw;
        }
    }

    /// <summary>批量替换</summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="list">实体列表</param>
    /// <param name="columns">要插入的字段，默认所有字段</param>
    /// <param name="session">指定会话，分表分库时必用</param>
    /// <returns>
    /// Oracle：当批量插入操作中有一条记录无法正常写入，则本次写入的所有数据都不会被写入（可以理解为自带事务）
    /// MySQL：当批量插入操作中有一条记录无法正常写入，则本次写入的所有数据都不会被写入（可以理解为自带事务）
    /// </returns>
    public static Int32 BatchReplace<T>(this IEnumerable<T> list, IDataColumn[] columns, IEntitySession? session = null) where T : IEntity
    {
        var option = new BatchOption(columns, null, null);
        return BatchReplace(list, option, session);
    }

    /// <summary>批量替换</summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="list">实体列表</param>
    /// <param name="option">批操作选项</param>
    /// <param name="session">指定会话，分表分库时必用</param>
    /// <returns>
    /// Oracle：当批量插入操作中有一条记录无法正常写入，则本次写入的所有数据都不会被写入（可以理解为自带事务）
    /// MySQL：当批量插入操作中有一条记录无法正常写入，则本次写入的所有数据都不会被写入（可以理解为自带事务）
    /// </returns>
    public static Int32 BatchReplace<T>(this IEnumerable<T> list, BatchOption? option = null, IEntitySession? session = null) where T : IEntity
    {
        var list2 = list.AsList();
        if (list2 == null || !list2.Any()) return 0;

        option ??= new BatchOption();

        var entity = list2.First();
        var fact = entity.GetType().AsFactory();
        session ??= fact.Session;
        session.InitData();

        var dal = session.Dal;
        if (option.BatchSize <= 0) option.BatchSize = dal.GetBatchSize();

        if (option.Columns == null)
        {
            var columns = fact.Fields.Where(e => e.Field != null).Select(e => e.Field!).ToArray();

            // 第一列数据包含非零自增，表示要插入自增值
            var id = columns.FirstOrDefault(e => e.Identity);
            if (id != null)
            {
                // 如果自增列数据为0，则提出自增列，让数据库填充自增值
                if (entity[id.Name].ToLong() == 0) columns = columns.Where(e => !e.Identity).ToArray();
            }

            // 每个列要么有脏数据，要么允许空。不允许空又没有脏数据的字段插入没有意义
            if (!option.FullInsert && !fact.FullInsert)
            {
                var dirtys = GetDirtyColumns(fact, list2.Cast<IEntity>());
                columns = columns.Where(e => dirtys.Contains(e)).ToArray();
            }

            option.Columns = columns;
        }

        var total = list2.Count();
        var tracer = dal.Tracer ?? DAL.GlobalTracer;
        using var span = tracer?.NewSpan($"db:{dal.ConnName}:BatchReplace:{fact.Table.TableName}", $"{session.TableName}[{total}]", total);
        span?.AppendTag($"BatchSize: {option.BatchSize}");
        span?.AppendTag($"Columns: {option.Columns.Join(",", e => e.Name)}");
        try
        {
            var rs = 0;
            for (var i = 0; i < total; i += option.BatchSize)
            {
                var tmp = list2.Skip(i).Take(option.BatchSize).ToList();
                rs += dal.Session.Replace(session.DataTable, option.Columns, tmp.Cast<IModel>());

                // 清除脏数据，避免重复提交保存
                foreach (var item in tmp)
                {
                    item.Dirtys.Clear();
                }
            }

            return rs;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, list2);
            throw;
        }
    }

    /// <summary>批量更新</summary>
    /// <remarks>
    /// 注意类似：XCode.Exceptions.XSqlException: ORA-00933: SQL 命令未正确结束
    /// [SQL:Update tablen_Name Set FieldName=:FieldName W [:FieldName=System.Int32[]]][DB:AAA/Oracle]
    /// 建议是优先检查表是否存在主键，如果由于没有主键导致，即使通过try...cache 依旧无法正常保存。
    /// </remarks>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="list">实体列表</param>
    /// <param name="columns">要更新的字段，默认所有字段</param>
    /// <param name="updateColumns">要更新的字段，默认脏数据</param>
    /// <param name="addColumns">要累加更新的字段，默认累加</param>
    /// <param name="session">指定会话，分表分库时必用</param>
    /// <returns></returns>
    public static Int32 BatchUpdate<T>(this IEnumerable<T> list, IDataColumn[] columns, ICollection<String>? updateColumns = null, ICollection<String>? addColumns = null, IEntitySession? session = null) where T : IEntity
    {
        var option = new BatchOption(columns, updateColumns, addColumns);
        return BatchUpdate(list, option, session);
    }

    /// <summary>批量更新</summary>
    /// <remarks>
    /// 注意类似：XCode.Exceptions.XSqlException: ORA-00933: SQL 命令未正确结束
    /// [SQL:Update tablen_Name Set FieldName=:FieldName W [:FieldName=System.Int32[]]][DB:AAA/Oracle]
    /// 建议是优先检查表是否存在主键，如果由于没有主键导致，即使通过try...cache 依旧无法正常保存。
    /// </remarks>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="list">实体列表</param>
    /// <param name="option">批操作选项</param>
    /// <param name="session">指定会话，分表分库时必用</param>
    /// <returns></returns>
    public static Int32 BatchUpdate<T>(this IEnumerable<T> list, BatchOption? option = null, IEntitySession? session = null) where T : IEntity
    {
        var list2 = list.AsList();
        if (list2 == null || !list2.Any()) return 0;

        option ??= new BatchOption();

        var entity = list2.First();
        var fact = entity.GetType().AsFactory();
        session ??= fact.Session;
        session.InitData();

        var dal = session.Dal;
        if (option.BatchSize <= 0) option.BatchSize = dal.GetBatchSize();

        option.Columns ??= fact.Fields.Where(e => e.Field != null).Select(e => e.Field!).Where(e => !e.Identity).ToArray();
        //if (updateColumns == null) updateColumns = entity.Dirtys.Keys;
        if (option.UpdateColumns == null)
        {
            // 所有实体对象的脏字段作为更新字段
            var dirtys = GetDirtyColumns(fact, list2.Cast<IEntity>());
            // 创建时间等字段不参与Update
            dirtys = dirtys.Where(e => !e.Name.StartsWithIgnoreCase("Create")).ToArray();

            // 统一约定，updateColumns 外部传入Name，内部再根据columns转为专用字段名
            if (dirtys.Count > 0) option.UpdateColumns = dirtys.Select(e => e.Name).ToArray();
        }
        var updateColumns = option.UpdateColumns;
        var addColumns = option.AddColumns ??= fact.AdditionalFields;

        if ((updateColumns == null || updateColumns.Count <= 0) && (addColumns == null || addColumns.Count <= 0)) return 0;

        var total = list2.Count();
        var tracer = dal.Tracer ?? DAL.GlobalTracer;
        using var span = tracer?.NewSpan($"db:{dal.ConnName}:BatchUpdate:{fact.Table.TableName}", $"{session.TableName}[{total}]", total);
        span?.AppendTag($"BatchSize: {option.BatchSize}");
        span?.AppendTag($"Columns: {option.Columns.Join(",", e => e.Name)}");
        span?.AppendTag($"UpdateColumns: {updateColumns?.Join()}");
        span?.AppendTag($"AddColumns: {addColumns.Join()}");
        try
        {
            var rs = 0;
            for (var i = 0; i < total; i += option.BatchSize)
            {
                var tmp = list2.Skip(i).Take(option.BatchSize).ToList();
                rs += dal.Session.Update(session.DataTable, option.Columns, updateColumns, addColumns, tmp.Cast<IModel>());

                // 清除脏数据，避免重复提交保存
                foreach (var item in tmp)
                {
                    item.Dirtys.Clear();
                }
            }

            return rs;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, list2);
            throw;
        }
    }

    /// <summary>批量插入或更新</summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="list">实体列表</param>
    /// <param name="columns">要插入的字段，默认所有字段</param>
    /// <param name="updateColumns">要更新的字段，默认脏数据</param>
    /// <param name="addColumns">要累加更新的字段，默认累加</param>
    /// <param name="session">指定会话，分表分库时必用</param>
    /// <returns>
    /// MySQL返回值：返回值相当于流程执行次数，及时insert失败也会累计一次执行（所以不建议通过该返回值确定操作记录数）
    /// do insert success = 1次; 
    /// do update success =2次(insert 1次+update 1次)，
    /// 简单来说：对于一行记录，如果Insert 成功则返回1，如果需要执行的是update 则返回2
    /// Oracle返回值：无论是插入还是更新返回的都始终为-1
    /// </returns>
    public static Int32 Upsert<T>(this IEnumerable<T> list, IDataColumn[]? columns = null, ICollection<String>? updateColumns = null, ICollection<String>? addColumns = null, IEntitySession? session = null) where T : IEntity
    {
        var option = new BatchOption(columns, updateColumns, addColumns);
        return BatchUpsert(list, option, session);
    }

    /// <summary>批量插入或更新</summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="list">实体列表</param>
    /// <param name="option">批操作选项</param>
    /// <param name="session">指定会话，分表分库时必用</param>
    /// <returns>
    /// MySQL返回值：返回值相当于流程执行次数，及时insert失败也会累计一次执行（所以不建议通过该返回值确定操作记录数）
    /// do insert success = 1次; 
    /// do update success =2次(insert 1次+update 1次)，
    /// 简单来说：对于一行记录，如果Insert 成功则返回1，如果需要执行的是update 则返回2
    /// Oracle返回值：无论是插入还是更新返回的都始终为-1
    /// </returns>
    public static Int32 BatchUpsert<T>(this IEnumerable<T> list, BatchOption? option = null, IEntitySession? session = null) where T : IEntity
    {
        var list2 = list.AsList();
        if (list2 == null || !list2.Any()) return 0;

        option ??= new BatchOption();

        var entity = list2.First();
        var fact = entity.GetType().AsFactory();
        session ??= fact.Session;
        session.InitData();

        var dal = session.Dal;
        if (option.BatchSize <= 0) option.BatchSize = dal.GetBatchSize();

        // 批量Upsert需要主键参与，哪怕是自增，构建update的where时用到主键
        if (option.Columns == null)
        {
            var columns = fact.Fields.Where(e => e.Field != null).Select(e => e.Field!).ToArray();

            if (!option.FullInsert && !fact.FullInsert)
            {
                var dirtys = GetDirtyColumns(fact, list2.Cast<IEntity>());
                columns = columns.Where(e => e.PrimaryKey || dirtys.Contains(e)).ToArray();
            }

            // 遇到自增字段，需要谨慎处理，部分insert部分update则无法执行upsert
            var uk = fact.Unique;
            if (uk != null && uk.IsIdentity)
            {
                // 如果所有自增字段都是0，则不参与批量Upsert
                if (list2.All(e => e[uk.Name].ToLong() == 0))
                    columns = columns.Where(e => !e.Identity).ToArray();
                else if (list2.Any(e => e[uk.Name].ToLong() == 0))
                    throw new NotSupportedException($"Upsert遇到自增字段，且部分为0部分不为0，无法同时支持Insert和Update");
            }

            option.Columns = columns;

            //var dbt = session.Dal.DbType;
            //if (dbt is DatabaseType.SqlServer or DatabaseType.Oracle)
            //    columns = fact.Fields.Select(e => e.Field).Where(e => !e.Identity || e.PrimaryKey).ToArray();
            //else if (dbt is DatabaseType.MySql)
            //{
            //    // 只有标识键的情况下会导致重复执行insert方法 目前只测试了Mysql库
            //    columns = fact.Fields.Select(e => e.Field).ToArray();
            //}
            //else if (dbt is DatabaseType.SQLite)
            //{
            //    // SQLite库集合更新这里用到了Insert Into Do Update,所以不能排除主键填充，所以这里增加了 or DatabaseType.SQLite
            //    // 如果所有自增字段都是0，则不参与批量Upsert
            //    var uk = fact.Unique;
            //    if (uk != null && uk.IsIdentity && list.All(e => e.IsNullKey))
            //        columns = fact.Fields.Select(e => e.Field).Where(e => !e.Identity).ToArray();
            //    else
            //        columns = fact.Fields.Select(e => e.Field).ToArray();
            //}
            //else
            //    columns = fact.Fields.Select(e => e.Field).Where(e => !e.Identity).ToArray();

            // 每个列要么有脏数据，要么允许空。不允许空又没有脏数据的字段插入没有意义
            //var dirtys = GetDirtyColumns(fact, list.Cast<IEntity>());
            //if (fact.FullInsert)
            //    columns = columns.Where(e => e.Nullable || dirtys.Contains(e.Name)).ToArray();
            //else
            //    columns = columns.Where(e => dirtys.Contains(e.Name)).ToArray();
        }
        //if (updateColumns == null) updateColumns = entity.Dirtys.Keys;
        if (option.UpdateColumns == null)
        {
            // 所有实体对象的脏字段作为更新字段
            var dirtys = GetDirtyColumns(fact, list2.Cast<IEntity>());
            // 创建时间等字段不参与Update
            dirtys = dirtys.Where(e => !e.Name.StartsWithIgnoreCase("Create")).ToArray();

            // 统一约定，updateColumns 外部传入Name，内部再根据columns转为专用字段名
            if (dirtys.Count > 0) option.UpdateColumns = dirtys.Select(e => e.Name).ToArray();
        }
        var updateColumns = option.UpdateColumns;
        var addColumns = option.AddColumns ??= fact.AdditionalFields;
        // 没有任何数据变更则直接返回0
        if ((updateColumns == null || updateColumns.Count <= 0) && (addColumns == null || addColumns.Count <= 0)) return 0;

        var total = list2.Count();
        var tracer = dal.Tracer ?? DAL.GlobalTracer;
        using var span = tracer?.NewSpan($"db:{dal.ConnName}:BatchUpsert:{fact.Table.TableName}", $"{session.TableName}[{total}]", total);
        span?.AppendTag($"BatchSize: {option.BatchSize}");
        span?.AppendTag($"Columns: {option.Columns.Join(",", e => e.Name)}");
        try
        {
            var rs = 0;
            for (var i = 0; i < total; i += option.BatchSize)
            {
                var tmp = list2.Skip(i).Take(option.BatchSize).ToList();
                rs += dal.Session.Upsert(session.DataTable, option.Columns, updateColumns, addColumns, tmp.Cast<IModel>());

                // 清除脏数据，避免重复提交保存
                foreach (var item in tmp)
                {
                    item.Dirtys.Clear();
                }
            }

            return rs;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, list2);
            throw;
        }
    }

    /// <summary>批量插入或更新</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="columns">要插入的字段，默认所有字段</param>
    /// <param name="updateColumns">主键已存在时，要更新的字段</param>
    /// <param name="addColumns">主键已存在时，要累加更新的字段</param>
    /// <param name="session">指定会话，分表分库时必用</param>
    /// <returns>
    /// MySQL返回值：返回值相当于流程执行次数，及时insert失败也会累计一次执行（所以不建议通过该返回值确定操作记录数）
    /// do insert success = 1次; 
    /// do update success =2次(insert 1次+update 1次)，
    /// 简单来说：如果Insert 成功则返回1，如果需要执行的是update 则返回2，
    /// </returns>
    public static Int32 Upsert(this IEntity entity, IDataColumn[]? columns, ICollection<String>? updateColumns = null, ICollection<String>? addColumns = null, IEntitySession? session = null)
    {
        var option = new BatchOption(columns, updateColumns, addColumns);
        return Upsert(entity, option, session);
    }

    /// <summary>批量插入或更新</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="option">批操作选项</param>
    /// <param name="session">指定会话，分表分库时必用</param>
    /// <returns>
    /// MySQL返回值：返回值相当于流程执行次数，及时insert失败也会累计一次执行（所以不建议通过该返回值确定操作记录数）
    /// do insert success = 1次; 
    /// do update success =2次(insert 1次+update 1次)，
    /// 简单来说：如果Insert 成功则返回1，如果需要执行的是update 则返回2，
    /// </returns>
    public static Int32 Upsert(this IEntity entity, BatchOption? option = null, IEntitySession? session = null)
    {
        option ??= new BatchOption();

        var fact = entity.GetType().AsFactory();
        session ??= fact.Session;
        session.InitData();

        var dal = session.Dal;

        if (option.Columns == null)
        {
            var columns = fact.Fields.Where(e => e.Field != null).Select(e => e.Field!).ToArray();
            columns = columns.Where(e => !e.Identity).ToArray();

            // 每个列要么有脏数据，要么允许空。不允许空又没有脏数据的字段插入没有意义
            //var dirtys = GetDirtyColumns(fact, new[] { entity });
            //if (fact.FullInsert)
            //    columns = columns.Where(e => e.Nullable || dirtys.Contains(e.Name)).ToArray();
            //else
            //    columns = columns.Where(e => dirtys.Contains(e.Name)).ToArray();
            if (!option.FullInsert && !fact.FullInsert)
            {
                var dirtys = GetDirtyColumns(fact, [entity]);
                columns = columns.Where(e => e.PrimaryKey || dirtys.Contains(e)).ToArray();
            }
            option.Columns = columns;
        }
        option.UpdateColumns ??= entity.Dirtys.Where(e => !e.StartsWithIgnoreCase("Create")).Distinct().ToArray();
        option.AddColumns ??= fact.AdditionalFields;

        //dal.CheckDatabase();
        //var tableName = dal.Db.FormatTableName(session.TableName);

        var tracer = dal.Tracer ?? DAL.GlobalTracer;
        using var span = tracer?.NewSpan($"db:{dal.ConnName}:Upsert:{fact.Table.TableName}");
        try
        {
            if (span != null) span.Tag = $"{session.TableName}[{entity}]";

            return dal.Session.Upsert(session.DataTable, option.Columns, option.UpdateColumns, option.AddColumns, [entity as IModel]);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, entity);
            throw;
        }
    }

    /// <summary>已有数据集合并新数据集，未存在时插入、存在时更新、多余则删除，常用于更新统计表</summary>
    /// <remarks>
    /// 合并操作应用于实体层，不同于数据库的MergeTo。
    /// 常用于数据分析场景，将新数据合并到已有数据中，已有数据中没有的数据插入，已有数据中有的数据更新，已有数据中多余的数据删除。
    /// 数据统计时，为了方便会构建新的数据集，然后合并到已有数据集中。
    /// </remarks>
    /// <typeparam name="T">已有数据集实体类型</typeparam>
    /// <typeparam name="T2">新数据集实体类型</typeparam>
    /// <param name="source">已有数据集。例如：该日已有统计数据列表</param>
    /// <param name="news">新数据集。例如：根据原始数据统计得到的数据列表</param>
    /// <param name="keys">业务主键。比较新旧两行统计对象的业务主键是否相同</param>
    /// <param name="trim">删除多余数据。默认true</param>
    /// <returns></returns>
    public static IList<T> Merge<T, T2>(this IList<T> source, IEnumerable<T2> news, String[] keys, Boolean trim = true) where T : class, IEntity where T2 : IModel => source.Merge(news, (x, y) => keys.All(k => x[k] == y[k]), trim);

    /// <summary>已有数据集合并新数据集，未存在时插入、存在时更新、多余则删除，常用于更新统计表</summary>
    /// <remarks>
    /// 合并操作应用于实体层，不同于数据库的MergeTo。
    /// 常用于数据分析场景，将新数据合并到已有数据中，已有数据中没有的数据插入，已有数据中有的数据更新，已有数据中多余的数据删除。
    /// 数据统计时，为了方便会构建新的数据集，然后合并到已有数据集中。
    /// </remarks>
    /// <typeparam name="T">数据集实体类型</typeparam>
    /// <param name="source">已有数据集。例如：该日已有统计数据列表</param>
    /// <param name="news">新数据集。例如：根据原始数据统计得到的数据列表</param>
    /// <param name="comparer">比较器。比较新旧两行统计对象是否相同业务维度</param>
    /// <param name="trim">删除多余数据。默认true</param>
    /// <returns></returns>
    public static IList<T> Merge<T>(this IList<T> source, IEnumerable<T> news, IEqualityComparer<T> comparer, Boolean trim = true) where T : class, IEntity => source.Merge(news, comparer.Equals, trim);

    /// <summary>已有数据集合并新数据集，未存在时插入、存在时更新、多余则删除，常用于更新统计表</summary>
    /// <remarks>
    /// 合并操作应用于实体层，不同于数据库的MergeTo。
    /// 常用于数据分析场景，将新数据合并到已有数据中，已有数据中没有的数据插入，已有数据中有的数据更新，已有数据中多余的数据删除。
    /// 数据统计时，为了方便会构建新的数据集，然后合并到已有数据集中。
    /// </remarks>
    /// <typeparam name="T">已有数据集实体类型</typeparam>
    /// <typeparam name="T2">新数据集实体类型</typeparam>
    /// <param name="source">已有数据集。例如：该日已有统计数据列表</param>
    /// <param name="news">新数据集。例如：根据原始数据统计得到的数据列表</param>
    /// <param name="predicate">比较器。比较新旧两行统计对象是否相同业务维度</param>
    /// <param name="trim">删除多余数据。默认true</param>
    /// <returns></returns>
    public static IList<T> Merge<T, T2>(this IList<T> source, IEnumerable<T2> news, Func<T, T2, Boolean> predicate, Boolean trim = true) where T : class, IEntity where T2 : IModel
    {
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));

        var rs = new List<T>();
        var fact = typeof(T).AsFactory();
        foreach (var model in news)
        {
            var entity = source.FirstOrDefault(e => predicate(e, model));
            if (entity == null)
            {
                entity = model as T;
                if (entity == null)
                {
                    entity = (T)fact.Create(true);
                    entity.CopyFrom(model, true);
                }

                rs.Add(entity);
            }
            else
            {
                // 启用脏数据，仅复制有脏数据的字段，同时避免拷贝主键
                entity.CopyFrom(model, true);

                rs.Add(entity);
                source.Remove(entity);
            }
        }

        // 删除多余的
        if (trim) source.Delete(true);

        // 保存数据，插入或更新
        rs.Save();

        return rs;
    }

    /// <summary>获取可用于插入的数据列</summary>
    /// <param name="fact"></param>
    /// <param name="list"></param>
    /// <returns></returns>
    public static IList<IDataColumn> GetDirtyColumns(this IEntityFactory fact, IEnumerable<IEntity> list)
    {
        // 任意实体来自数据库，则全部都是目标字段。因为有可能是从数据库查询出来的实体，然后批量插入
        if (list.Any(e => e.IsFromDatabase)) return fact.Fields.Select(e => e.Field).ToList();

        // 构建集合，已经标记为脏数据的字段不再搜索，减少循环次数
        var fields = fact.Fields.ToList();
        var columns = new List<FieldItem>();

        // 非空非字符串字段，都是目标字段
        foreach (var fi in fields)
        {
            // 非空字符串和时间日期类型不参与插入，因为数据库会自动填充默认值。这一点跟单体插入不同。
            if (!fi.IsNullable && fi.Type != typeof(String) && fi.Type != typeof(DateTime))
            {
                columns.Add(fi);
            }
        }
        fields.RemoveAll(e => columns.Contains(e));
        if (fields.Count > 0)
        {
            // 获取所有带有脏数据的字段
            foreach (var entity in list)
            {
                var tmps = new List<FieldItem>();
                foreach (var fi in fields)
                {
                    // 脏数据
                    if (entity.Dirtys[fi.Name]) tmps.Add(fi);
                }

                if (tmps.Count > 0)
                {
                    columns.AddRange(tmps);
                    fields.RemoveAll(e => tmps.Contains(e));
                    if (fields.Count == 0) break;
                }
            }
        }

        var rs = new List<IDataColumn>();
        foreach (var item in columns)
        {
            var dc = item.Field;
            if (dc != null) rs.Add(dc);
        }

        return rs;
    }
    #endregion

    #region 读写数据流
    /// <summary>实体列表转为DbTable</summary>
    /// <param name="list">实体列表</param>
    /// <returns></returns>
    public static DbTable ToTable<T>(this IEnumerable<T> list) where T : IEntity
    {
        //var entity = list.FirstOrDefault();
        //if (entity == null) return null;

        //var fact = (entity?.GetType() ?? typeof(T)).AsFactory();
        var fact = typeof(T).AsFactory();
        var fs = fact.Fields;

        var count = fs.Length;
        var dt = new DbTable
        {
            Columns = new String[count],
            Types = new Type[count],
            Rows = [],
        };
        for (var i = 0; i < fs.Length; i++)
        {
            var fi = fs[i];
            dt.Columns[i] = fi.Name;
            dt.Types[i] = fi.Type;
        }

        foreach (var item in list)
        {
            var dr = new Object?[count];
            for (var i = 0; i < fs.Length; i++)
            {
                var fi = fs[i];
                dr[i] = item[fi.Name];
            }
            dt.Rows.Add(dr);
        }

        return dt;
    }

    /// <summary>实体列表以二进制序列化写入数据流</summary>
    /// <param name="list">实体列表</param>
    /// <param name="stream">数据流</param>
    /// <returns></returns>
    public static Int64 Write<T>(this IEnumerable<T> list, Stream stream) where T : IEntity
    {
        if (list == null) return 0;

        //todo Binary需要字段记录已经写入多少字节，部分数据流不支持Position
        var bn = new Binary { Stream = stream, EncodeInt = true, FullTime = true };
        var p = stream.Position;
        foreach (var entity in list)
        {
            if (entity is IAccessor acc) acc.Write(stream, bn);
        }

        return stream.Position - p;
    }

    /// <summary>写入文件，二进制格式</summary>
    /// <param name="list">实体列表</param>
    /// <param name="file">文件</param>
    /// <returns></returns>
    public static Int64 SaveFile<T>(this IEnumerable<T> list, String file) where T : IEntity
    {
        if (list == null) return 0;

        var compressed = file.EndsWithIgnoreCase(".gz");
        return file.AsFile().OpenWrite(compressed, fs =>
        {
            var bn = new Binary { Stream = fs, EncodeInt = true, FullTime = true };
            foreach (var entity in list)
            {
                if (entity is IAccessor acc) acc.Write(fs, bn);
            }
        });
    }

    /// <summary>写入数据流，Csv格式</summary>
    /// <param name="list">实体列表</param>
    /// <param name="stream">数据量</param>
    /// <param name="fields">要导出的字段列表</param>
    /// <param name="displayfields">要导出的中文字段列表</param>
    /// <returns></returns>
    public static Int64 SaveCsv<T>(this IEnumerable<T> list, Stream stream, String[]? fields = null, String[]? displayfields = null) where T : IEntity
    {
        if (list == null) return 0;

        var count = 0;

        using var csv = new CsvFile(stream, true);
        if (displayfields != null)
            csv.WriteLine(displayfields);
        else if (fields != null)
        {
            // 第一行以ID开头的csv文件，容易被识别为SYLK文件
            if (fields.Length > 0)
                csv.WriteLine(fields.Select((e, k) => (k == 0 && e == "ID") ? "Id" : e));
            else
                csv.WriteLine(fields);
        }
        foreach (var entity in list)
        {
            csv.WriteLine(fields.Select(e => entity[e]));

            count++;
        }

        return count;
    }

    /// <summary>写入文件，Csv格式</summary>
    /// <param name="list">实体列表</param>
    /// <param name="file">文件</param>
    /// <param name="fields">要导出的字段列表</param>
    /// <param name="displayfields">中文字段列表</param>
    /// <returns></returns>
    public static Int64 SaveCsv<T>(this IEnumerable<T> list, String file, String[] fields, String[]? displayfields = null) where T : IEntity
    {
        if (list == null) return 0;

        var compressed = file.EndsWithIgnoreCase(".gz");
        return file.AsFile().OpenWrite(compressed, fs => SaveCsv(list, fs, fields, displayfields));
    }

    /// <summary>写入文件，Csv格式</summary>
    /// <param name="list">实体列表</param>
    /// <param name="file">文件</param>
    /// <param name="displayName">是否使用中文显示名，否则使用英文属性名</param>
    /// <returns></returns>
    public static Int64 SaveCsv<T>(this IEnumerable<T> list, String file, Boolean displayName = false) where T : IEntity
    {
        if (list == null) return 0;

        var fact = typeof(T).AsFactory();
        var fis = fact.Fields;
        return displayName
            ? SaveCsv(list, file, fis.Select(e => e.Name).ToArray(), fis.Select(e => e.DisplayName + "").ToArray())
            : SaveCsv(list, file, fis.Select(e => e.Name).ToArray(), null);


    }

    /// <summary>从数据流读取实体列表</summary>
    /// <param name="factory">实体工厂</param>
    /// <param name="stream">数据流</param>
    /// <returns>实体列表</returns>
    public static IEnumerable<IEntity> Read(this IEntityFactory factory, Stream stream)
    {
        if (factory == null || stream == null) yield break;

        var bn = new Binary { Stream = stream, EncodeInt = true, FullTime = true };
        while (stream.Position < stream.Length)
        {
            var entity = factory.Create();
            if (entity is IAccessor acc) acc.Read(stream, bn);

            yield return entity;
        }
    }

    /// <summary>从文件读取实体列表，二进制格式</summary>
    /// <param name="factory">实体工厂</param>
    /// <param name="file">文件</param>
    /// <returns>实体列表</returns>
    public static IEnumerable<IEntity> LoadFile(this IEntityFactory factory, String file)
    {
        if (file.IsNullOrEmpty()) return [];
        file = file.GetFullPath();
        if (!File.Exists(file)) return [];

        Stream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);

        if (file.EndsWithIgnoreCase(".gz"))
            fs = new GZipStream(fs, CompressionMode.Decompress);

        return Read(factory, fs);
    }

    /// <summary>从文件读取实体列表，二进制格式</summary>
    /// <param name="list">实体列表</param>
    /// <param name="file">文件</param>
    /// <returns>实体列表</returns>
    public static IList<T> LoadFile<T>(this IList<T> list, String file) where T : IEntity
    {
        if (file.IsNullOrEmpty()) return list;
        file = file.GetFullPath();
        if (!File.Exists(file)) return list;

        var factory = typeof(T).AsFactory();
        foreach (var entity in factory.LoadFile(file))
        {
            list.Add((T)entity);
        }

        return list;
    }

    /// <summary>从数据流读取实体列表，Csv格式</summary>
    /// <param name="factory">实体工厂</param>
    /// <param name="stream">数据流</param>
    /// <returns>实体列表</returns>
    public static IEnumerable<IEntity> LoadCsv(this IEntityFactory factory, Stream stream)
    {
        using var csv = new CsvFile(stream, true);

        // 匹配字段
        var names = csv.ReadLine();
        if (names == null || names.Length == 0) yield break;

        var fields = new FieldItem[names.Length];
        for (var i = 0; i < names.Length; i++)
        {
            fields[i] = factory.Fields.FirstOrDefault(e => names[i].EqualIgnoreCase(e.Name, e.DisplayName, e.ColumnName));
        }

        // 读取数据
        while (true)
        {
            var line = csv.ReadLine();
            if (line == null || line.Length == 0) break;

            var entity = factory.Create();
            for (var i = 0; i < fields.Length && i < line.Length; i++)
            {
                var fi = fields[i];
                if (fi != null && !line[i].IsNullOrEmpty()) entity.SetItem(fi.Name, line[i].ChangeType(fi.Type));
            }

            yield return entity;
        }
    }

    /// <summary>从文件读取列表，Csv格式</summary>
    /// <param name="list">实体列表</param>
    /// <param name="file">文件</param>
    /// <returns>实体列表</returns>
    public static IList<T> LoadCsv<T>(this IList<T> list, String file) where T : IEntity
    {
        if (file.IsNullOrEmpty()) return list;
        file = file.GetFullPath();
        if (!File.Exists(file)) return list;

        var compressed = file.EndsWithIgnoreCase(".gz");
        file.AsFile().OpenRead(compressed, fs =>
        {
            var factory = typeof(T).AsFactory();
            foreach (var entity in factory.LoadCsv(fs))
            {
                list.Add((T)entity);
            }
        });

        return list;
    }
    #endregion

    #region 转 DataTable/DataSet
    /// <summary>转为DataTable</summary>
    /// <param name="list">实体列表</param>
    /// <returns></returns>
    public static DataTable ToDataTable<T>(this IEnumerable<T> list) where T : IEntity
    {
        var dt = new DataTable();
        var entity = list.FirstOrDefault();
        if (entity == null) return dt;

        var fact = entity.GetType().AsFactory();

        foreach (var fi in fact.Fields)
        {
            var dc = new DataColumn
            {
                ColumnName = fi.Name,
                DataType = fi.Type,
                Caption = fi.Description,
                AutoIncrement = fi.IsIdentity
            };

            // 关闭这两项，让DataTable宽松一点
            //dc.Unique = item.PrimaryKey;
            //dc.AllowDBNull = item.IsNullable;

            dt.Columns.Add(dc);
        }

        foreach (var item in list)
        {
            var dr = dt.NewRow();
            foreach (var fi in fact.Fields)
            {
                dr[fi.Name] = item[fi.Name];
            }
            dt.Rows.Add(dr);
        }

        return dt;
    }

    /// <summary>转为DataSet</summary>
    /// <returns></returns>
    public static DataSet ToDataSet<T>(this IEnumerable<T> list) where T : IEntity
    {
        var ds = new DataSet();
        ds.Tables.Add(ToDataTable(list));
        return ds;
    }
    #endregion

    #region 辅助
    /// <summary>截断指定字段的超长字符</summary>
    /// <remarks>有些场景为了方便落库，宁可截断超长部分</remarks>
    /// <param name="entity">实体对象</param>
    /// <param name="names">要截断的字段集合</param>
    /// <returns></returns>
    public static Int32 TrimExtraLong(this IEntity entity, params String[] names)
    {
        var factory = entity.GetType().AsFactory();
        if (names == null || names.Length == 0)
        {
            // 处理所有字段
            foreach (var field in factory.Fields)
            {
                if (field.Type == typeof(String) && field.Length > 0)
                {
                    var val = entity[field.Name] as String;
                    if (val != null && val.Length > field.Length) entity.SetItem(field.Name, val.Substring(0, field.Length));
                }
            }
        }
        else
        {
            // 处理特定字段
            foreach (var item in names)
            {
                var field = factory.Table.FindByName(item) as FieldItem;
                if (field != null && field.Type == typeof(String) && field.Length > 0)
                {
                    var val = entity[field.Name] as String;
                    if (val != null && val.Length > field.Length) entity.SetItem(field.Name, val.Substring(0, field.Length));
                }
            }
        }

        return 0;
    }

    /// <summary>转为列表。避免原始迭代被多次遍历</summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="list">实体列表</param>
    internal static IList<T> AsList<T>(this IEnumerable<T> list)
    {
        if (list is IList<T> list2) return list2;

        return list.ToList();
    }
    #endregion
}