using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using NewLife.Security;
using XCode;
using XCode.DataAccessLayer;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

[Collection("Database")]
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class DAL_Backup_Tests
{
    [Fact]
    public void BackupTest()
    {
        var n = User.Meta.Count;
        var dal = User.Meta.Session.Dal;
        var table = User.Meta.Table.DataTable;

        {
            var file = BackupPathHelper.Ensure($"data/{table.Name}.table");
            dal.Backup(table, file);
            var fi = file.AsFile();
            Assert.True(fi.Exists);
            Assert.True(fi.Length > 0);
        }
        {
            var file = BackupPathHelper.Ensure($"data/{table.Name}.gz");
            dal.Backup(table, file);
            var fi = file.AsFile();
            Assert.True(fi.Exists);
            Assert.True(fi.Length > 0);
        }
    }

    [Fact(DisplayName = "备份不改动整进程的SQL日志开关")]
    public void Backup_KeepsGlobalShowSql()
    {
        var dal = User.Meta.Session.Dal;
        var old = dal.Db.ShowSQL;
        try
        {
            dal.Db.ShowSQL = true;

            dal.Backup(User.Meta.Table.DataTable, $"data/{User.Meta.Table.DataTable.Name}_showsql.table");

            Assert.True(dal.Db.ShowSQL);
        }
        finally
        {
            dal.Db.ShowSQL = old;
        }
    }

    [Fact]
    public void BackupAllTest()
    {
        var n = User.Meta.Count;
        var dal = User.Meta.Session.Dal;
        var tables = EntityFactory.GetTables(dal.ConnName, false);

        var f = BackupPathHelper.Ensure($"data/{dal.ConnName}.zip");
        if (File.Exists(f)) File.Delete(f);

        dal.BackupAll(tables, $"data/{dal.ConnName}.zip");
    }

    [Fact(DisplayName = "数据包内未知表默认拒绝恢复")]
    public void RestoreAll_UnknownTable_Rejected()
    {
        var connName = "dpk_" + Rand.NextString(8);
        var dbFile = Path.Combine(Path.GetTempPath(), connName + ".db");
        var file = BackupPathHelper.Ensure($"data/{connName}.zip");
        DAL.AddConnStr(connName, $"Data Source={dbFile}", null, "SQLite");
        var dal = DAL.Create(connName);
        try
        {
            // 构造只含未知表模型的 zip，恢复应被拒绝
            var table = DAL.CreateTable();
            table.Name = "evil_" + Rand.NextString(8);
            table.TableName = table.Name;
            var column = table.CreateColumn();
            column.Name = "id";
            column.ColumnName = "id";
            column.DataType = typeof(Int32);
            column.Identity = true;
            column.PrimaryKey = true;
            table.Columns.Add(column);

            var xml = DAL.Export(new[] { table });
            using (var fs = new FileStream(file, FileMode.Create))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, true, Encoding.UTF8))
            {
                using (var sw = new StreamWriter(zip.CreateEntry("model.xml").Open()))
                {
                    sw.Write(xml);
                }
                zip.CreateEntry(table.Name + ".table");
            }

            var dpk = new DbPackage { Dal = dal };
            var ex = Assert.Throws<XCodeException>(() => dpk.RestoreAll(file, null, true));
            Assert.Contains(table.TableName, ex.Message);
        }
        finally
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
            try { if (File.Exists(dbFile)) File.Delete(dbFile); } catch { }
        }
    }

    [Fact]
    public void RestoreAllTest()
    {
        try
        {
            var dal = User.Meta.Session.Dal;

            // 还原依赖备份文件存在，这里先做一次备份，避免依赖用例执行顺序
            var tables = EntityFactory.GetTables(dal.ConnName, false);
            dal.BackupAll(tables, $"data/{dal.ConnName}.zip");

            // 随机连接名，得到SQLite连接字符串，实行导入
            User.Meta.ConnName = Rand.NextString(8);

            var dal2 = User.Meta.Session.Dal;

            // 跨库搬运到全新库，显式允许创建包内新表
            var rs = dal2.RestoreAll($"data/{dal.ConnName}.zip", null, true, false, default, true);
            Assert.NotNull(rs);
        }
        finally
        {
            User.Meta.ConnName = null;
        }
    }
}