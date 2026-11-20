using System;
using System.Data;
using System.Linq;
using NewLife;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>Presto/Trino 数据库测试。两者均为联邦查询引擎，无事务、无自增主键、不支持自动建表</summary>
/// <remarks>Presto 真库用例优先读取环境变量 XCode_presto，其次读取测试目录下的 Config/presto.config，内容为驱动连接串</remarks>
[Collection("Database")]
public class PrestoTests
{
    private const String DefaultTrinoConnStr = "Host=localhost;Port=8080;Catalog=tpch;Schema=tiny;User=user";
    private const String DefaultPrestoConnStr = "Host=localhost;Port=8080;Catalog=hive;Schema=default;User=user";

    [LiveDatabaseFact("trino", "trino", 8080)]
    public void TrinoConnectTest()
    {
        var connStr = LiveDatabase.Resolve("trino", "trino", DefaultTrinoConnStr);
        var db = DbFactory.Create(DatabaseType.Trino);
        Assert.NotNull(db);

        db.ConnectionString = connStr;
        using var conn = db.OpenConnection();
        Assert.NotNull(conn);
    }

    [LiveDatabaseFact("trino", "trino", 8080)]
    public void TrinoSelectOneTest()
    {
        var connStr = LiveDatabase.Resolve("trino", "trino", DefaultTrinoConnStr);
        DAL.AddConnStr("Trino_Live", connStr, null, "Trino");
        var dal = DAL.Create("Trino_Live");
        Assert.Equal(DatabaseType.Trino, dal.DbType);

        // 只读冒烟：不建表、不写数据
        var dt = dal.Query("select 1 as n");
        Assert.NotNull(dt);
        Assert.Equal(1, dt.Rows.Count);
        Assert.Equal(1, dt.Rows[0][0].ToInt());
    }

    [LiveDatabaseFact("trino", "trino", 8080)]
    public void TrinoNationTest()
    {
        var connStr = LiveDatabase.Resolve("trino", "trino", DefaultTrinoConnStr);
        DAL.AddConnStr("Trino_Live", connStr, null, "Trino");
        var dal = DAL.Create("Trino_Live");

        // tpch 连接器的 tiny 数据集是 Trino 内置样例，可直接用于联邦只读冒烟
        var dt = dal.Query("select count(*) from tpch.tiny.nation");
        Assert.NotNull(dt);
        Assert.Equal(1, dt.Rows.Count);
        Assert.True(dt.Rows[0][0].ToInt() > 0);
    }

    [LiveDatabaseFact("trino", "trino", 8080)]
    public void TrinoGetTablesTest()
    {
        var connStr = LiveDatabase.Resolve("trino", "trino", DefaultTrinoConnStr);
        DAL.AddConnStr("Trino_Live", connStr, null, "Trino");
        var dal = DAL.Create("Trino_Live");

        // 反向工程走 information_schema，连接串 Schema=tiny 时只取该模式
        using var meta = dal.Db.CreateMetaData();
        var tables = meta.GetTables();
        Assert.True(tables.Count > 0);
        Assert.Contains(tables, e => e.TableName.EqualIgnoreCase("nation"));

        var tb = tables.FirstOrDefault(e => e.TableName.EqualIgnoreCase("nation"));
        Assert.NotNull(tb);
        Assert.True(tb!.Columns.Count > 0);
    }

    [LiveDatabaseFact("presto", "presto", 8080)]
    public void PrestoConnectTest()
    {
        var connStr = PrestoConnStr();
        var db = DbFactory.Create(DatabaseType.Presto);
        Assert.NotNull(db);

        db.ConnectionString = connStr;
        using var conn = db.OpenConnection();
        Assert.Equal(ConnectionState.Open, conn.State);
    }

    [LiveDatabaseFact("presto", "presto", 8080)]
    public void PrestoSelectOneTest()
    {
        var dal = PrestoDal("Presto_Live");
        Assert.Equal(DatabaseType.Presto, dal.DbType);

        // 只读冒烟：不建表、不写数据
        var dt = dal.Query("select 1 as n");
        Assert.NotNull(dt);
        Assert.Equal(1, dt.Rows.Count);
        Assert.Equal(1, dt.Rows[0][0].ToInt());
    }

    [LiveDatabaseFact("presto", "presto", 8080)]
    public void PrestoServerInfoTest()
    {
        var dal = PrestoDal("Presto_Live");

        // system.runtime.nodes 是 Presto 内置运行时表，可验证系统目录访问
        var dt = dal.Query("select node_version from system.runtime.nodes limit 1");
        Assert.NotNull(dt);
        Assert.Equal(1, dt.Rows.Count);

        var version = dt.Rows[0][0] + "";
        Assert.False(version.IsNullOrEmpty());
        XTrace.WriteLine("Presto node_version={0}", version);
    }

    [LiveDatabaseFact("presto", "presto", 8080)]
    public void PrestoGetTablesTest()
    {
        var dal = PrestoDal("Presto_Live");

        // 反向工程走 information_schema，连接串 Schema 有值时只取该模式
        using var meta = dal.Db.CreateMetaData();
        var names = meta.GetTableNames();
        Assert.NotNull(names);
        XTrace.WriteLine("Presto table names={0}", names.Count);

        var tables = meta.GetTables();
        Assert.NotNull(tables);
        XTrace.WriteLine("Presto tables={0}", tables.Count);

        foreach (var item in tables.Take(10))
        {
            XTrace.WriteLine("{0}.{1} columns={2}", item.Owner, item.TableName, item.Columns.Count);
        }

        // 无法解析的坏表（如 Hive 视图）会被跳过，其余表必须带模式和字段信息
        if (names.Count > 0) Assert.True(tables.Count > 0);
        Assert.All(tables, e => Assert.False(e.TableName.IsNullOrEmpty()));
        Assert.All(tables, e => Assert.False(e.Owner.IsNullOrEmpty()));

        var table = tables.FirstOrDefault(e => e.Columns.Count > 0);
        if (table != null) Assert.All(table.Columns, e => Assert.False(e.ColumnName.IsNullOrEmpty()));
    }

    [LiveDatabaseFact("presto", "presto", 8080)]
    public void PrestoQueryTableTest()
    {
        var dal = PrestoDal("Presto_Live");

        // SHOW TABLES 只查当前模式，比 information_schema 更少依赖 Metastore 能力
        var dt = dal.Query("show tables");
        Assert.NotNull(dt);

        var names = dt.Rows.Select(e => e[0] + "").Where(e => !e.IsNullOrEmpty()).Take(10).ToArray();
        if (names.Length == 0) return; // 空库时跳过，连接与元数据已由其它用例覆盖

        // 依次尝试前几张表，跳过个别无法读取的坏表；任意一张能读到即视为数据访问正常
        foreach (var name in names)
        {
            var sql = $"select * from {QuoteName(name)} limit 1";
            try
            {
                var table = dal.Query(sql);
                Assert.NotNull(table);
                Assert.True(table.Rows.Count <= 1);
                XTrace.WriteLine("Presto query {0} rows={1}", sql, table.Rows.Count);

                return;
            }
            catch (Exception ex)
            {
                XTrace.WriteLine("Presto query {0} skipped: {1}", sql, ex.Message);
            }
        }

        Assert.True(false, "Presto 无法读取任何一张表，请检查连接串指向的 Catalog/Schema");
    }

    [Theory]
    [InlineData(DatabaseType.Presto)]
    [InlineData(DatabaseType.Trino)]
    public void InitTest(DatabaseType dbType)
    {
        var db = DbFactory.Create(dbType);
        Assert.NotNull(db);
        Assert.Equal(dbType, db.Type);
    }

    [Fact]
    public void SupportTest()
    {
        var presto = DbFactory.Create(DatabaseType.Presto);
        Assert.True(presto.Support("Presto"));
        Assert.True(presto.Support("NReco.PrestoAdo"));
        Assert.False(presto.Support("Trino"));

        var trino = DbFactory.Create(DatabaseType.Trino);
        Assert.True(trino.Support("Trino"));
        Assert.True(trino.Support("Trino.Data.ADO"));
        Assert.False(trino.Support("Presto"));
    }

    [Theory]
    [InlineData(DatabaseType.Presto)]
    [InlineData(DatabaseType.Trino)]
    public void CapabilityTest(DatabaseType dbType)
    {
        var db = DbFactory.Create(dbType);
        Assert.NotNull(db);

        // 不自动建表、不启用参数化、不做批量
        Assert.Equal(Migration.Off, db.EffectiveMigration);
        Assert.False(db.UseParameter);
        Assert.True(db.Capabilities.HasFlag(DatabaseCapability.Sql));
        Assert.False(db.Capabilities.HasFlag(DatabaseCapability.Transactions));
        Assert.False(db.Capabilities.HasFlag(DatabaseCapability.Ddl));
        Assert.False(db.Capabilities.HasFlag(DatabaseCapability.BulkInsert));
    }

    [Theory]
    [InlineData(DatabaseType.Presto)]
    [InlineData(DatabaseType.Trino)]
    public void FormatKeyWordTest(DatabaseType dbType)
    {
        var db = (DbBase)DbFactory.Create(dbType)!;

        Assert.Equal("\"TableName\"", db.FormatKeyWord("TableName"));
        Assert.Equal("\"AlreadyQuoted\"", db.FormatKeyWord("\"AlreadyQuoted\""));
        Assert.Equal("\"a\"\"b\"", db.FormatKeyWord("a\"b"));
    }

    [Theory]
    [InlineData(DatabaseType.Presto)]
    [InlineData(DatabaseType.Trino)]
    public void FormatValueTest(DatabaseType dbType)
    {
        var db = (DbBase)DbFactory.Create(dbType)!;

        var table = DAL.CreateTable();
        var col = table.CreateColumn();
        col.DataType = typeof(Boolean);

        Assert.Equal("true", db.FormatValue(col, true));
        Assert.Equal("false", db.FormatValue(col, false));

        var bin = table.CreateColumn();
        bin.DataType = typeof(Byte[]);
        Assert.Equal("X'0A0B'", db.FormatValue(bin, new Byte[] { 0x0A, 0x0B }));
    }

    [Theory]
    [InlineData(DatabaseType.Presto)]
    [InlineData(DatabaseType.Trino)]
    public void FormatDateTimeTest(DatabaseType dbType)
    {
        var db = (DbBase)DbFactory.Create(dbType)!;

        var text = db.FormatDateTime(new DateTime(2024, 1, 2, 3, 4, 5));
        Assert.Equal("TIMESTAMP '2024-01-02 03:04:05'", text);
    }

    [Theory]
    [InlineData(DatabaseType.Presto)]
    [InlineData(DatabaseType.Trino)]
    public void PageSplitTest(DatabaseType dbType)
    {
        var db = DbFactory.Create(dbType);

        Assert.Equal("SELECT * FROM test limit 10", db.PageSplit("SELECT * FROM test", 0, 10, null));
        Assert.Equal("SELECT * FROM test offset 10 limit 10", db.PageSplit("SELECT * FROM test", 10, 10, null));
    }

    [Theory]
    [InlineData(DatabaseType.Presto)]
    [InlineData(DatabaseType.Trino)]
    public void StringConcatTest(DatabaseType dbType)
    {
        var db = (DbBase)DbFactory.Create(dbType)!;

        Assert.Equal("concat(a,b)", db.StringConcat("a", "b"));
        Assert.Equal("concat('',b)", db.StringConcat(null, "b"));
    }

    [Fact]
    public void ConnectionStringTest()
    {
        DAL.AddConnStr("presto_test", "Host=localhost;Database=hive;Owner=myschema;User=user", null, "Presto");
        var dal = DAL.Create("presto_test");
        Assert.Equal(DatabaseType.Presto, dal.DbType);

        var connStr = dal.Db.ConnectionString;
        Assert.Contains("Catalog=hive", connStr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Schema=myschema", connStr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Port=8080", connStr, StringComparison.OrdinalIgnoreCase);

        DAL.AddConnStr("trino_test", "Host=localhost;Database=hive;Owner=myschema;User=user", null, "Trino");
        var dal2 = DAL.Create("trino_test");
        Assert.Equal(DatabaseType.Trino, dal2.DbType);

        var connStr2 = dal2.Db.ConnectionString;
        Assert.Contains("Catalog=hive", connStr2, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Schema=myschema", connStr2, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Port=8080", connStr2, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EnableSSL=false", connStr2, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(DatabaseType.Presto)]
    [InlineData(DatabaseType.Trino)]
    public void DdlNotSupportedTest(DatabaseType dbType)
    {
        var db = DbFactory.Create(dbType);
        var meta = db.CreateMetaData();

        var table = DAL.CreateTable();
        table.TableName = "test";

        Assert.Throws<NotSupportedException>(() => meta.CreateTable(table));
    }

    [Theory]
    [InlineData(DatabaseType.Presto)]
    [InlineData(DatabaseType.Trino)]
    public void IdentityAndTransactionNotSupportedTest(DatabaseType dbType)
    {
        var db = DbFactory.Create(dbType);
        db.ConnectionString = "Host=localhost";

        var session = db.CreateSession();

        // 无自增列，进入自增路径时给出明确异常
        Assert.Throws<NotSupportedException>(() => session.InsertAndGetIdentity("insert into t(a) values(1)"));

        // 无事务
        Assert.Throws<NotSupportedException>(() => session.BeginTransaction(System.Data.IsolationLevel.ReadCommitted));
    }

    /// <summary>解析 Presto 连接串。优先级：环境变量 XCode_presto → Config/presto.config → 本机默认值</summary>
    /// <returns></returns>
    private static String PrestoConnStr() => LiveDatabase.Resolve("presto", "presto", DefaultPrestoConnStr);

    private static DAL PrestoDal(String name)
    {
        DAL.AddConnStr(name, PrestoConnStr(), null, "Presto");

        return DAL.Create(name);
    }

    private static String QuoteName(String? name) => "\"" + (name + "").Replace("\"", "\"\"") + "\"";
}
