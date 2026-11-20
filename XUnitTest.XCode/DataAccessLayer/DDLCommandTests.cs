using System;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>类型化 DDL 命令测试</summary>
public class DDLCommandTests
{
    [Fact]
    [System.ComponentModel.Description("类型化命令生成的建表 SQL 与旧接口一致")]
    public void CreateTable_CommandSql_MatchesLegacy()
    {
        var db = DbFactory.Create(DatabaseType.SQLite);
        Assert.NotNull(db);
        var meta = db.CreateMetaData();
        var table = CreateTable(DatabaseType.SQLite);

        var typed = meta.GetCommandSql(DDLCommand.CreateTable(table));
        var legacy = meta.GetSchemaSQL(DDLSchema.CreateTable, table);

        Assert.NotNull(typed);
        Assert.Equal(legacy, typed);
        Assert.Contains("Create Table", typed);
    }

    [Fact]
    [System.ComponentModel.Description("旧版模式参数通过兼容入口转发到类型化命令")]
    public void LegacyWrapper_ForwardsToCommand()
    {
        var db = DbFactory.Create(DatabaseType.SQLite);
        Assert.NotNull(db);
        var meta = db.CreateMetaData();
        var table = CreateTable(DatabaseType.SQLite);

        var typed = meta.GetCommandSql(DDLCommand.DropTable(table));
        var legacy = meta.GetSchemaSQL(DDLSchema.DropTable, table);

        Assert.Equal(typed, legacy);
    }

    [Fact]
    [System.ComponentModel.Description("不支持 DDL 的提供者在类型化命令上明确抛出异常")]
    public void UnsupportedProvider_CommandSql_Throws()
    {
        var db = DbFactory.Create(DatabaseType.Presto);
        Assert.NotNull(db);
        var table = CreateTable(DatabaseType.Presto);

        Assert.Throws<NotSupportedException>(() => db.CreateMetaData().GetCommandSql(DDLCommand.CreateTable(table)));
    }

    [Fact]
    [System.ComponentModel.Description("DDL 命令工厂拒绝缺失的核心参数")]
    public void Factory_RequiresPayload()
    {
        Assert.Throws<ArgumentNullException>(() => DDLCommand.CreateTable(null!));
        Assert.Throws<ArgumentNullException>(() => DDLCommand.RestoreDatabase(null!));
        Assert.Throws<ArgumentNullException>(() => DDLCommand.DropDatabase(null!));
    }

    private static IDataTable CreateTable(DatabaseType dbType)
    {
        var table = DAL.CreateTable();
        table.Name = "DdlTest";
        table.TableName = "DdlTest";
        table.DbType = dbType;

        var id = table.CreateColumn();
        id.Name = "Id";
        id.ColumnName = "Id";
        id.DataType = typeof(Int32);
        id.PrimaryKey = true;
        table.Columns.Add(id);

        var name = table.CreateColumn();
        name.Name = "Name";
        name.ColumnName = "Name";
        name.DataType = typeof(String);
        name.Length = 50;
        table.Columns.Add(name);

        return table;
    }
}
