﻿using System.Data;
using NewLife;
using NewLife.Data;
using XCode.Configuration;
using XCode.Shards;
using XCode.Statistics;

namespace XCode;

public partial class Entity<TEntity>
{
    /// <summary>默认的实体工厂</summary>
    public class DefaultEntityFactory : IEntityFactory
    {
        #region 主要属性
        /// <summary>实体类型</summary>
        public virtual Type EntityType => typeof(TEntity);

        /// <summary>实体会话</summary>
        public virtual IEntitySession Session => Meta.Session;

        /// <summary>实体持久化</summary>
        public IEntityPersistence Persistence { get; set; }

        /// <summary>数据行访问器，把数据行映射到实体类</summary>
        public IDataRowEntityAccessor Accessor { get; set; }
        #endregion

        #region 属性
        private IEntity? _Default;
        /// <summary>默认实体</summary>
        public virtual IEntity Default { get => _Default ??= new TEntity(); set => _Default = value; }

        /// <summary>数据表元数据</summary>
        public virtual TableItem Table => Meta.Table;

        /// <summary>所有数据属性</summary>
        public virtual FieldItem[] AllFields => Meta.AllFields;

        /// <summary>所有绑定到数据表的属性</summary>
        public virtual FieldItem[] Fields => Meta.Fields;

        /// <summary>字段名集合，不区分大小写的哈希表存储，外部不要修改元素数据</summary>
        public virtual ICollection<String> FieldNames => Meta.FieldNames;

        /// <summary>唯一键，返回第一个标识列或者唯一的主键</summary>
        public virtual FieldItem Unique => Meta.Unique;

        /// <summary>主字段。主字段作为业务主要字段，代表当前数据行意义</summary>
        public virtual FieldItem Master => Meta.Master;

        /// <summary>连接名。当前线程正在使用的连接名</summary>
        public virtual String ConnName { get => Meta.ConnName!; set => Meta.ConnName = value; }

        /// <summary>表名。当前线程正在使用的表名</summary>
        public virtual String TableName { get => Meta.TableName!; set => Meta.TableName = value; }
        #endregion

        #region 构造
        /// <summary>构造实体工厂</summary>
        public DefaultEntityFactory()
        {
            //MasterTime = GetMasterTime();
            Persistence = new EntityPersistence(this);
            Accessor = new DataRowEntityAccessor();
        }
        #endregion

        #region 创建实体、填充数据
        /// <summary>创建一个实体对象</summary>
        /// <param name="forEdit">是否为了编辑而创建，如果是，可以再次做一些相关的初始化工作</param>
        /// <returns></returns>
        public virtual IEntity Create(Boolean forEdit = false) => (Default as TEntity)!.CreateInstance(forEdit);

        /// <summary>加载记录集</summary>
        /// <param name="ds">记录集</param>
        /// <returns>实体数组</returns>
        public virtual IList<IEntity> LoadData(DataSet ds) => Entity<TEntity>.LoadData(ds).Cast<IEntity>().ToList();
        #endregion

        #region 查找单个实体
        /// <summary>根据属性以及对应的值，查找单个实体</summary>
        /// <param name="name">名称</param>
        /// <param name="value">数值</param>
        /// <returns></returns>
        public virtual IEntity? Find(String name, Object value) => Entity<TEntity>.Find(name, value);

        /// <summary>根据条件查找单个实体</summary>
        /// <param name="where"></param>
        /// <returns></returns>
        public virtual IEntity? Find(Expression where) => Entity<TEntity>.Find(where);

        /// <summary>根据主键查找单个实体</summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public virtual IEntity? FindByKey(Object key) => Entity<TEntity>.FindByKey(key);

        /// <summary>根据主键查询一个实体对象用于表单编辑</summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public virtual IEntity? FindByKeyForEdit(Object key) => Entity<TEntity>.FindByKeyForEdit(key);
        #endregion

        #region 静态查询
        /// <summary>获取所有实体对象。获取大量数据时会非常慢，慎用</summary>
        /// <returns>实体数组</returns>
        public virtual IList<IEntity> FindAll() => Entity<TEntity>.FindAll().Cast<IEntity>().ToList();

        /// <summary>查询并返回实体对象集合。
        /// 表名以及所有字段名，请使用类名以及字段对应的属性名，方法内转换为表名和列名
        /// </summary>
        /// <param name="where">条件，不带Where</param>
        /// <param name="order">排序，不带Order By</param>
        /// <param name="selects">查询列</param>
        /// <param name="startRowIndex">开始行，0表示第一行</param>
        /// <param name="maximumRows">最大返回行数，0表示所有行</param>
        /// <returns>实体数组</returns>
        public virtual IList<IEntity> FindAll(String? where, String? order, String? selects, Int64 startRowIndex, Int64 maximumRows) => Entity<TEntity>.FindAll(where, order, selects, startRowIndex, maximumRows).Cast<IEntity>().ToList();

        /// <summary>查询并返回实体对象集合。
        /// 表名以及所有字段名，请使用类名以及字段对应的属性名，方法内转换为表名和列名
        /// </summary>
        /// <param name="where">条件，不带Where</param>
        /// <param name="order">排序，不带Order By</param>
        /// <param name="selects">查询列</param>
        /// <param name="startRowIndex">开始行，0表示第一行</param>
        /// <param name="maximumRows">最大返回行数，0表示所有行</param>
        /// <returns>实体数组</returns>
        public virtual IList<IEntity> FindAll(Expression where, String? order, String? selects, Int64 startRowIndex, Int64 maximumRows) => Entity<TEntity>.FindAll(where, order, selects, startRowIndex, maximumRows).Cast<IEntity>().ToList();
        #endregion

        #region 缓存查询
        /// <summary>查找实体缓存所有数据</summary>
        /// <returns></returns>
        public virtual IList<IEntity> FindAllWithCache() => Entity<TEntity>.FindAllWithCache().Cast<IEntity>().ToList();
        #endregion

        #region 取总记录数
        /// <summary>返回总记录数</summary>
        /// <returns></returns>
        public virtual Int64 FindCount() => Entity<TEntity>.FindCount();

        /// <summary>返回总记录数</summary>
        /// <param name="where">条件，不带Where</param>
        /// <param name="order">排序，不带Order By</param>
        /// <param name="selects">查询列</param>
        /// <param name="startRowIndex">开始行，0表示第一行</param>
        /// <param name="maximumRows">最大返回行数，0表示所有行</param>
        /// <returns>总行数</returns>
        public virtual Int32 FindCount(String where, String order, String selects, Int64 startRowIndex, Int64 maximumRows) => Entity<TEntity>.FindCount(where, order, selects, startRowIndex, maximumRows);

        /// <summary>返回总记录数</summary>
        /// <param name="where">条件，不带Where</param>
        /// <returns>总行数</returns>
        public virtual Int64 FindCount(Expression where) => Entity<TEntity>.FindCount(where);
        #endregion

        #region 分表分库
        /// <summary>获取指定连接和表的实体会话。可用于分表逻辑</summary>
        /// <param name="connName">连接名</param>
        /// <param name="tableName">表名</param>
        /// <returns></returns>
        public virtual IEntitySession GetSession(String connName, String tableName) => EntitySession<TEntity>.Create(connName, tableName);

        /// <summary>分表分库策略</summary>
        public virtual IShardPolicy? ShardPolicy { get => Meta.ShardPolicy; set => Meta.ShardPolicy = value; }

        /// <summary>创建分库会话，using结束时自动还原</summary>
        /// <param name="connName">连接名</param>
        /// <param name="tableName">表名</param>
        /// <returns></returns>
        public virtual IDisposable CreateSplit(String connName, String tableName) => Meta.CreateSplit(connName, tableName);

        ///// <summary>针对实体对象自动分库分表</summary>
        ///// <param name="entity"></param>
        ///// <returns></returns>
        //public virtual IDisposable? CreateShard(IEntity entity) => Meta.CreateShard((entity as TEntity)!);

        /// <summary>为实体对象、时间、雪花Id等计算分表分库</summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public virtual IDisposable? CreateShard(Object value) => Meta.CreateShard(value);

        /// <summary>针对时间区间自动分库分表，常用于多表顺序查询，支持倒序</summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public virtual IEnumerable<T> AutoShard<T>(DateTime start, DateTime end, Func<T> callback) => Meta.AutoShard(start, end, callback);
        #endregion

        #region 高并发
        /// <summary>获取 或 新增 对象，带缓存查询，常用于统计等高并发新增或更新的场景</summary>
        /// <remarks>常规操作是插入数据前检查是否已存在，但可能存在并行冲突问题，GetOrAdd能很好解决该问题</remarks>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="key">业务主键</param>
        /// <param name="find">查找函数</param>
        /// <param name="create">创建对象</param>
        /// <returns></returns>
        public virtual IEntity GetOrAdd<TKey>(TKey key, Func<TKey, Boolean, IEntity?> find, Func<TKey, IEntity> create) => Entity<TEntity>.GetOrAdd(key, (k, b) => find?.Invoke(k, b) as TEntity, k => (create?.Invoke(k) as TEntity)!);
        #endregion

        #region 一些设置
        /// <summary>是否自增获取自增返回值。默认启用</summary>
        public Boolean AutoIdentity { get; set; } = true;

#if NET45
        private readonly ThreadLocal<Boolean> _AllowInsertIdentity = new();
#else
        private readonly AsyncLocal<Boolean> _AllowInsertIdentity = new();
#endif
        /// <summary>是否允许向自增列插入数据。为免冲突，仅本线程有效</summary>
        public virtual Boolean AllowInsertIdentity { get => _AllowInsertIdentity.Value; set => _AllowInsertIdentity.Value = value; }

        /// <summary>自动设置Guid的字段。对实体类有效，可在实体类类型构造函数里面设置</summary>
        public virtual FieldItem? AutoSetGuidField { get; set; }

        /// <summary>默认累加字段</summary>
        public virtual ICollection<String> AdditionalFields { get; } = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

        private Boolean _MasterTime_;
        private FieldItem? _MasterTime;
        /// <summary>主时间字段。代表当前数据行更新时间</summary>
        public FieldItem? MasterTime
        {
            get
            {
                if (_MasterTime == null && !_MasterTime_)
                {
                    _MasterTime = GetMasterTime();
                    _MasterTime_ = true;
                }

                return _MasterTime;
            }
            set { _MasterTime = value; _MasterTime_ = false; }
        }

        private static FieldItem? GetMasterTime()
        {
            // 优先数据规模字段
            var fi = Meta.Fields.FirstOrDefault(e => e.Column != null && !e.Column.DataScale.IsNullOrEmpty());
            if (fi != null) return fi;

            var fis = Meta.Fields.Where(e => e.Type == typeof(DateTime)).ToArray();
            if (fis.Length == 0) return null;

            var dt = Meta.Table.DataTable;

            // 时间作为主键
            fi = fis.FirstOrDefault(e => e.PrimaryKey);
            if (fi != null) return fi;

            // 第一个时间日期索引字段
            foreach (var di in dt.Indexes.OrderBy(e => e.Columns.Length).OrderByDescending(e => e.Unique).ThenByDescending(e => e.PrimaryKey))
            {
                if (di.Columns == null || di.Columns.Length == 0) continue;

                fi = fis.FirstOrDefault(e => di.Columns[0].EqualIgnoreCase(e.Name, e.ColumnName));
                if (fi != null) return fi;
            }

            fi = fis.FirstOrDefault(e => e.Name.StartsWithIgnoreCase("UpdateTime", "Modify", "Modified"));

            return fi;
        }

        /// <summary>默认选择的字段</summary>
        public String? Selects { get; set; }

        private String? _SelectStat;
        /// <summary>默认选择统计语句</summary>
        public String? SelectStat
        {
            get
            {
                if (_SelectStat == null)
                {
                    // 找到所有数字字段，进行求和统计
                    var concat = new ConcatExpression();
                    foreach (var item in StatFields)
                    {
                        concat &= item.Mode switch
                        {
                            StatModes.Max => item.Field.Max(),
                            StatModes.Min => item.Field.Min(),
                            StatModes.Avg => item.Field.Avg(),
                            StatModes.Sum => item.Field.Sum(),
                            StatModes.Count => item.Field.Count(),
                            _ => item.Field.Max(),
                        };
                    }

                    // 至少有个空字符串，避免重入
                    _SelectStat = concat + "";
                }

                return _SelectStat;
            }
            set => _SelectStat = value;
        }

        private IList<StatField> _StatFields;
        /// <summary>统计字段集合</summary>
        public IList<StatField> StatFields
        {
            get
            {
                if (_StatFields == null)
                {
                    var list = new List<StatField>();

                    //// 先来个行数
                    //if (!Fields.Any(e => e.Name.EqualIgnoreCase("Count"))) concat &= "Count(*) as Count";
                    foreach (var item in Fields)
                    {
                        // 自增和主键不参与
                        if (item.IsIdentity || item.PrimaryKey) continue;

                        // 只要Int32和Int64，一般Int16太小不适合聚合
                        if (item.Type != typeof(Int32) &&
                            item.Type != typeof(Int64) &&
                            item.Type != typeof(Single) &&
                            item.Type != typeof(Double) &&
                            item.Type != typeof(Decimal)) continue;

                        // 特殊处理 AbcID 形式的外键关联，不参与
                        var name = item.Name;
                        if (name.EndsWith("ID") || name.EndsWith("Id"))
                        {
                            // 倒数第三个字符为小写
                            if (name.Length >= 3 && !Char.IsUpper(name[^3])) continue;
                        }

                        // 第二名称，去掉后面的数字，便于模式匹配
                        var name2 = item.Name;
                        while (name2.Length > 1 && Char.IsDigit(name2[^1])) name2 = name2[0..^1];

                        if (name.StartsWith("Max") && name.Length > 3 && Char.IsUpper(name[3]))
                            list.Add(new(item, StatModes.Max));
                        else if (name.StartsWith("Min") && name.Length > 3 && Char.IsUpper(name[3]))
                            list.Add(new(item, StatModes.Min));
                        else if (name.StartsWith("Avg") && name.Length > 3 && Char.IsUpper(name[3]))
                            list.Add(new(item, StatModes.Avg));
                        else if (name2.EndsWith("Rate") || name2.EndsWith("Ratio"))
                            list.Add(new(item, StatModes.Max));
                        else
                            list.Add(new(item, StatModes.Sum));
                    }

                    _StatFields = list;
                }

                return _StatFields;
            }
            set => _StatFields = value;
        }

        /// <summary>实体模块集合</summary>
        public EntityModules Modules => Meta.Modules;

        /// <summary>是否完全插入所有字段。默认false表示不插入没有脏数据的字段</summary>
        public Boolean FullInsert { get; set; }

        /// <summary>雪花Id生成器。Int64主键非自增时，自动填充</summary>
        public Snowflake Snow { get; } = new Snowflake();

        private SqlTemplate? _Template;
        /// <summary>Sql模版</summary>
        public SqlTemplate Template
        {
            get
            {
                if (_Template == null)
                {
                    var st = new SqlTemplate();
                    if (TableName.StartsWith("#"))
                    {
                        var type = EntityType;
                        st.ParseEmbedded(type.Assembly, type.Namespace, TableName.TrimStart('#') + ".sql");
                    }

                    _Template = st;
                }

                return _Template;
            }
        }

        /// <summary>按照主键排序。默认查询没有指定排序字段时，是否增加主键排序，整型降序其它升序，默认false</summary>
        public Boolean OrderByKey { get; set; }

        ///// <summary>截断超长字符串。默认false</summary>
        //public Boolean TrimExtraLongString { get; set; }
        #endregion
    }
}