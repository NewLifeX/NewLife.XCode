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

[Collection("Database")]
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class HanaTests
{
    private static String _ConnStr = "Server=.;Port=3306;Database=sys;Uid=root;Pwd=root";

    public HanaTests()
    {
        var f = "Config\\hana.config".GetFullPath();
        if (File.Exists(f))
            _ConnStr = File.ReadAllText(f);
        else
            File.WriteAllText(f, _ConnStr);
    }

    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.Hana);
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

    [Fact(Skip = "跳过")]
    public void ConnectTest()
    {
        var db = DbFactory.Create(DatabaseType.Hana);
        var factory = db.Factory;

        var conn = factory.CreateConnection();
        //conn.ConnectionString = "Server=localhost;Port=3306;Database=Membership;Uid=root;Pwd=Pass@word";
        conn.ConnectionString = _ConnStr.Replace("Server=.;", "Server=localhost;");
        conn.Open();
    }

    [Fact(Skip = "跳过")]
    public void DALTest()
    {
        DAL.AddConnStr("sysHana", _ConnStr, null, "Hana");
        var dal = DAL.Create("sysHana");
        Assert.NotNull(dal);
        Assert.Equal("sysHana", dal.ConnName);
        Assert.Equal(DatabaseType.Hana, dal.DbType);

        var db = dal.Db;
        var connstr = db.ConnectionString;
        Assert.Equal("sys", db.DatabaseName);
        //Assert.EndsWith("CharSet=utf8mb4;Sslmode=none;AllowPublicKeyRetrieval=true", connstr);
        Assert.EndsWith("CharSet=utf8mb4;Sslmode=none", connstr);

        using var conn = db.OpenConnection();
        connstr = conn.ConnectionString;
        //Assert.EndsWith("characterset=utf8mb4;sslmode=Disabled;allowpublickeyretrieval=True", connstr);
        Assert.Contains("characterset=utf8mb4", connstr);
        Assert.Contains("sslmode=", connstr);
        Assert.Contains("allowpublickeyretrieval=True", connstr);

        var ver = db.ServerVersion;
        Assert.NotEmpty(ver);
    }

    [Fact(Skip = "跳过")]
    public void MetaTest()
    {
        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership;");
        DAL.AddConnStr("Hana_Meta", connStr, null, "Hana");
        var dal = DAL.Create("Hana_Meta");

        // 反向工程
        dal.SetTables(User.Meta.Table.DataTable);

        var tables = dal.Tables;
        Assert.NotNull(tables);
        Assert.True(tables.Count > 0);

        var tb = tables.FirstOrDefault(e => e.Name == "User");
        Assert.NotNull(tb);
        Assert.NotEmpty(tb.Description);
    }

    [Fact(Skip = "跳过")]
    public void SelectTest()
    {
        DAL.AddConnStr("sysHana", _ConnStr, null, "Hana");
        var dal = DAL.Create("sysHana");
        try
        {
            dal.Execute("drop database membership_test");
        }
        catch (Exception ex) { XTrace.WriteException(ex); }

        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership_Test;");
        DAL.AddConnStr("Hana_Select", connStr, null, "Hana");

        Role.Meta.ConnName = "Hana_Select";
        Area.Meta.ConnName = "Hana_Select";

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
        try
        {
            dal.Execute("drop database membership_test");
        }
        catch (Exception ex) { XTrace.WriteException(ex); }
    }

    [Fact(Skip = "跳过")]
    public void TablePrefixTest()
    {
        DAL.AddConnStr("sysHana", _ConnStr, null, "Hana");
        var dal = DAL.Create("sysHana");
        try
        {
            dal.Execute("drop database membership_table_prefix");
        }
        catch (Exception ex) { XTrace.WriteException(ex); }

        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership_Table_Prefix;");
        connStr += ";TablePrefix=member_";
        DAL.AddConnStr("Hana_Table_Prefix", connStr, null, "Hana");

        Role.Meta.ConnName = "Hana_Table_Prefix";
        //Area.Meta.ConnName = "Hana_Table_Prefix";

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
        try
        {
            dal.Execute("drop database membership_table_prefix");
        }
        catch (Exception ex) { XTrace.WriteException(ex); }
    }

    private IDisposable CreateForBatch(String action)
    {
        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership_Batch;");
        DAL.AddConnStr("Membership_Batch_hana", connStr, null, "Hana");

        var dt = Role2.Meta.Table.DataTable.Clone() as IDataTable;
        dt.TableName = $"Role2_{action}";

        // 分表
        var split = Role2.Meta.CreateSplit("Membership_Batch_hana", dt.TableName);

        var session = Role2.Meta.Session;
        session.Dal.SetTables(dt);

        // 清空数据
        session.Truncate();

        return split;
    }

    [Fact(Skip = "跳过")]
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

    [Fact(Skip = "跳过")]
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

    [Fact(Skip = "跳过")]
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

        list = new List<Role2>
        {
            new Role2 { Name = "管理员" },
            new Role2 { Name = "游客", Remark="guest" },
        };
        rs = list.BatchReplace();
        // 删除一行，插入2行
        Assert.Equal(3, rs);

        var list2 = Role2.FindAll();
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

    [Fact(Skip = "跳过")]
    public void GetTables()
    {
        DAL.AddConnStr("member_hana", _ConnStr.Replace("Database=sys", "Database=membership"), null, "Hana");
        var dal = DAL.Create("member_hana");
        var tables = dal.Tables;

        Assert.True(tables.Count > 0);

        dal.SetTables(User.Meta.Table.DataTable);
    }

    [Fact(Skip = "跳过")]
    public void PositiveAndNegative()
    {
        var connName = GetType().Name;
        DAL.AddConnStr(connName, _ConnStr, null, "Hana");
        var dal = DAL.Create(connName);

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

        tableNames = dal.GetTableNames();
        XTrace.WriteLine("tableNames: {0}", tableNames.Join());
        Assert.DoesNotContain(table.TableName, tableNames);
    }

    [Fact(Skip = "跳过")]
    public void SelectTinyintTest()
    {
        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership;");
        DAL.AddConnStr("Hana_tinyint", connStr, null, "Hana");

        var dal = DAL.Create("Hana_tinyint");

        var table = User.Meta.Table.DataTable.Clone() as IDataTable;
        table.TableName = "user_tinyint";
        table.DbType = DatabaseType.Hana;

        var column = table.GetColumn("RoleId");
        column.DataType = typeof(Byte);
        column.RawType = "tinyint(1)";

        //if (dal.TableNames.Contains(table.TableName)) 
        //    dal.Db.CreateMetaData().SetSchema(DDLSchema.DropTable, table);

        dal.SetTables(table);

        User.Meta.ConnName = dal.ConnName;
        User.Meta.TableName = table.TableName;

        var count = User.FindCount();
        XTrace.WriteLine("count={0}", count);

        var user = User.FindByName("stone");
        if (user == null)
        {
            user = new User { Name = "stone", RoleID = 4 };
            user.Insert();
        }

        XTrace.WriteLine("SelectDs");
        var ds = dal.Select("select * from user_tinyint");

        var list = User.FindAll();
        XTrace.WriteLine(list.ToJson(true));
    }

    [Fact(Skip = "跳过")]
    public void CreateTableWithMyISAM()
    {
        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership;");
        DAL.AddConnStr("Hana_member", connStr, null, "Hana");

        var dal = DAL.Create("Hana_member");

        var table = User.Meta.Table.DataTable.Clone() as IDataTable;
        table.TableName = "user_isam";
        table.DbType = DatabaseType.Hana;
        table.Properties["Engine"] = "MyISAM";

        var meta = dal.Db.CreateMetaData();
        var sql = meta.GetSchemaSQL(DDLSchema.CreateTable, table);

        Assert.Contains(" ENGINE=MyISAM", sql);

        if (dal.TableNames.Contains(table.TableName))
            dal.Db.CreateMetaData().SetSchema(DDLSchema.DropTable, table);

        dal.SetTables(table);

        Assert.Contains(dal.Tables, t => t.TableName == table.TableName);
    }

    [Fact(Skip = "跳过")]
    public void CreateTableWithCompressed()
    {
        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership;");
        DAL.AddConnStr("Hana_member", connStr, null, "Hana");

        var dal = DAL.Create("Hana_member");

        var table = User.Meta.Table.DataTable.Clone() as IDataTable;
        table.TableName = "user_compressed";
        table.DbType = DatabaseType.Hana;
        table.Properties["ROW_FORMAT"] = "COMPRESSED";
        table.Properties["KEY_BLOCK_SIZE"] = "4";

        var meta = dal.Db.CreateMetaData();
        var sql = meta.GetSchemaSQL(DDLSchema.CreateTable, table);

        Assert.Contains(" ROW_FORMAT=COMPRESSED", sql);
        Assert.Contains(" KEY_BLOCK_SIZE=4", sql);

        if (dal.TableNames.Contains(table.TableName))
            dal.Db.CreateMetaData().SetSchema(DDLSchema.DropTable, table);

        dal.SetTables(table);

        Assert.Contains(dal.Tables, t => t.TableName == table.TableName);

        // Log表自带压缩表能力
        table = Log.Meta.Table.DataTable.Clone() as IDataTable;
        table.DbType = DatabaseType.Hana;
        Assert.Equal("COMPRESSED", table.Properties["ROW_FORMAT"]);
        Assert.Equal("4", table.Properties["KEY_BLOCK_SIZE"]);

        sql = meta.GetSchemaSQL(DDLSchema.CreateTable, table);

        Assert.Contains(" ROW_FORMAT=COMPRESSED", sql);
        Assert.Contains(" KEY_BLOCK_SIZE=4", sql);
    }

    [Fact(Skip = "跳过")]
    public void CreateTableWithArchive()
    {
        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership;");
        DAL.AddConnStr("Hana_member", connStr, null, "Hana");

        var dal = DAL.Create("Hana_member");

        var table = User.Meta.Table.DataTable.Clone() as IDataTable;
        table.TableName = "user_archive";
        table.DbType = DatabaseType.Hana;
        table.Properties["Engine"] = "Archive";

        var meta = dal.Db.CreateMetaData();
        var sql = meta.GetSchemaSQL(DDLSchema.CreateTable, table);

        Assert.Contains(" ENGINE=Archive", sql);

        if (dal.TableNames.Contains(table.TableName))
            dal.Db.CreateMetaData().SetSchema(DDLSchema.DropTable, table);

        dal.SetTables(table);

        Assert.Contains(dal.Tables, t => t.TableName == table.TableName);
    }
}