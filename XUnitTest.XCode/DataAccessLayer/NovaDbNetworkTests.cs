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

/// <summary>NovaDb网络模式测试。连接字符串格式：Server=localhost;Port=3306;Database=mydb</summary>
[Collection("Database")]
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class NovaDbNetworkTests
{
    private static String _ConnStr = "Server=127.0.0.1;Port=3306;Database=sys;Uid=root;Pwd=root";

    public NovaDbNetworkTests()
    {
        // 优先使用环境变量（CI环境）
        var envConnStr = Environment.GetEnvironmentVariable("XCode_novadb");
        if (!envConnStr.IsNullOrEmpty())
        {
            _ConnStr = envConnStr;
            return;
        }

        // 本地开发使用配置文件
        var f = "Config\\novadb.config".GetFullPath();
        if (File.Exists(f))
            _ConnStr = File.ReadAllText(f);
        else
            File.WriteAllText(f.EnsureDirectory(true), _ConnStr);
    }

    [Fact(DisplayName = "网络模式驱动初始化")]
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

    [Fact(DisplayName = "网络模式连接")]
    public void ConnectTest()
    {
        var db = DbFactory.Create(DatabaseType.NovaDb);
        var factory = db.Factory;

        var conn = factory.CreateConnection();
        conn.ConnectionString = _ConnStr;
        conn.Open();
    }

    [Fact(DisplayName = "网络模式DAL层")]
    public void DALTest()
    {
        DAL.AddConnStr("sysNovaDb", _ConnStr, null, "NovaDb");
        var dal = DAL.Create("sysNovaDb");
        Assert.NotNull(dal);
        Assert.Equal("sysNovaDb", dal.ConnName);
        Assert.Equal(DatabaseType.NovaDb, dal.DbType);

        var db = dal.Db;
        var connstr = db.ConnectionString;
        Assert.Equal("sys", db.DatabaseName);

        using var conn = db.OpenConnection();

        var ver = db.ServerVersion;
        Assert.NotEmpty(ver);
    }

    [Fact(DisplayName = "网络模式元数据和反向工程")]
    public void MetaTest()
    {
        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership;");
        DAL.AddConnStr("NovaDb_Meta", connStr, null, "NovaDb");
        var dal = DAL.Create("NovaDb_Meta");

        // 反向工程
        dal.SetTables(User.Meta.Table.DataTable);

        var tables = dal.Tables;
        Assert.NotNull(tables);
        Assert.True(tables.Count > 0);

        var tb = tables.FirstOrDefault(e => e.Name == "User");
        Assert.NotNull(tb);
        Assert.NotEmpty(tb.Description);
    }

    [Fact(DisplayName = "网络模式查询操作")]
    public void SelectTest()
    {
        DAL.AddConnStr("sysNovaDb", _ConnStr, null, "NovaDb");
        var dal = DAL.Create("sysNovaDb");
        try
        {
            dal.Execute("drop database membership_nova_test");
        }
        catch (Exception ex)
        {
            XTrace.WriteLine(ex.Message);
        }

        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership_Nova_Test;");
        DAL.AddConnStr("NovaDb_Select", connStr, null, "NovaDb");

        Role.Meta.ConnName = "NovaDb_Select";
        Area.Meta.ConnName = "NovaDb_Select";

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
            dal.Execute("drop database membership_nova_test");
        }
        catch (Exception ex) { XTrace.WriteException(ex); }
    }

    [Fact(DisplayName = "网络模式Membership操作")]
    public void MembershipTest()
    {
        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership_Nova;");
        DAL.AddConnStr("NovaDb_member", connStr, null, "NovaDb");

        User.Meta.ConnName = "NovaDb_member";
        Role.Meta.ConnName = "NovaDb_member";

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

    [Fact(DisplayName = "网络模式表前缀")]
    public void TablePrefixTest()
    {
        DAL.AddConnStr("sysNovaDb", _ConnStr, null, "NovaDb");
        var dal = DAL.Create("sysNovaDb");
        try
        {
            dal.Execute("drop database membership_nova_prefix");
        }
        catch (Exception ex) { XTrace.WriteException(ex); }

        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership_Nova_Prefix;");
        connStr += ";TablePrefix=nova_";
        DAL.AddConnStr("NovaDb_Table_Prefix", connStr, null, "NovaDb");

        Role.Meta.ConnName = "NovaDb_Table_Prefix";

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
            dal.Execute("drop database membership_nova_prefix");
        }
        catch (Exception ex) { XTrace.WriteException(ex); }
    }

    private IDisposable CreateForBatch(String action)
    {
        var connStr = _ConnStr.Replace("Database=sys;", "Database=Membership_Nova_Batch;");
        DAL.AddConnStr("Membership_Batch_novadb", connStr, null, "NovaDb");

        var dt = Role2.Meta.Table.DataTable.Clone() as IDataTable;
        dt.TableName = $"Role2_{action}";

        // 分表
        var split = Role2.Meta.CreateSplit("Membership_Batch_novadb", dt.TableName);

        var session = Role2.Meta.Session;
        session.Dal.SetTables(dt);

        // 清空数据
        session.Truncate();

        return split;
    }

    [Fact(DisplayName = "网络模式批量插入")]
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

    [Fact(DisplayName = "网络模式批量InsertIgnore")]
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

    [Fact(DisplayName = "网络模式批量Replace")]
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

    [Fact(DisplayName = "网络模式获取所有表")]
    public void GetTables()
    {
        DAL.AddConnStr("member_novadb", _ConnStr.Replace("Database=sys", "Database=membership_nova"), null, "NovaDb");
        var dal = DAL.Create("member_novadb");

        dal.SetTables(User.Meta.Table.DataTable);

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
            }

            foreach (var di in table.Indexes)
            {
                Assert.NotEmpty(di.Name);
                Assert.NotEmpty(di.Columns);
            }
        }
    }

    [Fact(DisplayName = "网络模式正反向工程")]
    public void PositiveAndNegative()
    {
        var connName = GetType().Name;
        DAL.AddConnStr(connName, _ConnStr, null, "NovaDb");
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

    [Fact(DisplayName = "测试建表SQL生成")]
    public void CreateTableSQLTest()
    {
        var db = DbFactory.Create(DatabaseType.NovaDb);
        Assert.NotNull(db);

        var table = DAL.CreateTable();
        table.TableName = "TestTable";
        table.Description = "测试表";

        var field1 = table.CreateColumn();
        field1.ColumnName = "Id";
        field1.DataType = typeof(Int32);
        field1.Identity = true;
        field1.PrimaryKey = true;
        field1.Description = "编号";
        table.Columns.Add(field1);

        var field2 = table.CreateColumn();
        field2.ColumnName = "Name";
        field2.DataType = typeof(String);
        field2.Length = 50;
        field2.Description = "名称";
        table.Columns.Add(field2);

        var field3 = table.CreateColumn();
        field3.ColumnName = "CreateTime";
        field3.DataType = typeof(DateTime);
        field3.Nullable = true;
        field3.Description = "创建时间";
        table.Columns.Add(field3);

        var meta = db.CreateMetaData();
        var sql = meta.GetSchemaSQL(DDLSchema.CreateTable, table);
        Assert.NotNull(sql);
        Assert.NotEmpty(sql);

        XTrace.WriteLine("生成的建表SQL:");
        XTrace.WriteLine(sql);

        // 验证SQL结构
        Assert.Contains("Create Table If Not Exists", sql);
        Assert.Contains("DEFAULT CHARSET=utf8mb4", sql);
        Assert.Contains("Primary Key", sql);
        Assert.Contains("AUTO_INCREMENT", sql);
        Assert.Contains("COMMENT '编号'", sql);
        Assert.Contains("COMMENT '名称'", sql);
    }

    [Fact(DisplayName = "测试COMMENT中的单引号转义")]
    public void CommentWithSingleQuoteTest()
    {
        var db = DbFactory.Create(DatabaseType.NovaDb);
        Assert.NotNull(db);

        var table = DAL.CreateTable();
        table.TableName = "AlarmRule";
        table.Description = "告警规则";

        var field1 = table.CreateColumn();
        field1.ColumnName = "Id";
        field1.DataType = typeof(Int32);
        field1.Identity = true;
        field1.PrimaryKey = true;
        field1.Description = "编号";
        table.Columns.Add(field1);

        var field2 = table.CreateColumn();
        field2.ColumnName = "Threshold";
        field2.DataType = typeof(String);
        field2.Length = 50;
        field2.Description = "阈值。触发告警的阈值，支持单值和范围值'10,100'";
        table.Columns.Add(field2);

        var field3 = table.CreateColumn();
        field3.ColumnName = "Expression";
        field3.DataType = typeof(String);
        field3.Length = 500;
        field3.Description = "表达式。复杂条件时使用C#表达式，优先于简单值判断";
        table.Columns.Add(field3);

        var meta = db.CreateMetaData();
        var sql = meta.GetSchemaSQL(DDLSchema.CreateTable, table);
        Assert.NotNull(sql);
        Assert.NotEmpty(sql);

        XTrace.WriteLine("生成的建表SQL:");
        XTrace.WriteLine(sql);

        // 验证单引号已被转义
        Assert.Contains("COMMENT '阈值。触发告警的阈值，支持单值和范围值''10,100'''", sql);
        Assert.Contains("COMMENT '表达式。复杂条件时使用C#表达式，优先于简单值判断'", sql);

        // 验证不应该包含未转义的单引号序列
        Assert.DoesNotContain("范围值'10,100',", sql);

        // 测试表描述的转义
        var descSql = meta.GetSchemaSQL(DDLSchema.AddTableDescription, table);
        if (!String.IsNullOrEmpty(descSql))
        {
            Assert.NotNull(descSql);
            XTrace.WriteLine("生成的表描述SQL:");
            XTrace.WriteLine(descSql);
            Assert.Contains($"Comment '{table.Description}'", descSql);
        }
    }

    [Fact(DisplayName = "测试FormatComment方法的特殊字符转义")]
    public void FormatCommentTest()
    {
        var db = DbFactory.Create(DatabaseType.NovaDb);
        Assert.NotNull(db);

        var meta = db.CreateMetaData();
        Assert.NotNull(meta);

        // 使用反射访问protected方法FormatComment
        var formatCommentMethod = meta.GetType()
            .GetMethod("FormatComment", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(formatCommentMethod);

        // 测试单引号转义
        var result1 = formatCommentMethod!.Invoke(meta, ["It's a test"]) as String;
        Assert.Equal("It''s a test", result1);

        // 测试多个单引号
        var result2 = formatCommentMethod.Invoke(meta, ["范围值'10,100'"]) as String;
        Assert.Equal("范围值''10,100''", result2);

        // 测试回车换行转义
        var result3 = formatCommentMethod.Invoke(meta, ["Line1\r\nLine2"]) as String;
        Assert.Equal("Line1 Line2", result3);

        // 测试换行符转义
        var result4 = formatCommentMethod.Invoke(meta, ["Line1\nLine2"]) as String;
        Assert.Equal("Line1 Line2", result4);

        // 测试空字符串
        var result5 = formatCommentMethod.Invoke(meta, [""]) as String;
        Assert.Equal("", result5);

        // 测试null
        var result6 = formatCommentMethod.Invoke(meta, new Object?[] { null }) as String;
        Assert.Null(result6);
    }

    [Fact(DisplayName = "测试格式化关键字")]
    public void FormatKeyWordTest()
    {
        var db = DbFactory.Create(DatabaseType.NovaDb) as DbBase;
        Assert.NotNull(db);

        // 普通关键字应该加反引号
        Assert.Equal("`test`", db.FormatKeyWord("test"));

        // 已有反引号的不重复加
        Assert.Equal("`test`", db.FormatKeyWord("`test`"));

        // 空字符串原样返回
        Assert.Equal("", db.FormatKeyWord(""));
        Assert.Null(db.FormatKeyWord(null));
    }

    [Fact(DisplayName = "测试字符串连接")]
    public void StringConcatTest()
    {
        var db = DbFactory.Create(DatabaseType.NovaDb);
        Assert.NotNull(db);

        var result = db.StringConcat("a", "b");
        Assert.Equal("concat(a,b)", result);

        var result2 = db.StringConcat("", "b");
        Assert.Equal("concat('',b)", result2);

        var result3 = db.StringConcat("a", "");
        Assert.Equal("concat(a,'')", result3);
    }

    [Fact(DisplayName = "测试批量删除SQL")]
    public void BuildDeleteSqlTest()
    {
        var db = DbFactory.Create(DatabaseType.NovaDb);
        Assert.NotNull(db);

        // 无分批
        var sql = db.BuildDeleteSql("test_table", "Id>100", 0);
        Assert.Equal("Delete From test_table Where Id>100", sql);

        // 有分批
        var sql2 = db.BuildDeleteSql("test_table", "Id>100", 1000);
        Assert.Equal("Delete From test_table Where Id>100 limit 1000", sql2);
    }

    [Fact(DisplayName = "测试数据库类型识别")]
    public void SupportTest()
    {
        var db = DbFactory.Create(DatabaseType.NovaDb);
        Assert.NotNull(db);

        Assert.True(db.Support("NovaDb"));
        Assert.True(db.Support("novadb"));
        Assert.True(db.Support("Nova"));
        Assert.True(db.Support("nova"));
        Assert.False(db.Support("MySql"));
        Assert.False(db.Support("SQLite"));
    }

    [Fact(DisplayName = "测试参数前缀")]
    public void ParamPrefixTest()
    {
        var db = DbFactory.Create(DatabaseType.NovaDb);
        Assert.NotNull(db);

        var name = db.FormatParameterName("test");
        Assert.Equal("@test", name);
    }

    [Fact(DisplayName = "测试分页SQL生成")]
    public void PageSplitTest()
    {
        var db = DbFactory.Create(DatabaseType.NovaDb);
        Assert.NotNull(db);

        // 不分页
        var sql1 = db.PageSplit("Select * From test", 0, 0, null);
        Assert.Equal("Select * From test", sql1);

        // 第一页
        var sql2 = db.PageSplit("Select * From test", 0, 10, null);
        Assert.Equal("Select * From test limit 10", sql2);

        // 后续页
        var sql3 = db.PageSplit("Select * From test", 20, 10, null);
        Assert.Equal("Select * From test limit 20, 10", sql3);
    }

    [Fact(DisplayName = "测试数据库类型")]
    public void DatabaseTypeTest()
    {
        var db = DbFactory.Create(DatabaseType.NovaDb);
        Assert.NotNull(db);
        Assert.Equal(DatabaseType.NovaDb, db.Type);
    }
}
