using System;
using System.Collections.Generic;
using System.IO;
using XCode.DataAccessLayer;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>SQLite 模糊搜索转义测试。验证 LIKE 中的 % 与 _ 被真正当作字面量匹配</summary>
/// <remarks>SQLite 的 LIKE 没有默认转义字符，基类插入反斜杠转义后必须显式声明 ESCAPE 才生效</remarks>
public class SQLiteLikeEscapeTests : IDisposable
{
    private readonly List<String> _files = [];

    /// <summary>含下划线的关键字应精确匹配，不能把下划线当通配符</summary>
    [Fact(DisplayName = "SQLite_LIKE转义_下划线按字面量匹配")]
    public void Contains_Underscore_MatchesLiteral()
    {
        using var db = OpenSqlite();
        using var session = db.CreateSession();

        session.Execute("Create Table t (Name varchar(50))");
        session.Execute("Insert Into t (Name) Values ('sess_xxxx')");
        session.Execute("Insert Into t (Name) Values ('sessxxxx')");

        // 走实体字段的模糊搜索表达式，覆盖 FieldItem -> LikeExpression -> FormatLike 完整链路
        var where = User._.Name.Contains("sess_xxxx").GetString(db, null);
        var count = session.ExecuteScalar<Int32>($"Select Count(*) From t Where {where}");

        Assert.Equal(1, count);
    }

    /// <summary>含百分号的关键字应精确匹配，不能把百分号当通配符</summary>
    [Fact(DisplayName = "SQLite_LIKE转义_百分号按字面量匹配")]
    public void Contains_Percent_MatchesLiteral()
    {
        using var db = OpenSqlite();
        using var session = db.CreateSession();

        session.Execute("Create Table t (Name varchar(50))");
        session.Execute("Insert Into t (Name) Values ('50%off')");
        session.Execute("Insert Into t (Name) Values ('50Xoff')");

        var where = User._.Name.Contains("50%").GetString(db, null);
        var count = session.ExecuteScalar<Int32>($"Select Count(*) From t Where {where}");

        Assert.Equal(1, count);
    }

    /// <summary>四种模糊搜索格式在转义生效时都追加 ESCAPE 声明</summary>
    [Fact(DisplayName = "SQLite_LIKE转义_各格式均声明ESCAPE")]
    public void AllFormats_CarryEscapeClause()
    {
        using var db = OpenSqlite();

        var key = "a_b%c";
        String[] lines =
        [
            User._.Name.Contains(key).GetString(db, null),
            User._.Name.NotContains(key).GetString(db, null),
            User._.Name.StartsWith(key).GetString(db, null),
            User._.Name.EndsWith(key).GetString(db, null),
        ];

        foreach (var line in lines)
        {
            Assert.Contains(@"\_", line);
            Assert.Contains(@"\%", line);
            Assert.Contains("Escape '\\'", line);
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

    /// <summary>格式未引用操作数时不加 ESCAPE 子句</summary>
    [Fact(DisplayName = "SQLite_LIKE转义_未引用操作数不加ESCAPE")]
    public void FormatLike_FormatWithoutValue_NoEscapeClause()
    {
        using var db = OpenSqlite();
        var field = new XField { ColumnName = "Name" };

        var sql = db.FormatLike(field, "{0} Is Null", "a_b");

        Assert.DoesNotContain("Escape", sql);
        Assert.Contains("Is Null", sql);
    }

    /// <summary>参数化格式不转义操作数，% 与 _ 仍按通配符处理</summary>
    /// <remarks>与 DbBase.FormatLike 参数化重载的约定一致；此处锁定现状，避免后续改动悄悄改变语义</remarks>
    [Fact(DisplayName = "SQLite_LIKE转义_参数化格式不转义")]
    public void FormatLike_Parameterized_KeepsWildcards()
    {
        using var db = OpenSqlite();
        var field = new XField { ColumnName = "Name" };

        var sql = db.FormatLike(field, "{0} Like '%{1}%'");

        Assert.DoesNotContain("Escape", sql);
        Assert.Contains("@Name", sql);
    }

    /// <summary>创建临时 SQLite 数据库连接</summary>
    /// <returns>指向临时文件的 SQLite 数据库</returns>
    private IDatabase OpenSqlite()
    {
        var file = Path.Combine(Path.GetTempPath(), "xcode_like_" + Guid.NewGuid().ToString("N") + ".db");
        _files.Add(file);

        var db = DbFactory.Create(DatabaseType.SQLite)!;
        db.ConnectionString = $"Data Source={file}";

        return db;
    }

    /// <summary>清理测试过程中创建的临时数据库文件</summary>
    public void Dispose()
    {
        foreach (var file in _files)
        {
            try
            {
                if (File.Exists(file)) File.Delete(file);
            }
            catch
            {
                // 文件被占用时留给系统临时目录清理，不影响测试结论
            }
        }
    }
}
