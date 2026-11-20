using System;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>验证数据库基类对外公开，外部提供者可以复用默认实现</summary>
public class PublicBaseClassTests
{
    [Fact]
    [System.ComponentModel.Description("DbBase/DbSession/DbMetaData 对外公开，外部提供者可以继承")]
    public void BaseClasses_ArePublicAndInheritable()
    {
        Assert.True(typeof(DbBase).IsPublic);
        Assert.True(typeof(DbSession).IsPublic);
        Assert.True(typeof(DbMetaData).IsPublic);

        var db = new TestDatabase { ConnectionString = "test" };
        Assert.IsAssignableFrom<IDatabase>(db);
        Assert.IsAssignableFrom<IDbDialect>(db);
        Assert.Same(db, db.Dialect);
        Assert.Equal(db.LongTextLength, db.Dialect.LongTextLength);
        Assert.IsAssignableFrom<IDbSession>(db.CreateSession());
        var meta = db.CreateMetaData();
        Assert.IsAssignableFrom<IMetaData>(meta);
        Assert.IsAssignableFrom<ISchemaReader>(meta);
        Assert.IsAssignableFrom<ISchemaMigrator>(meta);
        Assert.IsAssignableFrom<IDatabaseCreator>(meta);
        Assert.IsAssignableFrom<IDatabaseMaintenance>(meta);
    }

    private sealed class TestDatabase : DbBase
    {
        public override DatabaseType Type => DatabaseType.None;

        protected override IDbSession OnCreateSession() => new TestSession(this);

        protected override IMetaData OnCreateMetaData() => new TestMetaData();

        public override String PageSplit(String sql, Int64 startRowIndex, Int64 maximumRows, String? keyColumn) => sql;
    }

    private sealed class TestSession : DbSession
    {
        public TestSession(IDatabase db) : base(db) { }
    }

    private sealed class TestMetaData : DbMetaData { }
}
