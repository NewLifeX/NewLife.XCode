using System;
using System.Linq;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>MonetDB 数据库测试。无真实服务时仅验证方言与注册</summary>
[Collection("Database")]
public class MonetDBTests
{
    private const String DefaultConnStr = "Host=127.0.0.1;Port=50000;Database=demo;User=monetdb;Password=monetdb";

    private static String _ConnStr = DefaultConnStr;

    /// <summary>解析连接串。环境变量 XCode_monetdb 优先，其次 Config/monetdb.config</summary>
    public MonetDBTests() => _ConnStr = LiveDatabase.Resolve("monetdb", "monetdb", DefaultConnStr);

    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.MonetDB);
        Assert.NotNull(db);
        Assert.Equal(DatabaseType.MonetDB, db.Type);
    }

    [Fact]
    public void SupportTest()
    {
        var db = DbFactory.Create(DatabaseType.MonetDB);
        Assert.True(db.Support("MonetDB"));
        Assert.True(db.Support("MonetDb.Mapi"));
        Assert.False(db.Support("SqlServer"));
    }

    [Fact]
    public void FormatKeyWordTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.MonetDB)!;

        Assert.Equal("\"TableName\"", db.FormatKeyWord("TableName"));
    }

    [Fact]
    public void FormatValueTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.MonetDB)!;

        var table = DAL.CreateTable();
        var col = table.CreateColumn();
        col.DataType = typeof(Boolean);

        Assert.Equal("TRUE", db.FormatValue(col, true));
        Assert.Equal("FALSE", db.FormatValue(col, false));
    }

    [Fact]
    public void PageSplitTest()
    {
        var db = DbFactory.Create(DatabaseType.MonetDB);

        Assert.Contains("limit 10", db.PageSplit("SELECT * FROM test", 0, 10, null));
        Assert.Contains("limit 10 offset 10", db.PageSplit("SELECT * FROM test", 10, 10, null));
    }

    [LiveDatabaseFact("monetdb", "monetdb", 50000)]
    public void ConnectTest()
    {
        var db = DbFactory.Create(DatabaseType.MonetDB);
        Assert.NotNull(db);

        db.ConnectionString = _ConnStr;
        using var conn = db.OpenConnection();
        Assert.NotNull(conn);
    }

    [LiveDatabaseFact("monetdb", "monetdb", 50000)]
    public void LiveQueryTest()
    {
        DAL.AddConnStr("MonetDB_Live", _ConnStr, null, "MonetDB");
        var dal = DAL.Create("MonetDB_Live");
        Assert.Equal(DatabaseType.MonetDB, dal.DbType);

        var dt = dal.Query("select 1 as n");
        Assert.NotNull(dt);
        Assert.Single(dt.Rows);
        Assert.Equal(1, dt.Rows[0][0].ToInt());
    }

    [LiveDatabaseFact("monetdb", "monetdb", 50000)]
    public void LiveCrudTest()
    {
        DAL.AddConnStr("MonetDB_Live", _ConnStr, null, "MonetDB");
        var dal = DAL.Create("MonetDB_Live");

        using var meta = dal.Db.CreateMetaData();
        var table = CreateUserTable("xcmonet_user");

        // 清理上次运行残留，首次运行不存在
        try
        {
            meta.DropTable(table);
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("DropTable ignored: {0}", ex.Message);
        }

        Assert.True(meta.CreateTable(table));
        try
        {
            // 新增。MonetDB 无法回读自增，显式指定主键；驱动不返回受影响行数，固定为 0
            Assert.Equal(0, dal.Execute("insert into xcmonet_user (id, name) values (1, 'monetdb-a')"));
            var dt = dal.Query("select id, name from xcmonet_user where id = 1");
            Assert.Single(dt.Rows);
            Assert.Equal(1, dt.Rows[0][0].ToInt());
            Assert.Equal("monetdb-a", dt.Rows[0][1] + "");

            // 更新
            dal.Execute("update xcmonet_user set name = 'monetdb-b' where id = 1");
            var count = dal.Query("select count(*) from xcmonet_user where name = 'monetdb-b'");
            Assert.Equal(1, count.Rows[0][0].ToInt());

            // 删除
            dal.Execute("delete from xcmonet_user where id = 1");
            count = dal.Query("select count(*) from xcmonet_user");
            Assert.Equal(0, count.Rows[0][0].ToInt());
        }
        finally
        {
            try
            {
                meta.DropTable(table);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }
    }

    [LiveDatabaseFact("monetdb", "monetdb", 50000)]
    public void LivePageSplitTest()
    {
        DAL.AddConnStr("MonetDB_Live", _ConnStr, null, "MonetDB");
        var dal = DAL.Create("MonetDB_Live");

        using var meta = dal.Db.CreateMetaData();
        var table = CreateUserTable("xcmonet_page");
        try
        {
            meta.DropTable(table);
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("DropTable ignored: {0}", ex.Message);
        }

        Assert.True(meta.CreateTable(table));
        try
        {
            for (var i = 1; i <= 5; i++)
            {
                dal.Execute($"insert into xcmonet_page (id, name) values ({i}, 'page-{i}')");
            }

            const String sql = "select id, name from xcmonet_page order by id";
            var page1 = dal.Query(dal.Db.PageSplit(sql, 0, 2, null));
            Assert.Equal(2, page1.Rows.Count);

            var page2 = dal.Query(dal.Db.PageSplit(sql, 2, 2, null));
            Assert.Equal(2, page2.Rows.Count);

            var page3 = dal.Query(dal.Db.PageSplit(sql, 4, 2, null));
            Assert.Single(page3.Rows);

            // 相邻页数据不重叠
            Assert.NotEqual(page1.Rows[1][0].ToInt(), page2.Rows[0][0].ToInt());
        }
        finally
        {
            try
            {
                meta.DropTable(table);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }
    }

    [LiveDatabaseFact("monetdb", "monetdb", 50000)]
    public void LiveGetTablesTest()
    {
        DAL.AddConnStr("MonetDB_Live", _ConnStr, null, "MonetDB");
        var dal = DAL.Create("MonetDB_Live");

        using var meta = dal.Db.CreateMetaData();
        var table = CreateUserTable("xcmonet_meta");
        try
        {
            meta.DropTable(table);
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("DropTable ignored: {0}", ex.Message);
        }

        Assert.True(meta.CreateTable(table));
        try
        {
            // 反向工程：新建的表能被 sys.tables 读出
            Assert.Contains(dal.GetTableNames(), e => e.EqualIgnoreCase("xcmonet_meta"));

            var tables = meta.GetTables();
            var tb = tables.FirstOrDefault(e => e.TableName.EqualIgnoreCase("xcmonet_meta"));
            Assert.NotNull(tb);
            Assert.Contains(tb!.Columns, e => e.ColumnName.EqualIgnoreCase("name"));
        }
        finally
        {
            try
            {
                meta.DropTable(table);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }
    }

    [Fact]
    public void ConnectionStringTest()
    {
        DAL.AddConnStr("monetdb_test", "Host=localhost;Database=test;User=monetdb;Password=monetdb", null, "MonetDB");
        var dal = DAL.Create("monetdb_test");
        Assert.Equal(DatabaseType.MonetDB, dal.DbType);

        var connStr = dal.Db.ConnectionString;
        Assert.True(connStr.Contains("Port=50000", StringComparison.OrdinalIgnoreCase), connStr);
    }

    [Fact]
    public void IdentityNotSupportedTest()
    {
        var db = DbFactory.Create(DatabaseType.MonetDB);
        db.ConnectionString = "Host=localhost;Database=test";

        var session = db.CreateSession();
        Assert.Throws<NotSupportedException>(() => session.InsertAndGetIdentity("insert into t(a) values(1)"));
    }

    [Fact]
    public async Task IdentityNotSupportedAsyncTest()
    {
        var db = DbFactory.Create(DatabaseType.MonetDB);
        db.ConnectionString = "Host=localhost;Database=test";

        var session = (IAsyncDbSession)db.CreateSession();
        await Assert.ThrowsAsync<NotSupportedException>(() => session.InsertAndGetIdentityAsync("insert into t(a) values(1)"));
    }

    [Fact]
    public void FormatDateTimeTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.MonetDB)!;

        Assert.Equal("TIMESTAMP '2024-01-02 03:04:05'", db.FormatDateTime(new DateTime(2024, 1, 2, 3, 4, 5)));
    }

    [Fact]
    public void CreateTableSqlTest()
    {
        var db = DbFactory.Create(DatabaseType.MonetDB)!;

        var sql = db.CreateMetaData().GetSchemaSQL(DDLSchema.CreateTable, CreateTable())!;

        Assert.StartsWith("Create Table", sql);
        Assert.Contains("testuser", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INTEGER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VARCHAR(50)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Primary Key", sql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>构造真库测试表：主键 + 50 长度字符串字段。表名带 xcmonet 前缀避免撞库</summary>
    private static IDataTable CreateUserTable(String tableName)
    {
        var table = DAL.CreateTable();
        table.Name = tableName;
        table.TableName = tableName;
        table.DbType = DatabaseType.MonetDB;

        var id = table.CreateColumn();
        id.Name = "Id";
        id.ColumnName = "Id";
        id.DataType = typeof(Int32);
        id.PrimaryKey = true;
        table.Columns.Add(id);

        var name = table.CreateColumn();
        name.Name = "Name";
        name.ColumnName = "Name";
        name.DataType = typeof(String);
        name.Length = 50;
        table.Columns.Add(name);

        return table;
    }

    /// <summary>构造模型表：主键 + 50 长度字符串字段</summary>
    private static IDataTable CreateTable()
    {
        var table = DAL.CreateTable();
        table.Name = "TestUser";
        table.TableName = "TestUser";
        table.DbType = DatabaseType.MonetDB;

        var id = table.CreateColumn();
        id.Name = "Id";
        id.ColumnName = "Id";
        id.DataType = typeof(Int32);
        id.PrimaryKey = true;
        table.Columns.Add(id);

        var name = table.CreateColumn();
        name.Name = "Name";
        name.ColumnName = "Name";
        name.DataType = typeof(String);
        name.Length = 50;
        table.Columns.Add(name);

        return table;
    }
}
