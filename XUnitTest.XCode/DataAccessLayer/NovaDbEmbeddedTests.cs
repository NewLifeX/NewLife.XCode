using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewLife;
using NewLife.Log;
using NewLife.Security;
using NewLife.Serialization;
using XCode;
using XCode.DataAccessLayer;
using XCode.Membership;
using Xunit;
using XUnitTest.XCode.TestEntity;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>NovaDb嵌入模式测试。连接字符串格式：Data Source=../data/mydb</summary>
[Collection("Database")]
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class NovaDbEmbeddedTests
{
    [Fact(DisplayName = "嵌入模式驱动初始化")]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.NovaDb);
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

    [Fact(DisplayName = "嵌入模式连接")]
    public void ConnectTest()
    {
        var db = DbFactory.Create(DatabaseType.NovaDb);
        var factory = db.Factory;

        var conn = factory.CreateConnection();
        conn.ConnectionString = "Data Source=Data\\nova_test";
        conn.Open();
    }

    [Fact(DisplayName = "嵌入模式DAL层")]
    public void DALTest()
    {
        var dataDir = "Data\\nova_dal";

        DAL.AddConnStr("novaEmbed_dal", $"Data Source={dataDir}", null, "NovaDb");
        var dal = DAL.Create("novaEmbed_dal");
        Assert.NotNull(dal);
        Assert.Equal("novaEmbed_dal", dal.ConnName);
        Assert.Equal(DatabaseType.NovaDb, dal.DbType);

        var db = dal.Db;
        Assert.NotNull(db.DatabaseName);

        using var conn = db.OpenConnection();

        var ver = db.ServerVersion;
        Assert.NotEmpty(ver);
    }

    [Fact(DisplayName = "嵌入模式查询操作")]
    public void SelectTest()
    {
        var dataDir = "Data\\nova_select";
        var fullDir = dataDir.GetFullPath();
        if (Directory.Exists(fullDir)) Directory.Delete(fullDir, true);

        DAL.AddConnStr("novaEmbed_select", $"Data Source={dataDir}", null, "NovaDb");

        Role.Meta.ConnName = "novaEmbed_select";
        Area.Meta.ConnName = "novaEmbed_select";

        Role.Meta.Session.InitData();

        var count = Role.Meta.Count;
        Assert.True(count > 0);

        var list = Role.FindAll();
        Assert.Equal(4, list.Count);

        var list2 = Role.FindAll(Role._.Name == "管理员");
        Assert.Single(list2);

        var list3 = Role.Search("用户", null);
        Assert.Equal(2, list3.Count);

        // 来个耗时操作，把前面堵住
        Area.FetchAndSave();

        // 清理现场
        if (Directory.Exists(fullDir)) Directory.Delete(fullDir, true);
    }

    [Fact(DisplayName = "嵌入模式Membership操作")]
    public void MembershipTest()
    {
        var dataDir = "Data\\nova_member";

        DAL.AddConnStr("novaEmbed_member", $"Data Source={dataDir}", null, "NovaDb");

        User.Meta.ConnName = "novaEmbed_member";
        Role.Meta.ConnName = "novaEmbed_member";

        Role.Meta.Session.Truncate();
        User.Meta.Session.InitData();
        Role.Meta.Session.InitData();

        var count = User.Meta.Count;
        Assert.True(count > 0);

        count = Role.Meta.Count;
        Assert.True(count > 0);

        var list = Role.FindAll();
        Assert.Equal(4, list.Count);

        var list2 = Role.FindAll(Role._.Name == "管理员");
        Assert.Single(list2);

        var list3 = Role.Search("用户", null);
        Assert.Equal(2, list3.Count);
    }

    [Fact(DisplayName = "嵌入模式表前缀")]
    public void TablePrefixTest()
    {
        var dataDir = "Data\\nova_prefix";
        var fullDir = dataDir.GetFullPath();
        if (Directory.Exists(fullDir)) Directory.Delete(fullDir, true);

        DAL.AddConnStr("novaEmbed_prefix", $"Data Source={dataDir};TablePrefix=nova_", null, "NovaDb");

        Role.Meta.ConnName = "novaEmbed_prefix";

        Role.Meta.Session.InitData();

        var count = Role.Meta.Count;
        Assert.True(count > 0);

        var list = Role.FindAll();
        Assert.Equal(4, list.Count);

        var list2 = Role.FindAll(Role._.Name == "管理员");
        Assert.Single(list2);

        var list3 = Role.Search("用户", null);
        Assert.Equal(2, list3.Count);

        // 清理现场
        if (Directory.Exists(fullDir)) Directory.Delete(fullDir, true);
    }

    private IDisposable CreateForBatch(String action)
    {
        var dataDir = "Data\\nova_batch";
        DAL.AddConnStr("novaEmbed_batch", $"Data Source={dataDir}", null, "NovaDb");

        var dt = Role2.Meta.Table.DataTable.Clone() as IDataTable;
        dt.TableName = $"Role2_{action}";

        // 分表
        var split = Role2.Meta.CreateSplit("novaEmbed_batch", dt.TableName);

        var session = Role2.Meta.Session;
        session.Dal.SetTables(dt);

        // 清空数据
        session.Truncate();

        return split;
    }

    [Fact(DisplayName = "嵌入模式批量插入")]
    public void BatchInsert()
    {
        using var split = CreateForBatch("BatchInsert");

        var list = new List<Role2>
        {
            new Role2 { Name = "管理员" },
            new Role2 { Name = "高级用户" },
            new Role2 { Name = "普通用户" }
        };
        var rs = list.BatchInsert();
        Assert.Equal(list.Count, rs);

        var list2 = Role2.FindAll();
        Assert.Equal(list.Count, list2.Count);
        Assert.Contains(list2, e => e.Name == "管理员");
        Assert.Contains(list2, e => e.Name == "高级用户");
        Assert.Contains(list2, e => e.Name == "普通用户");
    }

    [Fact(DisplayName = "嵌入模式批量InsertIgnore")]
    public void BatchInsertIgnore()
    {
        using var split = CreateForBatch("InsertIgnore");

        var list = new List<Role2>
        {
            new Role2 { Name = "管理员" },
            new Role2 { Name = "高级用户" },
            new Role2 { Name = "普通用户" }
        };
        var rs = list.BatchInsert();
        Assert.Equal(list.Count, rs);

        list =
        [
            new Role2 { Name = "管理员" },
            new Role2 { Name = "游客" },
        ];
        rs = list.BatchInsertIgnore();
        Assert.Equal(1, rs);

        var list2 = Role2.FindAll();
        Assert.Equal(4, list2.Count);
        Assert.Contains(list2, e => e.Name == "管理员");
        Assert.Contains(list2, e => e.Name == "高级用户");
        Assert.Contains(list2, e => e.Name == "普通用户");
        Assert.Contains(list2, e => e.Name == "游客");
    }

    [Fact(DisplayName = "嵌入模式批量Replace")]
    public void BatchReplace()
    {
        using var split = CreateForBatch("Replace");

        var list = new List<Role2>
        {
            new Role2 { Name = "管理员", Remark = "guanliyuan" },
            new Role2 { Name = "高级用户", Remark = "gaoji" },
            new Role2 { Name = "普通用户", Remark = "putong" }
        };
        var rs = list.BatchInsert();
        Assert.Equal(list.Count, rs);

        var gly = list.FirstOrDefault(e => e.Name == "管理员");
        Assert.NotNull(gly);
        Assert.Equal("guanliyuan", gly.Remark);

        list =
        [
            new Role2 { Name = "管理员" },
            new Role2 { Name = "游客", Remark = "guest" },
        ];
        rs = list.BatchReplace();

        var list2 = Role2.FindAll();
        Assert.Equal(4, list2.Count);
        Assert.Contains(list2, e => e.Name == "管理员");
        Assert.Contains(list2, e => e.Name == "高级用户");
        Assert.Contains(list2, e => e.Name == "普通用户");
        Assert.Contains(list2, e => e.Name == "游客");

        var gly2 = list2.FirstOrDefault(e => e.Name == "管理员");
        Assert.NotNull(gly2);
        Assert.Null(gly2.Remark);
    }

    [Fact(DisplayName = "嵌入模式正反向工程")]
    public void PositiveAndNegative()
    {
        var dataDir = "Data\\nova_positive";

        DAL.AddConnStr("novaEmbed_positive", $"Data Source={dataDir}", null, "NovaDb");
        var dal = DAL.Create("novaEmbed_positive");

        var table = User.Meta.Table.DataTable.Clone() as IDataTable;
        table.TableName = $"user_{Rand.Next(1000, 10000)}";

        dal.SetTables(table);

        var tableNames = dal.GetTableNames();
        XTrace.WriteLine("tableNames: {0}", tableNames.Join());
        Assert.Contains(table.TableName, tableNames);

        var tables = dal.Tables;
        XTrace.WriteLine("tables: {0}", tables.Join());
        Assert.Contains(tables, t => t.TableName == table.TableName);

        dal.Db.CreateMetaData().DropTable(table);

        tableNames = dal.GetTableNames();
        XTrace.WriteLine("tableNames: {0}", tableNames.Join());
        Assert.DoesNotContain(table.TableName, tableNames);
    }
}
