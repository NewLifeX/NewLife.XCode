using System.ComponentModel;
using XCode.DataAccessLayer;

namespace XCode;

/// <summary>MongoDB 文档写入器。把实体文档写入集合</summary>
/// <remarks>
/// MongoDB.Driver 不提供 ADO.NET 工厂，XCode 主包也不直接引用驱动。
/// 应用侧可在引用驱动后注册实现，例如把文档写入 <c>IMongoCollection&lt;BsonDocument&gt;</c>。
/// </remarks>
public interface IMongoDocumentWriter
{
    /// <summary>插入文档</summary>
    /// <param name="collection">集合名</param>
    /// <param name="document">文档，键为属性名</param>
    /// <returns>写入行数</returns>
    Int32 Insert(String collection, IDictionary<String, Object?> document);

    /// <summary>按主键替换文档</summary>
    /// <param name="collection">集合名</param>
    /// <param name="keyName">主键属性名</param>
    /// <param name="keyValue">主键值</param>
    /// <param name="document">文档，键为属性名</param>
    /// <returns>写入行数</returns>
    Int32 Replace(String collection, String keyName, Object? keyValue, IDictionary<String, Object?> document);

    /// <summary>按主键删除文档</summary>
    /// <param name="collection">集合名</param>
    /// <param name="keyName">主键属性名</param>
    /// <param name="keyValue">主键值</param>
    /// <returns>删除行数</returns>
    Int32 Delete(String collection, String keyName, Object? keyValue);
}

/// <summary>MongoDB 原生持久化。把实体映射为文档，交给 <see cref="IMongoDocumentWriter"/> 写入</summary>
/// <remarks>
/// 未注册写入器时所有写入操作都抛出明确异常，提示先接入 MongoDB.Driver。
/// 这样既不会让调用方误以为可以写入，也保留了按需接入的扩展点。
/// </remarks>
public class MongoEntityPersistence(IEntityFactory factory) : EntityPersistence(factory)
{
    #region 属性
    private static IMongoDocumentWriter? _writer;
    /// <summary>文档写入器。应用侧在引用 MongoDB.Driver 后注册</summary>
    public static IMongoDocumentWriter? Writer { get => _writer; set => _writer = value; }
    #endregion

    #region 添删改
    /// <summary>插入文档</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <returns>写入行数</returns>
    public override Int32 Insert(IEntitySession session, IEntity entity) => GetWriter().Insert(GetCollection(session), BuildDocument(entity));

    /// <summary>插入文档</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <returns>写入行数</returns>
    public override Task<Int32> InsertAsync(IEntitySession session, IEntity entity) => Task.FromResult(Insert(session, entity));

    /// <summary>按主键替换文档</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <returns>写入行数</returns>
    public override Int32 Update(IEntitySession session, IEntity entity)
    {
        var (name, value) = GetPrimaryKey(entity);
        return GetWriter().Replace(GetCollection(session), name, value, BuildDocument(entity));
    }

    /// <summary>按主键替换文档</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <returns>写入行数</returns>
    public override Task<Int32> UpdateAsync(IEntitySession session, IEntity entity) => Task.FromResult(Update(session, entity));

    /// <summary>按主键删除文档</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <returns>删除行数</returns>
    public override Int32 Delete(IEntitySession session, IEntity entity)
    {
        var (name, value) = GetPrimaryKey(entity);
        return GetWriter().Delete(GetCollection(session), name, value);
    }

    /// <summary>按主键删除文档</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <returns>删除行数</returns>
    public override Task<Int32> DeleteAsync(IEntitySession session, IEntity entity) => Task.FromResult(Delete(session, entity));

    /// <summary>插入。MongoDB 需要完整文档，请使用实体插入</summary>
    /// <param name="session">实体会话</param>
    /// <param name="names">属性列表</param>
    /// <param name="values">值列表</param>
    /// <returns></returns>
    public override Int32 Insert(IEntitySession session, String[] names, Object[] values) => throw CreateNotSupported("Insert with field list; insert a complete entity instead");

    /// <summary>批量更新。MongoDB 需要文档级操作</summary>
    /// <param name="session">实体会话</param>
    /// <param name="setClause">更新项</param>
    /// <param name="whereClause">条件</param>
    /// <returns></returns>
    public override Int32 Update(IEntitySession session, String setClause, String whereClause) => throw CreateNotSupported("Update by SQL clause; use entity update instead");

    /// <summary>批量更新。MongoDB 需要文档级操作</summary>
    /// <param name="session">实体会话</param>
    /// <param name="setNames">更新属性</param>
    /// <param name="setValues">更新值</param>
    /// <param name="whereNames">条件属性</param>
    /// <param name="whereValues">条件值</param>
    /// <returns></returns>
    public override Int32 Update(IEntitySession session, String[] setNames, Object[] setValues, String[] whereNames, Object[] whereValues) => throw CreateNotSupported("Update by field list; use entity update instead");

    /// <summary>批量删除。MongoDB 需要文档级操作</summary>
    /// <param name="session">实体会话</param>
    /// <param name="whereClause">条件</param>
    /// <param name="maximumRows">最大行数</param>
    /// <returns></returns>
    public override Int32 Delete(IEntitySession session, String whereClause, Int32 maximumRows = 0) => throw CreateNotSupported("Delete by SQL clause; use entity delete instead");

    /// <summary>批量删除。MongoDB 需要文档级操作</summary>
    /// <param name="session">实体会话</param>
    /// <param name="names">属性列表</param>
    /// <param name="values">值列表</param>
    /// <returns></returns>
    public override Int32 Delete(IEntitySession session, String[] names, Object[] values) => throw CreateNotSupported("Delete by field list; use entity delete instead");
    #endregion

    #region 语句
    /// <summary>获取SQL语句。MongoDB 不使用 SQL</summary>
    /// <param name="session">实体会话</param>
    /// <param name="entity">实体</param>
    /// <param name="methodType">方法类型</param>
    /// <returns>固定返回空</returns>
    public override String? GetSql(IEntitySession session, IEntity entity, DataObjectMethodType methodType) => null;

    /// <summary>插入语句。MongoDB 不使用 SQL</summary>
    /// <param name="session">实体会话</param>
    /// <returns>固定返回空</returns>
    public override String? InsertSQL(IEntitySession session) => null;
    #endregion

    #region 辅助
    /// <summary>把实体转换为文档。键为属性名，空值保留</summary>
    /// <param name="entity">实体</param>
    /// <returns>文档</returns>
    public IDictionary<String, Object?> BuildDocument(IEntity entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        var dic = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Factory.Fields)
        {
            if (item.Name.IsNullOrEmpty()) continue;

            dic[item.Name] = entity[item.Name];
        }

        return dic;
    }

    private String GetCollection(IEntitySession session) => session?.TableName ?? Factory.Table.TableName;

    private (String Name, Object? Value) GetPrimaryKey(IEntity entity)
    {
        var key = Factory.Unique;
        if (key == null) throw new NotSupportedException(L.T("MongoDB entity persistence requires a primary key; define one on the entity before using Update or Delete."));

        return (key.Name, entity[key.Name]);
    }

    private static IMongoDocumentWriter GetWriter() => _writer ?? throw new NotSupportedException(L.T("MongoDB entity persistence requires an IMongoDocumentWriter. Reference MongoDB.Driver and assign MongoEntityPersistence.Writer before writing entities."));

    private static NotSupportedException CreateNotSupported(String action) => new(L.T("MongoDB entity persistence does not support {0}.", action));
    #endregion
}

/// <summary>MongoDB 持久化工厂</summary>
public class MongoEntityPersistenceFactory : IEntityPersistenceFactory
{
    /// <summary>创建 MongoDB 原生持久化</summary>
    /// <param name="factory">实体工厂</param>
    /// <param name="db">当前数据库</param>
    /// <returns>实体持久化</returns>
    public IEntityPersistence Create(IEntityFactory factory, IDatabase? db) => new MongoEntityPersistence(factory);
}
