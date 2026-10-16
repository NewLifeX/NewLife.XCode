using System;
using System.IO;
using XCode.DataAccessLayer;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

public class NegativeTests
{
    static NegativeTests() => DAL.WriteLog("Init NegativeTests");

    private String ReadTarget(String file, String text, Boolean overwrite = true)
    {
        var target = "";

        // 统一转换分隔符，避免 Windows 风格路径在 Linux 等平台失效
        var dir = @"..\..\XUnitTest.XCode\".Replace('\\', Path.DirectorySeparatorChar);
        var file2 = dir.CombinePath(file.Replace('\\', Path.DirectorySeparatorChar));
        if (File.Exists(file2)) target = File.ReadAllText(file2.GetFullPath());

        File.WriteAllText(file2, text);

        return target;
    }

    [Fact]
    public void CreateUpperTableSQLite()
    {
        var table = User.Meta.Table.DataTable;

        var db = DbFactory.Create(DatabaseType.SQLite);
        db.NameFormat = NameFormats.Upper;

        var rs = db.CreateMetaData().GetSchemaSQL(DDLSchema.CreateTable, table);
        var target = ReadTarget(@"DataAccessLayer\Sqls\sqlite_upper.sql", rs);
        Assert.Equal(target.NormalizeNewLine(), rs.NormalizeNewLine());
    }

    [Fact]
    public void CreateLowerTableSQLite()
    {
        var table = User.Meta.Table.DataTable;

        var db = DbFactory.Create(DatabaseType.SQLite);
        db.NameFormat = NameFormats.Lower;

        var rs = db.CreateMetaData().GetSchemaSQL(DDLSchema.CreateTable, table);
        var target = ReadTarget(@"DataAccessLayer\Sqls\sqlite_lower.sql", rs);
        Assert.Equal(target.NormalizeNewLine(), rs.NormalizeNewLine());
    }

    [Fact]
    public void CreateUnderlineTableSQLite()
    {
        var table = User.Meta.Table.DataTable;

        var db = DbFactory.Create(DatabaseType.SQLite);
        db.NameFormat = NameFormats.Underline;

        var rs = db.CreateMetaData().GetSchemaSQL(DDLSchema.CreateTable, table);
        var target = ReadTarget(@"DataAccessLayer\Sqls\sqlite_underline.sql", rs);
        Assert.Equal(target.NormalizeNewLine(), rs.NormalizeNewLine());

        table = table.Clone() as IDataTable;
        table.TableName = db.FormatName(table);
        foreach (var column in table.Columns)
        {
            column.ColumnName = db.FormatName(column);
        }

        var dal = User.Meta.Session.Dal;
        //dal.Db.NameFormat = NameFormats.Underline;
        //dal.SetTables(table);
        dal.Db.CreateMetaData().SetTables(Migration.ReadOnly, table);
    }

    [Fact]
    public void CreateUpperTableMySql()
    {
        var table = User.Meta.Table.DataTable;

        var db = DbFactory.Create(DatabaseType.MySql);
        db.NameFormat = NameFormats.Upper;

        var rs = db.CreateMetaData().GetSchemaSQL(DDLSchema.CreateTable, table);
        var target = ReadTarget(@"DataAccessLayer\Sqls\msyql_upper.sql", rs);
        Assert.Equal(target.NormalizeNewLine(), rs.NormalizeNewLine());
    }

    [Fact]
    public void CreateLowerTableMySql()
    {
        var table = User.Meta.Table.DataTable;

        var db = DbFactory.Create(DatabaseType.MySql);
        db.NameFormat = NameFormats.Lower;

        var rs = db.CreateMetaData().GetSchemaSQL(DDLSchema.CreateTable, table);
        var target = ReadTarget(@"DataAccessLayer\Sqls\msyql_lower.sql", rs);
        Assert.Equal(target.NormalizeNewLine(), rs.NormalizeNewLine());
    }

    [Fact]
    public void CreateUnderlineTableMySql()
    {
        var table = User.Meta.Table.DataTable;

        var db = DbFactory.Create(DatabaseType.MySql);
        db.NameFormat = NameFormats.Underline;

        var rs = db.CreateMetaData().GetSchemaSQL(DDLSchema.CreateTable, table);
        var target = ReadTarget(@"DataAccessLayer\Sqls\msyql_underline.sql", rs);
        Assert.Equal(target.NormalizeNewLine(), rs.NormalizeNewLine());
    }

    [Fact]
    public void CreateUpperTableSqlServer()
    {
        var table = User.Meta.Table.DataTable;

        var db = DbFactory.Create(DatabaseType.SqlServer);
        db.NameFormat = NameFormats.Upper;

        var rs = db.CreateMetaData().GetSchemaSQL(DDLSchema.CreateTable, table);
        var target = ReadTarget(@"DataAccessLayer\Sqls\sqlserver_upper.sql", rs);
        Assert.Equal(target.NormalizeNewLine(), rs.NormalizeNewLine());
    }

    [Fact]
    public void CreateLowerTableSqlServer()
    {
        var table = User.Meta.Table.DataTable;

        var db = DbFactory.Create(DatabaseType.SqlServer);
        db.NameFormat = NameFormats.Lower;

        var rs = db.CreateMetaData().GetSchemaSQL(DDLSchema.CreateTable, table);
        var target = ReadTarget(@"DataAccessLayer\Sqls\sqlserver_lower.sql", rs);
        Assert.Equal(target.NormalizeNewLine(), rs.NormalizeNewLine());
    }

    [Fact]
    public void CreateUnderlineTableSqlServer()
    {
        var table = User.Meta.Table.DataTable;

        var db = DbFactory.Create(DatabaseType.SqlServer);
        db.NameFormat = NameFormats.Underline;

        var rs = db.CreateMetaData().GetSchemaSQL(DDLSchema.CreateTable, table);
        var target = ReadTarget(@"DataAccessLayer\Sqls\sqlserver_underline.sql", rs);
        Assert.Equal(target.NormalizeNewLine(), rs.NormalizeNewLine());
    }
}