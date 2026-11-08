using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewLife;
using XCode;
using XCode.DataAccessLayer;
using Xunit;
using XUnitTest.XCode.TestEntity;

namespace XUnitTest.XCode.EntityTests;

/// <summary>实体总数计数策略测试</summary>
[Collection("Database")]
public class EntityCountTests
{
    [Fact(DisplayName = "默认小表同步精确计数")]
    public void SmallTable_DefaultExact()
    {
        var connName = $"Count_Exact_{Guid.NewGuid():N}";
        var dbFile = $"Data\\{connName}.db".GetFullPath();
        try
        {
            Prepare(connName, dbFile, 4);

            var sqls = new List<String>();
            DAL.LocalFilter = s => sqls.Add(s);

            var count = Role2.Meta.Count;

            Assert.True(count > 0, String.Join("\n", sqls));
            Assert.True(sqls.Any(s => s.Contains("Count(*)", StringComparison.OrdinalIgnoreCase)), String.Join("\n", sqls));
        }
        finally
        {
            DAL.LocalFilter = null;
            Role2.Meta.ConnName = null;
            TryDelete(dbFile);
        }
    }

    [Fact(DisplayName = "大表阈值时只走快速计数")]
    public void LargeTable_UsesFastCountOnly()
    {
        var setting = XCodeSetting.Current;
        var oldFloor = setting.FullCountFloor;

        var connName = $"Count_Fast_{Guid.NewGuid():N}";
        var dbFile = $"Data\\{connName}.db".GetFullPath();
        try
        {
            Prepare(connName, dbFile, 4);

            setting.FullCountFloor = 1;

            var sqls = new List<String>();
            DAL.LocalFilter = s => sqls.Add(s);

            var count = Role2.Meta.Count;

            Assert.True(count > 0, String.Join("\n", sqls));
            Assert.DoesNotContain(sqls, s => s.Contains("Count(*)", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(sqls, s => s.Contains("MIN(", StringComparison.OrdinalIgnoreCase) || s.Contains("MAX(", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DAL.LocalFilter = null;
            Role2.Meta.ConnName = null;
            setting.FullCountFloor = oldFloor;
            TryDelete(dbFile);
        }
    }

    [Fact(DisplayName = "删除底部数据后主键区间仍可估算")]
    public void IdentityRange_AfterDelete()
    {
        var setting = XCodeSetting.Current;
        var oldFloor = setting.FullCountFloor;

        var connName = $"Count_Range_{Guid.NewGuid():N}";
        var dbFile = $"Data\\{connName}.db".GetFullPath();
        try
        {
            Prepare(connName, dbFile, 4);

            var roles = Role2.FindAll().OrderBy(e => e.ID).ToList();
            Assert.Equal(4, roles.Count);

            roles[0].Delete();

            // 换一个连接名，得到全新会话，清掉进程内计数缓存
            var connName2 = connName + "_2";
            DAL.AddConnStr(connName2, $"Data Source={dbFile}", null, "SQLite");
            Role2.Meta.ConnName = connName2;

            setting.FullCountFloor = 1;

            var count = Role2.Meta.Count;
            Assert.Equal(3, count);
            Assert.Equal(3, Role2.FindCount());
        }
        finally
        {
            DAL.LocalFilter = null;
            Role2.Meta.ConnName = null;
            setting.FullCountFloor = oldFloor;
            TryDelete(dbFile);
        }
    }

    private static void Prepare(String connName, String dbFile, Int32 rows)
    {
        DAL.AddConnStr(connName, $"Data Source={dbFile}", null, "SQLite");
        Role2.Meta.ConnName = connName;
        Role2.Meta.Session.InitData();

        for (var i = 0; i < rows; i++)
        {
            var entity = new Role2 { Name = $"role_{i}_{Guid.NewGuid():N}" };
            entity.Insert();
        }
    }

    private static void TryDelete(String file)
    {
        try
        {
            if (File.Exists(file)) File.Delete(file);
        }
        catch
        {
            // SQLite 连接可能仍被连接池持有，忽略清理失败
        }
    }
}

