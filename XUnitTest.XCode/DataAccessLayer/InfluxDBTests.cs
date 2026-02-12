using System;
using System.IO;
using NewLife;
using NewLife.Log;
using NewLife.UnitTest;
using XCode;
using XCode.DataAccessLayer;
using XCode.InfluxDB;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

[TestCaseOrderer("NewLife.UnitTest.PriorityOrderer", "NewLife.UnitTest")]
public class InfluxDBTests
{
    // InfluxDB 2.x 连接字符串格式：
    // Server=http://localhost:8086;Token=your-token;Organization=your-org;Bucket=your-bucket
    private static String _ConnStr = "Server=http://localhost:8086;Token=your-influxdb-token;Organization=your-org;Bucket=test";

    public InfluxDBTests()
    {
        var f = "Config\\influxdb.config".GetFullPath();
        if (File.Exists(f))
            _ConnStr = File.ReadAllText(f);
        else
            File.WriteAllText(f.EnsureDirectory(true), _ConnStr);
    }

    [TestOrder(0)]
    [Fact(Skip = "跳过")]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.InfluxDB);
        Assert.NotNull(db);

        var factory = db.Factory;
        Assert.NotNull(factory);

        var conn = factory.CreateConnection();
        Assert.NotNull(conn);

        var cmd = factory.CreateCommand();
        Assert.NotNull(cmd);

        var adp = factory.CreateDataAdapter();
        Assert.NotNull(adp);

        var dp = factory.CreateParameter();
        Assert.NotNull(dp);
    }

    [TestOrder(10)]
    [Fact(Skip = "跳过")]
    public void ConnectTest()
    {
        var db = DbFactory.Create(DatabaseType.InfluxDB);
        var factory = db.Factory;

        var conn = factory.CreateConnection() as InfluxDBConnection;
        Assert.NotNull(conn);

        conn.ConnectionString = _ConnStr;
        conn.Open();

        Assert.NotEmpty(conn.ServerVersion);
        XTrace.WriteLine("ServerVersion={0}", conn.ServerVersion);

        conn.Close();
    }

    [TestOrder(20)]
    [Fact(Skip = "跳过")]
    public void DALTest()
    {
        DAL.AddConnStr("sysInfluxDB", _ConnStr, null, "InfluxDB");
        var dal = DAL.Create("sysInfluxDB");
        Assert.NotNull(dal);
        Assert.Equal("sysInfluxDB", dal.ConnName);
        Assert.Equal(DatabaseType.InfluxDB, dal.DbType);

        var db = dal.Db;
        var connstr = db.ConnectionString;

        var ver = db.ServerVersion;
        Assert.NotEmpty(ver);
    }

    [TestOrder(30)]
    [Fact(Skip = "跳过")]
    public void WriteDataTest()
    {
        DAL.AddConnStr("sysInfluxDB", _ConnStr, null, "InfluxDB");
        var dal = DAL.Create("sysInfluxDB");

        // InfluxDB Line Protocol 格式写入
        // measurement,tag1=value1,tag2=value2 field1=value1,field2=value2 timestamp
        var lineProtocol = "temperature,location=room1,sensor=sensor1 value=23.5,humidity=45 " + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000;

        var rs = dal.Execute(lineProtocol);
        Assert.Equal(1, rs);

        XTrace.WriteLine("Write data success");
    }

    [TestOrder(40)]
    [Fact(Skip = "跳过")]
    public void QueryDataTest()
    {
        DAL.AddConnStr("sysInfluxDB", _ConnStr, null, "InfluxDB");
        var dal = DAL.Create("sysInfluxDB");

        // Flux 查询语句
        var flux = @"
from(bucket: ""test"")
  |> range(start: -1h)
  |> filter(fn: (r) => r._measurement == ""temperature"")
  |> limit(n: 10)
";

        var dt = dal.Query(flux);
        Assert.NotNull(dt);
        Assert.True(dt.Rows.Count >= 0);

        XTrace.WriteLine("Query returned {0} rows", dt.Rows.Count);
    }

    [TestOrder(50)]
    [Fact(Skip = "跳过")]
    public void GetTablesTest()
    {
        DAL.AddConnStr("sysInfluxDB", _ConnStr, null, "InfluxDB");
        var dal = DAL.Create("sysInfluxDB");

        var tables = dal.Tables;
        Assert.NotNull(tables);
        XTrace.WriteLine("Found {0} measurements", tables.Count);

        foreach (var table in tables)
        {
            XTrace.WriteLine("Measurement: {0}", table.TableName);
        }
    }

    [TestOrder(60)]
    [Fact(Skip = "跳过")]
    public void SupportTest()
    {
        var db = DbFactory.Create(DatabaseType.InfluxDB);
        Assert.NotNull(db);

        Assert.True(db.Support("InfluxDB"));
        Assert.True(db.Support("Influx"));
        Assert.False(db.Support("MySQL"));
    }

    [TestOrder(70)]
    [Fact(Skip = "跳过")]
    public void ConnectionStringBuilderTest()
    {
        var conn = new InfluxDBConnection();
        conn.ConnectionString = "Server=http://localhost:8086;Token=mytoken;Organization=myorg;Bucket=mybucket";

        conn.Open();

        Assert.Equal("mytoken", conn.Token);
        Assert.Equal("myorg", conn.Organization);
        Assert.Equal("mybucket", conn.Bucket);

        conn.Close();
    }

    [TestOrder(80)]
    [Fact(Skip = "跳过")]
    public void BatchWriteTest()
    {
        DAL.AddConnStr("sysInfluxDB", _ConnStr, null, "InfluxDB");
        var dal = DAL.Create("sysInfluxDB");

        // 批量写入多条数据
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000;
        var lines = new[]
        {
            $"temperature,location=room1 value=21.0 {timestamp}",
            $"temperature,location=room2 value=22.5 {timestamp + 1000000}",
            $"temperature,location=room3 value=23.0 {timestamp + 2000000}",
        };

        var lineProtocol = String.Join("\n", lines);
        var rs = dal.Execute(lineProtocol);
        Assert.Equal(1, rs);

        XTrace.WriteLine("Batch write success");
    }
}
