using System;
using System.Collections.Generic;
using XCode;
using XCode.DataAccessLayer;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>非 SQL 数据库原生持久化适配器测试</summary>
public class EntityPersistenceTests
{
    [Fact]
    [System.ComponentModel.Description("关系型数据库回退到默认 SQL 持久化")]
    public void RelationalProvider_UsesSqlPersistence()
    {
        var db = DbFactory.Create(DatabaseType.SQLite);
        Assert.NotNull(db);

        var persistence = EntityPersistenceFactory.Create(Role.Meta.Factory, db!);

        Assert.IsType<EntityPersistence>(persistence);
    }

    [Fact]
    [System.ComponentModel.Description("MongoDB 与 InfluxDB 使用原生持久化适配器")]
    public void NoSqlProviders_UseNativePersistence()
    {
        var factory = Role.Meta.Factory;

        Assert.IsType<MongoEntityPersistence>(EntityPersistenceFactory.Create(factory, DbFactory.Create(DatabaseType.MongoDB)!));
        Assert.IsType<InfluxEntityPersistence>(EntityPersistenceFactory.Create(factory, DbFactory.Create(DatabaseType.InfluxDB)!));
    }

    [Fact]
    [System.ComponentModel.Description("未注册文档写入器时，MongoDB 写入给出明确异常")]
    public void MongoInsert_WithoutWriter_Throws()
    {
        var persistence = new MongoEntityPersistence(Role.Meta.Factory);
        var role = new Role { Name = "mongo_missing_writer" };

        MongoEntityPersistence.Writer = null;
        var ex = Assert.Throws<NotSupportedException>(() => persistence.Insert(Role.Meta.Session, role));
        Assert.Contains("IMongoDocumentWriter", ex.Message);
    }

    [Fact]
    [System.ComponentModel.Description("MongoDB 文档按属性名映射，写入器收到集合名与主键")]
    public void MongoPersistence_UsesDocumentWriter()
    {
        var persistence = new MongoEntityPersistence(Role.Meta.Factory);
        var writer = new FakeMongoWriter();
        var role = new Role { Id = 4321, Name = "mongo_role", IsSystem = true };

        MongoEntityPersistence.Writer = writer;
        try
        {
            Assert.Equal(1, persistence.Insert(Role.Meta.Session, role));
            Assert.Equal("Role", writer.Collection);
            Assert.Equal("mongo_role", writer.LastDocument!["Name"]);

            Assert.Equal(1, persistence.Update(Role.Meta.Session, role));
            Assert.Equal("Id", writer.KeyName);
            Assert.Equal(4321, writer.KeyValue);

            Assert.Equal(1, persistence.Delete(Role.Meta.Session, role));
            Assert.Equal(4321, writer.KeyValue);
        }
        finally
        {
            MongoEntityPersistence.Writer = null;
        }
    }

    [Fact]
    [System.ComponentModel.Description("MongoDB 不使用 SQL 语句")]
    public void MongoPersistence_HasNoSql()
    {
        var persistence = new MongoEntityPersistence(Role.Meta.Factory);
        var role = new Role { Id = 1, Name = "mongo_sql" };

        Assert.Null(persistence.GetSql(Role.Meta.Session, role, System.ComponentModel.DataObjectMethodType.Insert));
        Assert.Null(persistence.InsertSQL(Role.Meta.Session));
    }

    [Fact]
    [System.ComponentModel.Description("InfluxDB 只支持追加写入，更新与删除明确抛错")]
    public void InfluxPersistence_RejectsUpdateAndDelete()
    {
        var persistence = new InfluxEntityPersistence(Role.Meta.Factory);
        var role = new Role { Name = "influx_role" };

        Assert.Throws<NotSupportedException>(() => persistence.Update(Role.Meta.Session, role));
        Assert.Throws<NotSupportedException>(() => persistence.Delete(Role.Meta.Session, role));
        Assert.Throws<NotSupportedException>(() => persistence.Delete(Role.Meta.Session, "Name='x'"));
        Assert.Null(persistence.GetSql(Role.Meta.Session, role, System.ComponentModel.DataObjectMethodType.Insert));
        Assert.Null(persistence.InsertSQL(Role.Meta.Session));
    }

    private sealed class FakeMongoWriter : IMongoDocumentWriter
    {
        public String? Collection { get; private set; }

        public IDictionary<String, Object?>? LastDocument { get; private set; }

        public String? KeyName { get; private set; }

        public Object? KeyValue { get; private set; }

        public Int32 Insert(String collection, IDictionary<String, Object?> document)
        {
            Collection = collection;
            LastDocument = document;

            return 1;
        }

        public Int32 Replace(String collection, String keyName, Object? keyValue, IDictionary<String, Object?> document)
        {
            Collection = collection;
            KeyName = keyName;
            KeyValue = keyValue;
            LastDocument = document;

            return 1;
        }

        public Int32 Delete(String collection, String keyName, Object? keyValue)
        {
            Collection = collection;
            KeyName = keyName;
            KeyValue = keyValue;

            return 1;
        }
    }
}
