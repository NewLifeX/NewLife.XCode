using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NewLife.Data;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>InfluxDB Line Protocol 纯单元测试，无需数据库连接</summary>
public class InfluxDBLineProtocolTests
{
    [Fact]
    public void BuildLineProtocol_ShouldFormatFieldTypes_AndSkipTimeField()
    {
        var db = DbFactory.Create(DatabaseType.InfluxDB);
        var method = GetBuildLineProtocolMethod();

        var table = DAL.CreateTable();
        table.TableName = "temperature";

        var id = table.CreateColumn();
        id.ColumnName = "DeviceId";
        id.PrimaryKey = true;
        id.DataType = typeof(Int32);
        table.Columns.Add(id);

        var count = table.CreateColumn();
        count.ColumnName = "Count";
        count.DataType = typeof(Int32);
        table.Columns.Add(count);

        var enabled = table.CreateColumn();
        enabled.ColumnName = "Enabled";
        enabled.DataType = typeof(Boolean);
        table.Columns.Add(enabled);

        var name = table.CreateColumn();
        name.ColumnName = "Name";
        name.DataType = typeof(String);
        table.Columns.Add(name);

        var time = table.CreateColumn();
        time.ColumnName = "Time";
        time.DataType = typeof(DateTime);
        table.Columns.Add(time);

        var dt = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        var model = new PlainModel
        {
            ["DeviceId"] = 1001,
            ["Count"] = 7,
            ["Enabled"] = true,
            ["Name"] = "sensor \"A\"",
            ["Time"] = dt
        };

        var lineProtocol = (String)method.Invoke(null, [db, table, table.Columns.ToArray(), new List<IModel> { model }])!;

        Assert.Contains("Count=7i", lineProtocol);
        Assert.Contains("Enabled=true", lineProtocol);
        Assert.Contains("Name=\"sensor \\\"A\\\"\"", lineProtocol);
        Assert.DoesNotContain("Time=", lineProtocol, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\r\n", lineProtocol);
        Assert.EndsWith("\n", lineProtocol);

        var nanos = (dt - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Ticks * 100;
        Assert.Contains($" {nanos}\n", lineProtocol);
    }

    private static MethodInfo GetBuildLineProtocolMethod()
    {
        var sessionType = typeof(DbFactory).Assembly.GetType("XCode.DataAccessLayer.InfluxDBSession", true)!;
        return sessionType.GetMethod("BuildLineProtocol", BindingFlags.NonPublic | BindingFlags.Static)!;
    }
}

file class PlainModel : IModel
{
    private readonly Dictionary<String, Object?> _data = new(StringComparer.OrdinalIgnoreCase);

    public Object? this[String name]
    {
        get => _data.GetValueOrDefault(name);
        set => _data[name] = value;
    }
}
