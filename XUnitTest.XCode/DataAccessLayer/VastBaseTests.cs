using System;
using System.IO;
using System.Linq;
using NewLife;
using NewLife.Log;
using NewLife.UnitTest;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>VastBase 数据库测试。VastBase 是基于 PostgreSQL 的国产数据库，使用 Npgsql 驱动</summary>
[TestCaseOrderer("NewLife.UnitTest.PriorityOrderer", "NewLife.UnitTest")]
public class VastBaseTests
{
    private static String _ConnStr = "Server=localhost;Port=5432;Database=test;Uid=postgres;Pwd=postgres;Search Path=public";

    public VastBaseTests()
    {
        var f = "Config\\vastbase.config".GetFullPath();
        if (File.Exists(f))
            _ConnStr = File.ReadAllText(f);
        else
            File.WriteAllText(f.EnsureDirectory(true), _ConnStr);
    }

    [TestOrder(0)]
    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.VastBase);
        Assert.NotNull(db);
        Assert.Equal(DatabaseType.VastBase, db.Type);

        XTrace.WriteLine("VastBase Provider Type: {0}", db.GetType().FullName);
    }

    [TestOrder(10)]
    [Fact(Skip = "需要 VastBase/PostgreSQL 数据库服务")]
    public void ConnectTest()
    {
        var db = DbFactory.Create(DatabaseType.VastBase);
        var factory = db.Factory;
        Assert.NotNull(factory);

        var conn = factory.CreateConnection();
        conn.ConnectionString = _ConnStr;
        conn.Open();

        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
        conn.Close();
    }

    [TestOrder(20)]
    [Fact(DisplayName = "元数据测试", Skip = "需要 VastBase/PostgreSQL 数据库服务")]
    public void MetaDataTest()
    {
        var db = DbFactory.Create(DatabaseType.VastBase);
        var meta = db.CreateMetaData();
        Assert.NotNull(meta);
    }

    [TestOrder(30)]
    [Fact(DisplayName = "DatabaseType 验证")]
    public void DatabaseTypeTest()
    {
        var db = DbFactory.Create(DatabaseType.VastBase);
        Assert.NotNull(db);
        Assert.Equal(DatabaseType.VastBase, db.Type);

        // 验证注册的数据库类型正确
        var db2 = DbFactory.Create(DatabaseType.VastBase);
        Assert.Same(db.GetType(), db2.GetType());
    }
}
