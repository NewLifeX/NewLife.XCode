using System.Data;
using NewLife.Data;
using XCode.Configuration;
using XCode.Shards;
using XCode.Statistics;

namespace XCode;

/// <summary>指定实体工厂</summary>
/// <remarks>指定实体工厂</remarks>
/// <param name="type"></param>
[AttributeUsage(AttributeTargets.Class)]
public class EntityFactoryAttribute(Type type) : Attribute
{
    /// <summary>实体工厂类型</summary>
    public Type Type { get; set; } = type;
}

/// <summary>数据实体操作接口</summary>
public interface IEntityFactory
{
    #region 主要属性
    /// <summary>实体类型</summary>
    Type EntityType { get; }

    /// <summary>实体会话</summary>
    IEntitySession Session { get; }

    /// <summary>实体持久化</summary>
    IEntityPersistence Persistence { get; set; }

    /// <summary>数据行访问器，把数据行映射到实体类</summary>
    IDataRowEntityAccessor Accessor { get; set; }
    #endregion

    #region 属性
    /// <summary>默认实体。用于初始化数据等扩展操作</summary>
    IEntity Default { get; set; }

    /// <summary>数据表元数据</summary>
    TableItem Table { get; }

    /// <summary>所有数据属性</summary>
    FieldItem[] AllFields { get; }

    /// <summary>所有绑定到数据表的属性</summary>
    FieldItem[] Fields { get; }

    /// <summary>字段名集合，不区分大小写的哈希表存储，外部不要修改元素数据</summary>
    ICollection<String> FieldNames { get; }

    /// <summary>唯一键，返回第一个标识列或者唯一的主键</summary>
    FieldItem Unique { get; }

    /// <summary>主字段。主字段作为业务主要字段，代表当前数据行意义</summary>
    FieldItem Master { get; }

    /// <summary>连接名。当前线程正在使用的连接名</summary>
    String ConnName { get; set; }

    /// <summary>表名。当前线程正在使用的表名</summary>
    String TableName { get; set; }
    #endregion

    #region 创建实体、填充数据
    /// <summary>创建一个实体对象</summary>
    /// <param name="forEdit">是否为了编辑而创建，如果是，可以再次做一些相关的初始化工作</param>
    /// <returns></returns>
    IEntity Create(Boolean forEdit = false);

    /// <summary>加载记录集</summary>
    /// <param name="ds">记录集</param>
    /// <returns>实体数组</returns>
    IList<IEntity> LoadData(DataSet ds);
    #endregion

    #region 查找单个实体
    /// <summary>根据属性以及对应的值，查找单个实体</summary>
    /// <param name="name">名称</param>
    /// <param name="value">数值</param>
    /// <returns></returns>
    IEntity? Find(String name, Object value);

    /// <summary>根据条件查找单个实体</summary>
    /// <param name="where"></param>
    /// <returns></returns>
    IEntity? Find(Expression where);

    /// <summary>根据主键查找单个实体</summary>
    /// <param name="key"></param>
    /// <returns></returns>
    IEntity? FindByKey(Object key);

    /// <summary>根据主键查询一个实体对象用于表单编辑</summary>
    /// <param name="key"></param>
    /// <returns></returns>
    IEntity? FindByKeyForEdit(Object key);
    #endregion

    #region 静态查询
    /// <summary>获取所有实体对象。获取大量数据时会非常慢，慎用</summary>
    /// <returns>实体数组</returns>
    IList<IEntity> FindAll();

    /// <summary>
    /// 查询并返回实体对象集合。
    /// 表名以及所有字段名，请使用类名以及字段对应的属性名，方法内转换为表名和列名
    /// </summary>
    /// <param name="where">条件，不带Where</param>
    /// <param name="order">排序，不带Order By</param>
    /// <param name="selects">查询列</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns>实体数组</returns>
    IList<IEntity> FindAll(String? where, String? order, String? selects, Int64 startRowIndex, Int64 maximumRows);

    /// <summary>
    /// 查询并返回实体对象集合。
    /// 表名以及所有字段名，请使用类名以及字段对应的属性名，方法内转换为表名和列名
    /// </summary>
    /// <param name="where">条件，不带Where</param>
    /// <param name="order">排序，不带Order By</param>
    /// <param name="selects">查询列</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns>实体数组</returns>
    IList<IEntity> FindAll(Expression where, String? order, String? selects, Int64 startRowIndex, Int64 maximumRows);
    #endregion

    #region 缓存查询
    /// <summary>查找实体缓存所有数据</summary>
    /// <returns></returns>
    IList<IEntity> FindAllWithCache();
    #endregion

    #region 取总记录数
    /// <summary>返回总记录数</summary>
    /// <returns></returns>
    Int64 FindCount();

    /// <summary>返回总记录数</summary>
    /// <param name="where">条件，不带Where</param>
    /// <param name="order">排序，不带Order By</param>
    /// <param name="selects">查询列</param>
    /// <param name="startRowIndex">开始行，0表示第一行</param>
    /// <param name="maximumRows">最大返回行数，0表示所有行</param>
    /// <returns>总行数</returns>
    Int32 FindCount(String where, String order, String selects, Int64 startRowIndex, Int64 maximumRows);

    /// <summary>返回总记录数</summary>
    /// <param name="where">条件，不带Where</param>
    /// <returns>总行数</returns>
    Int64 FindCount(Expression where);
    #endregion

    #region 分表分库
    /// <summary>获取指定连接和表的实体会话。可用于分表逻辑</summary>
    /// <param name="connName">连接名</param>
    /// <param name="tableName">表名</param>
    /// <returns></returns>
    IEntitySession GetSession(String connName, String tableName);

    /// <summary>分表分库策略</summary>
    IShardPolicy? ShardPolicy { get; set; }

    /// <summary>创建分库会话，using结束时自动还原</summary>
    /// <param name="connName">连接名</param>
    /// <param name="tableName">表名</param>
    /// <returns></returns>
    IDisposable CreateSplit(String connName, String tableName);

    ///// <summary>针对实体对象自动分库分表</summary>
    ///// <param name="entity"></param>
    ///// <returns></returns>
    //IDisposable? CreateShard(IEntity entity);

    /// <summary>为实体对象、时间、雪花Id等计算分表分库</summary>
    /// <param name="value"></param>
    /// <returns></returns>
    IDisposable? CreateShard(Object value);

    /// <summary>针对时间区间自动分库分表，常用于多表顺序查询，支持倒序</summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="callback"></param>
    /// <returns></returns>
    IEnumerable<T> AutoShard<T>(DateTime start, DateTime end, Func<T> callback);
    #endregion

    #region 应用应用
    /// <summary>获取 或 新增 对象，常用于统计等高并发更新的情况，一般配合SaveAsync</summary>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="key">业务主键</param>
    /// <param name="find">查找函数</param>
    /// <param name="create">创建对象</param>
    /// <returns></returns>
    IEntity GetOrAdd<TKey>(TKey key, Func<TKey, Boolean, IEntity?> find, Func<TKey, IEntity> create);

    /// <summary>合并数据。查出表中已有数据匹配，能匹配的更新，无法匹配的批量插入</summary>
    /// <remarks>一般用于数据导入等要求比较高的场合</remarks>
    /// <param name="source">数据源。实体列表或模型对象列表</param>
    /// <param name="fields">需要合并的字段，默认null合并所有字段</param>
    /// <returns></returns>
    Int32 Merge(IEnumerable<IModel> source, FieldItem[]? fields = null);
    #endregion

    #region 一些设置
    /// <summary>是否自增获取自增返回值。默认启用</summary>
    Boolean AutoIdentity { get; set; }

    /// <summary>是否允许向自增列插入数据。为免冲突，仅本线程有效</summary>
    Boolean AllowInsertIdentity { get; set; }

    /// <summary>自动设置Guid的字段。对实体类有效，可在实体类类型构造函数里面设置</summary>
    FieldItem? AutoSetGuidField { get; set; }

    /// <summary>默认累加字段</summary>
    ICollection<String> AdditionalFields { get; }

    /// <summary>主时间字段。代表当前数据行更新时间</summary>
    FieldItem? MasterTime { get; set; }

    /// <summary>默认选择的字段</summary>
    String? Selects { get; set; }

    /// <summary>默认选择统计语句</summary>
    String? SelectStat { get; set; }

    /// <summary>统计字段集合</summary>
    IList<StatField> StatFields { get; set; }

    /// <summary>实体模块集合</summary>
    EntityModules Modules { get; }

    /// <summary>是否完全插入所有字段。默认false表示不插入没有脏数据的字段</summary>
    Boolean FullInsert { get; set; }

    /// <summary>雪花Id生成器。Int64主键非自增时，自动填充</summary>
    Snowflake Snow { get; }

    /// <summary>Sql模版</summary>
    SqlTemplate Template { get; }

    /// <summary>按照主键排序。默认查询没有指定排序字段时，是否增加主键排序，整型降序其它升序，默认true</summary>
    Boolean OrderByKey { get; set; }
    #endregion
}