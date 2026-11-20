using System;
using System.Linq;
using NewLife;
using XCode.DataAccessLayer;
using XCode.InfluxDB;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>InfluxDB 1.x/2.x/3.x 版本适配测试。全部离线，不依赖服务端</summary>
public class InfluxDBVersionTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("Auto", 0)]
    [InlineData("1", 1)]
    [InlineData("1.8", 1)]
    [InlineData("2", 2)]
    [InlineData("2.7.1", 2)]
    [InlineData("3", 3)]
    [InlineData("3.12", 3)]
    public void ParseVersionTest(String? value, Int32 expected) => Assert.Equal(expected, InfluxDBConnection.ParseVersion(value));

    [Theory]
    [InlineData(null, InfluxQueryLanguage.Auto)]
    [InlineData("Auto", InfluxQueryLanguage.Auto)]
    [InlineData("Flux", InfluxQueryLanguage.Flux)]
    [InlineData("InfluxQL", InfluxQueryLanguage.InfluxQl)]
    [InlineData("SQL", InfluxQueryLanguage.Sql)]
    public void ParseQueryLanguageTest(String? value, InfluxQueryLanguage expected) => Assert.Equal(expected, InfluxDBConnection.ParseQueryLanguage(value));

    [Fact]
    public void Version1_DefaultsToInfluxQl()
    {
        var conn = new InfluxDBConnection("Server=http://localhost:8086;Database=telegraf;User=root;Password=root;Version=1");
        conn.Open();

        Assert.Equal(1, conn.Version);
        Assert.Equal(InfluxQueryLanguage.InfluxQl, conn.QueryLanguage);
        Assert.Equal("http://localhost:8086/write?db=telegraf&precision=ns", conn.GetWriteUrl());
        Assert.Contains("/query?db=telegraf", conn.GetQueryUrl(InfluxQueryLanguage.InfluxQl, "SHOW MEASUREMENTS"));

        conn.Close();
    }

    [Fact]
    public void Version2_DefaultsToFlux()
    {
        var conn = new InfluxDBConnection("Server=http://localhost:8086;Token=token;Organization=org;Bucket=bucket;Version=2");
        conn.Open();

        Assert.Equal(2, conn.Version);
        Assert.Equal(InfluxQueryLanguage.Flux, conn.QueryLanguage);
        Assert.Equal("http://localhost:8086/api/v2/write?org=org&bucket=bucket&precision=ns", conn.GetWriteUrl());

        conn.Close();
    }

    [Fact]
    public void Version3_DefaultsToSql()
    {
        var conn = new InfluxDBConnection("Server=http://localhost:8181;Database=sensors;Token=token;Version=3");
        conn.Open();

        Assert.Equal(3, conn.Version);
        Assert.Equal(InfluxQueryLanguage.Sql, conn.QueryLanguage);
        Assert.Equal("http://localhost:8181/api/v3/write_lp?db=sensors&precision=nanosecond&accept_partial=false", conn.GetWriteUrl());

        var url = conn.GetQueryUrl(InfluxQueryLanguage.Sql, "SELECT * FROM home");
        Assert.Contains("/api/v3/query_sql?db=sensors", url);
        Assert.Contains("format=jsonl", url);
        Assert.Contains("/api/v3/query_influxql?db=sensors", conn.GetQueryUrl(InfluxQueryLanguage.InfluxQl, "SHOW MEASUREMENTS"));

        conn.Close();
    }

    [Fact]
    public void Version3_FluxIsRejected()
    {
        var conn = new InfluxDBConnection("Server=http://localhost:8181;Database=sensors;Token=token;Version=3;QueryLanguage=Flux");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "from(bucket: \"sensors\")";

        Assert.Throws<NotSupportedException>(() => cmd.ExecuteReader());

        conn.Close();
    }

    [Fact]
    public void Version1_RequiresDatabase()
    {
        var conn = new InfluxDBConnection("Server=http://localhost:8086;Version=1");

        Assert.Throws<ArgumentException>(() => conn.Open());
    }

    [Fact]
    public void ParseInfluxQlResultTest()
    {
        const String json = """
{
  "results": [
    {
      "statement_id": 0,
      "series": [
        {
          "name": "cpu",
          "columns": ["time", "value"],
          "values": [
            ["2024-01-01T00:00:00Z", 0.5],
            ["2024-01-01T00:01:00Z", 0.6]
          ]
        }
      ]
    }
  ]
}
""";

        var table = InfluxDBResultParser.Parse(json, InfluxQueryLanguage.InfluxQl);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(new[] { "_measurement", "time", "value" }, table.Columns.Cast<System.Data.DataColumn>().Select(e => e.ColumnName).ToArray());
        Assert.Equal("cpu", table.Rows[0]["_measurement"]);
        Assert.Equal(0.5.ToDouble(), table.Rows[0]["value"].ToDouble());
    }

    [Fact]
    public void ParseInfluxQlErrorTest()
    {
        const String json = """{"results":[{"error":"database not found"}]}""";

        var ex = Assert.Throws<InvalidOperationException>(() => InfluxDBResultParser.Parse(json, InfluxQueryLanguage.InfluxQl));
        Assert.Contains("database not found", ex.Message);
    }

    [Fact]
    public void ParseSqlJsonLinesTest()
    {
        const String json = """
{"room":"Living room","temp":71.5}
{"room":"Kitchen","temp":70.0}
""";

        var table = InfluxDBResultParser.Parse(json, InfluxQueryLanguage.Sql);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(new[] { "room", "temp" }, table.Columns.Cast<System.Data.DataColumn>().Select(e => e.ColumnName).ToArray());
        Assert.Equal("Living room", table.Rows[0]["room"]);
        Assert.Equal(71.5, table.Rows[0]["temp"].ToDouble());
    }

    [Fact]
    public void ParseSqlJsonArrayTest()
    {
        const String json = """[{"room":"A","temp":1},{"room":"B","temp":2}]""";

        var table = InfluxDBResultParser.Parse(json, InfluxQueryLanguage.Sql);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("B", table.Rows[1]["room"]);
    }
}
