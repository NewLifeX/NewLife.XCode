using System.ComponentModel;
using XCode.DataAccessLayer;

namespace XCode;

/// <summary>OpenTSDB 原生持久化。把实体写入 /api/put 的 JSON 数据点</summary>
/// <remarks>
/// OpenTSDB 只支持追加写，更新与删除无法定位具体数据，因此明确不支持。
/// 数值字段映射为 value；主键、Master 与字符串字段映射为 tags；
/// 单数值列使用表名作为 metric，多数值列拆成“表名.字段名”子指标。
/// </remarks>
public class OpenTSDBEntityPersistence(IEntityFactory factory) : EntityPersistence(factory)
{
    #region 属性
    /// <summary>要写入的列</summary>
    /// <returns></returns>
    protected IDataColumn[] GetColumns() => Factory.Fields.Where(e => e.Field != null).Select(e => e.Field!).ToArray();
    #endregion

    #region 添删改
    /// <summary>插入。写入一个或多个数据点</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <returns>写入行数</returns>
    public override Int32 Insert(IEntitySession session, IEntity entity) => session.Dal.Session.Insert(session.DataTable, GetColumns(), [entity]);

    /// <summary>插入。写入一个或多个数据点</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <returns>写入行数</returns>
    public override Task<Int32> InsertAsync(IEntitySession session, IEntity entity) => Task.FromResult(Insert(session, entity));

    /// <summary>更新。OpenTSDB 不支持</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <returns></returns>
    public override Int32 Update(IEntitySession session, IEntity entity) => ThrowNotSupported(session, DatabaseCapability.Update, "entity update");

    /// <summary>更新。OpenTSDB 不支持</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <returns></returns>
    public override Task<Int32> UpdateAsync(IEntitySession session, IEntity entity) => throw new NotSupportedException(L.T("This operation is not supported!"));

    /// <summary>删除。OpenTSDB 不支持</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <returns></returns>
    public override Int32 Delete(IEntitySession session, IEntity entity) => ThrowNotSupported(session, DatabaseCapability.Delete, "entity delete");

    /// <summary>删除。OpenTSDB 不支持</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <returns></returns>
    public override Task<Int32> DeleteAsync(IEntitySession session, IEntity entity) => throw new NotSupportedException(L.T("This operation is not supported!"));

    /// <summary>插入。OpenTSDB 需要完整列信息，请使用实体插入</summary>
    /// <param name="session">实体会话</param>
    /// <param name="names">属性列表</param>
    /// <param name="values">值列表</param>
    /// <returns></returns>
    public override Int32 Insert(IEntitySession session, String[] names, Object[] values) => ThrowNotSupported(session, DatabaseCapability.Insert, "entity insert");

    /// <summary>批量更新。OpenTSDB 不支持</summary>
    /// <param name="session">实体会话</param>
    /// <param name="setClause">更新项</param>
    /// <param name="whereClause">条件</param>
    /// <returns></returns>
    public override Int32 Update(IEntitySession session, String setClause, String whereClause) => ThrowNotSupported(session, DatabaseCapability.Update, "entity update");

    /// <summary>批量更新。OpenTSDB 不支持</summary>
    /// <param name="session">实体会话</param>
    /// <param name="setNames">更新属性</param>
    /// <param name="setValues">更新值</param>
    /// <param name="whereNames">条件属性</param>
    /// <param name="whereValues">条件值</param>
    /// <returns></returns>
    public override Int32 Update(IEntitySession session, String[] setNames, Object[] setValues, String[] whereNames, Object[] whereValues) => ThrowNotSupported(session, DatabaseCapability.Update, "entity update");

    /// <summary>批量删除。OpenTSDB 不支持</summary>
    /// <param name="session">实体会话</param>
    /// <param name="whereClause">条件</param>
    /// <param name="maximumRows">最大行数</param>
    /// <returns></returns>
    public override Int32 Delete(IEntitySession session, String whereClause, Int32 maximumRows = 0) => ThrowNotSupported(session, DatabaseCapability.Delete, "entity delete");

    /// <summary>批量删除。OpenTSDB 不支持</summary>
    /// <param name="session">实体会话</param>
    /// <param name="names">属性列表</param>
    /// <param name="values">值列表</param>
    /// <returns></returns>
    public override Int32 Delete(IEntitySession session, String[] names, Object[] values) => ThrowNotSupported(session, DatabaseCapability.Delete, "entity delete");
    #endregion

    #region 语句
    /// <summary>获取SQL语句。OpenTSDB 不使用 SQL</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <param name="methodType">方法类型</param>
    /// <returns>固定返回空</returns>
    public override String? GetSql(IEntitySession session, IEntity entity, DataObjectMethodType methodType) => null;

    /// <summary>插入语句。OpenTSDB 不使用 SQL</summary>
    /// <param name="session">实体会话</param>
    /// <returns>固定返回空</returns>
    public override String? InsertSQL(IEntitySession session) => null;
    #endregion

    private static Int32 ThrowNotSupported(IEntitySession session, DatabaseCapability capability, String operation)
    {
        session.Dal.Db.EnsureSupported(capability, operation);
        throw new NotSupportedException(L.T("This operation is not supported!"));
    }
}

/// <summary>OpenTSDB 持久化工厂</summary>
public class OpenTSDBEntityPersistenceFactory : IEntityPersistenceFactory
{
    /// <summary>创建 OpenTSDB 原生持久化</summary>
    /// <param name="factory">实体工厂</param>
    /// <param name="db">当前数据库</param>
    /// <returns>实体持久化</returns>
    public IEntityPersistence Create(IEntityFactory factory, IDatabase? db) => new OpenTSDBEntityPersistence(factory);
}
