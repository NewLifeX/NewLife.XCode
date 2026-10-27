using System;
using System.IO;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>SQLite 模糊搜索转义测试。验证 LIKE 中的 % 与 _ 被真正当作字面量匹配</summary>
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class SQLiteLikeEscapeTests
{
    /// <summary>含下划线的关键字应精确匹配，不能把下划线当通配符</summary>
    [Fact(DisplayName = "SQLite_LIKE转义_下划线按字面量匹配")]
    public void FormatLike_Underscore_MatchesLiteral()
    {
        using var db = OpenSqlite();
        var session = db.CreateSession();
        try
        {
            session.Execute("Create Table t (Name varchar(50))");
            session.Execute("Insert Into t (Name) Values ('sess_xxxx')");
            session.Execute("Insert Into t (Name) Values ('sessxxxx')");

            var field = new XField { ColumnName = "Name" };
            var where = db.FormatLike(field, "{0} Like '%{1}%'", "sess_xxxx");
            var count = session.ExecuteScalar<Int32>($"Select Count(*) From t Where {where}");

            Assert.Equal(1, count);
        }
        finally
        {
            session.Dispose();
            db.Dispose();
        }
    }

    /// <summary>不含通配符的关键字不加 ESCAPE 子句，行为与旧版一致</summary>
    [Fact(DisplayName = "SQLite_LIKE转义_普通关键字不带ESCAPE")]
    public void FormatLike_PlainKeyword_NoEscapeClause()
    {
        using var db = OpenSqlite();
        var field = new XField { ColumnName = "Name" };

        var sql = db.FormatLike(field, "{0} Like '%{1}%'", "abc");

        Assert.DoesNotContain("Escape", sql);
        Assert.Contains("abc", sql);
    }

    /// <summary>创建临时 SQLite 数据库连接</summary>
    private static IDatabase OpenSqlite()
    {
        var file = Path.Combine(Path.GetTempPath(), "xcode_like_" + Guid.NewGuid().ToString("N") + ".db");
        var db = DbFactory.Create(DatabaseType.SQLite)!;
        db.ConnectionString = $"Data Source={file}";

        return db;
    }
}
