using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;
using NewLife;
using NewLife.Log;
using NewLife.UnitTest;
using XCode;
using XCode.DataAccessLayer;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>MongoDB 数据库测试。MongoDB 是文档型 NoSQL 数据库，默认端口 27017</summary>
[TestCaseOrderer("NewLife.UnitTest.PriorityOrderer", "NewLife.UnitTest")]
public class MongoDBTests
{
    private const String DefaultConnStr = "mongodb://127.0.0.1:27017/test";

    private static String _ConnStr = DefaultConnStr;

    /// <summary>解析连接串。环境变量 XCode_mongodb 优先，其次 Config/mongodb.config，均无则写入并使用本机默认值</summary>
    /// <remarks>MongoDB 连接串不是 key=value 形式，本地探测无法解析服务地址时按跳过处理，CI 通过环境变量 XCode_mongodb 注入</remarks>
    public MongoDBTests() => _ConnStr = LiveDatabase.Resolve("mongodb", "mongodb", DefaultConnStr);

    [TestOrder(0)]
    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.MongoDB);
        Assert.NotNull(db);
        Assert.Equal(DatabaseType.MongoDB, db.Type);

        XTrace.WriteLine("MongoDB Provider Type: {0}", db.GetType().FullName);
    }

    [TestOrder(10)]
    [LiveDatabaseFact("mongodb", "mongodb", 27017)]
    public void ConnectTest()
    {
        // MongoDB 是文档数据库，MongoDB.Driver 不提供 ADO.NET 工厂，不能用 OpenConnection 验证；
        // 这里直接用驱动 ping，既验证服务器可达，也验证驱动已正确引用
        var client = new MongoClient(_ConnStr);
        var result = client.GetDatabase("admin").RunCommand<BsonDocument>(new BsonDocument("ping", 1));
        Assert.Equal(1d, result["ok"].ToDouble());
    }

    [TestOrder(30)]
    [LiveDatabaseFact("mongodb", "mongodb", 27017)]
    public void EntityPersistenceTest()
    {
        var client = new MongoClient(_ConnStr);
        var database = client.GetDatabase("xcode_test");
        database.DropCollection("Role");

        var persistence = new MongoEntityPersistence(Role.Meta.Factory);
        var role = new Role { Id = 9527, Name = "mongo_live", IsSystem = true };
        var collection = database.GetCollection<BsonDocument>("Role");

        MongoEntityPersistence.Writer = new MongoDriverDocumentWriter(database);
        try
        {
            Assert.Equal(1, persistence.Insert(Role.Meta.Session, role));

            var doc = collection.Find(Builders<BsonDocument>.Filter.Eq("Id", 9527)).FirstOrDefault();
            Assert.NotNull(doc);
            Assert.Equal("mongo_live", doc!["Name"].AsString);

            role.Name = "mongo_live2";
            Assert.Equal(1, persistence.Update(Role.Meta.Session, role));
            doc = collection.Find(Builders<BsonDocument>.Filter.Eq("Id", 9527)).FirstOrDefault();
            Assert.Equal("mongo_live2", doc!["Name"].AsString);

            Assert.Equal(1, persistence.Delete(Role.Meta.Session, role));
            Assert.Equal(0, collection.CountDocuments(Builders<BsonDocument>.Filter.Eq("Id", 9527)));
        }
        finally
        {
            MongoEntityPersistence.Writer = null;
        }
    }

    [TestOrder(20)]
    [LiveDatabaseFact("mongodb", "mongodb", 27017)]
    public void DALTest()
    {
        DAL.AddConnStr("sysMongoDB", _ConnStr, null, "MongoDB");
        var dal = DAL.Create("sysMongoDB");
        Assert.NotNull(dal);
        Assert.Equal("sysMongoDB", dal.ConnName);
        Assert.Equal(DatabaseType.MongoDB, dal.DbType);

        var db = dal.Db;
        var connstr = db.ConnectionString;
        XTrace.WriteLine("ConnectionString: {0}", connstr);
    }

    [Fact]
    public void SupportTest()
    {
        var db = DbFactory.Create(DatabaseType.MongoDB);
        Assert.True(db.Support("MongoDB"));
        Assert.True(db.Support("mongo"));
        Assert.False(db.Support("SqlServer"));
    }

    [Fact]
    public void FormatKeyWordTest()
    {
        var db = DbFactory.Create(DatabaseType.MongoDB) as DbBase;
        Assert.NotNull(db);

        var result = db.FormatKeyWord("CollectionName");
        Assert.Equal("\"CollectionName\"", result);

        var result2 = db.FormatKeyWord("\"AlreadyQuoted\"");
        Assert.Equal("\"AlreadyQuoted\"", result2);
    }

    [Fact]
    public void FormatValueTest()
    {
        var db = DbFactory.Create(DatabaseType.MongoDB) as DbBase;
        Assert.NotNull(db);

        var table = DAL.CreateTable();
        var col = table.CreateColumn();
        col.DataType = typeof(Boolean);

        var trueVal = db.FormatValue(col, true);
        Assert.Equal("true", trueVal);

        var falseVal = db.FormatValue(col, false);
        Assert.Equal("false", falseVal);
    }

    [Fact]
    public void ConnectionStringTest()
    {
        var db = DbFactory.Create(DatabaseType.MongoDB);
        db.ConnectionString = "mongodb://localhost:27017/test";

        var connStr = db.ConnectionString;
        Assert.Contains("mongodb://", connStr);
        Assert.Contains("test", connStr);
    }

    [Fact]
    public void SystemDatabaseTest()
    {
        var db = DbFactory.Create(DatabaseType.MongoDB) as DbBase;
        Assert.NotNull(db);

        // MongoDB 通过 override SystemDatabaseName 返回 "admin"
        var sysDb = (db as RemoteDb)?.SystemDatabaseName;
        Assert.NotNull(sysDb);
        Assert.Equal("admin", sysDb);
    }

    [Fact]
    public void StringConcatTest()
    {
        var db = DbFactory.Create(DatabaseType.MongoDB) as DbBase;
        Assert.NotNull(db);

        var result = db.StringConcat("a", "b");
        Assert.Equal("concat(a,b)", result);
    }

    /// <summary>用 MongoDB.Driver 实现 XCode 的文档写入器，验证应用侧接入方式</summary>
    private sealed class MongoDriverDocumentWriter(IMongoDatabase database) : IMongoDocumentWriter
    {
        public Int32 Insert(String collection, IDictionary<String, Object?> document)
        {
            database.GetCollection<BsonDocument>(collection).InsertOne(ToDocument(document));

            return 1;
        }

        public Int32 Replace(String collection, String keyName, Object? keyValue, IDictionary<String, Object?> document)
        {
            var result = database.GetCollection<BsonDocument>(collection).ReplaceOne(Filter(keyName, keyValue), ToDocument(document));

            return result.MatchedCount > 0 ? 1 : 0;
        }

        public Int32 Delete(String collection, String keyName, Object? keyValue)
        {
            var result = database.GetCollection<BsonDocument>(collection).DeleteOne(Filter(keyName, keyValue));

            return (Int32)result.DeletedCount;
        }

        private static FilterDefinition<BsonDocument> Filter(String keyName, Object? keyValue) => Builders<BsonDocument>.Filter.Eq(keyName, BsonValue.Create(keyValue));

        private static BsonDocument ToDocument(IDictionary<String, Object?> document)
        {
            var doc = new BsonDocument();
            foreach (var item in document)
            {
                doc[item.Key] = BsonValue.Create(item.Value);
            }

            return doc;
        }
    }
}
