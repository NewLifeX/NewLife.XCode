using XCode.DataAccessLayer;

namespace XCode;

/// <summary>实体持久化工厂。用于按数据库类型选择不同的持久化实现</summary>
/// <remarks>
/// 关系型数据库默认使用 <see cref="EntityPersistence"/>，它把实体操作翻译为 SQL。
/// 文档型、时序型等非 SQL 数据库可实现本接口，提供原生写入适配器。
/// </remarks>
public interface IEntityPersistenceFactory
{
    /// <summary>创建持久化实例</summary>
    /// <param name="factory">实体工厂</param>
    /// <param name="db">当前数据库</param>
    /// <returns>实体持久化</returns>
    IEntityPersistence Create(IEntityFactory factory, IDatabase? db);
}

/// <summary>实体持久化工厂注册表。按数据库类型选择默认持久化实现</summary>
public static class EntityPersistenceFactory
{
    private static readonly Dictionary<DatabaseType, IEntityPersistenceFactory> _factories = [];

    static EntityPersistenceFactory()
    {
        // 非 SQL 数据库默认使用原生持久化适配器，避免落到 SQL 生成路径
        Register(DatabaseType.MongoDB, new MongoEntityPersistenceFactory());
        Register(DatabaseType.InfluxDB, new InfluxEntityPersistenceFactory());
        Register(DatabaseType.OpenTSDB, new OpenTSDBEntityPersistenceFactory());
    }

    /// <summary>注册指定数据库类型的持久化工厂</summary>
    /// <param name="dbType">数据库类型</param>
    /// <param name="factory">持久化工厂</param>
    public static void Register(DatabaseType dbType, IEntityPersistenceFactory factory)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        lock (_factories) _factories[dbType] = factory;
    }

    /// <summary>创建实体持久化。未注册的数据库类型回退到默认 SQL 持久化</summary>
    /// <param name="factory">实体工厂</param>
    /// <param name="db">当前数据库</param>
    /// <returns>实体持久化</returns>
    public static IEntityPersistence Create(IEntityFactory factory, IDatabase? db)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        if (db != null)
        {
            IEntityPersistenceFactory? pf;
            lock (_factories) _factories.TryGetValue(db.Type, out pf);

            if (pf != null) return pf.Create(factory, db);
        }

        return new EntityPersistence(factory);
    }
}
