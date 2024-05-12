using System;
using System.Data;
using System.IO;
using System.Linq;
using NewLife;
using NewLife.Log;
using NewLife.Security;
using XCode.DataAccessLayer;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class OracleTests
{
    private String _ConnStr = "Data Source=Tcp://127.0.0.1/ORCL;User Id=scott;Password=tiger";

    public OracleTests()
    {
        var f = "Config\\oracle.config".GetFullPath();
        if (File.Exists(f))
            _ConnStr = File.ReadAllText(f);
        else
            File.WriteAllText(f, _ConnStr);
    }

    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.Oracle);
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
        var db = DbFactory.Create(DatabaseType.Oracle);
        var factory = db.Factory;

        var conn = factory.CreateConnection();
        conn.ConnectionString = "Server=localhost;Port=5236;Database=dameng;user=SYSDBA;password=SYSDBA";
        conn.Open();
    }

    [Fact(Skip = "跳过")]
    public void DALTest()
    {
        DAL.AddConnStr("Oracle", _ConnStr, null, "Oracle");
        var dal = DAL.Create("Oracle");
        Assert.NotNull(dal);
        Assert.Equal("Oracle", dal.ConnName);
        Assert.Equal(DatabaseType.Oracle, dal.DbType);

        var db = dal.Db;
        var connstr = db.ConnectionString;
        Assert.Equal("dameng", db.Owner);
        Assert.Equal("Server=localhost;Port=5236;user=SYSDBA;password=SYSDBA", connstr);

        var ver = db.ServerVersion;
        Assert.NotEmpty(ver);
    }

    [Fact(Skip = "跳过")]
    public void MetaTest()
    {
        DAL.AddConnStr("Oracle", _ConnStr, null, "Oracle");
        var dal = DAL.Create("Oracle");

        var tables = dal.Tables;
        Assert.NotNull(tables);
        Assert.True(tables.Count > 0);
    }

    [Fact(Skip = "跳过")]
    public void SelectTest()
    {
        //DAL.AddConnStr("Membership", _ConnStr, null, "Oracle");
        DAL.AddConnStr("Oracle", _ConnStr, null, "Oracle");

        Role.Meta.ConnName = "Oracle";
        Area.Meta.ConnName = "Oracle";

        Role.Meta.Session.InitData();

        var count = Role.Meta.Count;
        Assert.True(count > 0);

        var list = Role.FindAll();
        Assert.Equal(4, list.Count);

        var list2 = Role.FindAll(Role._.Name == "管理员");
        Assert.Equal(1, list2.Count);

        var list3 = Role.Search("用户", null);
        Assert.Equal(2, list3.Count);

        // 来个耗时操作，把前面堵住
        Area.FetchAndSave();
    }

    [Fact(Skip = "跳过")]
    public void PositiveAndNegative()
    {
        var connName = GetType().Name;
        DAL.AddConnStr(connName, _ConnStr, null, "Oracle");
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
    public void CreateParameterForByteArray()
    {
        var connName = GetType().Name;
        DAL.AddConnStr(connName, _ConnStr, null, "Oracle");
        var dal = DAL.Create(connName);

        var data = "NewLife".GetBytes();
        var dp = dal.Db.CreateParameter("data", data);

        Assert.NotNull(dp);
        Assert.Equal(DbType.Binary, dp.DbType);
    }
}