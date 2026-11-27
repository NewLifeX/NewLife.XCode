using System;
using System.Linq;
using NewLife;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>Vertica 数据库测试。无真实服务时仅验证方言与注册</summary>
[Collection("Database")]
public class VerticaTests
{
    private const String DefaultConnStr = "Host=127.0.0.1;Port=5433;Database=VMart;User=dbadmin;Password=";

    private static String _ConnStr = DefaultConnStr;

    /// <summary>解析连接串。环境变量 XCode_vertica 优先，其次 Config/vertica.config</summary>
    public VerticaTests() => _ConnStr = LiveDatabase.Resolve("vertica", "vertica", DefaultConnStr);

    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.Vertica);
        Assert.NotNull(db);
        Assert.Equal(DatabaseType.Vertica, db.Type);
    }

    [Fact]
    public void SupportTest()
    {
        var db = DbFactory.Create(DatabaseType.Vertica);
        Assert.True(db.Support("Vertica"));
        Assert.True(db.Support("Vertica.Data"));
        Assert.False(db.Support("SqlServer"));
    }

    [Fact]
    public void FormatKeyWordTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.Vertica)!;

        Assert.Equal("\"TableName\"", db.FormatKeyWord("TableName"));
    }

    [Fact]
    public void FormatValueTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.Vertica)!;

        var table = DAL.CreateTable();
        var col = table.CreateColumn();
        col.DataType = typeof(Boolean);

        Assert.Equal("TRUE", db.FormatValue(col, true));
        Assert.Equal("FALSE", db.FormatValue(col, false));
    }

    [Fact]
    public void FormatDateTimeTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.Vertica)!;

        Assert.Equal("TIMESTAMP '2024-01-02 03:04:05'", db.FormatDateTime(new DateTime(2024, 1, 2, 3, 4, 5)));
    }

    [Fact]
    public void PageSplitTest()
    {
        var db = DbFactory.Create(DatabaseType.Vertica);

        Assert.Contains("limit 10", db.PageSplit("SELECT * FROM test", 0, 10, null));
        Assert.Contains("limit 10 offset 10", db.PageSplit("SELECT * FROM test", 10, 10, null));
    }

    [Fact]
    public void StringConcatTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.Vertica)!;

        Assert.Equal("a||b", db.StringConcat("a", "b"));
    }

    [LiveDatabaseFact("vertica", "vertica", 5433)]
    public void ConnectTest()
    {
        var db = DbFactory.Create(DatabaseType.Vertica);
        Assert.NotNull(db);

        db.ConnectionString = _ConnStr;
        using var conn = db.OpenConnection();
        Assert.NotNull(conn);
    }

    [LiveDatabaseFact("vertica", "vertica", 5433)]
    public void LiveQueryTest()
    {
        DAL.AddConnStr("Vertica_Live", _ConnStr, null, "Vertica");
        var dal = DAL.Create("Vertica_Live");
        Assert.Equal(DatabaseType.Vertica, dal.DbType);

        var dt = dal.Query("select 1 as n");
        Assert.NotNull(dt);
        Assert.Single(dt.Rows);
        Assert.Equal(1, dt.Rows[0][0].ToInt());
    }

    [LiveDatabaseFact("vertica", "vertica", 5433)]
    public void LiveCrudTest()
    {
        DAL.AddConnStr("Vertica_Live", _ConnStr, null, "Vertica");
        var dal = DAL.Create("Vertica_Live");

        using var meta = dal.Db.CreateMetaData();
        var table = CreateUserTable("xcvertica_user");

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
            // 新增。Vertica 无法回读自增，显式指定主键
            Assert.Equal(1, dal.Execute("insert into xcvertica_user (id, name) values (1, 'vertica-a')"));
            var dt = dal.Query("select id, name from xcvertica_user where id = 1");
            Assert.Single(dt.Rows);
            Assert.Equal(1, dt.Rows[0][0].ToInt());
            Assert.Equal("vertica-a", dt.Rows[0][1] + "");

            // 更新
            dal.Execute("update xcvertica_user set name = 'vertica-b' where id = 1");
            var count = dal.Query("select count(*) from xcvertica_user where name = 'vertica-b'");
            Assert.Equal(1, count.Rows[0][0].ToInt());

            // 删除
            dal.Execute("delete from xcvertica_user where id = 1");
            count = dal.Query("select count(*) from xcvertica_user");
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

    [LiveDatabaseFact("vertica", "vertica", 5433)]
    public void LiveGetTablesTest()
    {
        DAL.AddConnStr("Vertica_Live", _ConnStr, null, "Vertica");
        var dal = DAL.Create("Vertica_Live");

        using var meta = dal.Db.CreateMetaData();
        var table = CreateUserTable("xcvertica_meta");
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
            // 反向工程：新建的表能被 information_schema 读出
            Assert.Contains(dal.GetTableNames(), e => e.EqualIgnoreCase("xcvertica_meta"));

            var tables = meta.GetTables();
            var tb = tables.FirstOrDefault(e => e.TableName.EqualIgnoreCase("xcvertica_meta"));
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
        DAL.AddConnStr("vertica_test", "Host=localhost;Database=test;Owner=public;User=u;Password=p", null, "Vertica");
        var dal = DAL.Create("vertica_test");
        Assert.Equal(DatabaseType.Vertica, dal.DbType);

        var connStr = dal.Db.ConnectionString;
        Assert.True(connStr.Contains("Database=test", StringComparison.OrdinalIgnoreCase), connStr);
        Assert.True(connStr.Contains("SearchPath=public", StringComparison.OrdinalIgnoreCase), connStr);
    }

    [Fact]
    public void IdentityNotSupportedTest()
    {
        var db = DbFactory.Create(DatabaseType.Vertica);
        db.ConnectionString = "Host=localhost;Database=test";

        var session = db.CreateSession();
        Assert.Throws<NotSupportedException>(() => session.InsertAndGetIdentity("insert into t(a) values(1)"));
    }

    [Fact]
    public void CreateTableSqlTest()
    {
        var db = DbFactory.Create(DatabaseType.Vertica)!;

        var sql = db.CreateMetaData().GetSchemaSQL(DDLSchema.CreateTable, CreateTable())!;

        Assert.StartsWith("Create Table", sql);
        Assert.Contains("testuser", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INTEGER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VARCHAR(50)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Primary Key", sql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>构造真库测试表：主键 + 50 长度字符串字段。表名带 xcvertica 前缀避免撞库</summary>
    private static IDataTable CreateUserTable(String tableName)
    {
        var table = DAL.CreateTable();
        table.Name = tableName;
        table.TableName = tableName;
        table.DbType = DatabaseType.Vertica;

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
        table.DbType = DatabaseType.Vertica;

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
