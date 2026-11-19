using System;
using System.Reflection;
using System.Text;
using XCode;
using XCode.DataAccessLayer;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>名称格式与标识符引号策略</summary>
public class NameFormatsTests
{
    private static IDataTable CreateTable(String name = "User")
    {
        var table = DAL.CreateTable();
        table.TableName = name;
        table.Name = name;
        return table;
    }

    private static IDataColumn CreateColumn(IDataTable table, String name)
    {
        var dc = table.CreateColumn();
        dc.Name = name;
        dc.ColumnName = name;
        dc.DataType = typeof(Int32);
        table.Columns.Add(dc);
        return dc;
    }

    [Theory]
    [InlineData(DatabaseType.KingBase)]
    [InlineData(DatabaseType.HighGo)]
    public void ExactIsDefaultForQuotedDatabase(DatabaseType type)
    {
        var db = DbFactory.Create(type);
        Assert.NotNull(db);
        Assert.Equal(NameFormats.Exact, db!.EffectiveNameFormat);

        var table = CreateTable();
        var dc = CreateColumn(table, "Id");
        Assert.Equal("\"Id\"", db.FormatName(dc));
        Assert.Equal("\"User\"", db.FormatName(table));
    }

    [Theory]
    [InlineData(DatabaseType.PostgreSQL)]
    [InlineData(DatabaseType.VastBase)]
    public void UnderlineIsDefaultForPostgreSqlFamily(DatabaseType type)
    {
        var db = DbFactory.Create(type);
        Assert.NotNull(db);
        Assert.Equal(NameFormats.Underline, db!.EffectiveNameFormat);

        var table = CreateTable();
        var dc = CreateColumn(table, "CreateUserId");
        var name = db.FormatName(dc);
        Assert.Equal("create_user_id", name.Trim('"'));
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, "[Id]")]
    [InlineData(DatabaseType.MySql, "`Id`")]
    [InlineData(DatabaseType.SQLite, "[Id]")]
    [InlineData(DatabaseType.Oracle, "\"Id\"")]
    [InlineData(DatabaseType.DaMeng, "\"Id\"")]
    public void ExactQuotesIdentifier(DatabaseType type, String expected)
    {
        var db = DbFactory.Create(type);
        Assert.NotNull(db);

        db!.NameFormat = NameFormats.Exact;
        Assert.Equal(NameFormats.Exact, db.EffectiveNameFormat);

        var table = CreateTable();
        var dc = CreateColumn(table, "Id");
        Assert.Equal(expected, db.FormatName(dc));
    }

    [Fact]
    public void NoneDoesNotQuoteIdentifier()
    {
        var db = DbFactory.Create(DatabaseType.KingBase)!;
        db.NameFormat = NameFormats.None;

        var table = CreateTable("MyTable");
        var dc = CreateColumn(table, "Id");
        Assert.Equal("Id", db.FormatName(dc));
        Assert.Equal("MyTable", db.FormatName(table));

        // None 仍按方言转义关键字
        Assert.Equal("\"User\"", db.FormatName("User"));
    }

    [Fact]
    public void FormatNameStringHonorsExact()
    {
        var db = DbFactory.Create(DatabaseType.KingBase)!;
        Assert.Equal("\"Id\"", db.FormatName("Id"));

        db.NameFormat = NameFormats.None;
        Assert.Equal("Id", db.FormatName("Id"));
    }

    [Fact]
    public void VastBaseExactKeepsCase()
    {
        var db = DbFactory.Create(DatabaseType.VastBase)!;
        db.NameFormat = NameFormats.Exact;

        Assert.Equal("\"Id\"", db.FormatName("Id"));
    }

    [Fact]
    public void ConnectionStringOverridesDefault()
    {
        var db = DbFactory.Create(DatabaseType.KingBase)!;
        Assert.Equal(NameFormats.Exact, db.EffectiveNameFormat);

        db.NameFormat = NameFormats.None;
        Assert.Equal(NameFormats.None, db.EffectiveNameFormat);
    }

    [Fact]
    public void AliasIndexerHandlesCaseDifferences()
    {
        var user = new User { Id = 5 };
        Assert.Equal(5, user.ID);

        // 精确大小写走别名属性
        Assert.Equal(5, user["ID"]);

        // 其它大小写不再抛 AmbiguousMatchException
        Assert.Null(user["id"]);

        user["ID"] = 9;
        Assert.Equal(9, user.Id);
    }

    [Fact]
    public void RenameColumnSqlQuotesExactIdentifier()
    {
        var db = DbFactory.Create(DatabaseType.KingBase)!;
        var table = CreateTable("Test");
        var dc = CreateColumn(table, "ID");

        var sql = db.CreateMetaData().GetSchemaSQL(DDLSchema.RenameColumn, dc, "Id");

        Assert.Equal("Alter Table \"Test\" Rename Column \"ID\" To \"Id\"", sql);
    }

    [Fact]
    public void RenameColumnSqlOnSqlServerUsesSpRename()
    {
        var db = DbFactory.Create(DatabaseType.SqlServer)!;
        var table = CreateTable("Test");
        var dc = CreateColumn(table, "ID");

        var sql = db.CreateMetaData().GetSchemaSQL(DDLSchema.RenameColumn, dc, "Id");

        Assert.Equal("EXEC sp_rename N'Test.ID', N'Id', 'COLUMN'", sql);
    }

    [Fact]
    public void ExactNameFormatRenamesCaseMismatchColumn()
    {
        // KingBase 默认 Exact，库中 ID 与模型 Id 不同，自动生成改名语句
        var entity = CreateTable("Test");
        CreateColumn(entity, "Id");
        var dbtable = CreateTable("Test");
        CreateColumn(dbtable, "ID");

        var sb = new StringBuilder();
        InvokeCheckColumnCase(DbFactory.Create(DatabaseType.KingBase)!, entity, dbtable, sb);

        Assert.Contains("Rename Column \"ID\" To \"Id\"", sb.ToString());
        Assert.Equal("Id", dbtable.Columns[0].ColumnName);
    }

    [Fact]
    public void UnderlineNameFormatIgnoresCaseMismatch()
    {
        // PostgreSQL 默认 Underline，ID 与 Id 都折叠为 id，属于同一字段，不生成改名
        var entity = CreateTable("Test");
        CreateColumn(entity, "Id");
        var dbtable = CreateTable("Test");
        CreateColumn(dbtable, "ID");

        var sb = new StringBuilder();
        InvokeCheckColumnCase(DbFactory.Create(DatabaseType.PostgreSQL)!, entity, dbtable, sb);

        Assert.Equal("", sb.ToString());
        Assert.Equal("ID", dbtable.Columns[0].ColumnName);
    }

    private static void InvokeCheckColumnCase(IDatabase db, IDataTable entitytable, IDataTable dbtable, StringBuilder sb)
    {
        var meta = db.CreateMetaData();
        MethodInfo? mi = null;
        for (var type = meta.GetType(); type != null && mi == null; type = type.BaseType)
        {
            mi = type.GetMethod("CheckColumnCase", BindingFlags.NonPublic | BindingFlags.Instance);
        }
        Assert.NotNull(mi);

        mi!.Invoke(meta, [entitytable, dbtable, sb, true]);
    }

    [Fact]
    public void DuplicateColumnCaseThrows()
    {
        var type = typeof(DAL).Assembly.GetType("XCode.DataAccessLayer.DbMetaData");
        var mi = type?.GetMethod("CheckDuplicateColumnCase", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(mi);

        var table = CreateTable();
        CreateColumn(table, "Id");
        CreateColumn(table, "ID");

        var ex = Assert.Throws<TargetInvocationException>(() => mi!.Invoke(null, [table]));
        var inner = Assert.IsType<XCodeException>(ex.InnerException);
        Assert.Contains("大小写", inner.Message);
    }
}
