﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NewLife;
using NewLife.Log;
using NewLife.Security;
using XCode.DataAccessLayer;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class DaMengTests
{
    private static String _ConnStr = "Server=.;Port=5236;owner=dameng;user=SYSDBA;password=SYSDBA";

    [Fact]
    public void LoadDllTest()
    {
        var file = "DmProvider.dll".GetFullPath();
        if (!File.Exists(file)) file = "Plugins\\DmProvider.dll".GetFullPath();
        var asm = Assembly.LoadFrom(file);
        Assert.NotNull(asm);

        var types = asm.GetTypes();
        var t = types.FirstOrDefault(t => t.Name == "DmClientFactory");
        Assert.NotNull(t);

        var type = asm.GetType("Dm.DmClientFactory");
        Assert.NotNull(type);
    }

    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.DaMeng);
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
        var db = DbFactory.Create(DatabaseType.DaMeng);
        var factory = db.Factory;

        var conn = factory.CreateConnection();
        conn.ConnectionString = "Server=localhost;Port=5236;Database=dameng;user=SYSDBA;password=SYSDBA";
        conn.Open();
    }

    [Fact(Skip = "跳过")]
    public void DALTest()
    {
        DAL.AddConnStr("DaMeng", _ConnStr, null, "DaMeng");
        var dal = DAL.Create("DaMeng");
        Assert.NotNull(dal);
        Assert.Equal("DaMeng", dal.ConnName);
        Assert.Equal(DatabaseType.DaMeng, dal.DbType);

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
        DAL.AddConnStr("DaMeng", _ConnStr, null, "DaMeng");
        var dal = DAL.Create("DaMeng");

        var tables = dal.Tables;
        Assert.NotNull(tables);
        Assert.True(tables.Count > 0);
    }

    [Fact(Skip = "跳过")]
    public void SelectTest()
    {
        //DAL.AddConnStr("Membership", _ConnStr, null, "DaMeng");
        DAL.AddConnStr("DaMeng", _ConnStr, null, "DaMeng");

        Role.Meta.ConnName = "DaMeng";
        Area.Meta.ConnName = "DaMeng";

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
        DAL.AddConnStr(connName, _ConnStr, null, "DaMeng");
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
}