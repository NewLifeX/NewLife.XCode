using System;
using NewLife.Log;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;
using Xunit;
using XUnitTest.XCode.TestEntity;

namespace XUnitTest.XCode.Configuration;

public class TableItemTests
{
    [Fact]
    public void TrimIndex()
    {
        var ti = TableItem.Create(typeof(Log2));
        XTrace.WriteLine(ti.TableName);
        Assert.Equal(4, ti.DataTable.Indexes.Count);
    }

    [Fact]
    public void SetMigration()
    {
        var ti = TableItem.Create(typeof(Log2));
        var old = ti.Migration;

        try
        {
            ti.Migration = Migration.Off;

            Assert.Equal(Migration.Off, ti.Migration);
            Assert.Equal(nameof(Migration.Off), ti.DataTable.Properties[nameof(Migration)]);

            ti.Migration = null;

            Assert.Null(ti.Migration);
            Assert.False(ti.DataTable.Properties.ContainsKey(nameof(Migration)));
        }
        finally
        {
            ti.Migration = old;
        }
    }

    [Fact]
    public void Migration_UsesAttribute_WhenNoStartupOverride()
    {
        var ti = TableItem.Create(typeof(MigrationEntity));

        Assert.Equal(Migration.ReadOnly, ti.Migration);
        Assert.Equal(nameof(Migration.ReadOnly), ti.DataTable.Properties[nameof(Migration)]);
    }

    [Fact]
    public void Migration_StartupOverride_Wins_AndPersists()
    {
        var ti = TableItem.Create(typeof(MigrationEntity));
        var old = ti.Migration;

        try
        {
            ti.Migration = Migration.Off;

            Assert.Equal(Migration.Off, ti.Migration);
            Assert.Equal(nameof(Migration.Off), ti.DataTable.Properties[nameof(Migration)]);

            var ti2 = TableItem.Create(typeof(MigrationEntity));

            Assert.Same(ti, ti2);
            Assert.Equal(Migration.Off, ti2.Migration);
            Assert.Equal(nameof(Migration.Off), ti2.DataTable.Properties[nameof(Migration)]);

            ti2.Migration = null;

            Assert.Equal(Migration.ReadOnly, ti.Migration);
            Assert.Equal(nameof(Migration.ReadOnly), ti.DataTable.Properties[nameof(Migration)]);
        }
        finally
        {
            ti.Migration = old;
        }
    }

    [BindTable("MigrationEntity", Description = "迁移测试实体", ConnName = "test", DbType = DatabaseType.None, Migration = "ReadOnly")]
    private class MigrationEntity
    {
        [BindColumn("Id", "编号", "Int32")]
        public Int32 Id { get; set; }
    }
}