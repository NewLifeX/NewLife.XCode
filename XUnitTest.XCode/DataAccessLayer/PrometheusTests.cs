using System;
using NewLife;
using NewLife.Log;
using NewLife.UnitTest;
using XCode;
using XCode.DataAccessLayer;
using XCode.Prometheus;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

[TestCaseOrderer("NewLife.UnitTest.PriorityOrderer", "NewLife.UnitTest")]
public class PrometheusTests
{
    private const String DefaultConnStr = "Server=http://localhost:9090";

    private static String _ConnStr = DefaultConnStr;

    /// <summary>解析连接串。环境变量 XCode_prometheus 优先，其次 Config/prometheus.config</summary>
    public PrometheusTests() => _ConnStr = LiveDatabase.Resolve("prometheus", "prometheus", DefaultConnStr);

    [TestOrder(0)]
    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.Prometheus);
        Assert.NotNull(db);

        var factory = db.Factory;
        Assert.NotNull(factory);

        Assert.NotNull(factory.CreateConnection());
        Assert.NotNull(factory.CreateCommand());
        Assert.NotNull(factory.CreateParameter());
        Assert.NotNull(factory.CreateConnectionStringBuilder());
    }

    [TestOrder(10)]
    [Fact]
    public void SupportTest()
    {
        var db = DbFactory.Create(DatabaseType.Prometheus);
        Assert.NotNull(db);

        Assert.True(db.Support("Prometheus"));
        Assert.True(db.Support("VictoriaMetrics"));
        Assert.False(db.Support("MySQL"));
    }

    [TestOrder(20)]
    [Fact]
    public void ConnectionStringTest()
    {
        var conn = new PrometheusConnection("Server=localhost:9090;Mode=Range;Start=-1h;Step=60s;BearerToken=abc");
        conn.Open();

        Assert.Equal("http://localhost:9090", conn.DataSource);
        Assert.Equal(PrometheusQueryMode.Range, conn.Mode);
        Assert.Equal("-1h", conn.Start);
        Assert.Equal("60s", conn.Step);
        Assert.Equal("abc", conn.BearerToken);

        conn.Close();
    }

    [TestOrder(30)]
    [Fact]
    public void BuildQueryUrlTest()
    {
        var conn = new PrometheusConnection("Server=http://localhost:9090;Start=-1h;End=now;Step=60s");
        conn.Open();

        var instant = conn.BuildQueryUrl("up", false);
        Assert.Equal("/api/v1/query?query=up", instant);

        var range = conn.BuildQueryUrl("up", true);
        Assert.Contains("/api/v1/query_range?", range);
        Assert.Contains("start=", range);
        Assert.Contains("end=", range);
        Assert.Contains("step=60s", range);

        var now = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        Assert.Equal("1699996400", PrometheusConnection.ResolveTime("-1h", now));
        Assert.Equal("1700000000", PrometheusConnection.ResolveTime("now", now));
        Assert.Equal("2024-01-01T00:00:00Z", PrometheusConnection.ResolveTime("2024-01-01T00:00:00Z", now));

        conn.Close();
    }

    [TestOrder(40)]
    [Fact]
    public void ParseVectorTest()
    {
        const String json = """
{
  "status": "success",
  "data": {
    "resultType": "vector",
    "result": [
      { "metric": { "__name__": "up", "job": "api" }, "value": [1635232143.396, "1"] }
    ]
  }
}
""";

        var table = PrometheusResultParser.Parse(json);
        Assert.Single(table.Rows);
        Assert.Equal("up", table.Rows[0]["metric"]);
        Assert.Equal("api", table.Rows[0]["job"]);
        Assert.Equal(1.0, table.Rows[0]["value"]);
    }

    [TestOrder(50)]
    [Fact]
    public void ParseMatrixTest()
    {
        const String json = """
{
  "status": "success",
  "data": {
    "resultType": "matrix",
    "result": [
      { "metric": { "__name__": "cpu", "host": "a" }, "values": [[1635232143, "0.5"], [1635232203, "0.75"]] }
    ]
  }
}
""";

        var table = PrometheusResultParser.Parse(json);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(0.5, table.Rows[0]["value"]);
        Assert.Equal(0.75, table.Rows[1]["value"]);
    }

    [TestOrder(60)]
    [Fact]
    public void ParseScalarTest()
    {
        const String json = """{"status":"success","data":{"resultType":"scalar","result":[1635232143,"42"]}}""";

        var table = PrometheusResultParser.Parse(json);
        Assert.Single(table.Rows);
        Assert.Equal(42.0, table.Rows[0]["value"]);
    }

    [TestOrder(70)]
    [Fact]
    public void ParseErrorTest()
    {
        const String json = """{"status":"error","error":"bad query"}""";

        var ex = Assert.Throws<Exception>(() => PrometheusResultParser.Parse(json));
        Assert.Contains("bad query", ex.Message);
    }

    [TestOrder(80)]
    [Fact]
    public void ExecuteNonQuery_IsReadOnly()
    {
        var cmd = new PrometheusCommand { CommandText = "up" };
        Assert.Throws<NotSupportedException>(() => cmd.ExecuteNonQuery());
    }

    [TestOrder(90)]
    [LiveDatabaseFact("prometheus", "prometheus", 9090)]
    public void QueryTest()
    {
        DAL.AddConnStr("sysPrometheus", _ConnStr, null, "Prometheus");
        var dal = DAL.Create("sysPrometheus");

        var dt = dal.Query("up");
        Assert.NotNull(dt);
        Assert.True(dt.Rows.Count >= 0);

        XTrace.WriteLine("Prometheus rows: {0}", dt.Rows.Count);
    }

    [TestOrder(100)]
    [LiveDatabaseFact("prometheus", "prometheus", 9090)]
    public void GetTablesTest()
    {
        DAL.AddConnStr("sysPrometheus", _ConnStr, null, "Prometheus");
        var dal = DAL.Create("sysPrometheus");

        var tables = dal.Tables;
        Assert.NotNull(tables);
        XTrace.WriteLine("Found {0} metrics", tables.Count);
    }
}
