using System;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using NewLife;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>Firebird 数据库测试。无真实服务时仅验证方言与注册</summary>
[Collection("Database")]
public class FirebirdTests
{
    private const String DefaultConnStr = "Database=/firebird/data/test.fdb;DataSource=127.0.0.1;Port=3050;User=SYSDBA;Password=masterkey;Dialect=3";

    private static String _ConnStr = DefaultConnStr;

    /// <summary>解析连接串。环境变量 XCode_firebird 优先，其次 Config/firebird.config</summary>
    public FirebirdTests() => _ConnStr = LiveDatabase.Resolve("firebird", "firebird", DefaultConnStr);

    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.Firebird);
        Assert.NotNull(db);
        Assert.Equal(DatabaseType.Firebird, db.Type);
    }

    [Fact]
    public void SupportTest()
    {
        var db = DbFactory.Create(DatabaseType.Firebird);
        Assert.True(db.Support("Firebird"));
        Assert.True(db.Support("FirebirdSql.Data.FirebirdClient"));
        Assert.False(db.Support("SqlServer"));
    }

    [Fact]
    public void FormatKeyWordTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.Firebird)!;

        Assert.Equal("\"TableName\"", db.FormatKeyWord("TableName"));
        Assert.Equal("\"AlreadyQuoted\"", db.FormatKeyWord("\"AlreadyQuoted\""));
    }

    [Fact]
    public void WindowFunctionCapabilityTest()
    {
        var db = DbFactory.Create(DatabaseType.Firebird);
        Assert.NotNull(db);

        Assert.True(db.Capabilities.HasFlag(DatabaseCapability.WindowFunctions));
        // Firebird 3.0 只支持 OVER 分区/排序，不支持 ROWS/RANGE 窗口帧
        Assert.False(db.Capabilities.HasFlag(DatabaseCapability.WindowFrame));
    }

    [Fact]
    public void PageSplitTest()
    {
        var db = DbFactory.Create(DatabaseType.Firebird);

        Assert.Equal("SELECT * FROM test rows 1 to 10", db.PageSplit("SELECT * FROM test", 0, 10, null));
        Assert.Equal("SELECT * FROM test rows 11 to 20", db.PageSplit("SELECT * FROM test", 10, 10, null));
    }

    [Fact]
    public void StringConcatTest()
    {
        var db = (DbBase)DbFactory.Create(DatabaseType.Firebird)!;

        Assert.Equal("a||b", db.StringConcat("a", "b"));
        Assert.Equal("''||b", db.StringConcat(null, "b"));
    }

    [LiveDatabaseFact("firebird", "firebird", 3050)]
    public void ConnectTest()
    {
        var db = DbFactory.Create(DatabaseType.Firebird);
        Assert.NotNull(db);

        db.ConnectionString = _ConnStr;
        using var conn = db.OpenConnection();
        Assert.NotNull(conn);
    }

    [LiveDatabaseFact("firebird", "firebird", 3050)]
    public void LiveCreateDatabaseTest()
    {
        DAL.AddConnStr("Firebird_Live", _ConnStr, null, "Firebird");
        var dal = DAL.Create("Firebird_Live");
        Assert.Equal(DatabaseType.Firebird, dal.DbType);

        // Firebird 必须先有数据库文件；CI 镜像已建库时驱动会拒绝重复建库，忽略异常后以能否查询为准
        using var meta = dal.Db.CreateMetaData();
        try
        {
            meta.CreateDatabase("test", null);
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("CreateDatabase skipped: {0}", ex.Message);
        }

        // Firebird 不支持无 FROM 的 SELECT 1，改查系统表
        var dt = dal.Query("select 1 as n from rdb$database");
        Assert.NotNull(dt);
        Assert.Single(dt.Rows);
        Assert.Equal(1, dt.Rows[0][0].ToInt());
    }

    [LiveDatabaseFact("firebird", "firebird", 3050)]
    public void LiveCrudTest()
    {
        DAL.AddConnStr("Firebird_Live", _ConnStr, null, "Firebird");
        var dal = DAL.Create("Firebird_Live");

        using var meta = dal.Db.CreateMetaData();
        var table = CreateUserTable("xcfb_user");

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
            // 新增
            Assert.Equal(1, dal.Execute("insert into xcfb_user (name) values ('firebird-a')"));
            var dt = dal.Query("select id, name from xcfb_user where name = 'firebird-a'");
            Assert.Single(dt.Rows);
            Assert.True(dt.Rows[0][0].ToInt() > 0);
            Assert.Equal("firebird-a", dt.Rows[0][1] + "");

            // 更新
            Assert.Equal(1, dal.Execute("update xcfb_user set name = 'firebird-b' where name = 'firebird-a'"));
            var count = dal.Query("select count(*) from xcfb_user where name = 'firebird-b'");
            Assert.Equal(1, count.Rows[0][0].ToInt());

            // 删除
            Assert.Equal(1, dal.Execute("delete from xcfb_user where name = 'firebird-b'"));
            count = dal.Query("select count(*) from xcfb_user");
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

    [LiveDatabaseFact("firebird", "firebird", 3050)]
    public void LiveInsertAndGetIdentityTest()
    {
        DAL.AddConnStr("Firebird_Live", _ConnStr, null, "Firebird");
        var dal = DAL.Create("Firebird_Live");

        using var meta = dal.Db.CreateMetaData();
        var table = CreateUserTable("xcfb_identity");
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
            // Firebird 3+ 走 INSERT ... RETURNING 回读自增
            var id = dal.InsertAndGetIdentity("insert into xcfb_identity (name) values ('identity')");
            Assert.True(id > 0);
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

    [LiveDatabaseFact("firebird", "firebird", 3050)]
    public void LivePageSplitTest()
    {
        DAL.AddConnStr("Firebird_Live", _ConnStr, null, "Firebird");
        var dal = DAL.Create("Firebird_Live");

        using var meta = dal.Db.CreateMetaData();
        var table = CreateUserTable("xcfb_page");
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
                dal.Execute($"insert into xcfb_page (name) values ('page-{i}')");
            }

            const String sql = "select id, name from xcfb_page order by id";
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

    [Fact]
    public void ConnectionStringTest()
    {
        DAL.AddConnStr("firebird_test", "Database=..\\Data\\firebird_test.fdb;User=SYSDBA;Password=masterkey", null, "Firebird");
        var dal = DAL.Create("firebird_test");
        Assert.Equal(DatabaseType.Firebird, dal.DbType);

        // Database 表示数据库文件，解析为绝对路径
        Assert.NotNull(dal.Db.DatabaseName);
        Assert.EndsWith("firebird_test.fdb", dal.Db.DatabaseName!);
    }

    [Fact]
    public void CreateTableSqlTest()
    {
        var db = DbFactory.Create(DatabaseType.Firebird)!;

        var sql = db.CreateMetaData().GetSchemaSQL(DDLSchema.CreateTable, CreateTable())!;

        Assert.StartsWith("Create Table", sql);
        Assert.Contains("TESTUSER", sql);
        Assert.Contains("INTEGER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VARCHAR(50)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Generated By Default As Identity", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Primary Key", sql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>构造真库测试表：自增主键 + 50 长度字符串字段。表名带 xcfb 前缀避免撞库</summary>
    private static IDataTable CreateUserTable(String tableName)
    {
        var table = DAL.CreateTable();
        table.Name = tableName;
        table.TableName = tableName;
        table.DbType = DatabaseType.Firebird;

        var id = table.CreateColumn();
        id.Name = "Id";
        id.ColumnName = "Id";
        id.DataType = typeof(Int32);
        id.Identity = true;
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

    /// <summary>构造模型表：自增主键 + 50 长度字符串字段。Firebird 默认大写折叠</summary>
    private static IDataTable CreateTable()
    {
        var table = DAL.CreateTable();
        table.Name = "TestUser";
        table.TableName = "TestUser";
        table.DbType = DatabaseType.Firebird;

        var id = table.CreateColumn();
        id.Name = "Id";
        id.ColumnName = "Id";
        id.DataType = typeof(Int32);
        id.Identity = true;
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

    /// <summary>Firebird 3+ 的字段元数据用 IS_IDENTITY 标识自增列，需映射到 Identity</summary>
    [Fact]
    public void GetFieldsMapsIsIdentity()
    {
        var db = DbFactory.Create(DatabaseType.Firebird)!;
        var meta = db.CreateMetaData();

        var dt = new DataTable();
        dt.Columns.Add("COLUMN_NAME", typeof(String));
        dt.Columns.Add("DATA_TYPE", typeof(String));
        dt.Columns.Add("IS_IDENTITY", typeof(String));
        dt.Rows.Add("Id", "INTEGER", "YES");
        dt.Rows.Add("Name", "VARCHAR", "NO");

        var table = DAL.CreateTable();
        table.TableName = "TestUser";
        table.DbType = DatabaseType.Firebird;

        var mi = meta.GetType().GetMethod("GetFields", BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(IDataTable), typeof(DataRow[])], null);
        Assert.NotNull(mi);

        var list = (System.Collections.Generic.List<IDataColumn>)mi!.Invoke(meta, [table, dt.Select()])!;
        Assert.True(list.First(e => e.ColumnName == "Id").Identity);
        Assert.False(list.First(e => e.ColumnName == "Name").Identity);
    }

    [Fact]
    [DisplayName("Firebird远程Unix绝对路径_不被解析为本机路径")]
    public void RemoteUnixDatabasePathTest()
    {
        DAL.AddConnStr("Firebird_RemotePath", "Database=/var/lib/firebird/3.0/data/test.fdb;DataSource=127.0.0.1;User=SYSDBA;Password=masterkey", null, "Firebird");
        var dal = DAL.Create("Firebird_RemotePath");

        Assert.Contains("/var/lib/firebird/3.0/data/test.fdb", dal.Db.ConnectionString);
        Assert.Equal("/var/lib/firebird/3.0/data/test.fdb", dal.Db.DatabaseName);
    }
}
