using System;
using System.ComponentModel;
using System.Net.Http;
using System.Threading;
using NewLife;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>协议别名测试。MySQL/PostgreSQL 协议兼容的数据库直接复用对应提供者</summary>
[Collection("Database")]
public class ProtocolAliasTests
{
    [Theory]
    [InlineData("MariaDB")]
    [InlineData("TiDB")]
    [InlineData("StarRocks")]
    [InlineData("Doris")]
    [InlineData("OceanBase")]
    [InlineData("SingleStore")]
    [InlineData("PolarDB")]
    [InlineData("TDSQL")]
    [InlineData("GoldenDB")]
    [InlineData("GreatDB")]
    [InlineData("Databend")]
    public void MySqlAliasTest(string provider)
    {
        var db = DbFactory.Create(DatabaseType.MySql);
        Assert.True(db.Support(provider));

        Assert.Equal(typeof(MySql), DbFactory.GetProviderType("", provider));
    }

    [Theory]
    [InlineData("openGauss")]
    [InlineData("GaussDB")]
    [InlineData("Greenplum")]
    [InlineData("Redshift")]
    [InlineData("CockroachDB")]
    [InlineData("YugabyteDB")]
    [InlineData("TimescaleDB")]
    [InlineData("QuestDB")]
    [InlineData("GreptimeDB")]
    [InlineData("CrateDB")]
    public void PostgreSqlAliasTest(string provider)
    {
        var db = DbFactory.Create(DatabaseType.PostgreSQL);
        Assert.True(db.Support(provider));

        Assert.Equal(typeof(PostgreSQL), DbFactory.GetProviderType("", provider));
    }

    [Theory]
    [InlineData("HighGo")]
    [InlineData("KingBase")]
    [InlineData("VastBase")]
    public void DedicatedProviderNotHijackedTest(string provider)
    {
        var type = DbFactory.GetProviderType("", provider);
        Assert.NotNull(type);

        // 有专属提供者的库不能被 PostgreSQL 别名抢先匹配
        Assert.NotEqual(typeof(PostgreSQL), type);
    }

    [LiveDatabaseFact("cockroach", "cockroach", 26257)]
    [DisplayName("协议别名_CockroachDB真库_建表写入查询")]
    public void CockroachDbLiveTest()
    {
        var connStr = LiveDatabase.Resolve("cockroach", "cockroach", "Server=127.0.0.1;Port=26257;Database=test;Uid=root;Pwd=");
        DAL.AddConnStr("cockroach_live", connStr, null, "CockroachDB");
        var dal = DAL.Create("cockroach_live");
        Assert.Equal(DatabaseType.PostgreSQL, dal.DbType);

        var table = "xcode_proto_" + Guid.NewGuid().ToString("N");
        try
        {
            dal.Execute($"CREATE TABLE \"{table}\" (\"id\" INT PRIMARY KEY, \"name\" VARCHAR(50))");
            dal.Execute($"INSERT INTO \"{table}\" (\"id\", \"name\") VALUES (1, 'alpha')");

            var dt = dal.Query($"SELECT \"id\", \"name\" FROM \"{table}\"");
            Assert.Single(dt.Rows);
            Assert.Equal("alpha", dt.Rows[0][1] + "");
        }
        finally
        {
            dal.Execute($"DROP TABLE IF EXISTS \"{table}\"");
        }
    }

    [LiveDatabaseFact("questdb", "questdb", 8812)]
    [DisplayName("协议别名_QuestDB真库_建表写入查询")]
    public void QuestDbLiveTest()
    {
        var connStr = LiveDatabase.Resolve("questdb", "questdb", "Server=127.0.0.1;Port=8812;Database=qdb;Uid=admin;Pwd=quest");
        DAL.AddConnStr("questdb_live", connStr, null, "QuestDB");
        var dal = DAL.Create("questdb_live");
        Assert.Equal(DatabaseType.PostgreSQL, dal.DbType);

        var table = "xcode_proto_" + Guid.NewGuid().ToString("N");
        try
        {
            dal.Execute($"CREATE TABLE \"{table}\" (\"id\" INT, \"name\" VARCHAR)");
            dal.Execute($"INSERT INTO \"{table}\" (\"id\", \"name\") VALUES (1, 'alpha')");

            var dt = dal.Query($"SELECT \"id\", \"name\" FROM \"{table}\"");
            Assert.Single(dt.Rows);
            Assert.Equal("alpha", dt.Rows[0][1] + "");
        }
        finally
        {
            dal.Execute($"DROP TABLE IF EXISTS \"{table}\"");
        }
    }

    [LiveDatabaseFact("greptimedb", "greptimedb", 4003)]
    [DisplayName("协议别名_GreptimeDB真库_建表写入查询")]
    public void GreptimeDbLiveTest()
    {
        var connStr = LiveDatabase.Resolve("greptimedb", "greptimedb", "Server=127.0.0.1;Port=4003;Database=public;Uid=greptime;Pwd=");
        DAL.AddConnStr("greptimedb_live", connStr, null, "GreptimeDB");
        var dal = DAL.Create("greptimedb_live");
        Assert.Equal(DatabaseType.PostgreSQL, dal.DbType);

        // 协议兼容层需要自动补充 Npgsql 兼容选项
        Assert.Contains("NoTypeLoading", dal.Db.ConnectionString);
        Assert.Contains("No Reset On Close", dal.Db.ConnectionString);

        var table = "xcode_proto_" + Guid.NewGuid().ToString("N");
        try
        {
            // GreptimeDB 要求建表包含 TIME INDEX 列
            dal.Execute($"CREATE TABLE \"{table}\" (\"ts\" TIMESTAMP TIME INDEX, \"id\" INT, \"name\" STRING)");
            dal.Execute($"INSERT INTO \"{table}\" (\"ts\", \"id\", \"name\") VALUES (now(), 1, 'alpha')");

            var dt = dal.Query($"SELECT \"id\", \"name\" FROM \"{table}\"");
            Assert.Single(dt.Rows);
            Assert.Equal("alpha", dt.Rows[0][1] + "");
        }
        finally
        {
            dal.Execute($"DROP TABLE IF EXISTS \"{table}\"");
        }
    }

    [LiveDatabaseFact("cratedb", "cratedb", 15432)]
    [DisplayName("协议别名_CrateDB真库_建表写入查询")]
    public void CrateDbLiveTest()
    {
        var connStr = LiveDatabase.Resolve("cratedb", "cratedb", "Server=127.0.0.1;Port=15432;Database=doc;Uid=crate;Pwd=");
        DAL.AddConnStr("cratedb_live", connStr, null, "CrateDB");
        var dal = DAL.Create("cratedb_live");
        Assert.Equal(DatabaseType.PostgreSQL, dal.DbType);

        var table = "xcode_proto_" + Guid.NewGuid().ToString("N");
        try
        {
            dal.Execute($"CREATE TABLE \"{table}\" (\"id\" INT, \"name\" STRING)");
            dal.Execute($"INSERT INTO \"{table}\" (\"id\", \"name\") VALUES (1, 'alpha')");
            dal.Execute($"REFRESH TABLE \"{table}\"");

            var dt = dal.Query($"SELECT \"id\", \"name\" FROM \"{table}\"");
            Assert.Single(dt.Rows);
            Assert.Equal("alpha", dt.Rows[0][1] + "");
        }
        finally
        {
            dal.Execute($"DROP TABLE IF EXISTS \"{table}\"");
        }
    }

    [Theory]
    [InlineData("GreptimeDB", 4003)]
    [InlineData("CrateDB", 15432)]
    [DisplayName("协议别名_协议兼容库_自动追加Npgsql兼容选项")]
    public void PostgreSqlAliasCompatibilityTest(String provider, Int32 port)
    {
        var name = "alias_" + provider;
        DAL.AddConnStr(name, $"Server=127.0.0.1;Port={port};Database=public;Uid=u;Pwd=", null, provider);
        var dal = DAL.Create(name);
        Assert.Equal(DatabaseType.PostgreSQL, dal.DbType);

        // 协议兼容层不支持 Npgsql 默认的类型加载与 DISCARD ALL
        Assert.Contains("NoTypeLoading", dal.Db.ConnectionString);
        Assert.Contains("No Reset On Close", dal.Db.ConnectionString);
    }

    [LiveDatabaseFact("victoriametrics", "victoriametrics", 8428)]
    [DisplayName("协议别名_VictoriaMetrics真库_导入并范围查询指标")]
    public void VictoriaMetricsLiveTest()
    {
        var connStr = LiveDatabase.Resolve("victoriametrics", "victoriametrics", "Server=http://localhost:8428");
        DAL.AddConnStr("victoriametrics_live", connStr, null, "VictoriaMetrics");
        var dal = DAL.Create("victoriametrics_live");
        Assert.Equal(DatabaseType.Prometheus, dal.DbType);

        // 先通过 Prometheus 导入接口写入一个指标，避免依赖服务端抓取配置；时间回拨 2 分钟避开 VictoriaMetrics 的延迟偏移
        var server = connStr.SplitAsDictionary("=", ";", true)["Server"];
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeMilliseconds();
        using var http = new HttpClient();
        using var content = new StringContent($"xcode_vm_live_metric 42 {timestamp}");
        http.PostAsync($"{server.TrimEnd('/')}/api/v1/import/prometheus", content).GetAwaiter().GetResult().EnsureSuccessStatusCode();

        // VictoriaMetrics 导入为异步落盘，短暂延迟后才可查询
        var dt = dal.Query("range:xcode_vm_live_metric");
        for (var i = 0; i < 20 && (dt == null || dt.Rows.Count == 0); i++)
        {
            Thread.Sleep(500);
            dt = dal.Query("range:xcode_vm_live_metric");
        }

        Assert.NotNull(dt);
        Assert.True(dt.Rows.Count > 0);
    }
}
