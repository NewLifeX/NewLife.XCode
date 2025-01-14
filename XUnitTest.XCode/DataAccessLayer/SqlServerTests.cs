using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Security;
using XCode;
using XCode.DataAccessLayer;
using XCode.Membership;
using Xunit;
using XUnitTest.XCode.TestEntity;

namespace XUnitTest.XCode.DataAccessLayer;

[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class SqlServerTests
{
    private static String _ConnStr = "Server=127.0.0.1;Database=sys;Uid=sa;Pwd=sa;Connection Timeout=2";

    public SqlServerTests()
    {
        var f = "Config\\sqlserver.config".GetFullPath();
        if (File.Exists(f))
            _ConnStr = File.ReadAllText(f);
        else
            File.WriteAllText(f.EnsureDirectory(), _ConnStr);
    }

    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.SqlServer);
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
        var db = DbFactory.Create(DatabaseType.SqlServer);
        var factory = db.Factory;

        var conn = factory.CreateConnection();
        //conn.ConnectionString = "Server=localhost;Port=3306;Database=Membership;Uid=root;Pwd=Pass@word";
        conn.ConnectionString = _ConnStr.Replace("Server=.;", "Server=localhost;");
        conn.Open();
    }

    [Fact]
    public void DALTest()
    {
        DAL.AddConnStr("sysSqlServer", _ConnStr, null, "SqlServer");
        var dal = DAL.Create("sysSqlServer");
        Assert.NotNull(dal);
        Assert.Equal("sysSqlServer", dal.ConnName);
        Assert.Equal(DatabaseType.SqlServer, dal.DbType);

        var db = dal.Db;
        var connstr = db.ConnectionString;
        //Assert.Equal("sys", db.DatabaseName);
        Assert.EndsWith(";Application Name=XCode_testhost_sysSqlServer", connstr);

        var ver = db.ServerVersion;
        Assert.NotEmpty(ver);
    }

    [Fact]
    public void MetaTest()
    {
        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership;");
        DAL.AddConnStr("SqlServer_Meta", connStr, null, "SqlServer");
        var dal = DAL.Create("SqlServer_Meta");

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
        DAL.AddConnStr("sysSqlServer", _ConnStr, null, "SqlServer");
        var dal = DAL.Create("sysSqlServer");
        try
        {
            dal.Execute("drop database membership_test");
        }
        catch (Exception ex) { XTrace.WriteException(ex); }

        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership_Test;");
        DAL.AddConnStr("SqlServer_Select", connStr, null, "SqlServer");

        Role.Meta.ConnName = "SqlServer_Select";
        Area.Meta.ConnName = "SqlServer_Select";

        Role.Meta.Session.InitData();

        //// 创建数据库后，等待一段时间，否则可能出现找不到数据库的情况
        //Thread.Sleep(5_000);

        var count = Role.Meta.Count;
        Assert.True(count > 0);

        var list = Role.FindAll();
        Assert.True(list.Count >= 4);

        var list2 = Role.FindAll(Role._.Name == "管理员");
        Assert.Equal(1, list2.Count);

        var list3 = Role.Search("用户", null);
        Assert.Equal(2, list3.Count);

        // 来个耗时操作，把前面堵住
        Area.FetchAndSave();

        // 清理现场
        Task.Run(() =>
        {
            try
            {
                //dal.Execute("drop database membership_test");
                dal.Db.CreateMetaData().SetSchema(DDLSchema.DropDatabase, "membership_test");
            }
            catch (Exception ex)
            {
                //XTrace.WriteException(ex);
                XTrace.WriteLine(ex.Message);
            }
        });
    }

    [Fact]
    public void TablePrefixTest()
    {
        DAL.AddConnStr("sysSqlServer", _ConnStr, null, "SqlServer");
        var dal = DAL.Create("sysSqlServer");
        try
        {
            dal.Execute("drop database membership_table_prefix");
        }
        catch (Exception ex) { XTrace.WriteException(ex); }

        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership_Table_Prefix;");
        connStr += ";TablePrefix=member_";
        DAL.AddConnStr("SqlServer_Table_Prefix", connStr, null, "SqlServer");

        Role.Meta.ConnName = "SqlServer_Table_Prefix";
        //Area.Meta.ConnName = "SqlServer_Table_Prefix";

        Role.Meta.Session.InitData();

        var count = Role.Meta.Count;
        Assert.True(count > 0);

        var list = Role.FindAll();
        Assert.Equal(4, list.Count);

        var list2 = Role.FindAll(Role._.Name == "管理员");
        Assert.Equal(1, list2.Count);

        var list3 = Role.Search("用户", null);
        Assert.Equal(2, list3.Count);

        // 清理现场
        Task.Run(() =>
        {
            try
            {
                //dal.Execute("drop database membership_table_prefix");
                dal.Db.CreateMetaData().SetSchema(DDLSchema.DropDatabase, "membership_table_prefix");
            }
            catch (Exception ex)
            {
                //XTrace.WriteException(ex);
                XTrace.WriteLine(ex.Message);
            }
        });
    }

    private IDisposable CreateForBatch(String action)
    {
        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership_Batch;");
        DAL.AddConnStr("Membership_Batch", connStr, null, "SqlServer");

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

    //[Fact]
    //public void BatchInsertIgnore()
    //{
    //    using var split = CreateForBatch("InsertIgnore");

    //    var list = new List<Role2>
    //    {
    //        new Role2 { Name = "管理员" },
    //        new Role2 { Name = "高级用户" },
    //        new Role2 { Name = "普通用户" }
    //    };
    //    var rs = list.BatchInsert();
    //    Assert.Equal(list.Count, rs);

    //    list = new List<Role2>
    //    {
    //        new Role2 { Name = "管理员" },
    //        new Role2 { Name = "游客" },
    //    };
    //    rs = list.BatchInsertIgnore();
    //    Assert.Equal(1, rs);

    //    var list2 = Role2.FindAll();
    //    Assert.Equal(4, list2.Count);
    //    Assert.Contains(list2, e => e.Name == "管理员");
    //    Assert.Contains(list2, e => e.Name == "高级用户");
    //    Assert.Contains(list2, e => e.Name == "普通用户");
    //    Assert.Contains(list2, e => e.Name == "游客");
    //}

    //[Fact]
    //public void BatchReplace()
    //{
    //    using var split = CreateForBatch("Replace");

    //    var list = new List<Role2>
    //    {
    //        new Role2 { Name = "管理员", Remark="guanliyuan" },
    //        new Role2 { Name = "高级用户", Remark="gaoji" },
    //        new Role2 { Name = "普通用户", Remark="putong" }
    //    };
    //    var rs = list.BatchInsert();
    //    Assert.Equal(list.Count, rs);

    //    var gly = list.FirstOrDefault(e => e.Name == "管理员");
    //    Assert.NotNull(gly);
    //    Assert.Equal("guanliyuan", gly.Remark);

    //    list = new List<Role2>
    //    {
    //        new Role2 { Name = "管理员" },
    //        new Role2 { Name = "游客", Remark="guest" },
    //    };
    //    rs = list.BatchReplace();
    //    // 删除一行，插入2行
    //    Assert.Equal(3, rs);

    //    var list2 = Role2.FindAll();
    //    Assert.Equal(4, list2.Count);
    //    Assert.Contains(list2, e => e.Name == "管理员");
    //    Assert.Contains(list2, e => e.Name == "高级用户");
    //    Assert.Contains(list2, e => e.Name == "普通用户");
    //    Assert.Contains(list2, e => e.Name == "游客");

    //    var gly2 = list2.FirstOrDefault(e => e.Name == "管理员");
    //    Assert.NotNull(gly2);
    //    Assert.Null(gly2.Remark);
    //    // 管理员被删除后重新插入，自增ID改变
    //    Assert.NotEqual(gly.ID, gly2.ID);
    //}

    [Fact]
    public void PositiveAndNegative()
    {
        var connName = GetType().Name;
        DAL.AddConnStr(connName, _ConnStr, null, "SqlServer");
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
    public void Backup()
    {
        DAL.AddConnStr("bakSqlServer", _ConnStr, null, "SqlServer");
        var dal = DAL.Create("bakSqlServer");
        var meta = dal.Db.CreateMetaData();

        var file1 = meta.SetSchema(DDLSchema.BackupDatabase) as String;
        Assert.NotEmpty(file1);
        Assert.True(File.Exists(file1));
        File.Delete(file1);

        var dbname = "AO_Test";
        var file2 = meta.SetSchema(DDLSchema.BackupDatabase, dbname) as String;
        Assert.NotEmpty(file2);
        Assert.Contains(dbname, file2);
        Assert.True(File.Exists(file2));
        File.Delete(file2);

        var file = $"bak_{Rand.NextString(8)}.bak";
        var file4 = meta.SetSchema(DDLSchema.BackupDatabase, dbname, file) as String;
        Assert.NotEmpty(file4);
        Assert.Equal(file, Path.GetFileName(file4));
        Assert.True(File.Exists(file4));
        File.Delete(file4);
    }

    [Fact(Skip = "跳过")]
    public void Restore()
    {
        DAL.AddConnStr("restoreSqlServer", _ConnStr, null, "SqlServer");
        var dal = DAL.Create("restoreSqlServer");
        var meta = dal.Db.CreateMetaData();

        var result = meta.SetSchema(DDLSchema.RestoreDatabase) as String;
        Assert.Empty(result);

        var result2 = meta.SetSchema(DDLSchema.RestoreDatabase, "C:\\bak_bvi93mq5.bak") as String;
        Assert.NotEmpty(result2);
        Assert.Equal("ok", result2);


        var result3 = meta.SetSchema(DDLSchema.RestoreDatabase, "C:\\bak_bvi93mq5.bak", "D:\\Program Files (x86)\\Microsoft SQL Server\\MSSQL10_50.MSSQLSERVER\\MSSQL\\DATA") as String;
        Assert.NotEmpty(result3);
        Assert.Equal("ok", result3);

    }

    [Fact]
    public void QuerySqlTest()
    {
        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership;");

        DAL.AddConnStr("sysSqlServerv", connStr, null, "SqlServer");
        var dal = DAL.Create("sysSqlServerv");

        //dal.SetTables(Role.Meta.Table.DataTable);
        Role.Meta.Session.InitData();

        dal.Query<Role>("select * from Role order by id");

        var p1 = new PageParameter() { PageSize = 20, PageIndex = 1, Sort = "Id", RetrieveTotalCount = true, Desc = false };
        dal.Query<Role>("select * from Role ", null, p1);

        var p2 = new PageParameter() { PageSize = 20, PageIndex = 1, Sort = "Id", RetrieveTotalCount = true, Desc = false };
        dal.Query<Role>("select * from Role", null, p2);


        dal.Query<Role>("select * from Role order by id");

        var p11 = new PageParameter() { PageSize = 20, PageIndex = 2, Sort = "Id", RetrieveTotalCount = true, Desc = false };
        dal.Query<Role>("select * from Role ", null, p11);

        var p21 = new PageParameter() { PageSize = 20, PageIndex = 2, Sort = "Id", RetrieveTotalCount = true, Desc = false };
        dal.Query<Role>("select * from Role", null, p21);
        // 清理现场
        //try
        //{
        //    dal.Execute("drop database membership_test");
        //}
        //catch (Exception ex) { XTrace.WriteException(ex); }
    }

    [Fact]
    public void CreateTableWithStringLength()
    {
        //var table = User.Meta.Table.DataTable.Clone() as IDataTable;
        //table.TableName = "user_sqlserver";
        //table.DbType = DatabaseType.SqlServer;

        var str = """
            <EntityModel>
             <Table Name="ActEvtLog" TableName="ACT_EVT_LOG" DbType="SqlServer">
                <Columns>
                  <Column Name="LogNr" ColumnName="LOG_NR_" DataType="Decimal" RawType="numeric(19, 0)" Identity="True" PrimaryKey="True" />
                  <Column Name="Type" ColumnName="TYPE_" DataType="String" Length="64" />
                  <Column Name="ProcDefId" ColumnName="PROC_DEF_ID_" DataType="String" Length="64" />
                  <Column Name="ProcInstId" ColumnName="PROC_INST_ID_" DataType="String" Length="64" />
                  <Column Name="ExecutionId" ColumnName="EXECUTION_ID_" DataType="String" Length="64" />
                  <Column Name="TaskId" ColumnName="TASK_ID_" DataType="String" Length="64" />
                  <Column Name="TimeStamp" ColumnName="TIME_STAMP_" DataType="DateTime" Scale="3" Nullable="False" />
                  <Column Name="UserId" ColumnName="USER_ID_" DataType="String" Length="255" />
                  <Column Name="Data" ColumnName="DATA_" DataType="Byte[]" RawType="varbinary(-1)" Length="-1" />
                  <Column Name="LockOwner" ColumnName="LOCK_OWNER_" DataType="String" Length="255" />
                  <Column Name="LockTime" ColumnName="LOCK_TIME_" DataType="DateTime" Scale="3" />
                  <Column Name="IsProcessed" ColumnName="IS_PROCESSED_" DataType="Byte" Nullable="True" />
                </Columns>
              </Table>
            </EntityModel>
            """;
        var table = DAL.Import(str).FirstOrDefault();
        Assert.NotNull(table);
        Assert.Equal("ActEvtLog", table.Name);
        Assert.Equal("ACT_EVT_LOG", table.TableName);
        Assert.Equal(DatabaseType.SqlServer, table.DbType);

        var db = DbFactory.Create(DatabaseType.SqlServer);
        var meta = db.CreateMetaData();
        var sql = meta.GetSchemaSQL(DDLSchema.CreateTable, table);

        var targetSql = """
            Create Table ACT_EVT_LOG(
            	LOG_NR_ money IDENTITY(1,1) Primary Key,
            	TYPE_ nvarchar(64) NULL,
            	PROC_DEF_ID_ nvarchar(64) NULL,
            	PROC_INST_ID_ nvarchar(64) NULL,
            	EXECUTION_ID_ nvarchar(64) NULL,
            	TASK_ID_ nvarchar(64) NULL,
            	TIME_STAMP_ datetime NOT NULL DEFAULT '0001-01-01',
            	USER_ID_ nvarchar(255) NULL,
            	DATA_ varbinary(-1) NULL,
            	LOCK_OWNER_ nvarchar(255) NULL,
            	LOCK_TIME_ datetime NULL,
            	IS_PROCESSED_ tinyint NULL
            )
            """;
        Assert.Equal(targetSql, sql);
    }

    [Fact]
    public void GetTables()
    {
        DAL.AddConnStr("sysSqlServer", _ConnStr, null, "SqlServer");
        var dal = DAL.Create("sysSqlServer");

        var dbprovider = dal.DbType.ToString();
        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = dal.ConnStr
        };

        var dt = dal.Db.CreateSession().GetSchema(null, "Databases", null);
        var sysdbnames = new String[] { "master", "tempdb", "model", "msdb" };
        foreach (DataRow dr in dt.Rows)
        {
            var dbname = dr[0].ToString();
            if (Array.IndexOf(sysdbnames, dbname) >= 0) continue;

            var connName = String.Format("{0}_{1}", "ms", dbname);

            builder["Database"] = dbname;
            var connstr = builder.ToString();
            DAL.AddConnStr(connName, connstr, null, dbprovider);

            try
            {
                var dal2 = DAL.Create(connName);
                var tables = dal2.Tables;
                XTrace.WriteLine("数据库{0}有表{1}张", dbname, tables.Count);

                var xml = DAL.Export(tables);
                File.WriteAllText($"data\\{connName}.xml".GetFullPath(), xml);
            }
            catch
            {
                if (DAL.ConnStrs.ContainsKey(connName)) DAL.ConnStrs.Remove(connName);
            }
        }
    }
}