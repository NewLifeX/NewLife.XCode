using System;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Serialization;
using NewLife.UnitTest;
using XCode;
using XCode.DataAccessLayer;
using XCode.OpenTSDB;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

[TestCaseOrderer("NewLife.UnitTest.PriorityOrderer", "NewLife.UnitTest")]
public class OpenTSDBTests
{
    private const String DefaultConnStr = "Server=http://localhost:4242";

    private static String _ConnStr = DefaultConnStr;

    /// <summary>解析连接串。环境变量 XCode_opentsdb 优先，其次 Config/opentsdb.config</summary>
    public OpenTSDBTests() => _ConnStr = LiveDatabase.Resolve("opentsdb", "opentsdb", DefaultConnStr);

    [TestOrder(0)]
    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.OpenTSDB);
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
        var db = DbFactory.Create(DatabaseType.OpenTSDB);
        Assert.NotNull(db);

        Assert.True(db.Support("OpenTSDB"));
        Assert.True(db.Support("opentsdb"));
        Assert.False(db.Support("MySQL"));
    }

    [TestOrder(20)]
    [Fact]
    public void ConnectionStringTest()
    {
        var conn = new OpenTSDBConnection("Server=localhost:4242;User=admin;Password=pass");
        conn.Open();

        Assert.Equal("http://localhost:4242", conn.DataSource);
        Assert.Equal("admin", conn.User);
        Assert.Equal("http://localhost:4242", conn.BaseUrl);

        conn.Close();
    }

    [TestOrder(30)]
    [Fact]
    public void BuildPutJson_SingleValue_UsesTableMetric()
    {
        var table = CreateTable();
        var columns = new[]
        {
            CreateColumn(table, "Time", typeof(DateTime), true),
            CreateColumn(table, "Location", typeof(String), false, true),
            CreateColumn(table, "Temperature", typeof(Double), false),
        };

        var entity = new TestModel
        {
            ["Time"] = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            ["Location"] = "room1",
            ["Temperature"] = 23.5,
        };

        var json = OpenTSDBSession.BuildPutJson(table, columns, [entity]);
        Assert.NotEmpty(json);

        var list = JsonHelper.Parse(json) as IList<Object>;
        Assert.NotNull(list);
        Assert.Single(list!);

        var point = (IDictionary<String, Object?>)list![0]!;
        Assert.Equal("sensor", point["metric"]);
        Assert.Equal(23.5.ToDouble(), point["value"].ToDouble());

        var tags = (IDictionary<String, Object?>)point["tags"]!;
        Assert.Equal("room1", tags["Location"]);
        Assert.True(point["timestamp"].ToLong() > 0);
    }

    [TestOrder(40)]
    [Fact]
    public void BuildPutJson_MultipleValues_SplitsMetrics()
    {
        var table = CreateTable();
        var columns = new[]
        {
            CreateColumn(table, "Time", typeof(DateTime), true),
            CreateColumn(table, "Location", typeof(String), false, true),
            CreateColumn(table, "Temperature", typeof(Double), false),
            CreateColumn(table, "Humidity", typeof(Double), false),
        };

        var entity = new TestModel
        {
            ["Time"] = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            ["Location"] = "room1",
            ["Temperature"] = 23.5,
            ["Humidity"] = 45.0,
        };

        var json = OpenTSDBSession.BuildPutJson(table, columns, [entity]);
        var list = JsonHelper.Parse(json) as IList<Object>;
        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);

        var metrics = list.Select(e => ((IDictionary<String, Object?>)e!)["metric"] + "").OrderBy(e => e).ToArray();
        Assert.Equal(["sensor.Humidity", "sensor.Temperature"], metrics);
    }

    [TestOrder(50)]
    [Fact]
    public void BuildPutJson_NoNumeric_Throws()
    {
        var table = CreateTable();
        var columns = new[]
        {
            CreateColumn(table, "Time", typeof(DateTime), true),
            CreateColumn(table, "Location", typeof(String), false, true),
        };

        var entity = new TestModel { ["Time"] = DateTime.Now, ["Location"] = "room1" };

        Assert.Throws<InvalidOperationException>(() => OpenTSDBSession.BuildPutJson(table, columns, [entity]));
    }

    [TestOrder(60)]
    [Fact]
    public void ParseQueryTest()
    {
        const String json = """
[
  {
    "metric": "sys.cpu",
    "tags": { "host": "web01", "dc": "lga" },
    "aggregateTags": [],
    "dps": { "1356998400": 18, "1356998460": 19.5 }
  }
]
""";

        var table = OpenTSDBResultParser.Parse(json);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(new[] { "time", "metric", "dc", "host", "value" }, table.Columns.Cast<System.Data.DataColumn>().Select(e => e.ColumnName).ToArray());
        Assert.Equal("sys.cpu", table.Rows[0]["metric"]);
        Assert.Equal("web01", table.Rows[0]["host"]);
    }

    [TestOrder(70)]
    [LiveDatabaseFact("opentsdb", "opentsdb", 4242)]
    public void WriteDataTest()
    {
        DAL.AddConnStr("sysOpenTSDB", _ConnStr, null, "OpenTSDB");
        var dal = DAL.Create("sysOpenTSDB");

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var json = $"[{{\"metric\":\"sys.test\",\"timestamp\":{now},\"value\":42,\"tags\":{{\"host\":\"unit-test\"}}}}]";

        var rs = dal.Execute(json);
        Assert.True(rs >= 0);

        XTrace.WriteLine("OpenTSDB write success");
    }

    [TestOrder(80)]
    [LiveDatabaseFact("opentsdb", "opentsdb", 4242)]
    public void QueryDataTest()
    {
        DAL.AddConnStr("sysOpenTSDB", _ConnStr, null, "OpenTSDB");
        var dal = DAL.Create("sysOpenTSDB");

        var dt = dal.Query("m=sum:sys.test{host=unit-test}&start=1h-ago");
        Assert.NotNull(dt);
        Assert.True(dt.Rows.Count >= 0);
    }

    [TestOrder(90)]
    [LiveDatabaseFact("opentsdb", "opentsdb", 4242)]
    public void GetTablesTest()
    {
        DAL.AddConnStr("sysOpenTSDB", _ConnStr, null, "OpenTSDB");
        var dal = DAL.Create("sysOpenTSDB");

        var tables = dal.Tables;
        Assert.NotNull(tables);
        XTrace.WriteLine("Found {0} metrics", tables.Count);
    }

    private static IDataTable CreateTable()
    {
        var table = DAL.CreateTable();
        table.TableName = "sensor";
        table.DbType = DatabaseType.OpenTSDB;
        return table;
    }

    private static IDataColumn CreateColumn(IDataTable table, String name, Type type, Boolean primaryKey = false, Boolean master = false)
    {
        var column = table.CreateColumn();
        column.Name = name;
        column.ColumnName = name;
        column.DataType = type;
        column.PrimaryKey = primaryKey;
        column.Master = master;
        table.Columns.Add(column);
        return column;
    }

    /// <summary>测试用模型。仅实现 IModel 的索引器</summary>
    private sealed class TestModel : IModel
    {
        private readonly Dictionary<String, Object?> _values = new(StringComparer.OrdinalIgnoreCase);

        public Object? this[String name]
        {
            get => _values.TryGetValue(name, out var value) ? value : null;
            set => _values[name] = value;
        }
    }
}
