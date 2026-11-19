using System;
using System.Data;
using System.IO;
using NewLife;
using NewLife.Security;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>SQLiteSession.Truncate 方法的集成测试，覆盖全部代码路径</summary>
[Collection("Database")]
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class SQLiteTruncateTests
{
    #region 辅助方法

    /// <summary>创建独立的测试 DAL（每次删除旧文件，保证干净状态）</summary>
    private static DAL CreateTestDal(String suffix, String? extraParams = null)
    {
        var db = $"Data\\Truncate_{suffix}.db";
        var dbf = db.GetFullPath();
        dbf.EnsureDirectory(true);
        if (File.Exists(dbf)) File.Delete(dbf);

        var connStr = $"Data Source={db}";
        if (!extraParams.IsNullOrEmpty()) connStr += ";" + extraParams;

        var connName = $"SQLite_Truncate_{suffix}";
        DAL.AddConnStr(connName, connStr, null, "SQLite");
        return DAL.Create(connName);
    }

    /// <summary>批量插入（用事务加速），保证多页数据以便 VACUUM 测试有效</summary>
    private static void InsertRows(IDbSession ss, String tableName, Int32 count)
    {
        ss.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            for (var i = 0; i < count; i++)
            {
                ss.Execute($"Insert Into [{tableName}] ([Data]) Values ('data_{i:D4}_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx')");
            }
            ss.Commit();
        }
        catch
        {
            ss.Rollback();
            throw;
        }
    }

    #endregion

    #region 主路径：Drop+Recreate

    [Fact]
    [System.ComponentModel.Description("Truncate通过Drop重建清空所有数据，表结构保留可继续写入")]
    public void Truncate_DropAndRecreate_ClearsAllRowsAndPreservesStructure()
    {
        var dal = CreateTestDal("ClearRows");
        var ss = dal.Session;

        ss.Execute("Create Table [TestRole] ([Id] integer PRIMARY KEY AUTOINCREMENT, [Name] nvarchar(50) NOT NULL)");
        ss.Execute("Insert Into [TestRole] ([Name]) Values ('管理员')");
        ss.Execute("Insert Into [TestRole] ([Name]) Values ('普通用户')");
        ss.Execute("Insert Into [TestRole] ([Name]) Values ('游客')");

        Assert.Equal(3L, ss.ExecuteScalar<Int64>("Select count(*) From [TestRole]"));

        ss.Truncate("TestRole");

        // 数据已清空
        Assert.Equal(0L, ss.ExecuteScalar<Int64>("Select count(*) From [TestRole]"));

        // 表结构保留，可继续写入
        ss.Execute("Insert Into [TestRole] ([Name]) Values ('新用户')");
        Assert.Equal(1L, ss.ExecuteScalar<Int64>("Select count(*) From [TestRole]"));
    }

    [Fact]
    [System.ComponentModel.Description("Truncate后Drop自动清理sqlite_sequence，新插入行自增Id从1重新开始")]
    public void Truncate_DropAndRecreate_ResetsAutoIncrement()
    {
        var dal = CreateTestDal("ResetAutoIncrement");
        var ss = dal.Session;

        ss.Execute("Create Table [TestRole] ([Id] integer PRIMARY KEY AUTOINCREMENT, [Name] nvarchar(50) NOT NULL)");
        ss.Execute("Insert Into [TestRole] ([Name]) Values ('管理员')");
        ss.Execute("Insert Into [TestRole] ([Name]) Values ('普通用户')");

        // 插入两行后最大Id为2
        Assert.Equal(2L, ss.ExecuteScalar<Int64>("Select max(Id) From [TestRole]"));

        ss.Truncate("TestRole");

        // Truncate后自增从1重新开始
        ss.Execute("Insert Into [TestRole] ([Name]) Values ('新管理员')");
        var newId = ss.ExecuteScalar<Int64>("Select max(Id) From [TestRole]");
        Assert.Equal(1L, newId);
    }

    [Fact]
    [System.ComponentModel.Description("Truncate后单个唯一索引被正确重建，唯一性约束继续生效")]
    public void Truncate_DropAndRecreate_PreservesSingleIndex()
    {
        var dal = CreateTestDal("PreserveSingleIndex");
        var ss = dal.Session;

        ss.Execute("Create Table [TestRole] ([Id] integer PRIMARY KEY AUTOINCREMENT, [Name] nvarchar(50) NOT NULL)");
        ss.Execute("Create Unique Index IX_TestRole_Name On [TestRole] ([Name])");
        ss.Execute("Insert Into [TestRole] ([Name]) Values ('管理员')");

        ss.Truncate("TestRole");

        // 索引仍存在于 sqlite_master
        var indexCount = ss.ExecuteScalar<Int64>(
            "Select count(*) From sqlite_master Where type='index' And tbl_name='TestRole'");
        Assert.Equal(1L, indexCount);

        // 唯一性约束仍然有效
        ss.Execute("Insert Into [TestRole] ([Name]) Values ('管理员')");
        var ex = Record.Exception(() => ss.Execute("Insert Into [TestRole] ([Name]) Values ('管理员')"));
        Assert.NotNull(ex);
    }

    [Fact]
    [System.ComponentModel.Description("Truncate后多个索引全部被正确重建")]
    public void Truncate_DropAndRecreate_PreservesMultipleIndexes()
    {
        var dal = CreateTestDal("PreserveMultiIndex");
        var ss = dal.Session;

        ss.Execute("Create Table [TestRole] ([Id] integer PRIMARY KEY AUTOINCREMENT, [Name] nvarchar(50) NOT NULL, [Code] nvarchar(20))");
        ss.Execute("Create Unique Index IX_TestRole_Name On [TestRole] ([Name])");
        ss.Execute("Create Index IX_TestRole_Code On [TestRole] ([Code])");
        ss.Execute("Insert Into [TestRole] ([Name], [Code]) Values ('管理员', 'admin')");

        ss.Truncate("TestRole");

        // 两个索引全部重建
        var indexCount = ss.ExecuteScalar<Int64>(
            "Select count(*) From sqlite_master Where type='index' And tbl_name='TestRole'");
        Assert.Equal(2L, indexCount);

        // 表可正常读写
        ss.Execute("Insert Into [TestRole] ([Name], [Code]) Values ('普通用户', 'user')");
        Assert.Equal(1L, ss.ExecuteScalar<Int64>("Select count(*) From [TestRole]"));
    }

    #endregion

    #region 多表隔离

    [Fact]
    [System.ComponentModel.Description("多表数据库中Truncate一张表，其他表数据完整保留")]
    public void Truncate_MultipleTableDatabase_OtherTablesDataIntact()
    {
        var dal = CreateTestDal("MultiTable");
        var ss = dal.Session;

        ss.Execute("Create Table [Table1] ([Id] integer PRIMARY KEY AUTOINCREMENT, [Name] nvarchar(50))");
        ss.Execute("Create Table [Table2] ([Id] integer PRIMARY KEY AUTOINCREMENT, [Value] nvarchar(50))");
        ss.Execute("Insert Into [Table1] ([Name]) Values ('row1')");
        ss.Execute("Insert Into [Table1] ([Name]) Values ('row2')");
        ss.Execute("Insert Into [Table2] ([Value]) Values ('保留数据1')");
        ss.Execute("Insert Into [Table2] ([Value]) Values ('保留数据2')");

        ss.Truncate("Table1");

        // Table1 已清空
        Assert.Equal(0L, ss.ExecuteScalar<Int64>("Select count(*) From [Table1]"));
        // Table2 数据完好
        Assert.Equal(2L, ss.ExecuteScalar<Int64>("Select count(*) From [Table2]"));
    }

    #endregion

    #region VACUUM 条件分支

    [Fact]
    [System.ComponentModel.Description("单表数据库Truncate后执行VACUUM，freelist_count归零")]
    public void Truncate_SingleTableDatabase_RunsVacuumAndFreesPagelist()
    {
        var dal = CreateTestDal("SingleTableVacuum");
        var ss = dal.Session;

        ss.Execute("Create Table [BigTable] ([Id] integer PRIMARY KEY, [Data] nvarchar(200))");
        // 插入足够多行以占用多个页面（每页约4KB，每行约60字节，200行≈3KB→跨多页）
        InsertRows(ss, "BigTable", 200);

        Assert.True(ss.ExecuteScalar<Int64>("Select count(*) From [BigTable]") > 0);

        ss.Truncate("BigTable");

        // 单表且 AutoVacuum=false：VACUUM已执行，空闲页全部回收
        var freelist = ss.ExecuteScalar<Int64>("PRAGMA freelist_count");
        Assert.Equal(0L, freelist);
    }

    [Fact]
    [System.ComponentModel.Description("多表数据库Truncate时跳过VACUUM，释放的页面保留在空闲列表中供后续写入复用")]
    public void Truncate_MultipleTableDatabase_SkipsVacuumLeavesFreelist()
    {
        var dal = CreateTestDal("MultiTableNoVacuum");
        var ss = dal.Session;

        ss.Execute("Create Table [BigTable] ([Id] integer PRIMARY KEY AUTOINCREMENT, [Data] nvarchar(200))");
        ss.Execute("Create Table [SmallTable] ([Id] integer PRIMARY KEY AUTOINCREMENT, [Name] nvarchar(50))");
        ss.Execute("Insert Into [SmallTable] ([Name]) Values ('保留数据')");
        // 插入足够多行确保 Drop Table 后空闲列表不为零
        InsertRows(ss, "BigTable", 200);

        ss.Truncate("BigTable");

        // 多表时跳过 VACUUM，释放的页面仍在空闲列表中（>0）
        var freelist = ss.ExecuteScalar<Int64>("PRAGMA freelist_count");
        Assert.True(freelist > 0,
            $"多表时应跳过VACUUM，freelist_count应>0，实际={freelist}");
    }

    #endregion

    #region 内存数据库

    [Fact]
    [System.ComponentModel.Description("内存数据库Truncate正常清空数据，不受VACUUM限制")]
    public void Truncate_MemoryDatabase_ClearsAllRows()
    {
        // 共享内存数据库配合事务保持连接生命周期，避免命令间连接切换导致表丢失
        var connName = $"SQLite_Truncate_Memory_{Rand.Next(10000, 99999)}";
        DAL.AddConnStr(connName, $"Data Source={connName};Mode=Memory;Cache=Shared", null, "SQLite");
        var dal = DAL.Create(connName);
        using var keepAlive = dal.Db.OpenConnection();
        var ss = dal.Session;

        ss.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            ss.Execute("Create Table [TestRole] ([Id] integer PRIMARY KEY AUTOINCREMENT, [Name] nvarchar(50) NOT NULL)");
            ss.Execute("Insert Into [TestRole] ([Name]) Values ('管理员')");
            ss.Execute("Insert Into [TestRole] ([Name]) Values ('普通用户')");

            Assert.Equal(2L, ss.ExecuteScalar<Int64>("Select count(*) From [TestRole]"));

            ss.Truncate("TestRole");

            Assert.Equal(0L, ss.ExecuteScalar<Int64>("Select count(*) From [TestRole]"));
            ss.Commit();
        }
        catch
        {
            ss.Rollback();
            throw;
        }
    }

    #endregion
}
