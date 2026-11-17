using System;
using System.Collections.Generic;
using System.IO;
using NewLife;
using NewLife.Log;
using NewLife.Security;
using XCode;
using XCode.DataAccessLayer;
using XCode.Membership;
using Xunit;
using XUnitTest.XCode.TestEntity;

namespace XUnitTest.XCode.DataAccessLayer;

[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class PostgreSQLTests
{
    private const String DefaultConnStr = "Server=127.0.0.1;Port=5432;Database=postgres;Uid=postgres;Pwd=postgres";

    private static String _ConnStr = DefaultConnStr;

    /// <summary>解析当前环境的连接字符串。环境变量优先，其次本地配置文件，供真库用例探测服务可用性</summary>
    /// <returns>连接字符串</returns>
    internal static String ResolveConnectionString()
    {
        // 优先使用环境变量（CI环境）
        var envConnStr = Environment.GetEnvironmentVariable("XCode_pgsql");
        if (!envConnStr.IsNullOrEmpty()) return envConnStr;

        // 本地开发使用配置文件
        var f = "Config\\pgsql.config".GetFullPath();
        return File.Exists(f) ? File.ReadAllText(f) : DefaultConnStr;
    }

    public PostgreSQLTests()
    {
        _ConnStr = ResolveConnectionString();

        // 无环境变量时首次落地默认配置，方便本地修改
        if (Environment.GetEnvironmentVariable("XCode_pgsql").IsNullOrEmpty())
        {
            var f = "Config\\pgsql.config".GetFullPath();
            if (!File.Exists(f)) File.WriteAllText(f.EnsureDirectory(true), _ConnStr);
        }
    }

    [Fact]
    public void InitTest()
    {
        var db = DbFactory.Create(DatabaseType.PostgreSQL);
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

    [PostgreSQLFact]
    public void ConnectTest()
    {
        var db = DbFactory.Create(DatabaseType.PostgreSQL);
        var factory = db.Factory;

        var conn = factory.CreateConnection();
        conn.ConnectionString = _ConnStr;
        conn.Open();
    }

    [PostgreSQLFact]
    public void DALTest()
    {
        DAL.AddConnStr("sysPgSql", _ConnStr, null, "PostgreSQL");
        var dal = DAL.Create("sysPgSql");
        Assert.NotNull(dal);
        Assert.Equal("sysPgSql", dal.ConnName);
        Assert.Equal(DatabaseType.PostgreSQL, dal.DbType);

        var db = dal.Db;
        var connstr = db.ConnectionString;
        Assert.Equal("postgres", db.DatabaseName);

        var ver = db.ServerVersion;
        Assert.NotEmpty(ver);
    }

    [PostgreSQLFact]
    public void MetaTest()
    {
        var connStr = _ConnStr.Replace("Database=postgres;", "Database=Membership;");
        DAL.AddConnStr("PgSql_Meta", connStr, null, "PostgreSQL");
        var dal = DAL.Create("PgSql_Meta");

        // 反向工程
        dal.SetTables(User.Meta.Table.DataTable);

        var tables = dal.Tables;
        Assert.NotNull(tables);
        Assert.True(tables.Count > 0);
    }

    [PostgreSQLFact]
    public void SelectTest()
    {
        DAL.AddConnStr("sysPgSql", _ConnStr, null, "PostgreSQL");
        var dal = DAL.Create("sysPgSql");
        try
        {
            dal.Execute("drop database \"Membership_Test\"");
        }
        catch (Exception ex) { XTrace.WriteException(ex); }

        var connStr = _ConnStr.Replace("Database=postgres;", "Database=Membership_Test;");
        DAL.AddConnStr("PgSql_Select", connStr, null, "PostgreSQL");

        Role.Meta.ConnName = "PgSql_Select";
        Area.Meta.ConnName = "PgSql_Select";

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
            dal.Execute("drop database \"Membership_Test\"");
        }
        catch (Exception ex) { XTrace.WriteException(ex); }
    }

    [PostgreSQLFact]
    public void TablePrefixTest()
    {
        DAL.AddConnStr("sysPgSql", _ConnStr, null, "PostgreSQL");
        var dal = DAL.Create("sysPgSql");
        try
        {
            dal.Execute("drop database \"Membership_Table_Prefix\"");
        }
        catch (Exception ex) { XTrace.WriteException(ex); }

        var connStr = _ConnStr.Replace("Database=postgres;", "Database=Membership_Table_Prefix;");
        connStr += ";TablePrefix=member_";
        DAL.AddConnStr("PgSql_Table_Prefix", connStr, null, "PostgreSQL");

        Role.Meta.ConnName = "PgSql_Table_Prefix";
        //Area.Meta.ConnName = "PgSql_Table_Prefix";

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
            dal.Execute("drop database \"Membership_Table_Prefix\"");
        }
        catch (Exception ex) { XTrace.WriteException(ex); }
    }

    private IDisposable CreateForBatch(String action)
    {
        var connStr = _ConnStr.Replace("Database=postgres;", "Database=Membership_Batch;");
        DAL.AddConnStr("Membership_Batch_PostgreSQL", connStr, null, "PostgreSQL");

        var dt = Role2.Meta.Table.DataTable.Clone() as IDataTable;
        dt.TableName = $"Role2_{action}";

        // 分表
        var split = Role2.Meta.CreateSplit("Membership_Batch_PostgreSQL", dt.TableName);

        var session = Role2.Meta.Session;
        session.Dal.SetTables(dt);

        // 清空数据
        session.Truncate();

        return split;
    }

    [PostgreSQLFact]
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

    [PostgreSQLFact]
    public void PositiveAndNegative()
    {
        var connName = GetType().Name;
        DAL.AddConnStr(connName, _ConnStr, null, "PostgreSQL");
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

        dal.Db.CreateMetaData().DropTable(table);

        tableNames = dal.GetTableNames();
        XTrace.WriteLine("tableNames: {0}", tableNames.Join());
        Assert.DoesNotContain(table.TableName, tableNames);
    }

    /// <summary>验证 CreateDatabaseSQL 生成正确的建库SQL（PostgreSQL不支持IF NOT EXISTS，幂等性由存在性检查保障）</summary>
    [Fact(DisplayName = "CreateDatabaseSQL应生成正确的PostgreSQL建库SQL")]
    public void CreateDatabaseSQL_ShouldBeValid()
    {
        var db = DbFactory.Create(DatabaseType.PostgreSQL);
        var meta = db.CreateMetaData();

        var sql = meta.GetSchemaSQL(DDLSchema.CreateDatabase, "test_db", null);
        Assert.NotNull(sql);
        Assert.Equal("Create Database \"test_db\" ENCODING \"UTF8\"", sql);
    }
}