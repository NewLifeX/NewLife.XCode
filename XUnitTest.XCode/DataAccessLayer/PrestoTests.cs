using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>Presto/Trino 数据库测试。两者均为联邦查询引擎，无事务、无自增主键、不支持自动建表</summary>
/// <remarks>Presto 真库用例优先读取环境变量 XCode_presto，其次读取测试目录下的 Config/presto.config，内容为驱动连接串。
/// 本地探测按 /v1/info 的 nodeVersion 确认服务身份，避免 8080 端口被其它 Web 服务占用时误跑真库用例</remarks>
[Collection("Database")]
public class PrestoTests
{
    private const String DefaultTrinoConnStr = "Host=localhost;Port=8080;Catalog=tpch;Schema=tiny;User=user";
    private const String DefaultPrestoConnStr = "Host=localhost;Port=8080;Catalog=hive;Schema=default;User=user";

    [LiveDatabaseFact("trino", "trino", 8080, "/v1/info", "nodeVersion")]
    public void TrinoConnectTest()
    {
        var connStr = LiveDatabase.Resolve("trino", "trino", DefaultTrinoConnStr);
        var db = DbFactory.Create(DatabaseType.Trino);
        Assert.NotNull(db);

        db.ConnectionString = connStr;
        using var conn = db.OpenConnection();
        Assert.NotNull(conn);
    }

    [LiveDatabaseFact("trino", "trino", 8080, "/v1/info", "nodeVersion")]
    public void TrinoSelectOneTest()
    {
        var connStr = LiveDatabase.Resolve("trino", "trino", DefaultTrinoConnStr);
        DAL.AddConnStr("Trino_Live", connStr, null, "Trino");
        var dal = DAL.Create("Trino_Live");
        Assert.Equal(DatabaseType.Trino, dal.DbType);

        // 只读冒烟：不建表、不写数据
        var dt = dal.Query("select 1 as n");
        Assert.NotNull(dt);
        Assert.Single(dt.Rows);
        Assert.Equal(1, dt.Rows[0][0].ToInt());
    }

    [LiveDatabaseFact("trino", "trino", 8080, "/v1/info", "nodeVersion")]
    public void TrinoNationTest()
    {
        var connStr = LiveDatabase.Resolve("trino", "trino", DefaultTrinoConnStr);
        DAL.AddConnStr("Trino_Live", connStr, null, "Trino");
        var dal = DAL.Create("Trino_Live");

        // tpch 连接器的 tiny 数据集是 Trino 内置样例，可直接用于联邦只读冒烟
        var dt = dal.Query("select count(*) from tpch.tiny.nation");
        Assert.NotNull(dt);
        Assert.Single(dt.Rows);
        Assert.True(dt.Rows[0][0].ToInt() > 0);
    }

    [LiveDatabaseFact("trino", "trino", 8080, "/v1/info", "nodeVersion")]
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

    [LiveDatabaseFact("presto", "presto", 8080, "/v1/info", "nodeVersion")]
    public void PrestoConnectTest()
    {
        var connStr = PrestoConnStr();
        var db = DbFactory.Create(DatabaseType.Presto);
        Assert.NotNull(db);

        db.ConnectionString = connStr;
        using var conn = db.OpenConnection();
        Assert.Equal(ConnectionState.Open, conn.State);
    }

    [LiveDatabaseFact("presto", "presto", 8080, "/v1/info", "nodeVersion")]
    public void PrestoSelectOneTest()
    {
        var dal = PrestoDal("Presto_Live");
        Assert.Equal(DatabaseType.Presto, dal.DbType);

        // 只读冒烟：不建表、不写数据
        var dt = dal.Query("select 1 as n");
        Assert.NotNull(dt);
        Assert.Single(dt.Rows);
        Assert.Equal(1, dt.Rows[0][0].ToInt());
    }

    [LiveDatabaseFact("presto", "presto", 8080, "/v1/info", "nodeVersion")]
    public void PrestoServerInfoTest()
    {
        var dal = PrestoDal("Presto_Live");

        // system.runtime.nodes 是 Presto 内置运行时表，可验证系统目录访问
        var dt = dal.Query("select node_version from system.runtime.nodes limit 1");
        Assert.NotNull(dt);
        Assert.Single(dt.Rows);

        var version = dt.Rows[0][0] + "";
        Assert.False(version.IsNullOrEmpty());
        XTrace.WriteLine("Presto node_version={0}", version);
    }

    [LiveDatabaseFact("presto", "presto", 8080, "/v1/info", "nodeVersion")]
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

    [LiveDatabaseFact("presto", "presto", 8080, "/v1/info", "nodeVersion")]
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

        Assert.Fail("Presto 无法读取任何一张表，请检查连接串指向的 Catalog/Schema");
    }

    [LiveDatabaseFact("presto", "presto", 8080, "/v1/info", "nodeVersion")]
    public void PrestoParameterTest()
    {
        var dal = PrestoDal("Presto_Live");

        // NewLife.Presto 在客户端把 @name 安全替换为字面量，验证参数化查询可用
        var dt = dal.Query("select @a as a, @b as b, @c as c", new Dictionary<String, Object>
        {
            ["a"] = 123,
            ["b"] = "it's ok",
            ["c"] = new DateTime(2024, 1, 2, 3, 4, 5),
        });

        Assert.NotNull(dt);
        Assert.Single(dt.Rows);
        Assert.Equal(123, dt.Rows[0][0].ToInt());
        Assert.Equal("it's ok", dt.Rows[0][1] + "");

        // 单引号必须被转义，否则会拼出非法 SQL
        var safe = dal.Query("select @s as s", new Dictionary<String, Object> { ["s"] = "a'; drop table t; --" });
        Assert.Equal("a'; drop table t; --", safe.Rows[0][0] + "");
    }

    [LiveDatabaseFact("presto", "presto", 8080, "/v1/info", "nodeVersion")]
    public async Task PrestoAsyncQueryTest()
    {
        var dal = PrestoDal("Presto_Live");

        var dt = await dal.QueryAsync("select 1 as n, '中文' as s");
        Assert.NotNull(dt);
        Assert.Single(dt.Rows);
        Assert.Equal(1, dt.Rows[0][0].ToInt());
        Assert.Equal("中文", dt.Rows[0][1] + "");
    }

    [LiveDatabaseFact("presto", "presto", 8080, "/v1/info", "nodeVersion")]
    public void PrestoDataSetTest()
    {
        var dal = PrestoDal("Presto_Live");

        // DataSet 走驱动的 DataAdapter，验证字符串列不会被判为超长（旧 NReco 驱动的 ColumnSize 问题）
        var dt = dal.Select("select cast('abc' as varchar) as s, 1 as n");
        Assert.NotNull(dt);
        Assert.Equal(1, dt.Tables[0].Rows.Count);
        Assert.Equal("abc", dt.Tables[0].Rows[0]["s"] + "");
        Assert.Equal(typeof(String), dt.Tables[0].Columns["s"].DataType);
    }

    [LiveDatabaseFact("presto", "presto", 8080, "/v1/info", "nodeVersion")]
    public void PrestoPagingTest()
    {
        var dal = PrestoDal("Presto_Live");

        var sql = dal.Db.PageSplit("select nationkey from tpch.tiny.nation", 2, 3, null);
        Assert.Contains("offset 2", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("limit 3", sql, StringComparison.OrdinalIgnoreCase);

        // tpch 内置数据集不受业务库权限限制，可稳定验证分页与多行读取
        var dt = dal.Query(sql);
        Assert.NotNull(dt);
        Assert.Equal(3, dt.Rows.Count);
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
        Assert.True(presto.Support("NewLife.Presto"));
        Assert.False(presto.Support("Trino"));

        var trino = DbFactory.Create(DatabaseType.Trino);
        Assert.True(trino.Support("Trino"));
        Assert.False(trino.Support("Presto"));
    }

    [Theory]
    [InlineData(DatabaseType.Presto)]
    [InlineData(DatabaseType.Trino)]
    public void CapabilityTest(DatabaseType dbType)
    {
        var db = DbFactory.Create(dbType);
        Assert.NotNull(db);

        // 不自动建表、不做批量；NewLife.Presto 支持 @ 参数在客户端安全替换
        Assert.Equal(Migration.Off, db.EffectiveMigration);
        Assert.True(db.UseParameter);
        Assert.True(db.Capabilities.HasFlag(DatabaseCapability.Sql));
        Assert.False(db.Capabilities.HasFlag(DatabaseCapability.Transactions));
        Assert.False(db.Capabilities.HasFlag(DatabaseCapability.Ddl));
        Assert.False(db.Capabilities.HasFlag(DatabaseCapability.BulkInsert));
    }

    [Theory]
    [DisplayName("Arrays 开关启用数组读取并校验 ComplexTypes")]
    [InlineData(DatabaseType.Presto)]
    [InlineData(DatabaseType.Trino)]
    public void ArraySwitchTest(DatabaseType dbType)
    {
        var db = DbFactory.Create(dbType);
        Assert.NotNull(db);

        // 默认开启数组强类型
        db.ConnectionString = "Host=localhost;Port=8080;Catalog=hive;Schema=default";
        Assert.True(((PrestoBase)db).EnableArrays);
        Assert.True(db.Supports(DatabaseCapability.Array));

        // 显式 Arrays=On 与默认一致
        db.ConnectionString = "Host=localhost;Port=8080;Catalog=hive;Schema=default;Arrays=On";
        Assert.True(((PrestoBase)db).EnableArrays);
        Assert.True(db.Supports(DatabaseCapability.Array));

        // Arrays=Off 退回驱动默认的 JSON 文本，能力位随之撤销
        db.ConnectionString = "Host=localhost;Port=8080;Catalog=hive;Schema=default;Arrays=Off";
        Assert.False(((PrestoBase)db).EnableArrays);
        Assert.False(db.Supports(DatabaseCapability.Array));

        // 只声明 ComplexTypes=String 视为调用方要 JSON 文本，自动关闭数组
        db.ConnectionString = "Host=localhost;Port=8080;Catalog=hive;Schema=default;ComplexTypes=String";
        Assert.False(((PrestoBase)db).EnableArrays);
        Assert.False(db.Supports(DatabaseCapability.Array));

        // 显式把 ComplexTypes 设为 String 与 Arrays 冲突，必须尽早失败
        Assert.Throws<XCodeException>(() => db.ConnectionString = "Host=localhost;Port=8080;Arrays=On;ComplexTypes=String");

        // 数组字段使用方言字面量写入，而不是参数绑定
        var column = DAL.CreateTable().CreateColumn();
        column.DataType = typeof(String[]);
        var sql = db.FormatValue(column, new[] { "a" });
        Assert.Contains("CAST(ARRAY[", sql);
        Assert.Contains("array(varchar)", sql);

        // 反向工程的 array(decimal(p,s)) 保留精度
        var amount = DAL.CreateTable().CreateColumn();
        amount.DataType = typeof(Decimal[]);
        amount.RawType = "array(decimal(10,2))";
        Assert.Contains("array(decimal(10,2))", db.FormatValue(amount, new[] { 1.25m }));

        Assert.Throws<NotSupportedException>(() => db.CreateParameter("p", new[] { "a" }, column));
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
        Assert.Contains("UseSsl=false", connStr2, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Protocol=Trino", connStr2, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProtocolHeaderTest()
    {
        // Presto 与 Trino 共用 NewLife.Presto，仅协议头不同
        DAL.AddConnStr("presto_protocol", "Host=localhost;Catalog=hive", null, "Presto");
        DAL.AddConnStr("trino_protocol", "Host=localhost;Catalog=hive", null, "Trino");
        DAL.AddConnStr("trino_legacy", "Host=localhost;Catalog=hive;EnableSSL=true;TrinoHeaders=true", null, "Trino");

        Assert.Contains("Protocol=Presto", DAL.Create("presto_protocol").Db.ConnectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Protocol=Trino", DAL.Create("trino_protocol").Db.ConnectionString, StringComparison.OrdinalIgnoreCase);

        // 旧写法 EnableSSL/TrinoHeaders 归一化为 UseSsl/Protocol
        var connStr = DAL.Create("trino_legacy").Db.ConnectionString;
        Assert.Contains("UseSsl=true", connStr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Protocol=Trino", connStr, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EnableSSL", connStr, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TrinoHeaders", connStr, StringComparison.OrdinalIgnoreCase);
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

    [Theory]
    [InlineData("Off", "Off")]
    [InlineData("none", "Off")]
    [InlineData("false", "Off")]
    [InlineData("Stats", "Stats")]
    [InlineData("statistics", "Stats")]
    [InlineData("Iceberg", "Iceberg")]
    [InlineData("Auto", "Auto")]
    [InlineData("true", "Auto")]
    [InlineData("unknown", "Auto")]
    public void FastCountModeTest(String value, String expected)
    {
        var db = DbFactory.Create(DatabaseType.Presto);
        db.ConnectionString = $"Host=localhost;Port=8080;Catalog=tpch;Schema=tiny;User=test;FastCount={value}";

        var presto = Assert.IsType<Presto>(db);
        Assert.Equal(expected, presto.FastCount.ToString());

        // FastCount 是 XCode 自身开关，不能传给底层驱动
        Assert.False(db.ConnectionString.Contains("FastCount", StringComparison.OrdinalIgnoreCase));
    }

    [LiveDatabaseFact("trino", "trino", 8080, "/v1/info", "nodeVersion")]
    public async Task TrinoFastCountStatsTest()
    {
        var connStr = LiveDatabase.Resolve("trino", "trino", DefaultTrinoConnStr);
        var builder = new ConnectionStringBuilder(connStr);
        builder["FastCount"] = "Stats";
        DAL.AddConnStr("Trino_FastStats", builder.ConnectionString, null, "Trino");
        var dal = DAL.Create("Trino_FastStats");
        using var session = (DbSession)dal.Session;

        Assert.Equal(25, session.QueryCountFast("tpch.tiny.nation"));
        Assert.Equal(25, await session.QueryCountFastAsync("tpch.tiny.nation"));
    }

    [LiveDatabaseFact("trino", "trino", 8080, "/v1/info", "nodeVersion")]
    public async Task TrinoFastCountAutoTest()
    {
        var connStr = LiveDatabase.Resolve("trino", "trino", DefaultTrinoConnStr);
        var builder = new ConnectionStringBuilder(connStr);
        builder["FastCount"] = "Auto";
        DAL.AddConnStr("Trino_FastAuto", builder.ConnectionString, null, "Trino");
        var dal = DAL.Create("Trino_FastAuto");
        using var session = (DbSession)dal.Session;

        // TPCH 不是 Iceberg，Auto 应回退到 SHOW STATS
        Assert.Equal(25, session.QueryCountFast("tpch.tiny.nation"));
        Assert.Equal(25, await session.QueryCountFastAsync("tpch.tiny.nation"));
    }

    [LiveDatabaseFact("trino", "trino", 8080, "/v1/info", "nodeVersion")]
    public async Task TrinoFastCountIcebergTest()
    {
        var connStr = LiveDatabase.Resolve("trino", "trino", DefaultTrinoConnStr);
        var builder = new ConnectionStringBuilder(connStr);
        builder["Catalog"] = "memory";
        builder["Schema"] = "default";
        builder["FastCount"] = "Iceberg";
        DAL.AddConnStr("Trino_FastIceberg", builder.ConnectionString, null, "Trino");
        var dal = DAL.Create("Trino_FastIceberg");
        using var session = (DbSession)dal.Session;

        // 用 memory 连接器模拟 Iceberg 元数据表，验证 SQL、目录/模式解析与同步/异步取值
        var table = "ft_" + Guid.NewGuid().ToString("N")[..8];
        var snapshots = $"memory.default.\"{table}$snapshots\"";
        var refs = $"memory.default.\"{table}$refs\"";
        try
        {
            session.Execute($"CREATE TABLE {snapshots} (snapshot_id bigint, summary map(varchar, varchar))");
            session.Execute($"CREATE TABLE {refs} (name varchar, snapshot_id bigint)");
            session.Execute($"INSERT INTO {snapshots} VALUES (1, MAP(ARRAY['total-records', 'total-delete-files'], ARRAY['7', '0']))");
            session.Execute($"INSERT INTO {refs} VALUES ('main', 1)");

            Assert.Equal(7, session.QueryCountFast($"\"{table}\""));
            Assert.Equal(7, await session.QueryCountFastAsync($"\"{table}\""));
        }
        finally
        {
            try { session.Execute($"DROP TABLE IF EXISTS {snapshots}"); } catch { }
            try { session.Execute($"DROP TABLE IF EXISTS {refs}"); } catch { }
        }
    }

    [LiveDatabaseFact("presto", "presto", 8081, "/v1/info", "nodeVersion")]
    public async Task PrestoFastCountStatsTest()
    {
        var connStr = PrestoConnStr();
        // 只在 tpch/tiny 数据集上断言，避免依赖其它 catalog 的 nation 表
        if (!connStr.Contains("Catalog=tpch", StringComparison.OrdinalIgnoreCase) ||
            !connStr.Contains("Schema=tiny", StringComparison.OrdinalIgnoreCase)) return;

        DAL.AddConnStr("Presto_FastStats", connStr, null, "Presto");
        var dal = DAL.Create("Presto_FastStats");
        using var session = (DbSession)dal.Session;

        Assert.Equal(25, session.QueryCountFast("tpch.tiny.nation"));
        Assert.Equal(25, await session.QueryCountFastAsync("tpch.tiny.nation"));
    }

    [LiveDatabaseFact("presto", "presto", 8080, "/v1/info", "nodeVersion")]
    public void PrestoSelectBuilderTest()
    {
        var dal = PrestoDal("Presto_Live");

        // 走 XCode 的查询生成器与分页方言
        var sb = new SelectBuilder
        {
            Column = "nationkey, name",
            Table = "tpch.tiny.nation",
            OrderBy = "nationkey",
        };

        var dt = dal.Query(sb, 0, 5);
        Assert.NotNull(dt);
        Assert.Equal(5, dt.Rows.Count);
        Assert.Equal(0, dt.Rows[0][0].ToInt());
        Assert.Equal("ALGERIA", dt.Rows[0][1] + "");

        Assert.Equal(25, dal.SelectCount(sb));
    }

    [LiveDatabaseFact("presto", "presto", 8080, "/v1/info", "nodeVersion")]
    public void PrestoCommentsTest()
    {
        // information_schema 不带注释，反向工程时自动通过 DESCRIBE 补齐
        DAL.AddConnStr("Presto_Comments", PrestoConnStr(), null, "Presto");
        using var meta = DAL.Create("Presto_Comments").Db.CreateMetaData();

        var tables = meta.GetTables();
        var table = tables.FirstOrDefault(e => e.Columns.Any(c => !c.Description.IsNullOrEmpty()));
        if (table == null) return; // 当前模式没有带注释的表

        foreach (var column in table.Columns)
            XTrace.WriteLine("Presto comment {0}.{1} = {2}", table.TableName, column.ColumnName, column.Description);
    }

    [Fact]
    public void PrimaryKeyConventionTest()
    {
        // Hive 不返回主键约束，约定把首个 id 列当作主键
        var table = DAL.CreateTable();
        table.TableName = "test";
        var name = table.CreateColumn();
        name.ColumnName = "name";
        name.DataType = typeof(String);
        var id = table.CreateColumn();
        id.ColumnName = "id";
        id.DataType = typeof(Int64);
        table.Columns.Add(name);
        table.Columns.Add(id);

        PrestoMetaData.FixPrimaryKey(table);

        Assert.Single(table.PrimaryKeys);
        Assert.Equal("id", table.PrimaryKeys[0].ColumnName);

        // 没有 id 列的表不臆造主键
        var table2 = DAL.CreateTable();
        table2.TableName = "test2";
        var code = table2.CreateColumn();
        code.ColumnName = "code";
        code.DataType = typeof(String);
        table2.Columns.Add(code);

        PrestoMetaData.FixPrimaryKey(table2);

        Assert.Empty(table2.PrimaryKeys);
    }

    [Fact]
    public void PartitionIndexTest()
    {
        var table = DAL.CreateTable();
        table.Name = "PTest";
        table.TableName = "p_test";
        var id = table.CreateColumn();
        id.Name = "Id";
        id.ColumnName = "id";
        id.DataType = typeof(Int64);
        var ds = table.CreateColumn();
        ds.Name = "Ds";
        ds.ColumnName = "ds";
        ds.DataType = typeof(String);
        table.Columns.Add(id);
        table.Columns.Add(ds);

        PrestoMetaData.AddPartitionIndex(table, ["ds"]);

        var index = Assert.Single(table.Indexes);
        Assert.Equal("IX_PTest_Ds", index.Name);
        Assert.Equal(["Ds"], index.Columns);
        Assert.False(index.Unique);

        // 重复添加不会产生第二个索引
        PrestoMetaData.AddPartitionIndex(table, ["ds"]);
        Assert.Single(table.Indexes);
    }

    [LiveDatabaseFact("presto", "presto", 8080, "/v1/info", "nodeVersion")]
    public void PrestoMetadataCompletenessTest()
    {
        // 反向工程读不到字段的表（元数据损坏的视图等）应被跳过，避免生成空模型
        var dal = PrestoDal("Presto_Meta");

        using var meta = dal.Db.CreateMetaData();
        var tables = meta.GetTables();
        Assert.NotEmpty(tables);
        Assert.DoesNotContain(tables, e => e.Columns.Count == 0);

        // ds 是分区列时自动生成索引，便于按分区过滤
        var example = tables.FirstOrDefault(e => e.TableName.EqualIgnoreCase("p_example"));
        if (example != null)
            Assert.Contains(example.Indexes, e => e.Columns != null && e.Columns.Any(c => c.EqualIgnoreCase("Ds")));
    }

    [LiveDatabaseFact("presto", "presto", 8080, "/v1/info", "nodeVersion")]
    public void PrestoPrimaryKeyAndPartitionTest()
    {
        var dal = PrestoDal("Presto_Live");

        using var meta = dal.Db.CreateMetaData();
        var tables = meta.GetTables();

        // 带 id 列的表应被标记主键
        var withId = tables.FirstOrDefault(e => e.Columns.Any(c => c.ColumnName.EqualIgnoreCase("id")));
        if (withId != null)
        {
            Assert.Single(withId.PrimaryKeys);
            Assert.Equal("id", withId.PrimaryKeys[0].ColumnName, ignoreCase: true);
        }

        // 分区列应生成索引，便于按分区过滤
        var partitioned = tables.FirstOrDefault(e => e.Indexes.Any(i => i.Columns != null && i.Columns.Length == 1 && (i.Columns[0].EqualIgnoreCase("ds") || i.Columns[0].EqualIgnoreCase("dt"))));
        if (partitioned != null)
            XTrace.WriteLine("Presto partition index: {0}.{1}", partitioned.TableName, partitioned.Indexes[0].Name);
    }

    [LiveDatabaseFact("presto", "presto", 8080, "/v1/info", "nodeVersion")]
    public async Task PrestoFastCountLiveTest()
    {
        // tpch 内置数据集始终存在，SHOW STATS 可返回 row_count
        DAL.AddConnStr("Presto_FastStats", PrestoConnStr() + ";FastCount=Stats", null, "Presto");
        var dal = DAL.Create("Presto_FastStats");
        using var session = (DbSession)dal.Session;

        Assert.Equal(25, session.QueryCountFast("tpch.tiny.nation"));
        Assert.Equal(25, await session.QueryCountFastAsync("tpch.tiny.nation"));

        // 非 Iceberg 表没有 $snapshots 元数据表，Auto 模式应回退到 SHOW STATS
        DAL.AddConnStr("Presto_FastAuto", PrestoConnStr(), null, "Presto");
        var dal2 = DAL.Create("Presto_FastAuto");
        using var session2 = (DbSession)dal2.Session;

        Assert.Equal(25, session2.QueryCountFast("tpch.tiny.nation"));

        // 强制 Iceberg 模式时，非 Iceberg 表取不到行数，返回 -1 交由上层精确计数
        DAL.AddConnStr("Presto_FastIceberg", PrestoConnStr() + ";FastCount=Iceberg", null, "Presto");
        Assert.Equal(-1, DAL.Create("Presto_FastIceberg").Session.QueryCountFast("tpch.tiny.nation"));
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
