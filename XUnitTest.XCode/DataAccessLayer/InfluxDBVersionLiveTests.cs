using System;
using NewLife.UnitTest;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>InfluxDB 1.x/3.x 真库用例。默认跳过，CI 通过 XCode_influxdb1 / XCode_influxdb3 注入</summary>
public class InfluxDBVersionLiveTests
{
    private static String Resolve(String key, String defaultConnStr) => LiveDatabase.Resolve(key, key, defaultConnStr);

    [LiveDatabaseFact("influxdb1", "influxdb1", 8086)]
    public void Version1_WriteAndQuery()
    {
        var connStr = Resolve("influxdb1", "Server=http://localhost:8086;Database=test;User=root;Password=root;Version=1");

        DAL.AddConnStr("sysInfluxDB1", connStr, null, "InfluxDB");
        var dal = DAL.Create("sysInfluxDB1");

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000;
        dal.Execute($"temperature,location=room1 value=23.5 {timestamp}");

        var dt = dal.Query("SELECT * FROM temperature WHERE time > now() - 1h LIMIT 10");
        Assert.NotNull(dt);
    }

    [LiveDatabaseFact("influxdb3", "influxdb3", 8181)]
    public void Version3_WriteAndQuery()
    {
        var connStr = Resolve("influxdb3", "Server=http://localhost:8181;Database=test;Token=test-token;Version=3");

        DAL.AddConnStr("sysInfluxDB3", connStr, null, "InfluxDB");
        var dal = DAL.Create("sysInfluxDB3");

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000;
        dal.Execute($"temperature,location=room1 value=23.5 {timestamp}");

        var dt = dal.Query("""SELECT * FROM temperature LIMIT 10""");
        Assert.NotNull(dt);
    }
}
