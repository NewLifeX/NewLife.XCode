using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using XCode.DataAccessLayer;
using XCode.Model;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>通过正式 Server 验证远程接入，连接字符串由测试环境提供。</summary>
public class SonnetDBRemoteTests
{
    [SonnetServerTheory]
    [InlineData("rest")]
    public void DatabaseLifecycle(String protocol)
    {
        var connName = "SonnetDatabase_" + Guid.NewGuid().ToString("N");
        DAL.AddConnStr(connName, Environment.GetEnvironmentVariable("SONNETDB_TEST_CONNECTION") + ";Protocol=" + protocol, null, "SonnetDB");
        var dal = DAL.Create(connName);
        var meta = dal.Db.CreateMetaData();
        var databaseName = "db_" + Guid.NewGuid().ToString("N");
        try
        {
            Assert.False(meta.DatabaseExist(databaseName));
            Assert.True(meta.CreateDatabase(databaseName));
            Assert.True(meta.CreateDatabase(databaseName));
            Assert.True(meta.DatabaseExist(databaseName));
            Assert.True(meta.DropDatabase(databaseName));
            Assert.False(meta.DatabaseExist(databaseName));
            Assert.True(meta.DropDatabase(databaseName));
        }
        finally
        {
            if (meta.DatabaseExist(databaseName)) meta.DropDatabase(databaseName);
            dal.Db.Dispose();
        }
    }

    [SonnetServerTheory]
    [InlineData("rest")]
    [InlineData("auto")]
    [InlineData("frame-http2")]
    public async Task SchemaCrudAndTransactions(String protocol)
    {
        var connName = "SonnetRemote_" + Guid.NewGuid().ToString("N");
        var setting = protocol == "frame-http2" ? "SONNETDB_TEST_HTTP2_CONNECTION" : "SONNETDB_TEST_CONNECTION";
        var connectionString = Environment.GetEnvironmentVariable(setting) + ";Protocol=" + protocol;
        DAL.AddConnStr(connName, connectionString, null, "SonnetDB");
        var dal = DAL.Create(connName);
        var meta = dal.Db.CreateMetaData();
        var table = new XTable("Remote_" + Guid.NewGuid().ToString("N"));
        var name = table.CreateColumn();
        name.Name = name.ColumnName = "Name";
        name.DataType = typeof(String);
        name.Nullable = false;
        table.Columns.Add(name);
        var id = table.CreateColumn();
        id.Name = id.ColumnName = "Id";
        id.DataType = typeof(Int64);
        id.PrimaryKey = id.Identity = true;
        table.Columns.Add(id);
        table.Fix();
        var formatted = dal.Db.FormatName(table.TableName);
        try
        {
            Assert.True(meta.CreateTable(table));
            var actual = Assert.Single(meta.GetTables(), item => item.TableName == table.TableName);
            Assert.True(actual.Columns.Single(column => column.ColumnName == "Id").Identity);
            var first = await dal.InsertAndGetIdentityAsync($"INSERT INTO {formatted}(Name) VALUES(@name)",
                CommandType.Text, dal.Db.CreateParameter("name", "远程 O'Brien 😀"));
            Assert.True(first > 0);
            var second = dal.InsertAndGetIdentity($"INSERT INTO {formatted}(Name) VALUES('second')");
            Assert.True(second > first);
            var data = dal.Session.Query($"SELECT * FROM {formatted} ORDER BY Id", CommandType.Text);
            Assert.Equal(2, data.Tables[0].Rows.Count);
            Assert.Equal("远程 O'Brien 😀", data.Tables[0].Rows[0]["Name"]);
            var page = dal.Query(dal.Db.PageSplit($"SELECT * FROM {formatted} ORDER BY Id", 1, 1, "Id"));
            Assert.Single(page.Rows);
            Assert.Equal("second", page.Rows[0][0]);
            var session = dal.Session;
            session.BeginTransaction(IsolationLevel.ReadCommitted);
            var pending = session.InsertAndGetIdentity($"INSERT INTO {formatted}(Name) VALUES('pending')");
            Assert.True(pending > second);
            Assert.Equal(3, session.ExecuteScalar<Int32>($"SELECT COUNT(*) FROM {formatted}"));
            session.Rollback(false);
            Assert.Equal(2, session.ExecuteScalar<Int32>($"SELECT COUNT(*) FROM {formatted}"));
            session.BeginTransaction(IsolationLevel.ReadCommitted);
            session.Execute($"UPDATE {formatted} SET Name='committed' WHERE Id={second}");
            session.Commit();
            Assert.Equal("committed", session.ExecuteScalar<String>($"SELECT Name FROM {formatted} WHERE Id={second}"));
            Assert.Equal(1, dal.Execute(dal.Db.BuildDeleteSql(table.TableName, "Id>0", 1)));
            Assert.Equal(1, session.ExecuteScalar<Int32>($"SELECT COUNT(*) FROM {formatted}"));
        }
        finally
        {
            if (dal.Session.Transaction != null) dal.Session.Rollback(false);
            meta.DropTable(table);
            dal.Db.Dispose();
        }
    }
}

/// <summary>没有真实 SonnetDB Server 时明确跳过远程验收。</summary>
public sealed class SonnetServerTheoryAttribute : TheoryAttribute
{
    public SonnetServerTheoryAttribute()
    {
        if (String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SONNETDB_TEST_CONNECTION")) ||
            String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SONNETDB_TEST_HTTP2_CONNECTION")))
            Skip = "🧪 远程验收需要 SONNETDB_TEST_CONNECTION 和 SONNETDB_TEST_HTTP2_CONNECTION。";
    }
}
