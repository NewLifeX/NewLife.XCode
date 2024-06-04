using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
public class HighGoTests
{
    private static String _ConnStr = "Server=127.0.0.1;User Id=highgo;Password=P12345!@;Database=highgo;Port=5866";

    public HighGoTests()
    {
        var f = "Config\\HighGo.config".GetFullPath();
        if (File.Exists(f))
            _ConnStr = File.ReadAllText(f);
        else
            File.WriteAllText(f.EnsureDirectory(), _ConnStr);
    }

    [Fact(Skip = "跳过")]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.HighGo);
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
        var db = DbFactory.Create(DatabaseType.HighGo);
        var factory = db.Factory;
        var conn = factory.CreateConnection();
        conn.ConnectionString = _ConnStr;
        conn.Open();
    }

    [Fact(Skip = "跳过")]
    public void DALTest()
    {
        DAL.AddConnStr("HighGo", _ConnStr, null, "HighGo");
        var dal = DAL.Create("HighGo");
        Assert.NotNull(dal);
        Assert.Equal("HighGo", dal.ConnName);
        Assert.Equal(DatabaseType.HighGo, dal.DbType);

        var db = dal.Db;
        var connstr = db.ConnectionString;
        Assert.EndsWith(_ConnStr, connstr);

        var ver = db.ServerVersion;
        Assert.NotEmpty(ver);
    }

    [Fact(Skip = "跳过")]
    public void MetaTest()
    {
        DAL.AddConnStr("HighGo", _ConnStr, null, "HighGo");
        DAL.AddConnStr("Membership", _ConnStr, null, "HighGo");
        var dal = DAL.Create("HighGo");
        // 反向工程
        dal.SetTables(User.Meta.Table.DataTable);
        EntityFactory.InitConnection("Membership");

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
        DAL.AddConnStr("HighGo", _ConnStr, null, "HighGo");
        var dal = DAL.Create("HighGo");
        try
        {
            dal.Execute("drop database  if EXISTS \"test\"");
        }
        catch (Exception ex) { XTrace.WriteException(ex); }

        var connStr = _ConnStr.Replace("Database=highgo;", "Database=Membership_Test;");
        DAL.AddConnStr("HighGo_Select", connStr, null, "HighGo");

        Role.Meta.ConnName = "HighGo_Select";
        Area.Meta.ConnName = "HighGo_Select";

        Role.Meta.Session.InitData();

        var count = Role.Meta.Count;
        Assert.True(count > 0);

        var list = Role.FindAll();
        Assert.True(list.Count >= 4);

        var list2 = Role.FindAll(Role._.Name == "管理员");
        Assert.Equal(1, list2.Count);

        var list3 = Role.Search("用户", null);
        Assert.Equal(2, list3.Count);

        var list4 = Role.FindAll(Role._.Name.Contains("用户"));
        Assert.Equal(2, list4.Count);
        var list5 = Role.FindAll(Role._.Name.StartsWith("用户"));

        var list6 = Role.FindAll(Role._.Name.EndsWith("用户"));
        Assert.Equal(2, list6.Count);
        var list7 = Role.FindAll(Role._.Name.NotContains("用户"));
        Assert.Equal(2, list7.Count);

        // 清理现场
        try
        {
            dal.Execute("drop database if EXISTS \"Membership_Test\"");
        }
        catch (Exception ex) { XTrace.WriteException(ex); }
    }

    [Fact(Skip = "跳过")]
    public void TablePrefixTest()
    {
        DAL.AddConnStr("HighGo", _ConnStr, null, "HighGo");
        var dal = DAL.Create("HighGo");
        try
        {
            dal.Execute("drop database if EXISTS \"Membership_Table_Prefix\"");
        }
        catch (Exception ex) { XTrace.WriteException(ex); }

        var connStr = _ConnStr.Replace("Database=highgo;", "Database=Membership_Table_Prefix;");
        connStr += ";TablePrefix=member_";
        DAL.AddConnStr("HighGo_Table_Prefix", connStr, null, "HighGo");

        Role.Meta.ConnName = "HighGo_Table_Prefix";

        Role.Meta.Session.InitData();

        var count = Role.Meta.Count;
        Assert.True(count > 0);

        var list = Role.FindAll();
        Assert.Equal(4, list.Count);

        var list2 = Role.FindAll(Role._.Name == "管理员");
        Assert.Equal(1, list2.Count);


        var list3 = Role.Search("用户", null);
        Assert.Equal(2, list3.Count);
    }

    private IDisposable CreateForBatch(String action)
    {
        var connStr = _ConnStr.Replace("Database=highgo;", "Database=Membership_Batch;");
        DAL.AddConnStr("Membership_Batch", connStr, null, "HighGo");
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
    public void PositiveAndNegative()
    {
        var connName = GetType().Name;
        DAL.AddConnStr(connName, _ConnStr, null, "HighGo");
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
    public void QuerySqlTest()
    {
        DAL.AddConnStr("Membership", _ConnStr, null, "HighGo");
        var dal = DAL.Create("Membership");
        var a = dal.Query<Role>("select * from \"Role\" order by \"ID\"");

        var p1 = new PageParameter() { PageSize = 20, PageIndex = 1, Sort = "\"ID\"", RetrieveTotalCount = true, Desc = false };
        a = dal.Query<Role>("select * from \"Role\" ", null, p1);

        var p2 = new PageParameter() { PageSize = 20, PageIndex = 1, Sort = "\"ID\"", RetrieveTotalCount = true, Desc = false };
        a = dal.Query<Role>("select * from \"Role\"", null, p2);


        dal.Query<Role>("select * from \"Role\" order by \"ID\"");

        var p11 = new PageParameter() { PageSize = 20, PageIndex = 2, Sort = "\"ID\"", RetrieveTotalCount = true, Desc = false };
        a = dal.Query<Role>("select * from \"Role\" ", null, p11);

        var p21 = new PageParameter() { PageSize = 20, PageIndex = 2, Sort = "\"ID\"", RetrieveTotalCount = true, Desc = false };
        a = dal.Query<Role>("select * from \"Role\"", null, p21);
        // 清理现场
        //try
        //{
        //    dal.Execute("drop database membership_test");
        //}
        //catch (Exception ex) { XTrace.WriteException(ex); }
    }

    [Fact(Skip = "跳过")]
    public void CreateTableWithStringLength()
    {
        //var table = User.Meta.Table.DataTable.Clone() as IDataTable;
        //table.TableName = "user_sqlserver";
        //table.DbType = DatabaseType.SqlServer;

        var str = """
            <EntityModel>
             <Table Name="ActEvtLog" TableName="ACT_EVT_LOG" DbType="HighGo">
                <Columns>
                  <Column Name="LogNr" ColumnName="LOG_NR_" DataType="Int32" RawType="numeric(19, 0)" Identity="True" PrimaryKey="True" />
                  <Column Name="Type" ColumnName="TYPE_" DataType="String" Length="64" />
                  <Column Name="ProcDefId" ColumnName="PROC_DEF_ID_" DataType="String" Length="64" />
                  <Column Name="ProcInstId" ColumnName="PROC_INST_ID_" DataType="String" Length="64" />
                  <Column Name="ExecutionId" ColumnName="EXECUTION_ID_" DataType="String" Length="64" />
                  <Column Name="TaskId" ColumnName="TASK_ID_" DataType="String" Length="64" />
                  <Column Name="TimeStamp" ColumnName="TIME_STAMP_" DataType="DateTime" Scale="3" Nullable="False" />
                  <Column Name="UserId" ColumnName="USER_ID_" DataType="String" Length="255" />
                  <Column Name="Data" ColumnName="DATA_" DataType="Byte[]" />
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
        Assert.Equal(DatabaseType.HighGo, table.DbType);

        var db = DbFactory.Create(DatabaseType.HighGo);
        var meta = db.CreateMetaData();
        var sql = meta.GetSchemaSQL(DDLSchema.CreateTable, table);

        var targetSql = @$"Create Table ""ACT_EVT_LOG""(
	""LOG_NR_"" serial Primary Key,
	""TYPE_"" varchar(64) NULL,
	""PROC_DEF_ID_"" varchar(64) NULL,
	""PROC_INST_ID_"" varchar(64) NULL,
	""EXECUTION_ID_"" varchar(64) NULL,
	""TASK_ID_"" varchar(64) NULL,
	""TIME_STAMP_"" timestamp NOT NULL DEFAULT '0001-01-01',
	""USER_ID_"" varchar(255) NULL,
	""DATA_"" bytea NULL,
	""LOCK_OWNER_"" varchar(255) NULL,
	""LOCK_TIME_"" timestamp NULL,
	""IS_PROCESSED_"" bit null
)";
        Assert.Equal(targetSql, sql, true);
    }

    [Fact(Skip = "跳过")]
    public void BuildDeleteSql()
    {
        DAL.AddConnStr("HighGo", _ConnStr, null, "HighGo");
        var dal = DAL.Create("HighGo");
        Role.Meta.ConnName = "HighGo";
        Role.Meta.Session.InitData();
        var count = Role.Delete(Role._.Name == "管理员");
        Assert.Equal(count, 1);
    }
}