using System;
using NewLife;
using NewLife.Log;
using NewLife.UnitTest;
using XCode;
using XCode.DataAccessLayer;
using XCode.IoTDB;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

[TestCaseOrderer("NewLife.UnitTest.PriorityOrderer", "NewLife.UnitTest")]
public class IoTDBTests
{
    private const String DefaultConnStr = "Server=http://localhost:18080;User=root;Password=root";

    private static String _ConnStr = DefaultConnStr;

    /// <summary>解析连接串。环境变量 XCode_iotdb 优先，其次 Config/iotdb.config</summary>
    public IoTDBTests() => _ConnStr = LiveDatabase.Resolve("iotdb", "iotdb", DefaultConnStr);

    [TestOrder(0)]
    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.IoTDB);
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
        var db = DbFactory.Create(DatabaseType.IoTDB);
        Assert.NotNull(db);

        Assert.True(db.Support("IoTDB"));
        Assert.True(db.Support("Apache.IoTDB"));
        Assert.False(db.Support("MySQL"));
    }

    [TestOrder(20)]
    [Fact]
    public void ConnectionStringTest()
    {
        var conn = new IoTDBConnection("Server=localhost:18080;User=admin;Password=secret;RowLimit=100");
        conn.Open();

        Assert.Equal("http://localhost:18080", conn.DataSource);
        Assert.Equal("admin", conn.User);
        Assert.Equal(100, conn.RowLimit);
        Assert.StartsWith("Basic ", conn.GetAuthorization());

        conn.Close();
    }

    [TestOrder(30)]
    [Fact]
    public void ParseDataQueryTest()
    {
        const String json = """
{
  "expressions": ["root.sg27.s3", "root.sg27.s4"],
  "timestamps": [1635232143960, 1635232153960],
  "values": [[11, null], [false, true]]
}
""";

        var table = IoTDBResultParser.Parse(json);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(3, table.Columns.Count);
        Assert.Equal("Time", table.Columns[0].ColumnName);
        Assert.Equal("root.sg27.s3", table.Columns[1].ColumnName);
        Assert.Equal(11, table.Rows[0]["root.sg27.s3"].ToInt());
        Assert.Equal(false, table.Rows[0]["root.sg27.s4"]);
        Assert.Equal(DBNull.Value, table.Rows[1]["root.sg27.s3"]);
        Assert.Equal(true, table.Rows[1]["root.sg27.s4"]);
    }

    [TestOrder(40)]
    [Fact]
    public void ParseMetaQueryTest()
    {
        const String json = """
{
  "column_names": ["Timeseries", "DataType"],
  "values": [["root.sg.s1", "INT32"], ["root.sg.s2", "DOUBLE"]]
}
""";

        var table = IoTDBResultParser.Parse(json);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(2, table.Columns.Count);
        Assert.Equal("Timeseries", table.Columns[0].ColumnName);
        Assert.Equal("root.sg.s1", table.Rows[0]["Timeseries"]);
    }

    [TestOrder(50)]
    [Fact]
    public void ParseErrorTest()
    {
        const String json = """{"code":401,"message":"Unauthorized"}""";

        var ex = Assert.Throws<Exception>(() => IoTDBResultParser.Parse(json));
        Assert.Contains("Unauthorized", ex.Message);
    }

    [TestOrder(60)]
    [LiveDatabaseFact("iotdb", "iotdb", 18080)]
    public void QueryTest()
    {
        DAL.AddConnStr("sysIoTDB", _ConnStr, null, "IoTDB");
        var dal = DAL.Create("sysIoTDB");

        var dt = dal.Query("SHOW DATABASES");
        Assert.NotNull(dt);
        Assert.True(dt.Rows.Count >= 0);

        XTrace.WriteLine("IoTDB databases: {0}", dt.Rows.Count);
    }

    [TestOrder(70)]
    [LiveDatabaseFact("iotdb", "iotdb", 18080)]
    public void GetTablesTest()
    {
        DAL.AddConnStr("sysIoTDB", _ConnStr, null, "IoTDB");
        var dal = DAL.Create("sysIoTDB");

        var tables = dal.Tables;
        Assert.NotNull(tables);
        XTrace.WriteLine("Found {0} databases", tables.Count);
    }
}
