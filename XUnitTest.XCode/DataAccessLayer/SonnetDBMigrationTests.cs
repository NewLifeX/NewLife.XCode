using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using Microsoft.Data.Sqlite;
using NewLife.Data;
using SonnetDB.Data;
using XCode.DataAccessLayer;
using XCode.Transform;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>验证涉及 SonnetDB 的 NULL 迁移以及其它数据库和抽取器扩展点的原有行为。</summary>
[Collection("Database")]
public class SonnetDBMigrationTests
{
    [Theory]
    [InlineData("paging")]
    [InlineData("id")]
    [InlineData("time")]
    public void Sync_DefaultExtracters_PreserveNullInBothDirections(String algorithm)
    {
        using var scope = new TransferScope();
        var source = scope.Open("source", false);
        var target = scope.Open("target", true);
        var reverse = scope.Open("reverse", false);
        var numeric = algorithm == "id";
        var key = numeric ? "INTEGER" : "VARCHAR(30)";
        source.Execute($"CREATE TABLE TransferRows(Id {key} NOT NULL PRIMARY KEY, Value INTEGER NULL, Created DATETIME NOT NULL)");
        reverse.Execute($"CREATE TABLE TransferRows(Id {key} NOT NULL PRIMARY KEY, Value INTEGER NULL, Created DATETIME NOT NULL)");
        target.Execute($"CREATE TABLE TransferRows(Id {(numeric ? "INT" : "STRING")} NOT NULL, Value INT NULL, Created DATETIME2 NOT NULL, PRIMARY KEY(Id))");
        source.Execute($"INSERT INTO TransferRows(Id,Value,Created) VALUES({(numeric ? "1" : "'1'")},NULL,'{DateTime.Now.AddSeconds(-5):yyyy-MM-dd HH:mm:ss}')");
        if (algorithm == "time")
        {
            source.Execute("CREATE INDEX IX_TransferRows_Created ON TransferRows(Created)");
            target.Execute("CREATE INDEX IX_TransferRows_Created ON TransferRows(Created)");
        }
        var table = source.Db.CreateMetaData().GetTables().Single(t => t.TableName == "TransferRows");
        var package = new SonnetDBPackage { Dal = source, IgnoreError = false };
        var extractor = package.GetExtracter(table);
        Assert.Equal(algorithm == "id" ? typeof(IdExtracter) : algorithm == "time" ? typeof(TimeExtracter) : typeof(PagingExtracter), extractor.GetType());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Assert.Equal(1, package.Sync(table, target.ConnName, false, cancellation.Token));
        Assert.True(IsNull(target));
        table = target.Db.CreateMetaData().GetTables().Single(t => t.TableName == "TransferRows");
        var reversePackage = new SonnetDBPackage { Dal = target, IgnoreError = false };
        Assert.Equal(extractor.GetType(), reversePackage.GetExtracter(table).GetType());
        Assert.Equal(1, reversePackage.Sync(table, reverse.ConnName, false, cancellation.Token));
        Assert.True(IsNull(reverse));
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void DateTime_ParametersAndLiterals_PreserveClockTicks(DateTimeKind kind)
    {
        using var scope = new TransferScope();
        var dal = scope.Open("temporal", true);
        dal.Execute("CREATE TABLE TemporalRows(Id INT NOT NULL, Created DATETIME2 NOT NULL, PRIMARY KEY(Id))");
        var value = new DateTime(2026, 11, 3, 12, 34, 56, kind).AddTicks(1234567);
        Assert.Equal(1, dal.Execute("INSERT INTO TemporalRows(Id,Created) VALUES(1,@value)", CommandType.Text, dal.Db.CreateParameter("value", value)));
        Assert.Equal(1, dal.Execute("INSERT INTO TemporalRows(Id,Created) SELECT 2," + dal.Db.FormatDateTime(value)));
        var rows = dal.Query("SELECT Created FROM TemporalRows ORDER BY Id").Rows;
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal(value.Ticks, Assert.IsType<DateTime>(row[0]).Ticks));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sync_ExternalExtracter_UsesItsOwnSourceAndFetch(Boolean derived)
    {
        using var scope = new TransferScope();
        var host = scope.Open("host", false);
        var external = scope.Open("external", false);
        var target = scope.Open("target", true);
        foreach (var source in new[] { host, external })
            source.Execute("CREATE TABLE TransferRows(Id VARCHAR(30) NOT NULL PRIMARY KEY, Value INTEGER NULL)");
        target.Execute("CREATE TABLE TransferRows(Id STRING NOT NULL, Value INT NULL, PRIMARY KEY(Id))");
        host.Execute("INSERT INTO TransferRows(Id,Value) VALUES('host',1)");
        external.Execute("INSERT INTO TransferRows(Id,Value) VALUES('external',911)");
        var tracking = new TrackingExtracter(external, "TransferRows");
        var table = host.Db.CreateMetaData().GetTables().Single(t => t.TableName == "TransferRows");
        var package = new SonnetDBPackage
        {
            Dal = host,
            IgnoreError = false,
            CreateExtracterCallback = _ => derived ? tracking : new PagingExtracter(external, "TransferRows", "Id"),
        };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Assert.Equal(1, package.Sync(table, target.ConnName, false, cancellation.Token));
        Assert.Equal("external", target.Query("SELECT Id FROM TransferRows").Rows.Single()[0]);
        Assert.Equal(derived, tracking.Called);
    }

    [Fact]
    public void Sync_OtherDatabases_KeepTheirDefaultNullBehavior()
    {
        using var scope = new TransferScope();
        var source = scope.Open("source", false);
        var target = scope.Open("target", false);
        foreach (var dal in new[] { source, target })
            dal.Execute("CREATE TABLE TransferRows(Id VARCHAR(30) NOT NULL PRIMARY KEY, Value INTEGER NULL)");
        source.Execute("INSERT INTO TransferRows(Id,Value) VALUES('1',NULL)");
        var before = source.Query("SELECT Value FROM TransferRows").Rows.Single()[0];
        Assert.Equal(0L, Convert.ToInt64(before));
        var table = source.Db.CreateMetaData().GetTables().Single(t => t.TableName == "TransferRows");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Assert.Equal(1, new DbPackage { Dal = source, IgnoreError = false }.Sync(table, target.ConnName, false, cancellation.Token));
        Assert.False(IsNull(target));
        Assert.Equal(before, source.Query("SELECT Value FROM TransferRows").Rows.Single()[0]);
    }

    [Fact]
    public void Sync_CancelledAfterFirstPage_StopsBeforeTheNextPage()
    {
        using var scope = new TransferScope();
        var source = scope.Open("source", false);
        var target = scope.Open("target", true);
        source.Execute("CREATE TABLE TransferRows(Id INTEGER NOT NULL PRIMARY KEY, Value INTEGER NULL)");
        source.Execute("INSERT INTO TransferRows(Id,Value) VALUES(1,NULL),(2,NULL)");
        target.Execute("CREATE TABLE TransferRows(Id INT NOT NULL, Value INT NULL, PRIMARY KEY(Id))");
        var table = source.Db.CreateMetaData().GetTables().Single(t => t.TableName == "TransferRows");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var package = new OneRowPackage { Dal = source, IgnoreError = false };
        var pages = 0;
        package.OnPage += (_, _) => { pages++; cancellation.Cancel(); };
        Assert.ThrowsAny<OperationCanceledException>(() => package.Sync(table, target.ConnName, false, cancellation.Token));
        Assert.Equal(1, pages);
    }

    [Fact]
    public void WriteXml_ExactPhysicalNameCase_IsExplicitlyOptedIn()
    {
        var table = DAL.CreateTable();
        table.Name = "MixedName";
        table.TableName = "mixedname";
        table.DbType = DatabaseType.SonnetDB;
        var column = table.CreateColumn();
        column.Name = "Value";
        column.ColumnName = "value";
        column.DataType = typeof(Int32);
        table.Columns.Add(column);
        Assert.Contains("TableName=\"mixedname\"", Export(table, NameFormats.Exact));
        Assert.Contains("ColumnName=\"value\"", Export(table, NameFormats.Exact));
        table.DbType = DatabaseType.SQLite;
        Assert.DoesNotContain("TableName=", Export(table));
        Assert.DoesNotContain("ColumnName=", Export(table));
    }

    private static Boolean IsNull(DAL dal)
    {
        using DbConnection connection = dal.Db.Factory.CreateConnection()!;
        connection.ConnectionString = dal.Db.ConnectionString;
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM TransferRows";
        using var reader = command.ExecuteReader();
        return reader.Read() && reader.IsDBNull(0);
    }

    private static String Export(IDataTable table, NameFormats mode = NameFormats.Default)
    {
        var text = new StringBuilder();
        using (var writer = XmlWriter.Create(text, new XmlWriterSettings { OmitXmlDeclaration = true }))
        {
            writer.WriteStartElement("Table");
            table.WriteXml(writer, mode);
            writer.WriteEndElement();
        }
        return text.ToString();
    }

    private sealed class TrackingExtracter(DAL dal, String table) : PagingExtracter(dal, table, "Id")
    {
        public Boolean Called { get; private set; }
        public override IEnumerable<DbTable> Fetch()
        {
            Called = true;
            foreach (var page in base.Fetch()) yield return page;
        }
    }

    private sealed class OneRowPackage : SonnetDBPackage
    {
        public override IExtracter<DbTable> GetExtracter(IDataTable table)
        {
            var extractor = base.GetExtracter(table);
            if (extractor is IdExtracter id) id.BatchSize = 1;
            return extractor;
        }
    }

    private sealed class TransferScope : IDisposable
    {
        private readonly String _directory = Path.Combine(Path.GetTempPath(), "xcode-sonnetdb-transfer-" + Guid.NewGuid().ToString("N"));
        private readonly List<DAL> _dals = new();

        public TransferScope()
        {
            Directory.CreateDirectory(_directory);
            GC.KeepAlive(SqliteFactory.Instance);
            GC.KeepAlive(SndbProviderFactory.Instance);
        }

        public DAL Open(String name, Boolean sonnet)
        {
            var connectionName = "transfer_" + Guid.NewGuid().ToString("N");
            var source = Path.Combine(_directory, name + (sonnet ? "" : ".sqlite"));
            DAL.AddConnStr(connectionName, "Data Source=" + source + (sonnet ? "" : ";Pooling=False"), null, sonnet ? "SonnetDB" : "SQLite");
            var dal = DAL.Create(connectionName);
            _dals.Add(dal);
            return dal;
        }

        public void Dispose()
        {
            foreach (var dal in _dals)
            {
                dal.Session.Dispose();
                dal.Db.Dispose();
            }
            SqliteConnection.ClearAllPools();
            var absolute = Path.GetFullPath(_directory);
            var temporary = Path.GetFullPath(Path.GetTempPath());
            if (!absolute.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(absolute).StartsWith("xcode-sonnetdb-transfer-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected temporary transfer directory.");
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
        }
    }
}
