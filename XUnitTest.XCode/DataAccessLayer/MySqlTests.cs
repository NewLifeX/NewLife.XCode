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
public class MySqlTests
{
    private static String _ConnStr = "Server=.;Port=3306;Database=sys;Uid=root;Pwd=root";

    public MySqlTests()
    {
        var f = "Config\\mysql.config".GetFullPath();
        if (File.Exists(f))
            _ConnStr = File.ReadAllText(f);
        else
            File.WriteAllText(f, _ConnStr);
    }

    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.MySql);
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
        var db = DbFactory.Create(DatabaseType.MySql);
        var factory = db.Factory;

        var conn = factory.CreateConnection();
        //conn.ConnectionString = "Server=localhost;Port=3306;Database=Membership;Uid=root;Pwd=Pass@word";
        conn.ConnectionString = _ConnStr.Replace("Server=.;", "Server=localhost;");
        conn.Open();
    }

    [Fact]
    public void DALTest()
    {
        DAL.AddConnStr("sysMySql", _ConnStr, null, "MySql");
        var dal = DAL.Create("sysMySql");
        Assert.NotNull(dal);
        Assert.Equal("sysMySql", dal.ConnName);
        Assert.Equal(DatabaseType.MySql, dal.DbType);

        var db = dal.Db;
        var connstr = db.ConnectionString;
        Assert.Equal("sys", db.DatabaseName);
        //Assert.EndsWith("CharSet=utf8mb4;Sslmode=none;AllowPublicKeyRetrieval=true", connstr);
        //Assert.EndsWith("CharSet=utf8mb4;Sslmode=none", connstr);
        Assert.Contains("Sslmode=Preferred", connstr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CharSet=utf8mb4", connstr, StringComparison.OrdinalIgnoreCase);

        using var conn = db.OpenConnection();
        connstr = conn.ConnectionString;
        //Assert.EndsWith("characterset=utf8mb4;sslmode=Disabled;allowpublickeyretrieval=True", connstr);
        Assert.Contains("=utf8mb4", connstr);
        //Assert.Contains("sslmode=", connstr);
        //Assert.Contains("allowpublickeyretrieval=True", connstr);

        var ver = db.ServerVersion;
        Assert.NotEmpty(ver);
    }

    [Fact]
    public void MetaTest()
    {
        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership;");
        DAL.AddConnStr("MySql_Meta", connStr, null, "MySql");
        var dal = DAL.Create("MySql_Meta");

        // 反向工程
        dal.SetTables(User.Meta.Table.DataTable);

        var tables = dal.Tables;
        Assert.NotNull(tables);
        Assert.True(tables.Count > 0);

        var tb = tables.FirstOrDefault(e => e.Name == "User");
        Assert.NotNull(tb);
        Assert.NotEmpty(tb.Description);
    }

    [Fact]
    public void SelectTest()
    {
        DAL.AddConnStr("sysMySql", _ConnStr, null, "MySql");
        var dal = DAL.Create("sysMySql");
        try
        {
            dal.Execute("drop database membership_test");
        }
        catch (Exception ex)
        {
            XTrace.WriteLine(ex.Message);
        }

        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership_Test;");
        DAL.AddConnStr("MySql_Select", connStr, null, "MySql");

        Role.Meta.ConnName = "MySql_Select";
        Area.Meta.ConnName = "MySql_Select";

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

    [Fact]
    public void MembershipTest()
    {
        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership;");
        DAL.AddConnStr("MySql_member", connStr, null, "MySql");

        User.Meta.ConnName = "MySql_member";
        Role.Meta.ConnName = "MySql_member";

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

    [Fact]
    public void TablePrefixTest()
    {
        DAL.AddConnStr("sysMySql", _ConnStr, null, "MySql");
        var dal = DAL.Create("sysMySql");
        try
        {
            dal.Execute("drop database membership_table_prefix");
        }
        catch (Exception ex) { XTrace.WriteException(ex); }

        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership_Table_Prefix;");
        connStr += ";TablePrefix=member_";
        DAL.AddConnStr("MySql_Table_Prefix", connStr, null, "MySql");

        Role.Meta.ConnName = "MySql_Table_Prefix";
        //Area.Meta.ConnName = "MySql_Table_Prefix";

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
        DAL.AddConnStr("Membership_Batch_mysql", connStr, null, "MySql");

        var dt = Role2.Meta.Table.DataTable.Clone() as IDataTable;
        dt.TableName = $"Role2_{action}";

        // 分表
        var split = Role2.Meta.CreateSplit("Membership_Batch_mysql", dt.TableName);

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

    [Fact]
    public void GetTables()
    {
        DAL.AddConnStr("member_mysql", _ConnStr.Replace("Database=sys", "Database=membership"), null, "MySql");
        var dal = DAL.Create("member_mysql");
        var tables = dal.Tables;

        Assert.NotEmpty(tables);

        foreach (var table in tables)
        {
            Assert.NotEmpty(table.Columns);
            foreach (var dc in table.Columns)
            {
                Assert.NotEmpty(dc.Name);
                Assert.NotEmpty(dc.ColumnName);
                Assert.NotEmpty(dc.RawType);
                Assert.NotNull(dc.DataType);
                Assert.NotEmpty(dc.DisplayName);
                Assert.NotEmpty(dc.Description);
            }

            //Assert.NotEmpty(table.Indexes);
            foreach (var di in table.Indexes)
            {
                Assert.NotEmpty(di.Name);
                Assert.NotEmpty(di.Columns);
            }
        }

        dal.SetTables(User.Meta.Table.DataTable);
    }

    [Fact]
    public void PositiveAndNegative()
    {
        var connName = GetType().Name;
        DAL.AddConnStr(connName, _ConnStr, null, "MySql");
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

    [Fact]
    public void SelectTinyintTest()
    {
        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership;");
        DAL.AddConnStr("MySql_tinyint", connStr, null, "MySql");

        var dal = DAL.Create("MySql_tinyint");

        var table = User.Meta.Table.DataTable.Clone() as IDataTable;
        table.TableName = "user_tinyint";
        table.DbType = DatabaseType.MySql;

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

    [Fact]
    public void CreateTableWithMyISAM()
    {
        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership;");
        DAL.AddConnStr("MySql_member", connStr, null, "MySql");

        var dal = DAL.Create("MySql_member");

        var table = User.Meta.Table.DataTable.Clone() as IDataTable;
        table.TableName = "user_isam";
        table.DbType = DatabaseType.MySql;
        table.Properties["Engine"] = "MyISAM";

        var meta = dal.Db.CreateMetaData();
        var sql = meta.GetSchemaSQL(DDLSchema.CreateTable, table);

        Assert.Contains(" ENGINE=MyISAM", sql);

        if (dal.TableNames.Contains(table.TableName))
            dal.Db.CreateMetaData().SetSchema(DDLSchema.DropTable, table);

        dal.SetTables(table);

        Assert.Contains(dal.Tables, t => t.TableName == table.TableName);
    }

    [Fact]
    public void CreateTableWithCompressed()
    {
        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership;");
        DAL.AddConnStr("MySql_member", connStr, null, "MySql");

        var dal = DAL.Create("MySql_member");

        var table = User.Meta.Table.DataTable.Clone() as IDataTable;
        table.TableName = "user_compressed";
        table.DbType = DatabaseType.MySql;
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
        table.DbType = DatabaseType.MySql;
        Assert.Equal("COMPRESSED", table.Properties["ROW_FORMAT"]);
        Assert.Equal("4", table.Properties["KEY_BLOCK_SIZE"]);

        sql = meta.GetSchemaSQL(DDLSchema.CreateTable, table);

        Assert.Contains(" ROW_FORMAT=COMPRESSED", sql);
        Assert.Contains(" KEY_BLOCK_SIZE=4", sql);
    }

    [Fact]
    public void CreateTableWithArchive()
    {
        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership;");
        DAL.AddConnStr("MySql_member", connStr, null, "MySql");

        var dal = DAL.Create("MySql_member");

        var table = User.Meta.Table.DataTable.Clone() as IDataTable;
        table.TableName = "user_archive";
        table.DbType = DatabaseType.MySql;
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