﻿using System.ComponentModel;
using System.Data;
using System.Text.RegularExpressions;
using NewLife;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Serialization;
using XCode.Common;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Model;
using XCode.Shards;

namespace XCode;

/// <summary>数据实体类基类。所有数据实体类都必须继承该类。</summary>
[Serializable]
public partial class Entity<TEntity> : EntityBase, IAccessor where TEntity : Entity<TEntity>, new()
{
    #region 构造函数
    /// <summary>静态构造</summary>
    static Entity()
    {
        DAL.InitLog();

        //EntityFactory.Register(typeof(TEntity), new DefaultEntityFactory());

        //var ioc = ObjectContainer.Current;
        //ioc.AddSingleton<IDataRowEntityAccessorProvider, DataRowEntityAccessorProvider>();

        // 1，可以初始化该实体类型的操作工厂
        // 2，CreateOperate将会实例化一个TEntity对象，从而引发TEntity的静态构造函数，
        // 避免实际应用中，直接调用Entity的静态方法时，没有引发TEntity的静态构造函数。
        new TEntity();
    }

    /// <summary>创建实体。</summary>
    /// <remarks>
    /// 可以重写改方法以实现实体对象的一些初始化工作。
    /// 切记，写为实例方法仅仅是为了方便重载，所要返回的实例绝对不会是当前实例。
    /// </remarks>
    /// <param name="forEdit">是否为了编辑而创建，如果是，可以再次做一些相关的初始化工作</param>
    /// <returns></returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected virtual TEntity CreateInstance(Boolean forEdit = false)
    {
        var entity = new TEntity();
        // new TEntity会被编译为Activator.CreateInstance<TEntity>()，还不如Activator.CreateInstance()呢
        // Activator.CreateInstance()有缓存功能，而泛型的那个没有
        //return Activator.CreateInstance(typeof(TEntity)) as TEntity;
        //var entity = typeof(TEntity).CreateInstance() as TEntity;
        Meta.Modules.Create(entity, forEdit);

        return entity;
    }
    #endregion

    #region 填充数据
    /// <summary>加载记录集。无数据时返回空集合而不是null。</summary>
    /// <param name="ds">记录集</param>
    /// <returns>实体数组</returns>
    public static IList<TEntity> LoadData(DataSet ds)
    {
        if (ds == null || ds.Tables.Count <= 0) return new List<TEntity>();

        return LoadData(ds.Tables[0]);
    }

    /// <summary>加载数据表。无数据时返回空集合而不是null。</summary>
    /// <param name="dt">数据表</param>
    /// <returns>实体数组</returns>
    public static IList<TEntity> LoadData(DataTable dt)
    {
        if (dt == null) return new List<TEntity>();

        var list = Meta.Factory.Accessor.LoadData<TEntity>(dt);
        OnLoadData(list);

        return list;
    }

    /// <summary>加载数据表。无数据时返回空集合而不是null。</summary>
    /// <param name="ds">数据表</param>
    /// <returns>实体数组</returns>
    public static IList<TEntity> LoadData(DbTable ds)
    {
        if (ds == null) return new List<TEntity>();

        var list = Meta.Factory.Accessor.LoadData<TEntity>(ds);
        OnLoadData(list);

        return list;
    }

    /// <summary>加载数据表。无数据时返回空集合而不是null。</summary>
    /// <param name="dr">数据读取器</param>
    /// <returns>实体数组</returns>
    public static IList<TEntity> LoadData(IDataReader dr)
    {
        if (dr == null) return new List<TEntity>();

        var list = Meta.Factory.Accessor.LoadData<TEntity>(dr);
        OnLoadData(list);

        return list;
    }

    private static void OnLoadData(IList<TEntity> list)
    {
        // 设置默认累加字段
        EntityAddition.SetField(list.Cast<IEntity>());
        foreach (var entity in list)
        {
            entity.OnLoad();
        }
    }

    private static void LoadSingleCache(IEnumerable<TEntity> list)
    {
        // 如果正在使用单对象缓存，则批量进入
        //!!! 特别注意，如果列表查询指定了列名，可能会导致实体错误覆盖单对象缓存
        var sc = Meta.SingleCache;
        if (sc.Using)
        {
            // 查询列表异步加入对象缓存
            ThreadPool.UnsafeQueueUserWorkItem(es =>
            {
                foreach (var entity in (es as TEntity[])!)
                {
                    sc.Add(entity);
                }
            }, list.ToArray());
        }
    }
    #endregion

    #region 操作
    private static IEntityPersistence Persistence => Meta.Factory.Persistence;

    /// <summary>插入数据，Valid后，在事务中调用<see cref="OnInsert"/>。</summary>
    /// <returns></returns>
    public override Int32 Insert() => DoAction(OnInsert, DataMethod.Insert);

    /// <summary>把该对象持久化到数据库，添加/更新实体缓存。</summary>
    /// <returns></returns>
    protected virtual Int32 OnInsert()
    {
        var rs = Meta.Session.Insert(this);

        // 标记来自数据库
        IsFromDatabase = true;

        // 设置默认累加字段
        EntityAddition.SetField(this);

        return rs;
    }

    /// <summary>更新数据，Valid后，在事务中调用<see cref="OnUpdate"/>。</summary>
    /// <returns></returns>
    public override Int32 Update() => DoAction(OnUpdate, DataMethod.Update);

    /// <summary>更新数据库，同时更新实体缓存</summary>
    /// <returns></returns>
    protected virtual Int32 OnUpdate()
    {
        var rs = Meta.Session.Update(this);

        // 标记来自数据库
        IsFromDatabase = true;

        return rs;
    }

    /// <summary>删除数据，通过在事务中调用OnDelete实现。</summary>
    /// <remarks>
    /// 删除时，如果有且仅有主键有脏数据，则可能是ObjectDataSource之类的删除操作。
    /// 该情况下，实体类没有完整的信息（仅有主键信息），将会导致无法通过扩展属性删除附属数据。
    /// 如果需要避开该机制，请清空脏数据。
    /// </remarks>
    /// <returns></returns>
    public override Int32 Delete() => DoAction(OnDelete, DataMethod.Delete);

    /// <summary>从数据库中删除该对象，同时从实体缓存中删除</summary>
    /// <returns></returns>
    protected virtual Int32 OnDelete() => Meta.Session.Delete(this);

    /// <summary>保存。Insert/Update/Upsert</summary>
    /// <remarks>
    /// Save的几个场景：
    /// 1，Find, Update()
    /// 2，new, Insert()
    /// 3，new, Upsert()
    /// </remarks>
    /// <returns></returns>
    public override Int32 Save()
    {
        // 来自数据库直接Update
        if (IsFromDatabase) return Update();

        // 优先使用自增字段判断
        var fi = Meta.Table.Identity;
        if (fi != null) return Convert.ToInt64(this[fi.Name]) > 0 ? Update() : Insert();

        /*
         * 慈母手中线，游子身上衣。
         * 淳淳教诲时，草木尽芬芳。
         */

        // 如果唯一主键不为空，应该通过后面判断，而不是直接Update
        var isnew = IsNullKey;
        if (isnew) return Insert();

        // Oracle/MySql批量插入
        var db = Meta.Session.Dal;
        if (db.SupportBatch)
        {
            if (!Valid(isnew ? DataMethod.Insert : DataMethod.Update)) return -1;

            if (Meta.InShard) return this.Upsert(null, null, null, Meta.Session);

            // 自动分库分表
            using var split = Meta.CreateShard((this as TEntity)!);
            return this.Upsert(null, null, null, Meta.Session);
        }

        return FindCount(Persistence.GetPrimaryCondition(this), null, null, 0, 0) > 0 ? Update() : Insert();
    }

    /// <summary>不需要验证的保存，不执行Valid，一般用于快速导入数据</summary>
    /// <returns></returns>
    public override Int32 SaveWithoutValid()
    {
        _enableValid = false;
        try
        {
            return Save();
        }
        finally
        {
            _enableValid = true;
        }
    }

    /// <summary>异步保存。实现延迟保存，大事务保存。主要面向日志表和频繁更新的在线记录表</summary>
    /// <param name="msDelay">延迟保存的时间。默认0ms近实时保存</param>
    /// <remarks>
    /// 调用平均耗时190.86ns，IPModule占38.89%，TimeModule占16.31%，UserModule占7.20%，Valid占14.36%
    /// </remarks>
    /// <returns>是否成功加入异步队列，实体对象已存在于队列中则返回false</returns>
    public override Boolean SaveAsync(Int32 msDelay = 0)
    {
        var isnew = false;

        // 优先使用自增字段判断
        var fi = Meta.Unique;
        if (fi != null && fi.Type.IsInt())
            isnew = Convert.ToInt64(this[fi.Name]) == 0;
        // 如果唯一主键不为空，应该通过后面判断，而不是直接Update
        else if (IsNullKey)
            isnew = true;

        // 提前执行Valid，让它提前准备好验证数据
        if (_enableValid)
        {
            if (!Valid(isnew ? DataMethod.Insert : DataMethod.Update)) return false;
        }
        if (!HasDirty) return false;

        if (Meta.InShard) return Meta.Session.Queue.Add(this, msDelay);

        // 自动分库分表，影响后面的Meta.Session
        using var split = Meta.CreateShard((this as TEntity)!);

        return Meta.Session.Queue.Add(this, msDelay);
    }

    /// <summary>插入数据，Valid后调用<see cref="OnInsertAsync"/>。</summary>
    /// <returns></returns>
    public override Task<Int32> InsertAsync() => DoAction(OnInsertAsync, DataMethod.Insert);

    /// <summary>把该对象持久化到数据库，添加/更新实体缓存。</summary>
    /// <returns></returns>
    protected virtual Task<Int32> OnInsertAsync()
    {
        var rs = Meta.Session.InsertAsync(this);

        // 标记来自数据库
        IsFromDatabase = true;

        // 设置默认累加字段
        EntityAddition.SetField(this);

        return rs;
    }

    /// <summary>更新数据，Valid后调用<see cref="OnUpdateAsync"/>。</summary>
    /// <returns></returns>
    public override Task<Int32> UpdateAsync() => DoAction(OnUpdateAsync, DataMethod.Update);

    /// <summary>更新数据库，同时更新实体缓存</summary>
    /// <returns></returns>
    protected virtual Task<Int32> OnUpdateAsync()
    {
        var rs = Meta.Session.UpdateAsync(this);

        // 标记来自数据库
        IsFromDatabase = true;

        return rs;
    }

    /// <summary>删除数据，Valid后调用<see cref="OnDeleteAsync"/>。</summary>
    /// <remarks>
    /// 删除时，如果有且仅有主键有脏数据，则可能是ObjectDataSource之类的删除操作。
    /// 该情况下，实体类没有完整的信息（仅有主键信息），将会导致无法通过扩展属性删除附属数据。
    /// 如果需要避开该机制，请清空脏数据。
    /// </remarks>
    /// <returns></returns>
    public override Task<Int32> DeleteAsync() => DoAction(OnDeleteAsync, DataMethod.Delete);

    /// <summary>从数据库中删除该对象，同时从实体缓存中删除</summary>
    /// <returns></returns>
    protected virtual Task<Int32> OnDeleteAsync() => Meta.Session.DeleteAsync(this);

#if NETCOREAPP
    [StackTraceHidden]
#endif
    private TResult DoAction<TResult>(Func<TResult> func, DataMethod method)
    {
        if (Meta.Table.DataTable.InsertOnly)
        {
            switch (method)
            {
                case DataMethod.Update:
                    throw new XCodeException($"只写的日志型数据[{Meta.ThisType.FullName}]禁止修改！");
                case DataMethod.Delete:
                    throw new XCodeException($"只写的日志型数据[{Meta.ThisType.FullName}]禁止删除！");
            }
        }

        if (_enableValid)
        {
            var rt = Valid(method);

            // 没有更新任何数据
            if (!rt) return typeof(TResult) == typeof(Task<Int32>) ? (TResult)(Object)Task.FromResult(0) : (TResult)(Object)0;
        }

        AutoFillSnowIdPrimaryKey();

        if (Meta.InShard) return func();

        // 自动分库分表
        using var split = Meta.CreateShard((this as TEntity)!);

        return func();
    }

    [NonSerialized]
    private Boolean _enableValid = true;

    /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
    /// <remarks>建议重写者调用基类的实现，因为基类自动生成雪花Id、填充创建更新信息以及验证字符串字段是否超长。</remarks>
    /// <param name="isNew">是否新数据</param>
    public override void Valid(Boolean isNew)
    {
        // 2017-8-17 实体基类不再自动根据唯一索引判断唯一性，一切由用户自己解决

        //var factory = Meta.Factory;
        //// 校验字符串长度，超长时抛出参数异常
        //foreach (var fi in factory.Fields)
        //{
        //    if (fi.Type == typeof(String) && fi.Length > 0)
        //    {
        //        if (this[fi.Name] is String str && str.Length > fi.Length && Dirtys[fi.Name])
        //            throw new ArgumentOutOfRangeException(fi.Name, $"{fi.DisplayName}长度限制{fi.Length}字符");
        //    }
        //}

        //!!! 自动填充雪花Id，用户可能直接调用Valid
        AutoFillSnowIdPrimaryKey();
    }

    /// <summary>验证数据，支持添删改，通过返回值表示验证失败。基类实现字符串字段长度检查</summary>
    /// <param name="method"></param>
    /// <returns></returns>
    public override Boolean Valid(DataMethod method)
    {
        switch (method)
        {
            case DataMethod.Insert:
                Valid(true);
                break;
            case DataMethod.Update:
                Valid(false);
                break;
            case DataMethod.Delete:
                break;
            default:
                break;
        }

        var factory = Meta.Factory;
        // 校验字符串长度，超长时抛出参数异常
        foreach (var fi in factory.Fields)
        {
            if (fi.Type == typeof(String) && fi.Length > 0)
            {
                if (this[fi.Name] is String str && str.Length > fi.Length && Dirtys[fi.Name])
                {
                    // 优先使用主字段，不足时使用主键
                    var uk = Meta.Master;
                    if (uk == null || this[uk.Name] is String str2 && str2.Length > 50) uk = Meta.Unique;

                    if (uk != null)
                        throw new ArgumentOutOfRangeException(fi.Name, $"[{fi.Name}/{fi.DisplayName}]长度限制{fi.Length}字符[{uk.Name}={this[uk.Name]}]");
                    else
                        throw new ArgumentOutOfRangeException(fi.Name, $"[{fi.Name}/{fi.DisplayName}]长度限制{fi.Length}字符");
                }
            }
        }

        var rs = Meta.Modules.Valid(this, method);
        if (!rs) return false;

        //!!! 自动填充雪花Id，用户可能直接调用Valid
        AutoFillSnowIdPrimaryKey();

        return true;
    }

    /// <summary>
    /// 雪花Id生成器。Int64主键非自增时，自动填充
    /// </summary>
    protected void AutoFillSnowIdPrimaryKey()
    {
        var factory = Meta.Factory;

        // 雪花Id生成器。Int64主键非自增时，自动填充
        var pks = factory.Table.PrimaryKeys;
        if (pks != null && pks.Length == 1)
        {
            var pk = pks[0];
            if (!pk.IsIdentity && pk.Type == typeof(Int64) && this[pk.Name].ToLong() == 0)
            {
                SetItem(pk.Name, factory.Snow.NewId());
            }
        }
    }

    /// <summary>根据指定键检查数据，返回数据是否已存在</summary>
    /// <param name="names"></param>
    /// <returns></returns>
    public virtual Boolean Exist(params String[] names) => Exist(true, names);

    /// <summary>根据指定键检查数据是否已存在，若已存在，抛出ArgumentOutOfRangeException异常</summary>
    /// <param name="names"></param>
    public virtual void CheckExist(params String[] names) => CheckExist(true, names);

    /// <summary>根据指定键检查数据是否已存在，若已存在，抛出ArgumentOutOfRangeException异常</summary>
    /// <param name="isNew">是否新数据</param>
    /// <param name="names"></param>
    public virtual void CheckExist(Boolean isNew, params String[] names)
    {
        if (Exist(isNew, names))
        {
            var sb = Pool.StringBuilder.Get();
            String? name = null;
            for (var i = 0; i < names.Length; i++)
            {
                if (sb.Length > 0) sb.Append('，');

                FieldItem? field = Meta.Table.FindByName(names[i]);
                if (field != null) name = field.Description;
                if (String.IsNullOrEmpty(name)) name = names[i];

                sb.AppendFormat("{0}={1}", name, this[names[i]]);
            }

            name = Meta.Table.Description;
            if (!name.IsNullOrEmpty())
            {
                var p = name.IndexOfAny(['。', '，']);
                if (p > 0) name = name[..p];
            }
            if (name.IsNullOrEmpty()) name = typeof(TEntity).Name;
            sb.AppendFormat(" 的{0}已存在！", name);

            throw new ArgumentOutOfRangeException(String.Join(",", names), this[names[0]], sb.Put(true));
        }
    }

    /// <summary>根据指定键检查数据，返回数据是否已存在</summary>
    /// <param name="isNew">是否新数据</param>
    /// <param name="names"></param>
    /// <returns></returns>
    public virtual Boolean Exist(Boolean isNew, params String[] names)
    {
        // 根据指定键查找所有符合的数据，然后比对。
        // 当然，也可以通过指定键和主键配合，找到拥有指定键，但是不是当前主键的数据，只查记录数。
        var values = new Object?[names.Length];
        for (var i = 0; i < names.Length; i++)
        {
            values[i] = this[names[i]];
        }

        var field = Meta.Unique;
        if (field == null) return false;

        var val = this[field.Name];
        var cache = Meta.Session.Cache;
        if (!cache.Using)
        {
            //// 如果是空主键，则采用直接判断记录数的方式，以加快速度
            //if (IsNullKey) return FindCount(names, values) > 0;

            var exp = new WhereExpression();
            for (var i = 0; i < names.Length; i++)
            {
                var fi = Meta.Table.FindByName(names[i]);
                if (ReferenceEquals(fi, null)) throw new ArgumentOutOfRangeException(nameof(names), $"{names[i]} not found");

                exp &= fi == values[i];
            }

            var list = FindAll(exp, null, null, 0, 0);
            if (list == null || list.Count <= 0) return false;
            if (list.Count > 1) return true;

            // 如果是Guid等主键，可能提前赋值，插入操作不能比较主键，直接判断判断存在的唯一索引即可
            if (isNew && !field.IsIdentity) return true;

            return !Equals(val, list[0][field.Name]);
        }
        else
        {
            // 如果是空主键，则采用直接判断记录数的方式，以加快速度
            var list = cache.FindAll(e =>
            {
                for (var i = 0; i < names.Length; i++)
                {
                    if (!Equals(e[names[i]], values[i])) return false;
                }
                return true;
            });
            if (IsNullKey) return list.Count > 0;

            if (list == null || list.Count <= 0) return false;
            if (list.Count > 1) return true;

            // 如果是Guid等主键，可能提前赋值，插入操作不能比较主键，直接判断判断存在的唯一索引即可
            if (isNew && !field.IsIdentity) return true;

            return !Equals(val, list[0][field.Name]);
        }
    }
    #endregion

    #region 查找单个实体
    /// <summary>根据属性以及对应的值，查找单个实体</summary>
    /// <param name="name">属性名称</param>
    /// <param name="value">属性值</param>
    /// <returns></returns>
    public static TEntity? Find(String name, Object value) => Find([name], [value]);

    /// <summary>根据属性以及对应的值，查找单个实体</summary>
    /// <param name="name">属性名称</param>
    /// <param name="value">属性值</param>
    /// <param name="selects">查询列，默认null表示所有字段</param>
    /// <returns></returns>
    public static TEntity? Find(String name, Object value, String selects) => Find([name], [value], selects);

    /// <summary>根据属性列表以及对应的值列表，查找单个实体</summary>
    /// <param name="names">属性名称集合</param>
    /// <param name="values">属性值集合</param>
    /// <param name="selects">查询列，默认null表示所有字段</param>
    /// <returns></returns>
    public static TEntity? Find(String[] names, Object[] values, String? selects = null)
    {
        if (names == null || names.Length == 0) throw new ArgumentNullException(nameof(names));
        if (values == null || values.Length == 0) throw new ArgumentNullException(nameof(values));

        var exp = new WhereExpression();
        // 判断自增和主键
        if (names.Length == 1)
        {
            var field = Meta.Table.FindByName(names[0]);
            if ((field as FieldItem) != null && (field.IsIdentity || field.PrimaryKey))
            {
                // 唯一键为自增且参数小于等于0时，返回空
                if (Helper.IsNullKey(values[0], field.Type)) return null;

                exp &= field == values[0];
                return FindUnique(exp, selects);
            }
        }

        for (var i = 0; i < names.Length; i++)
        {
            var fi = Meta.Table.FindByName(names[i]);
            if (ReferenceEquals(fi, null)) throw new ArgumentOutOfRangeException(nameof(names), $"{names[i]} not found");

            exp &= fi == values[i];
        }

        // 判断唯一索引，唯一索引也不需要分页
        var di = Meta.Table.DataTable.GetIndex(names);
        if (di != null && di.Unique) return FindUnique(exp, selects);

        return Find(exp, selects);
    }

    /// <summary>根据条件查找唯一的单个实体</summary>
    /// 根据条件查找唯一的单个实体，因为是唯一的，所以不需要分页和排序。
    /// 如果不确定是否唯一，一定不要调用该方法，否则会返回大量的数据。
    /// <remarks>
    /// </remarks>
    /// <param name="where">查询条件</param>
    /// <param name="selects">查询列，默认null表示所有字段</param>
    /// <returns></returns>
    private static TEntity? FindUnique(Expression where, String? selects = null)
    {
        var session = Meta.Session;
        var db = session.Dal.Db;
        var ps = db.UseParameter ? new Dictionary<String, Object>() : null;
        var wh = where?.GetString(db, ps);

        var builder = new SelectBuilder
        {
            Table = session.FormatedTableName,
            // 谨记：某些项目中可能在where中使用了GroupBy，在分页时可能报错
            Where = wh,
            Column = selects
        };

        // 使用默认选择列
        if (builder.Column.IsNullOrEmpty()) builder.Column = Meta.Factory.Selects;

        // 提取参数
        builder = FixParam(builder, ps);

        var list = LoadData(session.Query(builder, 0, 0));
        //var list = session.Query(builder, 0, 0, LoadData);
        if (list == null || list.Count <= 0) return null;

        // 如果正在使用单对象缓存，则批量进入
        LoadSingleCache(list);

        if (list.Count > 1 && DAL.Debug)
        {
            DAL.WriteLog("调用FindUnique(\"{0}\")不合理，只有返回唯一记录的查询条件才允许调用！", wh);
        }
        return list[0];
    }

    /// <summary>根据条件查找单个实体</summary>
    /// <param name="whereClause">查询条件</param>
    /// <returns></returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static TEntity? Find(String whereClause)
    {
        var list = FindAll(whereClause, null, null, 0, 1);
        return list.Count <= 0 ? null : list[0];
    }

    /// <summary>根据条件查找单个实体</summary>
    /// <param name="where">查询条件</param>
    /// <returns></returns>
    public static TEntity? Find(Expression where) => Find(where, null);

    /// <summary>根据条件查找单个实体</summary>
    /// <param name="where">查询条件</param>
    /// <param name="selects">查询列，默认null表示所有字段</param>
    /// <returns></returns>
    public static TEntity? Find(Expression where, String? selects)
    {
        var max = 1;

        // 优待主键查询
        if (where is FieldExpression fe && fe.Field != null && fe.Field.PrimaryKey) max = 0;

        var list = FindAll(where, null, selects.IsNullOrWhiteSpace() ? null : selects, 0, max);
        return list.Count <= 0 ? null : list[0];
    }

    /// <summary>根据主键查找单个实体</summary>
    /// <param name="key">唯一主键的值</param>
    /// <param name="selects">查询列，默认null表示所有字段</param>
    /// <returns></returns>
    public static TEntity? FindByKey(Object key, String? selects)
    {
        var field = Meta.Unique ?? throw new ArgumentNullException(nameof(Meta.Unique), "FindByKey方法要求" + typeof(TEntity).FullName + "有唯一主键！");

        // 查询时可能传入IModel，为了分表查询，主要解决分表字段不是主键的场景
        var model = key as IModel;
        if (model != null) key = model[field.Name]!;

        // 唯一键为自增且参数小于等于0时，返回空
        if (Helper.IsNullKey(key, field.Type)) return null;

        if (Meta.InShard) return Find(field.Name, key);

        // 自动分库分表
        if (model == null)
        {
            model = new TEntity();
            model[field.Name] = key;
        }
        using var split = Meta.CreateShard(model);

        // 此外，一律返回 查找值，即使可能是空。而绝不能在找不到数据的情况下给它返回空，因为可能是找不到数据而已，而返回新实例会导致前端以为这里是新增数据
        return Find(field.Name, key);
    }

    /// <summary>根据主键查找单个实体</summary>
    /// <param name="key">唯一主键的值</param>
    /// <returns></returns>
    public static TEntity? FindByKey(Object key) => FindByKey(key, null);

    /// <summary>根据主键查询一个实体对象用于表单编辑</summary>
    /// <param name="key">唯一主键的值</param>
    /// <returns></returns>
    public static TEntity? FindByKeyForEdit(Object key)
    {
        var field = Meta.Unique ?? throw new ArgumentNullException("Meta.Unique", "FindByKeyForEdit方法要求该表有唯一主键！");

        // 查询时可能传入IModel，为了分表查询，主要解决分表字段不是主键的场景
        var model = key as IModel;
        if (model != null) key = model[field.Name]!;

        // 参数为空时，返回新实例
        if (key == null) return Meta.Factory.Create(true) as TEntity;

        var type = field.Type;

        // 唯一键为自增且参数小于等于0时，返回新实例
        if (Helper.IsNullKey(key, type))
        {
            if (type.IsInt() && !field.IsIdentity && DAL.Debug) DAL.WriteLog("{0}的{1}字段是整型主键，你是否忘记了设置自增？", Meta.TableName, field.ColumnName);

            return Meta.Factory.Create(true) as TEntity;
        }

        TEntity? entity = null;
        if (Meta.InShard)
        {
            entity = Find(field.Name, key);
        }
        else
        {
            // 自动分库分表
            if (model == null)
            {
                model = new TEntity();
                model[field.Name] = key;
            }
            using var split = Meta.CreateShard(model);

            // 此外，一律返回 查找值，即使可能是空。而绝不能在找不到数据的情况下给它返回空，因为可能是找不到数据而已，而返回新实例会导致前端以为这里是新增数据
            entity = Find(field.Name, key);
        }

        // 判断实体
        if (entity == null)
        {
            var des = Meta.Table.Description;
            if (!des.IsNullOrEmpty())
            {
                var p = des.IndexOfAny(['。', '，']);
                if (p > 0) des = des[..p];
            }

            String msg;
            if (Helper.IsNullKey(key, field.Type))
                msg = $"参数错误！无法取得编号为{key}的{des}！可能未设置自增主键！";
            else
                msg = $"参数错误！无法取得编号为{key}的{des}！";

            throw new XCodeException(msg);
        }

        return entity;
    }

    /// <summary>查询指定字段的最小值</summary>
    /// <param name="field">指定字段</param>
    /// <param name="where">条件字句</param>
    /// <returns></returns>
    public static Int32 FindMin(String field, Expression? where = null)
    {
        var fd = Meta.Table.FindByName(field);
        if (ReferenceEquals(fd, null)) throw new ArgumentOutOfRangeException(nameof(field), $"{field} not found");

        var list = FindAll(where, fd, null, 0, 1);
        return list.Count <= 0 ? 0 : Convert.ToInt32(list[0][fd.Name]);
    }

    /// <summary>查询指定字段的最大值</summary>
    /// <param name="field">指定字段</param>
    /// <param name="where">条件字句</param>
    /// <returns></returns>
    public static Int32 FindMax(String field, Expression? where = null)
    {
        var fd = Meta.Table.FindByName(field);
        if (ReferenceEquals(fd, null)) throw new ArgumentOutOfRangeException(nameof(field), $"{field} not found");

        var list = FindAll(where, fd.Desc(), null, 0, 1);
        return list.Count <= 0 ? 0 : Convert.ToInt32(list[0][fd.Name]);
    }
    #endregion

    #region 静态查询
    /// <summary>获取所有数据。获取大量数据时会非常慢，慎用。没有数据时返回空集合而不是null</summary>
    /// <returns>实体数组</returns>
    public static IList<TEntity> FindAll() => FindAll("", null, null, 0, 0);

    /// <summary>最标准的查询数据。没有数据时返回空集合而不是null</summary>
    /// <remarks>
    /// 最经典的批量查询，看这个Select @selects From Table Where @where Order By @order Limit @startRowIndex,@maximumRows，你就明白各参数的意思了。
    /// </remarks>
    /// <param name="where">条件字句，不带Where</param>
    /// <param name="order">排序字句，不带Order By</param>
    /// <param name="selects">查询列，默认null表示所有字段</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns>实体集</returns>
    public static IList<TEntity> FindAll(String? where, String? order, String? selects, Int64 startRowIndex, Int64 maximumRows)
    {
        var session = Meta.Session;
        var needOrderByID = startRowIndex > 0 || maximumRows > 0;

        var builder = CreateBuilder(where, order, selects, needOrderByID);
        var list = LoadData(session.Query(builder, startRowIndex, maximumRows));

        // 如果正在使用单对象缓存，则批量进入
        if (selects.IsNullOrEmpty() || selects == "*") LoadSingleCache(list);

        return list;
    }

    /// <summary>最标准的查询数据。没有数据时返回空集合而不是null</summary>
    /// <remarks>
    /// 最经典的批量查询，看这个Select @selects From Table Where @where Order By @order Limit @startRowIndex,@maximumRows，你就明白各参数的意思了。
    /// </remarks>
    /// <param name="where">条件字句，不带Where</param>
    /// <param name="order">排序字句，不带Order By</param>
    /// <param name="selects">查询列，默认null表示所有字段</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns>实体集</returns>
    public static IList<TEntity> FindAll(Expression? where, String? order, String? selects, Int64 startRowIndex, Int64 maximumRows)
    {
        var session = Meta.Session;

        #region 海量数据查询优化
        // 海量数据尾页查询优化
        // 在海量数据分页中，取越是后面页的数据越慢，可以考虑倒序的方式
        // 只有在百万数据，且开始行大于五十万时才使用

        // 如下优化，避免了每次都调用Meta.Count而导致形成一次查询，虽然这次查询时间损耗不大
        // 但是绝大多数查询，都不需要进行类似的海量数据优化，显然，这个startRowIndex将会挡住99%以上的浪费
        Int64 count;
        if (startRowIndex > 500_000 && (count = session.LongCount) > 1_000_000)
        {
            //// 计算本次查询的结果行数
            //var wh = where?.GetString(null);
            // 数据量巨大，每次都查总记录数很不划算，还不如用一个不太准的数据
            //if (!wh.IsNullOrEmpty()) count = FindCount(where, order, selects, startRowIndex, maximumRows);
            // 游标在中间偏后
            if (startRowIndex * 2 > count)
            {
                var order2 = order;
                var bk = false; // 是否跳过

                #region 排序倒序
                // 默认是自增字段的降序
                var fi = Meta.Unique;
                if (order2.IsNullOrEmpty() && fi != null && fi.IsIdentity) order2 = fi.Name + " Desc";

                if (!order2.IsNullOrEmpty())
                {
                    //2014-01-05 Modify by Apex
                    //处理order by带有函数的情况，避免分隔时将函数拆分导致错误
                    foreach (Match match in Regex.Matches(order2, @"\([^\)]*\)", RegexOptions.Singleline))
                    {
                        order2 = order2.Replace(match.Value, match.Value.Replace(",", "★"));
                    }
                    var ss = order2.Split(',');
                    var sb = Pool.StringBuilder.Get();
                    foreach (var item in ss)
                    {
                        var fn = item;
                        var od = "asc";

                        var p = fn.LastIndexOf(" ");
                        if (p > 0)
                        {
                            od = item[p..].Trim().ToLower();
                            fn = item[..p].Trim();
                        }

                        switch (od)
                        {
                            case "asc":
                                od = "desc";
                                break;
                            case "desc":
                                //od = "asc";
                                od = null;
                                break;
                            default:
                                bk = true;
                                break;
                        }
                        if (bk) break;

                        if (sb.Length > 0) sb.Append(", ");
                        sb.AppendFormat("{0} {1}", fn, od);
                    }

                    order2 = sb.Put(true).Replace("★", ",");
                }
                #endregion

                // 没有排序的实在不适合这种办法，因为没办法倒序
                if (!order2.IsNullOrEmpty())
                {
                    // 最大可用行数改为实际最大可用行数
                    var max = (Int32)Math.Min(maximumRows, count - startRowIndex);
                    if (max <= 0) return new List<TEntity>();

                    var start = (Int32)(count - (startRowIndex + maximumRows));
                    var builder2 = CreateBuilder(where, order2, selects);
                    var list = LoadData(session.Query(builder2, start, max));
                    if (list.Count <= 0) return list;

                    // 如果正在使用单对象缓存，则批量进入
                    if (selects.IsNullOrEmpty() || selects == "*") LoadSingleCache(list);

                    // 因为这样取得的数据是倒过来的，所以这里需要再倒一次
                    //list.Reverse();
                    return list.Reverse().ToList();
                }
            }
        }
        #endregion

        // 自动分表
        var shards = Meta.InShard || where == null ? null : Meta.ShardPolicy?.Shards(where);
        if (shards == null || shards.Length == 0)
        {
            var builder = CreateBuilder(where, order, selects);
            var list2 = LoadData(session.Query(builder, startRowIndex, maximumRows));

            // 如果正在使用单对象缓存，则批量进入
            if (selects.IsNullOrEmpty() || selects == "*") LoadSingleCache(list2);

            return list2;
        }
        else
        {
            var ss = Meta.Session;
            using var span = DAL.GlobalTracer?.NewSpan($"db:{ss.ConnName}:{ss.TableName}:AutoShard", "自动分页查询", shards.Length);

            // 先生成查询语句
            var builder = CreateBuilder(where, order, selects);
            if (order.IsNullOrEmpty()) order = builder.OrderBy;

            // 根据分页字段排序分页表
            shards = FixOrder(shards, order);
            span?.AppendTag($"分库分表：{shards.Join(", ", e => $"[{e.ConnName}]{e.TableName}")}");

            // 分表查询，需要跳过前面的表
            var row = startRowIndex;
            var max = maximumRows;

            var rs = new List<TEntity>();
            for (var i = 0; i < shards.Length; i++)
            {
                var connName = shards[i].ConnName ?? session.ConnName;
                var tableName = shards[i].TableName ?? session.TableName;

                // 如果目标分表不存在，则不要展开查询
                var dal = DAL.Create(connName);
                if (!dal.TableNames.Contains(tableName)) continue;

                // 分表查询
                session = EntitySession<TEntity>.Create(connName, tableName);
                span?.AppendTag($"使用 [{connName}]{tableName} 查询({row}, {max})");

                builder.Table = session.FormatedTableName;
                var list2 = LoadData(session.Query(builder, row, max));
                if (list2.Count > 0) rs.AddRange(list2);

                // 总数已满足要求，返回
                if (maximumRows > 0 && rs.Count >= maximumRows) return rs;
                span?.AppendTag($"当前表[{rs.Count}]无法凑齐数据[{maximumRows}]，需要使用下一分表");

                // 避免最后一张表没有查询到相关数据还继续进行查询，减少不必要查询
                var skipCount = 0;
                if (row > 0 && i < shards.Length - 1)
                {
                    skipCount = session.QueryCount(builder);
                    span?.AppendTag($"当前表满足条件总行数[{skipCount}]，将在下一分表中跳过");
                }

                max -= list2.Count;
                // 后边表索引记录数应该是减去前张表查询出来的记录总数，有可能负数
                row -= skipCount;
                if (row < 0) row = 0;
            }

#if DEBUG
            if (span?.Tag != null) XTrace.WriteLine(span.Tag);
#endif

            return rs;
        }
    }

    /// <summary>根据排序字段调整查询多表（分表）时的顺序</summary>
    /// <param name="shards"></param>
    /// <param name="order"></param>
    /// <returns></returns>
    static ShardModel[] FixOrder(ShardModel[] shards, String? order)
    {
        // 根据分页字段排序分页表
        var sfield = Meta.ShardPolicy?.Field;
        var ds = order?.Split(",");
        if (ds != null && sfield != null)
        {
            foreach (var item in ds)
            {
                var ss = item.Split(' ');
                if (ss[0].EqualIgnoreCase(sfield.Name, sfield.ColumnName))
                {
                    // 默认升序，第二个元素是desc才是降序
                    if (ss.Length >= 2 && ss[1].EqualIgnoreCase("desc"))
                        shards = shards.OrderByDescending(e => e.ConnName).ThenByDescending(e => e.TableName).ToArray();
                    else
                        shards = shards.OrderBy(e => e.ConnName).ThenBy(e => e.TableName).ToArray();
                }
            }
        }

        return shards;
    }

    /// <summary>同时查询满足条件的记录集和记录总数。没有数据时返回空集合而不是null</summary>
    /// <param name="where">条件，不带Where</param>
    /// <param name="page">分页排序参数，同时返回满足条件的总记录数</param>
    /// <param name="selects">查询列，默认null表示所有字段</param>
    /// <returns></returns>
    public static IList<TEntity> FindAll(Expression? where, PageParameter? page = null, String? selects = null)
    {
        if (page == null) return FindAll(where, null, selects, 0, 0);

        where ??= new WhereExpression();

        // 页面参数携带进来的扩展查询
        if (page.State is Expression exp)
            where &= exp;
        else if (page.State is WhereBuilder builder)
        {
            builder.Factory ??= Meta.Factory;
            where &= builder.GetExpression();
        }

        // 先查询满足条件的记录数，如果没有数据，则直接返回空集合，不再查询数据
        var session = Meta.Session;
        if (page.RetrieveTotalCount)
        {
            Int64 rows;

            // 如果总记录数超过10万，为了提高性能，返回快速查找且带有缓存的总记录数
            if (where.IsEmpty && session.LongCount > 100_000)
                rows = session.LongCount;
            else
                rows = FindCount(where, null, selects, 0, 0);
            if (rows <= 0) return new List<TEntity>();

            page.TotalCount = rows;
        }

        // 验证排序字段，避免非法注入
        var orderby = SqlBuilder.BuildOrder(page, Meta.Factory);

        // 采用起始行还是分页
        IList<TEntity> list;
        if (page.StartRow >= 0)
            list = FindAll(where, orderby, selects, page.StartRow, page.PageSize);
        else
            list = FindAll(where, orderby, selects, (page.PageIndex - 1) * page.PageSize, page.PageSize);

        if (list.Count == 0) return list;

        // 统计数据。100万以上数据要求带where才支持统计
        if (page.RetrieveState &&
            (page.RetrieveTotalCount && page.TotalCount < 10_000_000
            || Meta.Session.LongCount < 10_000_000 || where != null)
            )
        {
            var selectStat = Meta.Factory.SelectStat;
            if (!selectStat.IsNullOrEmpty()) page.State = FindAll(where, null, selectStat).FirstOrDefault();
        }

        return list;
    }

    /// <summary>执行SQl获取数据集</summary>
    /// <param name="sql">SQL语句</param>
    /// <returns>实体集</returns>
    public static IList<TEntity> FindAll(String sql)
    {
        var session = Meta.Session;

        return LoadData(session.Query(sql));
    }

    /// <summary>查询数据，返回内存表DbTable而不是实体列表</summary>
    /// <remarks>
    /// 最经典的批量查询，看这个Select @selects From Table Where @where Order By @order Limit @startRowIndex,@maximumRows，你就明白各参数的意思了。
    /// </remarks>
    /// <param name="where">条件字句，不带Where</param>
    /// <param name="order">排序字句，不带Order By</param>
    /// <param name="selects">查询列，默认null表示所有字段</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns>内存表</returns>
    public static DbTable FindData(Expression where, String order, String selects, Int64 startRowIndex, Int64 maximumRows)
    {
        var session = Meta.Session;

        var builder = CreateBuilder(where, order, selects);
        return session.Query(builder, startRowIndex, maximumRows);
    }
    #endregion

    #region 缓存查询
    /// <summary>查找所有缓存。没有数据时返回空集合而不是null</summary>
    /// <returns></returns>
    public static IList<TEntity> FindAllWithCache() => Meta.Session.Cache.Entities;
    #endregion

    #region 异步查询
    /// <summary>根据条件查找单个实体</summary>
    /// <param name="where">查询条件</param>
    /// <returns></returns>
    public static async Task<TEntity?> FindAsync(Expression where)
    {
        var max = 1;

        // 优待主键查询
        if (where is FieldExpression fe && fe.Field != null && fe.Field.PrimaryKey) max = 0;

        var list = await FindAllAsync(where, null, null, 0, max);
        return list.Count <= 0 ? null : list[0];
    }

    /// <summary>获取所有数据。获取大量数据时会非常慢，慎用。没有数据时返回空集合而不是null</summary>
    /// <returns>实体数组</returns>
    public static Task<IList<TEntity>> FindAllAsync() => FindAllAsync(new WhereExpression(), null, null, 0, 0);

    /// <summary>最标准的查询数据。没有数据时返回空集合而不是null</summary>
    /// <remarks>
    /// 最经典的批量查询，看这个Select @selects From Table Where @where Order By @order Limit @startRowIndex,@maximumRows，你就明白各参数的意思了。
    /// </remarks>
    /// <param name="where">条件字句，不带Where</param>
    /// <param name="order">排序字句，不带Order By</param>
    /// <param name="selects">查询列，默认null表示所有字段</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns>实体集</returns>
    public static async Task<IList<TEntity>> FindAllAsync(Expression where, String? order, String? selects, Int64 startRowIndex, Int64 maximumRows)
    {
        var session = Meta.Session;

        #region 海量数据查询优化
        // 海量数据尾页查询优化
        // 在海量数据分页中，取越是后面页的数据越慢，可以考虑倒序的方式
        // 只有在百万数据，且开始行大于五十万时才使用

        // 如下优化，避免了每次都调用Meta.Count而导致形成一次查询，虽然这次查询时间损耗不大
        // 但是绝大多数查询，都不需要进行类似的海量数据优化，显然，这个startRowIndex将会挡住99%以上的浪费
        Int64 count;
        if (startRowIndex > 500_000 && (count = session.LongCount) > 1_000_000)
        {
            //// 计算本次查询的结果行数
            //var wh = where?.GetString(null);
            // 数据量巨大，每次都查总记录数很不划算，还不如用一个不太准的数据
            //if (!wh.IsNullOrEmpty()) count = FindCount(where, order, selects, startRowIndex, maximumRows);
            // 游标在中间偏后
            if (startRowIndex * 2 > count)
            {
                var order2 = order;
                var bk = false; // 是否跳过

                #region 排序倒序
                // 默认是自增字段的降序
                var fi = Meta.Unique;
                if (order2.IsNullOrEmpty() && fi != null && fi.IsIdentity) order2 = fi.Name + " Desc";

                if (!order2.IsNullOrEmpty())
                {
                    //2014-01-05 Modify by Apex
                    //处理order by带有函数的情况，避免分隔时将函数拆分导致错误
                    foreach (Match match in Regex.Matches(order2, @"\([^\)]*\)", RegexOptions.Singleline))
                    {
                        order2 = order2.Replace(match.Value, match.Value.Replace(",", "★"));
                    }
                    var ss = order2.Split(',');
                    var sb = Pool.StringBuilder.Get();
                    foreach (var item in ss)
                    {
                        var fn = item;
                        var od = "asc";

                        var p = fn.LastIndexOf(" ");
                        if (p > 0)
                        {
                            od = item[p..].Trim().ToLower();
                            fn = item[..p].Trim();
                        }

                        switch (od)
                        {
                            case "asc":
                                od = "desc";
                                break;
                            case "desc":
                                //od = "asc";
                                od = null;
                                break;
                            default:
                                bk = true;
                                break;
                        }
                        if (bk) break;

                        if (sb.Length > 0) sb.Append(", ");
                        sb.AppendFormat("{0} {1}", fn, od);
                    }

                    order2 = sb.Put(true).Replace("★", ",");
                }
                #endregion

                // 没有排序的实在不适合这种办法，因为没办法倒序
                if (!order2.IsNullOrEmpty())
                {
                    // 最大可用行数改为实际最大可用行数
                    var max = (Int32)Math.Min(maximumRows, count - startRowIndex);
                    if (max <= 0) return new List<TEntity>();

                    var start = (Int32)(count - (startRowIndex + maximumRows));
                    var builder2 = CreateBuilder(where, order2, selects);
                    var list = LoadData(await session.QueryAsync(builder2, start, max));
                    if (list.Count <= 0) return list;

                    // 如果正在使用单对象缓存，则批量进入
                    if (selects.IsNullOrEmpty() || selects == "*") LoadSingleCache(list);

                    // 因为这样取得的数据是倒过来的，所以这里需要再倒一次
                    //list.Reverse();
                    return list.Reverse().ToList();
                }
            }
        }
        #endregion

        // 自动分表
        var shards = Meta.InShard || where == null ? null : Meta.ShardPolicy?.Shards(where);
        if (shards == null || shards.Length == 0)
        {
            var builder = CreateBuilder(where, order, selects);
            var list2 = LoadData(await session.QueryAsync(builder, startRowIndex, maximumRows));

            // 如果正在使用单对象缓存，则批量进入
            if (selects.IsNullOrEmpty() || selects == "*") LoadSingleCache(list2);

            return list2;
        }
        else
        {
            // 根据分页字段排序分页表
            shards = FixOrder(shards, order);

            // 分表查询，需要跳过前面的表
            var row = startRowIndex;
            var max = maximumRows;

            var rs = new List<TEntity>();
            for (var i = 0; i < shards.Length; i++)
            {
                var connName = shards[i].ConnName ?? session.ConnName;
                var tableName = shards[i].TableName ?? session.TableName;

                // 如果目标分表不存在，则不要展开查询
                var dal = DAL.Create(connName);
                if (!dal.TableNames.Contains(tableName)) continue;

                // 分表查询
                session = EntitySession<TEntity>.Create(connName, tableName);

                var builder = CreateBuilder(where, order, selects);
                builder.Table = session.FormatedTableName;
                var list2 = LoadData(await session.QueryAsync(builder, row, max));
                if (list2.Count > 0) rs.AddRange(list2);

                // 总数已满足要求，返回
                if (maximumRows > 0 && rs.Count >= maximumRows) return rs;

                // 避免最后一张表没有查询到相关数据还继续进行查询，减少不必要查询
                var skipCount = 0;
                if (i < shards.Length) skipCount = (Int32)await session.QueryCountAsync(builder);

                max -= list2.Count;
                // 后边表索引记录数应该是减去前张表查询出来的记录总数，有可能负数
                row -= skipCount;
                if (row < 0) row = 0;
            }

            return rs;
        }
    }

    /// <summary>同时查询满足条件的记录集和记录总数。没有数据时返回空集合而不是null</summary>
    /// <param name="where">条件，不带Where</param>
    /// <param name="page">分页排序参数，同时返回满足条件的总记录数</param>
    /// <param name="selects">查询列，默认null表示所有字段</param>
    /// <returns></returns>
    public static async Task<IList<TEntity>> FindAllAsync(Expression where, PageParameter? page = null, String? selects = null)
    {
        if (page == null) return await FindAllAsync(where, null, selects, 0, 0);

        // 页面参数携带进来的扩展查询
        if (page.State is Expression exp)
            where &= exp;
        else if (page.State is WhereBuilder builder)
        {
            builder.Factory ??= Meta.Factory;
            where &= builder.GetExpression();
        }

        // 先查询满足条件的记录数，如果没有数据，则直接返回空集合，不再查询数据
        var session = Meta.Session;
        if (page.RetrieveTotalCount)
        {
            Int64 rows;

            // 如果总记录数超过10万，为了提高性能，返回快速查找且带有缓存的总记录数
            if (where.IsEmpty && session.LongCount > 100_000)
                rows = session.LongCount;
            else
                rows = await FindCountAsync(where, null, selects, 0, 0);
            if (rows <= 0) return new List<TEntity>();

            page.TotalCount = rows;
        }

        // 验证排序字段，避免非法注入
        var orderby = SqlBuilder.BuildOrder(page, Meta.Factory);

        // 采用起始行还是分页
        IList<TEntity> list;
        if (page.StartRow >= 0)
            list = await FindAllAsync(where, orderby, selects, page.StartRow, page.PageSize);
        else
            list = await FindAllAsync(where, orderby, selects, (page.PageIndex - 1) * page.PageSize, page.PageSize);

        if (list.Count == 0) return list;

        // 统计数据。100万以上数据要求带where才支持统计
        if (page.RetrieveState &&
            (page.RetrieveTotalCount && page.TotalCount < 10_000_000
            || Meta.Session.LongCount < 10_000_000 || where != null)
            )
        {
            var selectStat = Meta.Factory.SelectStat;
            if (!selectStat.IsNullOrEmpty()) page.State = (await FindAllAsync(where, null, selectStat)).FirstOrDefault();
        }

        return list;
    }

    /// <summary>返回总记录数</summary>
    /// <returns></returns>
    public static Task<Int64> FindCountAsync() => FindCountAsync(new WhereExpression(), null, null, 0, 0);

    /// <summary>返回总记录数</summary>
    /// <param name="where">条件，不带Where</param>
    /// <param name="order">排序，不带Order By。这里无意义，仅仅为了保持与FindAll相同的方法签名</param>
    /// <param name="selects">查询列。这里无意义，仅仅为了保持与FindAll相同的方法签名</param>
    /// <param name="startRowIndex">开始行，0表示第一行。这里无意义，仅仅为了保持与FindAll相同的方法签名</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行。这里无意义，仅仅为了保持与FindAll相同的方法签名</param>
    /// <returns>总行数</returns>
    public static async Task<Int64> FindCountAsync(Expression where, String? order = null, String? selects = null, Int64 startRowIndex = 0, Int64 maximumRows = 0)
    {
        var session = Meta.Session;
        var db = session.Dal.Db;
        var ps = db.UseParameter ? new Dictionary<String, Object>() : null;
        var wh = where?.GetString(db, ps);

        //// 如果总记录数超过10万，为了提高性能，返回快速查找且带有缓存的总记录数
        //if (String.IsNullOrEmpty(wh) && session.LongCount > 100000) return session.LongCount;

        var builder = new SelectBuilder
        {
            Table = session.FormatedTableName,
            Where = wh
        };

        // 提取参数
        builder = FixParam(builder, ps);

        // 分组查分组数的时候，必须带上全部selects字段
        if (!builder.GroupBy.IsNullOrEmpty()) builder.Column = selects;

        // 自动分表
        var shards = Meta.InShard || where == null ? null : Meta.ShardPolicy?.Shards(where);
        if (shards == null || shards.Length == 0) return await session.QueryCountAsync(builder);

        var rs = 0L;
        foreach (var shard in shards)
        {
            var connName = shard.ConnName ?? session.ConnName;
            var tableName = shard.TableName ?? session.TableName;

            // 如果目标分表不存在，则不要展开查询
            var dal = DAL.Create(connName);
            if (!dal.TableNames.Contains(tableName)) continue;

            session = EntitySession<TEntity>.Create(connName, tableName);
            builder.Table = session.FormatedTableName;
            rs += await session.QueryCountAsync(builder);
        }
        return rs;
    }
    #endregion

    #region 取总记录数
    /// <summary>返回总记录数</summary>
    /// <returns></returns>
    public static Int64 FindCount() => FindCount("", null, null, 0, 0);

    /// <summary>返回总记录数</summary>
    /// <param name="where">条件，不带Where</param>
    /// <param name="order">排序，不带Order By。这里无意义，仅仅为了保持与FindAll相同的方法签名</param>
    /// <param name="selects">查询列。这里无意义，仅仅为了保持与FindAll相同的方法签名</param>
    /// <param name="startRowIndex">开始行，0表示第一行。这里无意义，仅仅为了保持与FindAll相同的方法签名</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行。这里无意义，仅仅为了保持与FindAll相同的方法签名</param>
    /// <returns>总行数</returns>
    public static Int32 FindCount(String? where, String? order = null, String? selects = null, Int64 startRowIndex = 0, Int64 maximumRows = 0)
    {
        var session = Meta.Session;

        //// 如果总记录数超过10万，为了提高性能，返回快速查找且带有缓存的总记录数
        //if (String.IsNullOrEmpty(where) && session.LongCount > 100000) return session.Count;

        var sb = new SelectBuilder
        {
            Table = session.FormatedTableName,
            Where = where
        };

        // 分组查分组数的时候，必须带上全部selects字段
        if (!sb.GroupBy.IsNullOrEmpty()) sb.Column = selects;

        return session.QueryCount(sb);
    }

    /// <summary>返回总记录数</summary>
    /// <param name="where">条件，不带Where</param>
    /// <param name="order">排序，不带Order By。这里无意义，仅仅为了保持与FindAll相同的方法签名</param>
    /// <param name="selects">查询列。这里无意义，仅仅为了保持与FindAll相同的方法签名</param>
    /// <param name="startRowIndex">开始行，0表示第一行。这里无意义，仅仅为了保持与FindAll相同的方法签名</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行。这里无意义，仅仅为了保持与FindAll相同的方法签名</param>
    /// <returns>总行数</returns>
    public static Int64 FindCount(Expression where, String? order = null, String? selects = null, Int64 startRowIndex = 0, Int64 maximumRows = 0)
    {
        var session = Meta.Session;
        var db = session.Dal.Db;
        var ps = db.UseParameter ? new Dictionary<String, Object>() : null;
        var wh = where?.GetString(db, ps);

        //// 如果总记录数超过10万，为了提高性能，返回快速查找且带有缓存的总记录数
        //if (String.IsNullOrEmpty(wh) && session.LongCount > 100000) return session.LongCount;

        var builder = new SelectBuilder
        {
            Table = session.FormatedTableName,
            Where = wh
        };

        // 提取参数
        builder = FixParam(builder, ps);

        // 分组查分组数的时候，必须带上全部selects字段
        if (!builder.GroupBy.IsNullOrEmpty()) builder.Column = selects;

        // 自动分表
        var shards = Meta.InShard || where == null ? null : Meta.ShardPolicy?.Shards(where);
        if (shards == null || shards.Length == 0) return session.QueryCount(builder);

        var rs = 0L;
        foreach (var shard in shards)
        {
            var connName = shard.ConnName ?? session.ConnName;
            var tableName = shard.TableName ?? session.TableName;

            // 如果目标分表不存在，则不要展开查询
            var dal = DAL.Create(connName);
            if (!dal.TableNames.Contains(tableName)) continue;

            session = EntitySession<TEntity>.Create(connName, tableName);
            builder.Table = session.FormatedTableName;
            rs += session.QueryCount(builder);
        }
        return rs;
    }
    #endregion

    #region 获取查询SQL
    /// <summary>获取查询SQL。主要用于构造子查询</summary>
    /// <param name="where">条件，不带Where</param>
    /// <param name="order">排序，不带Order By</param>
    /// <param name="selects">查询列</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns>实体集</returns>
    public static SelectBuilder FindSQL(String? where, String? order, String? selects, Int32 startRowIndex = 0, Int32 maximumRows = 0)
    {
        var needOrderByID = startRowIndex > 0 || maximumRows > 0;
        var builder = CreateBuilder(where, order, selects, needOrderByID);
        return Meta.Session.Dal.PageSplit(builder, startRowIndex, maximumRows);
    }

    /// <summary>获取查询唯一键的SQL。比如Select ID From Table</summary>
    /// <param name="where"></param>
    /// <returns></returns>
    public static SelectBuilder FindSQLWithKey(String? where = null)
    {
        var uk = Meta.Unique?.Field;
        if (uk == null) throw new XCodeException("实体类缺少唯一主键字段，无法生成SQL语句！");

        var columnName = Meta.Session.Dal.Db.FormatName(uk);
        return FindSQL(where, null, columnName, 0, 0);
    }
    #endregion

    #region 高级查询
    /// <summary>同时查询满足条件的记录集和记录总数。没有数据时返回空集合而不是null</summary>
    /// <param name="key"></param>
    /// <param name="page">分页排序参数，同时返回满足条件的总记录数</param>
    /// <returns></returns>
    //[Obsolete("=>Search(DateTime start, DateTime end, String key, PageParameter page)")]
    public static IList<TEntity> Search(String key, PageParameter page) => FindAll(SearchWhereByKeys(key), page);

    /// <summary>同时查询满足条件的指定查询列的记录集和记录总数。没有数据时返回空集合而不是null</summary>
    /// <param name="key"></param>
    /// <param name="page">分页排序参数，同时返回满足条件的总记录数</param>
    /// <param name="selects">查询列</param>
    /// <returns></returns>
    //[Obsolete("=>Search(DateTime start, DateTime end, String key, PageParameter page)")]
    public static IList<TEntity> Search(String key, PageParameter page, String selects) => FindAll(SearchWhereByKeys(key), page, selects);

    /// <summary>同时查询满足条件的记录集和记录总数。没有数据时返回空集合而不是null</summary>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页排序参数，同时返回满足条件的总记录数</param>
    /// <returns></returns>
    public static IList<TEntity> Search(DateTime start, DateTime end, String key, PageParameter page) => FindAll(SearchWhere(start, end, key), page);

    /// <summary>构造高级查询条件</summary>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <param name="key">关键字</param>
    /// <returns></returns>
    protected static WhereExpression SearchWhere(DateTime start, DateTime end, String key)
    {
        var exp = SearchWhereByKeys(key);

        if (start > DateTime.MinValue || end > DateTime.MinValue)
        {
            var fi = Meta.Factory.MasterTime;
            if (fi != null)
            {
                if (fi.Type == typeof(Int64) && !fi.IsIdentity)
                    exp &= fi.Between(start, end, Meta.Factory.Snow);
                else if (start == end)
                    exp &= fi.Equal(start);
                else
                    exp &= fi.Between(start, end);
            }
        }

        return exp;
    }

    /// <summary>根据空格分割的关键字集合构建查询条件</summary>
    /// <param name="keys">空格分割的关键字集合</param>
    /// <param name="fields">要查询的字段，默认为空表示查询所有字符串字段</param>
    /// <param name="func">处理每一个查询关键字的回调函数</param>
    /// <returns></returns>
    public static WhereExpression SearchWhereByKeys(String keys, FieldItem[]? fields = null, Func<String, FieldItem[]?, WhereExpression>? func = null)
    {
        var exp = new WhereExpression();
        if (String.IsNullOrEmpty(keys)) return exp;

        func ??= SearchWhereByKey;

        var ks = keys.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < ks.Length; i++)
        {
            if (!ks[i].IsNullOrWhiteSpace()) exp &= func(ks[i].Trim(), fields);
        }

        return exp;
    }

    /// <summary>构建关键字查询条件</summary>
    /// <param name="key">关键字</param>
    /// <param name="fields">要查询的字段，默认为空表示查询所有字符串字段</param>
    /// <returns></returns>
    public static WhereExpression SearchWhereByKey(String key, FieldItem[]? fields = null)
    {
        var exp = new WhereExpression();
        if (key.IsNullOrEmpty()) return exp;

        if (fields == null || fields.Length == 0) fields = Meta.Fields;
        foreach (var item in fields)
        {
            if (item.Type != typeof(String)) continue;

            exp |= item.Contains(key);
        }

        return exp;
    }
    #endregion

    #region 静态操作
    /// <summary>把一个实体对象持久化到数据库</summary>
    /// <param name="names">更新属性列表</param>
    /// <param name="values">更新值列表</param>
    /// <returns>返回受影响的行数</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static Int32 Insert(String[] names, Object[] values) => Persistence.Insert(Meta.Session, names, values);

    /// <summary>更新一批实体数据</summary>
    /// <param name="setClause">要更新的项和数据</param>
    /// <param name="whereClause">指定要更新的实体</param>
    /// <returns></returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static Int32 Update(String setClause, String whereClause) => Persistence.Update(Meta.Session, setClause, whereClause);

    /// <summary>更新一批实体数据</summary>
    /// <param name="setNames">更新属性列表</param>
    /// <param name="setValues">更新值列表</param>
    /// <param name="whereNames">条件属性列表</param>
    /// <param name="whereValues">条件值列表</param>
    /// <returns>返回受影响的行数</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static Int32 Update(String[] setNames, Object[] setValues, String[] whereNames, Object[] whereValues) => Persistence.Update(Meta.Session, setNames, setValues, whereNames, whereValues);

    /// <summary>从数据库中删除指定条件的实体对象。</summary>
    /// <param name="whereClause">限制条件</param>
    /// <returns></returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static Int32 Delete(String whereClause) => Persistence.Delete(Meta.Session, whereClause);

    /// <summary>从数据库中删除指定属性列表和值列表所限定的实体对象。</summary>
    /// <param name="names">属性列表</param>
    /// <param name="values">值列表</param>
    /// <returns></returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static Int32 Delete(String[] names, Object[] values) => Persistence.Delete(Meta.Session, names, values);
    #endregion

    #region 构造SQL语句
    /// <summary>构造SQL查询语句</summary>
    /// <param name="where">条件</param>
    /// <param name="order">排序</param>
    /// <param name="selects">选择列</param>
    /// <returns></returns>
    public static SelectBuilder CreateBuilder(Expression? where, String? order, String? selects)
    {
        var session = Meta.Session;
        var db = session.Dal.Db;
        var ps = db.UseParameter ? new Dictionary<String, Object>() : null;
        var wh = where?.GetString(db, ps);
        var builder = CreateBuilder(wh, order, selects, true);

        builder = FixParam(builder, ps);

        return builder;
    }

    private static SelectBuilder CreateBuilder(String? where, String? order, String? selects, Boolean needOrderByID)
    {
        var factory = Meta.Factory;
        var session = Meta.Session;
        var db = session.Dal.Db;

        var builder = new SelectBuilder
        {
            Column = selects,
            Table = session.FormatedTableName,
            OrderBy = order,
            // 谨记：某些项目中可能在where中使用了GroupBy，在分页时可能报错
            Where = where
        };

        // stone [2023-02-25] 经确认，select *不会影响到索引命中
        //// chenqi [2018-5-7] 
        //// 处理Select列
        //// SQL Server数据库特殊处理：由于T-SQL查询列为*号，order by未使用索引字段，将导致索引不会被命中。
        //if (session.Dal.DbType == DatabaseType.SqlServer)
        //{
        //    if (builder.Column.IsNullOrEmpty() || builder.Column.Equals("*"))
        //    {
        //        var fields = factory.Selects;
        //        if (fields.IsNullOrWhiteSpace())
        //            //fields = Meta.Factory.FieldNames.Select(Meta.FormatName).Join(",");
        //            //不能直接通过获取FieldNames的方式拼接查询字段，如果列名和实际的属性名称存在差异的情况下会导致查询错误 By Xiyunfei
        //            fields = factory.Fields.Join(",", e => db.FormatName(e.Field));
        //        builder.Column = fields;
        //    }
        //}
        //else
        {
            if (builder.Column.IsNullOrEmpty())
                builder.Column = factory.Selects;
        }

        // XCode对于默认排序的规则：整型主键降序，其它情况默认
        // 返回所有记录
        if (!needOrderByID || !factory.OrderByKey) return builder;

        var fi = Meta.Unique;
        if (fi != null && fi.Type.IsInt())
        {
            var key = builder.Key = db.FormatName(fi.Field);

            // 默认获取数据时，还是需要指定按照自增字段降序，符合使用习惯
            // 有GroupBy也不能加排序
            if (builder.OrderBy.IsNullOrEmpty() &&
                builder.GroupBy.IsNullOrEmpty() &&
                // 未指定查询字段的时候才默认加上排序，因为指定查询字段的很多时候是统计
                (selects.IsNullOrWhiteSpace() || selects == "*")
                )
            {
                // 数字降序，其它升序
                var b = fi.Type.IsInt();
                //builder.IsDesc = b;
                //builder.IsDescs = new[] { b };
                //// 修正没有设置builder.IsInt导致分页没有选择最佳的MaxMin的BUG，感谢 @RICH(20371423)
                //builder.IsInt = b;

                //builder.OrderBy = builder.KeyOrder;
                builder.OrderBy = b ? (key + " Desc") : key;
            }
        }
        else
        {
            // 如果找不到唯一键，并且排序又为空，则采用全部字段一起，确保MSSQL能够分页
            if (builder.OrderBy.IsNullOrEmpty() && session.Dal.DbType == DatabaseType.SqlServer)
            {
                var pks = Meta.Table.PrimaryKeys;
                if (pks != null && pks.Length > 0)
                {
                    var key = builder.Key = db.FormatName(pks[0].Field);

                    //chenqi [2017-5-7] 非自增列 + order为空时，指定order by 主键
                    builder.OrderBy = key;
                }
            }
        }
        return builder;
    }

    private static SelectBuilder FixParam(SelectBuilder builder, IDictionary<String, Object>? ps)
    {
        // 提取参数
        if (ps != null)
        {
            foreach (var item in ps)
            {
                var dp = Meta.Session.Dal.Db.CreateParameter(item.Key, item.Value, Meta.Table.FindByName(item.Key)?.Field);
                //// 不能传递类型，因为参数名可能已经改变
                //var dp = Meta.Session.Dal.Db.CreateParameter(item.Key, item.Value);

                builder.Parameters.Add(dp);
            }
        }

        return builder;
    }
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值。</summary>
    /// <remarks>
    /// 一个索引，反射实现。
    /// 派生实体类可重写该索引，以避免发射带来的性能损耗。
    /// 基类已经实现了通用的快速访问，但是这里仍然重写，以增加控制，
    /// 比如字段名是属性名前面加上_，并且要求是实体字段才允许这样访问，否则一律按属性处理。
    /// </remarks>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object? this[String name]
    {
        get
        {
            // 扩展属性
            if (Meta.Table.ExtendFieldNames.Contains(name))
            {
                var pi = GetType().GetPropertyEx(name, true);
                if (pi != null && pi.CanRead) return this.GetValue(pi);
            }

            // 检查动态增加的字段，返回默认值
            var f = Meta.Table.FindByName(name) as FieldItem;

            if (_Items != null && _Items.TryGetValue(name, out var obj))
            {
                if (f != null && f.IsDynamic) return obj.ChangeType(f.Type);

                return obj;
            }

            if (f != null && f.IsDynamic) return f.Type.CreateInstance();

            //if (_Extends != null) return Extends[name];

            return null;
        }
        set
        {
            // 扩展属性
            if (Meta.Table.ExtendFieldNames.Contains(name))
            {
                var pi = GetType().GetPropertyEx(name, true);
                if (pi != null && pi.CanWrite)
                {
                    this.SetValue(pi, value);
                    return;
                }
            }

            // 检查动态增加的字段，返回默认值
            if (Meta.Table.FindByName(name) is FieldItem f && f.IsDynamic) value = value.ChangeType(f.Type);

            //Extends[name] = value;
            (this as IExtend).Items[name] = value;
        }
    }
    #endregion

    #region 序列化
    Boolean IAccessor.Read(Stream stream, Object? context) => OnRead(stream, context, false);

    Boolean IAccessor.Write(Stream stream, Object? context) => OnWrite(stream, context, false);

    /// <summary>从数据流反序列化</summary>
    /// <param name="stream">数据流</param>
    /// <param name="context">上下文</param>
    /// <param name="extend">是否序列化扩展属性</param>
    protected virtual Boolean OnRead(Stream stream, Object? context, Boolean extend)
    {
        if (context is not Binary bn) bn = new Binary { Stream = stream, EncodeInt = true, FullTime = true };

        var fs = extend ? Meta.AllFields : Meta.Fields;
        foreach (var fi in fs)
        {
            // 顺序要求很高
            SetItem(fi.Name, bn.Read(fi.Type));
        }

        return true;
    }

    /// <summary>二进制序列化到数据流</summary>
    /// <param name="stream">数据流</param>
    /// <param name="context">上下文</param>
    /// <param name="extend">是否序列化扩展属性</param>
    protected virtual Boolean OnWrite(Stream stream, Object? context, Boolean extend)
    {
        if (context is not Binary bn) bn = new Binary { Stream = stream, EncodeInt = true, FullTime = true };

        var fs = extend ? Meta.AllFields : Meta.Fields;
        foreach (var fi in fs)
        {
            bn.Write(this[fi.Name], fi.Type);
        }

        return true;
    }
    #endregion

    #region 克隆
    /// <summary>创建当前对象的克隆对象，仅拷贝基本字段</summary>
    /// <returns></returns>
    public override Object Clone() => CloneEntity();

    /// <summary>克隆实体。创建当前对象的克隆对象，仅拷贝基本字段</summary>
    /// <param name="setDirty">是否设置脏数据。默认不设置</param>
    /// <returns></returns>
    public virtual TEntity CloneEntity(Boolean setDirty = false)
    {
        var obj = (Meta.Factory.Create() as TEntity)!;
        foreach (var fi in Meta.Fields)
        {
            if (setDirty)
                obj.SetItem(fi.Name, this[fi.Name]);
            else
                obj[fi.Name] = this[fi.Name];
        }

        //Extends.CopyTo(obj.Extends);
        if (_Items != null && _Items.Count > 0)
        {
            foreach (var item in _Items)
            {
                this[item.Key] = item.Value;
            }
        }

        return obj;
    }

    /// <summary>克隆实体</summary>
    /// <param name="setDirty"></param>
    /// <returns></returns>
    protected internal override IEntity CloneEntityInternal(Boolean setDirty = true) => CloneEntity(setDirty);
    #endregion

    #region 其它
    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String ToString()
    {
        // 优先主字段作为实体对象的字符串显示
        if (Meta.Master != null && Meta.Master != Meta.Unique) return this[Meta.Master.Name] + "";

        // 优先采用业务主键，也就是唯一索引
        var table = Meta.Table.DataTable;
        var dis = table.Indexes;
        if (dis != null && dis.Count > 0)
        {
            IDataIndex? di = null;
            foreach (var item in dis)
            {
                if (!item.Unique) continue;
                if (item.Columns == null || item.Columns.Length <= 0) continue;

                var columns = table.GetColumns(item.Columns);
                if (columns == null || columns.Length <= 0) continue;

                di = item;

                // 如果不是唯一自增，再往下找别的。如果后面实在找不到，至少还有现在这个。
                if (!(columns.Length == 1 && columns[0].Identity)) break;
            }

            if (di != null)
            {
                var columns = table.GetColumns(di.Columns);

                // [v1,v2,...vn]
                var sb = Pool.StringBuilder.Get();
                foreach (var dc in columns)
                {
                    if (sb.Length > 0) sb.Append(',');
                    if (Meta.FieldNames.Contains(dc.Name)) sb.Append(this[dc.Name]);
                }

                var vs = sb.Put(true);
                if (columns.Length > 1)
                    return $"[{vs}]";
                else
                    return vs;
            }
        }

        var fs = Meta.FieldNames;
        if (fs.Contains("Name"))
            return this["Name"] + "";
        else if (fs.Contains("Title"))
            return this["Title"] + "";
        else if (fs.Contains("ID"))
            return this["ID"] + "";
        else
            return "实体" + typeof(TEntity).Name;
    }
    #endregion

    #region 脏数据
    /// <summary>从数据库查询数据，对比重置脏数据</summary>
    /// <remarks>
    /// 在MVC中直接用实体对象接收前端数据进行更新操作时，脏数据可能不准确。
    /// 该方法实现脏数据重置，确保可以准确保存到数据库。
    /// </remarks>
    /// <returns></returns>
    public Int32 ResetDirty()
    {
        var key = Meta.Unique ?? throw new InvalidOperationException("要求有唯一主键");
        var k = this[key.Name];
        if (k == null) return 0;

        var entity = FindByKey(k);
        if (entity == null) return 0;

        var rs = 0;
        foreach (var item in Meta.Fields)
        {
            var change = !CheckEqual(this[item.Name], entity[item.Name]);
            if (change)
            {
                Dirtys.Add(item.Name, entity[item.Name]);
                rs++;
            }
        }

        return rs;
    }
    #endregion

    #region 高并发
    /// <summary>获取 或 新增 对象，带缓存查询，常用于统计等高并发新增或更新的场景</summary>
    /// <remarks>常规操作是插入数据前检查是否已存在，但可能存在并行冲突问题，GetOrAdd能很好解决该问题</remarks>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="key">业务主键，如果是多字段混合索引，则建立一个模型类</param>
    /// <param name="find">查找函数</param>
    /// <param name="create">创建对象</param>
    /// <returns></returns>
    public static TEntity GetOrAdd<TKey>(TKey key, Func<TKey, Boolean, TEntity?> find, Func<TKey, TEntity> create)
    {
        //if (key == null) return null;

        var entity = find != null ? find(key, true) : FindByKey(key!);
        if (entity != null) return entity;

        // 加锁，避免同一个key并发新增
        lock (KeyedLocker<TEntity>.SharedLock(key!.ToString()))
        {
            // 打开事务，提升可靠性也避免读写分离造成数据不一致
            using var trans = Meta.CreateTrans();

            entity = find != null ? find(key, false) : FindByKey(key!);
            if (entity != null) return entity;

            if (create != null)
                entity = create(key);
            else
            {
                entity = new TEntity();
                entity.SetItem(Meta.Factory.Unique.Name, key);
            }

            // 插入失败时，再次查询
            try
            {
                entity.Insert();
            }
            catch (Exception ex)
            {
                entity = find != null ? find(key, false) : FindByKey(key!);
                if (entity == null) throw ex.GetTrue();
            }

            trans.Commit();

            return entity;
        }
    }

    /// <summary>获取 或 新增 对象，不带缓存查询，常用于统计等高并发新增或更新的场景</summary>
    /// <remarks>常规操作是插入数据前检查是否已存在，但可能存在并行冲突问题，GetOrAdd能很好解决该问题</remarks>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="key">业务主键，如果是多字段混合索引，则建立一个模型类</param>
    /// <param name="find">查找函数</param>
    /// <param name="create">创建对象</param>
    /// <returns></returns>
    public static TEntity GetOrAdd<TKey>(TKey key, Func<TKey, TEntity?> find, Func<TKey, TEntity> create)
    {
        //if (key == null) return null;
        //if (find == null) throw new ArgumentNullException(nameof(find));

        var entity = find != null ? find(key) : FindByKey(key!);
        if (entity != null) return entity;

        // 加锁，避免同一个key并发新增
        lock (KeyedLocker<TEntity>.SharedLock(key!.ToString()))
        {
            // 打开事务，提升可靠性也避免读写分离造成数据不一致
            using var trans = Meta.CreateTrans();

            entity = find != null ? find(key) : FindByKey(key!);
            if (entity != null) return entity;

            if (create != null)
                entity = create(key);
            else
            {
                entity = new TEntity();
                entity.SetItem(Meta.Factory.Unique.Name, key);
            }

            // 插入失败时，再次查询
            try
            {
                entity.Insert();
            }
            catch (Exception ex)
            {
                entity = find != null ? find(key) : FindByKey(key!);
                if (entity == null) throw ex.GetTrue();
            }

            trans.Commit();

            return entity;
        }
    }

    /// <summary>获取 或 新增 对象，根据业务主键查询，常用于统计等高并发新增或更新的场景</summary>
    /// <remarks>常规操作是插入数据前检查是否已存在，但可能存在并行冲突问题，GetOrAdd能很好解决该问题</remarks>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="field">主键名</param>
    /// <param name="key">业务主键，如果是多字段混合索引，则建立一个模型类</param>
    /// <returns></returns>
    public static TEntity GetOrAdd<TKey>(String field, TKey key)
    {
        if (field.IsNullOrEmpty()) field = Meta.Unique?.Name ?? throw new ArgumentNullException(nameof(field));
        var f = Meta.Table.FindByName(field) ?? throw new ArgumentNullException(nameof(field));

        return GetOrAdd(key, k => Find(f.Equal(k)), k =>
        {
            var v = new TEntity();
            v.SetItem(f.Name, k);
            return v;
        });
    }
    #endregion
}