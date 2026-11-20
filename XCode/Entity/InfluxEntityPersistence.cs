using System.ComponentModel;
using XCode.DataAccessLayer;

namespace XCode;

/// <summary>InfluxDB 原生持久化。把实体写入 Line Protocol</summary>
/// <remarks>
/// InfluxDB 2.x 只支持追加写入，更新与删除需要显式指定时间范围，因此这里明确不支持。
/// 单条插入复用会话的批量写入实现，保证与 <see cref="EntityExtension.BatchInsert{T}(IEnumerable{T}, IDataColumn[], IEntitySession?)"/> 一致。
/// </remarks>
public class InfluxEntityPersistence(IEntityFactory factory) : EntityPersistence(factory)
{
    #region 属性
    /// <summary>要写入的列</summary>
    /// <returns></returns>
    protected IDataColumn[] GetColumns() => Factory.Fields.Where(e => e.Field != null).Select(e => e.Field!).ToArray();
    #endregion

    #region 添删改
    /// <summary>插入。写入一条 Line Protocol</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <returns>写入行数</returns>
    public override Int32 Insert(IEntitySession session, IEntity entity) => session.Dal.Session.Insert(session.DataTable, GetColumns(), [entity]);

    /// <summary>插入。写入一条 Line Protocol</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <returns>写入行数</returns>
    public override Task<Int32> InsertAsync(IEntitySession session, IEntity entity) => Task.FromResult(Insert(session, entity));

    /// <summary>更新。InfluxDB 2.x 不支持</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <returns></returns>
    public override Int32 Update(IEntitySession session, IEntity entity) => throw CreateNotSupported(nameof(Update));

    /// <summary>更新。InfluxDB 2.x 不支持</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <returns></returns>
    public override Task<Int32> UpdateAsync(IEntitySession session, IEntity entity) => throw CreateNotSupported(nameof(Update));

    /// <summary>删除。InfluxDB 2.x 不支持</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <returns></returns>
    public override Int32 Delete(IEntitySession session, IEntity entity) => throw CreateNotSupported(nameof(Delete));

    /// <summary>删除。InfluxDB 2.x 不支持</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <returns></returns>
    public override Task<Int32> DeleteAsync(IEntitySession session, IEntity entity) => throw CreateNotSupported(nameof(Delete));

    /// <summary>插入。InfluxDB 需要完整的列信息，请使用实体插入</summary>
    /// <param name="session">实体会话</param>
    /// <param name="names">属性列表</param>
    /// <param name="values">值列表</param>
    /// <returns></returns>
    public override Int32 Insert(IEntitySession session, String[] names, Object[] values) => throw CreateNotSupported(nameof(Insert));

    /// <summary>批量更新。InfluxDB 2.x 不支持</summary>
    /// <param name="session">实体会话</param>
    /// <param name="setClause">更新项</param>
    /// <param name="whereClause">条件</param>
    /// <returns></returns>
    public override Int32 Update(IEntitySession session, String setClause, String whereClause) => throw CreateNotSupported(nameof(Update));

    /// <summary>批量更新。InfluxDB 2.x 不支持</summary>
    /// <param name="session">实体会话</param>
    /// <param name="setNames">更新属性</param>
    /// <param name="setValues">更新值</param>
    /// <param name="whereNames">条件属性</param>
    /// <param name="whereValues">条件值</param>
    /// <returns></returns>
    public override Int32 Update(IEntitySession session, String[] setNames, Object[] setValues, String[] whereNames, Object[] whereValues) => throw CreateNotSupported(nameof(Update));

    /// <summary>批量删除。InfluxDB 2.x 不支持</summary>
    /// <param name="session">实体会话</param>
    /// <param name="whereClause">条件</param>
    /// <param name="maximumRows">最大行数</param>
    /// <returns></returns>
    public override Int32 Delete(IEntitySession session, String whereClause, Int32 maximumRows = 0) => throw CreateNotSupported(nameof(Delete));

    /// <summary>批量删除。InfluxDB 2.x 不支持</summary>
    /// <param name="session">实体会话</param>
    /// <param name="names">属性列表</param>
    /// <param name="values">值列表</param>
    /// <returns></returns>
    public override Int32 Delete(IEntitySession session, String[] names, Object[] values) => throw CreateNotSupported(nameof(Delete));
    #endregion

    #region 语句
    /// <summary>获取SQL语句。InfluxDB 不使用 SQL</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <param name="methodType">方法类型</param>
    /// <returns>固定返回空</returns>
    public override String? GetSql(IEntitySession session, IEntity entity, DataObjectMethodType methodType) => null;

    /// <summary>插入语句。InfluxDB 不使用 SQL</summary>
    /// <param name="session">实体会话</param>
    /// <returns>固定返回空</returns>
    public override String? InsertSQL(IEntitySession session) => null;
    #endregion

    private static NotSupportedException CreateNotSupported(String action) => new(L.T("InfluxDB only supports append writes; {0} is not supported. Use DAL raw commands for history corrections.", action));
}

/// <summary>InfluxDB 持久化工厂</summary>
public class InfluxEntityPersistenceFactory : IEntityPersistenceFactory
{
    /// <summary>创建 InfluxDB 原生持久化</summary>
    /// <param name="factory">实体工厂</param>
    /// <param name="db">当前数据库</param>
    /// <returns>实体持久化</returns>
    public IEntityPersistence Create(IEntityFactory factory, IDatabase? db) => new InfluxEntityPersistence(factory);
}
