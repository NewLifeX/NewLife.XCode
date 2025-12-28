using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NewLife;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Security;
using NewLife.Serialization;
using XCode;
using XCode.DataAccessLayer;
using XCode.Membership;
using Xunit;
using XUnitTest.XCode.TestEntity;

namespace XUnitTest.XCode.DataAccessLayer;

[Collection("Database")]
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class SQLiteTests
{
    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.SQLite);
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

    [Fact]
    public void ConnectTest()
    {
        var db = DbFactory.Create(DatabaseType.SQLite);
        var factory = db.Factory;

        var conn = factory.CreateConnection();
        conn.ConnectionString = "Data Source=Data\\Membership.db";
        conn.Open();
    }

    [Fact]
    public void DALTest()
    {
        var file = "Data\\Membership.db";
        var dbf = file.GetFullPath();

        DAL.AddConnStr("sysSQLite", $"Data Source={file}", null, "SQLite");
        var dal = DAL.Create("sysSQLite");
        Assert.NotNull(dal);
        Assert.Equal("sysSQLite", dal.ConnName);
        Assert.Equal(DatabaseType.SQLite, dal.DbType);

        var db = dal.Db;
        var connstr = db.ConnectionString;
        Assert.Equal(dbf, dal.Db.DatabaseName);
        //Assert.EndsWith("\\Data\\Membership.db;Cache Size=-524288;Synchronous=Off;Journal Mode=WAL", connstr);
        Assert.EndsWith("\\Data\\Membership.db", connstr);

        using var conn = db.OpenConnection();
        connstr = conn.ConnectionString;
        Assert.EndsWith("\\Data\\Membership.db;Cache Size=-524288;Synchronous=Off;Journal Mode=WAL", connstr);

        var ver = dal.Db.ServerVersion;
        Assert.NotEmpty(ver);
    }

    [Fact]
    public void MapToTest()
    {
        var db = "Data\\Membership.db";
        var dbf = db.GetFullPath();

        DAL.AddConnStr("sysSQLite", $"Data Source={db}", null, "SQLite");
        DAL.AddConnStr("mapTest", "MapTo=sysSQLite", null, null);

        var dal1 = DAL.Create("sysSQLite");
        var dal2 = DAL.Create("mapTest");
        Assert.NotNull(dal2);
        Assert.Equal(dal1, dal2);
        Assert.Equal("sysSQLite", dal2.ConnName);
        Assert.Equal(DatabaseType.SQLite, dal2.DbType);
        Assert.Equal($"Data Source={db}", dal2.ConnStr);
    }

    [Fact]
    public void MapToTest2()
    {
        var db = "Data\\Membership.db";
        var dbf = db.GetFullPath();

        DAL.AddConnStr("sysSQLite", $"Data Source={db}", null, "SQLite");
        DAL.AddConnStr("mapTest", "MapTo=sysSQLite;TablePrefix=xcwl_", null, null);

        var dal1 = DAL.Create("sysSQLite");
        var dal2 = DAL.Create("mapTest");
        Assert.NotNull(dal2);
        Assert.NotEqual(dal1, dal2);
        Assert.Equal("mapTest", dal2.ConnName);
        Assert.Equal(DatabaseType.SQLite, dal2.DbType);
        Assert.NotEqual($"Data Source={db};TablePrefix=xcwl_", dal2.ConnStr);
        //Assert.EndsWith(";TablePrefix=xcwl_", dal2.Db.ConnectionString);
        Assert.Equal(dbf, (dal2.Db as FileDbBase).DatabaseName);
    }

    [Fact]
    public void MetaTest()
    {
        DAL.AddConnStr("SQLite_Meta", "Data Source=Data\\Membership.db", null, "SQLite");
        var dal = DAL.Create("SQLite_Meta");

        var tables = dal.Tables;
        Assert.NotNull(tables);
        Assert.True(tables.Count > 0);
    }

    [Fact]
    public void SelectTest()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var db = "Data\\Membership_Test.db";
        var dbf = db.GetFullPath();
        if (File.Exists(dbf)) File.Delete(dbf);

        DAL.AddConnStr("SQLite_Select", $"Data Source={db}", null, "SQLite");

        Role.Meta.ConnName = "SQLite_Select";
        Area.Meta.ConnName = "SQLite_Select";

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
        if (File.Exists(dbf)) File.Delete(dbf);
    }

    [Fact]
    public void TablePrefixTest()
    {
        var db = "Data\\Membership_Table_Prefix.db";
        var dbf = db.GetFullPath();
        if (File.Exists(dbf)) File.Delete(dbf);

        DAL.AddConnStr("SQLite_Table_Prefix", $"Data Source={db};TablePrefix=member_", null, "SQLite");

        Role.Meta.ConnName = "SQLite_Table_Prefix";
        //Area.Meta.ConnName = "SQLite_Table_Prefix";

        Role.Meta.Session.InitData();

        var count = Role.Meta.Count;
        Assert.True(count > 0);

        var list = Role.FindAll();
        Assert.Equal(4, list.Count);

        var list2 = Role.FindAll(Role._.Name == "管理员");
        Assert.Single(list2);

        var list3 = Role.Search("用户", null);
        Assert.Equal(2, list3.Count);

        User.Meta.ConnName = "SQLite_Table_Prefix";
        count = User.Meta.Count;
        Assert.True(count > 0);

        Department.Meta.ConnName = "SQLite_Table_Prefix";
        count = Department.Meta.Count;
        Assert.True(count > 0);

        // 清理现场
        if (File.Exists(dbf)) File.Delete(dbf);
    }

    private IDisposable CreateForBatch(String action)
    {
        var db = "Data\\Membership_Batch.db";
        DAL.AddConnStr("Membership_Batch", $"Data Source={db}", null, "SQLite");

        var dt = Role2.Meta.Table.DataTable.Clone() as IDataTable;
        dt.TableName = $"Role2_{action}";

        // 分表
        var split = Role2.Meta.CreateSplit("Membership_Batch", dt.TableName);

        var session = Role2.Meta.Session;
        session.Dal.SetTables(dt);

        // 清空数据
        session.Truncate();

        return split;
    }

    [Fact]
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

    [Fact]
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

        list = new List<Role2>
        {
            new Role2 { Name = "管理员" },
            new Role2 { Name = "游客" },
        };
        rs = list.BatchInsertIgnore();
        Assert.Equal(1, rs);

        var list2 = Role2.FindAll();
        Assert.Equal(4, list2.Count);
        Assert.Contains(list2, e => e.Name == "管理员");
        Assert.Contains(list2, e => e.Name == "高级用户");
        Assert.Contains(list2, e => e.Name == "普通用户");
        Assert.Contains(list2, e => e.Name == "游客");
    }

    [Fact]
    public void BatchReplace()
    {
        using var split = CreateForBatch("Replace");

        var list = new List<Role2>
        {
            new Role2 { Name = "管理员", Remark="guanliyuan" },
            new Role2 { Name = "高级用户", Remark="gaoji" },
            new Role2 { Name = "普通用户", Remark="putong" }
        };
        var rs = list.BatchInsert();
        Assert.Equal(list.Count, rs);

        var gly = list.FirstOrDefault(e => e.Name == "管理员");
        Assert.NotNull(gly);
        Assert.Equal("guanliyuan", gly.Remark);

        XTrace.WriteLine(Role2.FindAll().ToJson());

        list = new List<Role2>
        {
            new Role2 { Name = "管理员" },
            new Role2 { Name = "游客", Remark="guest" },
        };
        rs = list.BatchReplace();
        // 删除一行，插入2行，但是影响行为2，这一点跟MySql不同
        Assert.Equal(2, rs);

        var list2 = Role2.FindAll();
        XTrace.WriteLine(list2.ToJson());
        Assert.Equal(4, list2.Count);
        Assert.Contains(list2, e => e.Name == "管理员");
        Assert.Contains(list2, e => e.Name == "高级用户");
        Assert.Contains(list2, e => e.Name == "普通用户");
        Assert.Contains(list2, e => e.Name == "游客");

        var gly2 = list2.FirstOrDefault(e => e.Name == "管理员");
        Assert.NotNull(gly2);
        Assert.Null(gly2.Remark);
        // 管理员被删除后重新插入，自增ID改变
        Assert.NotEqual(gly.ID, gly2.ID);
    }

    [Fact]
    public void PositiveAndNegative()
    {
        DAL.AddConnStr("positiveSQLite", "Data Source=Data\\Membership.db", null, "SQLite");
        var dal = DAL.Create("positiveSQLite");

        var table = User.Meta.Table.DataTable.Clone() as IDataTable;
        table.TableName = $"user_{Rand.Next(1000, 10000)}";

        dal.SetTables(table);

        var tableNames = dal.GetTableNames();
        XTrace.WriteLine("tableNames: {0}", tableNames.Join());
        Assert.Contains(table.TableName, tableNames);

        var tables = dal.Tables;
        XTrace.WriteLine("tables: {0}", tables.Join());
        Assert.Contains(tables, t => t.TableName == table.TableName);

        dal.Db.CreateMetaData().SetSchema(DDLSchema.DropTable, table);

        //Thread.Sleep(10000);

        tableNames = dal.GetTableNames();
        XTrace.WriteLine("tableNames: {0}", tableNames.Join());
        Assert.DoesNotContain(table.TableName, tableNames);
    }

    [Fact]
    public void Backup()
    {
        DAL.AddConnStr("bakSQLite", "Data Source=Data\\Membership.db", null, "SQLite");
        var dal = DAL.Create("bakSQLite");

        var meta = dal.Db.CreateMetaData();

        var file = meta.SetSchema(DDLSchema.BackupDatabase) as String;
        Assert.NotEmpty(file);
        Assert.True(File.Exists(file));
        File.Delete(file);

        file = $"bak_{Rand.NextString(8)}.db";
        var file2 = meta.SetSchema(DDLSchema.BackupDatabase, file) as String;
        Assert.Equal(file, Path.GetFileName(file2));
        Assert.True(File.Exists(file2));
        File.Delete(file2);
    }

    [Fact]
    public void CompactDatabase()
    {
        DAL.AddConnStr("compactSQLite", "Data Source=Data\\Membership.db", null, "SQLite");
        var dal = DAL.Create("compactSQLite");

        var meta = dal.Db.CreateMetaData();

        var rs = meta.SetSchema(DDLSchema.CompactDatabase);
        Assert.Equal(0, rs);
    }

    [Fact]
    public void ParseColumns()
    {
        var dal = DAL.Create("Membership");
        var md = dal.Db.CreateMetaData();

        var table = new XTable();
        var sql = """
            [Id] integer NOT NULL PRIMARY KEY AUTOINCREMENT, TenantId int NOT NULL DEFAULT 0, [PurchasingAgent] nvarchar(50), [VendorId] int NOT NULL DEFAULT 0, [SettlementMethod] int NOT NULL DEFAULT 0, [PaymentClause] int NOT NULL DEFAULT 0, [PurchaseType] int NOT NULL DEFAULT 0, [UserId] nvarchar(50) NOT NULL DEFAULT '0', [AuditTime] datetime, [OrderNumber] nvarchar(50), [OrderType] int NOT NULL DEFAULT 0, [OrderStatus] int NOT NULL DEFAULT 0, [OrderDate] datetime, [DeliveryDate] datetime, [ReplyDeliveryDate] datetime, [OrderQuantity] int NOT NULL DEFAULT 0, [UnitPriceExcludingTax] decimal(53, 0) NOT NULL DEFAULT 0, [UnitPrice] decimal(53, 0) NOT NULL DEFAULT 0, [TaxIncludedMoney] decimal(53, 0) NOT NULL DEFAULT 0, [TaxRate] decimal(53, 0) NOT NULL DEFAULT 0, [CreateUserId] int NOT NULL DEFAULT 0, [CreateTime] datetime, [CreateIP] nvarchar(50), [UpdateUserId] int NOT NULL DEFAULT 0, [UpdateTime] datetime, [UpdateIP] nvarchar(50), [Remark] nvarchar(1000), [PurchaseOrderNo] nvarchar(50), [VendorCode] nvarchar(50), [SettlementRemark] nvarchar(50), [AttachmentUrl] nvarchar(50), [OrderQty] int, [ContactPerson] nvarchar(50), [ContactNumber] nvarchar(50), [AuditStatus] int, [OrderSource] int, [TaxIncludedMoneyTotal] decimal(53, 0), [AuditUserId] int, [ConsigneeUser] nvarchar(50), [ConsigneeUserTel] nvarchar(50), [ConsigneeUserAddress] nvarchar(50)
            """;

        md.Invoke("ParseColumns", table, sql);

        Assert.Equal(41, table.Columns.Count);

        Assert.Equal("Id", table.Columns[0].Name);
        Assert.Equal("integer", table.Columns[0].RawType);

        Assert.Equal("TenantId", table.Columns[1].Name);
        Assert.Equal("int", table.Columns[1].RawType);

        Assert.Equal("ConsigneeUserAddress", table.Columns[^1].Name);
        Assert.Equal("nvarchar(50)", table.Columns[^1].RawType);
    }

    [Fact]
    public void ParseColumns2()
    {
        var dal = DAL.Create("Membership");
        var md = dal.Db.CreateMetaData();

        var table = new XTable();
        var sql = """
            CREATE TABLE [PurchaseOrder] (
                [Id] integer NOT NULL PRIMARY KEY AUTOINCREMENT,
                [TenantId] int NOT NULL DEFAULT 0,
                [PurchasingAgent] nvarchar(50),
                [VendorId] int NOT NULL DEFAULT 0,
                [SettlementMethod] int NOT NULL DEFAULT 0,
                [PaymentClause] int NOT NULL DEFAULT 0,
                [PurchaseType] int NOT NULL DEFAULT 0,
                [UserId] nvarchar(50) NOT NULL DEFAULT '0',
                [AuditTime] datetime,
                [OrderNumber] nvarchar(50),
                [OrderType] int NOT NULL DEFAULT 0,
                [OrderStatus] int NOT NULL DEFAULT 0,
                [OrderDate] datetime,
                [DeliveryDate] datetime,
                [ReplyDeliveryDate] datetime,
                [OrderQuantity] int NOT NULL DEFAULT 0,
                [UnitPriceExcludingTax] decimal(53, 0) NOT NULL DEFAULT 0,
                [UnitPrice] decimal(53, 0) NOT NULL DEFAULT 0,
                [TaxIncludedMoney] decimal(53, 0) NOT NULL DEFAULT 0,
                [TaxRate] decimal(53, 0) NOT NULL DEFAULT 0,
                [CreateUserId] int NOT NULL DEFAULT 0,
                [CreateTime] datetime,
                [CreateIP] nvarchar(50),
                [UpdateUserId] int NOT NULL DEFAULT 0,
                [UpdateTime] datetime,
                [UpdateIP] nvarchar(50),
                [Remark] nvarchar(1000),
                [PurchaseOrderNo] nvarchar(50),
                [VendorCode] nvarchar(50),
                [SettlementRemark] nvarchar(50),
                [AttachmentUrl] nvarchar(50),
                [OrderQty] int,
                [ContactPerson] nvarchar(50),
                [ContactNumber] nvarchar(50),
                [AuditStatus] int,
                [OrderSource] int,
                [TaxIncludedMoneyTotal] decimal(53, 0),
                [AuditUserId] int,
                [ConsigneeUser] nvarchar(50),
                [ConsigneeUserTel] nvarchar(50),
                [ConsigneeUserAddress] nvarchar(50)
            )
            """;

        md.Invoke("ParseColumns", table, sql);

        Assert.Equal(41, table.Columns.Count);

        Assert.Equal("Id", table.Columns[0].Name);
        Assert.Equal("integer", table.Columns[0].RawType);

        Assert.Equal("TenantId", table.Columns[1].Name);
        Assert.Equal("int", table.Columns[1].RawType);

        Assert.Equal("ConsigneeUserAddress", table.Columns[^1].Name);
        Assert.Equal("nvarchar(50)", table.Columns[^1].RawType);
    }
}