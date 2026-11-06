using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using Microsoft.Data.Sqlite;
using NewLife.Data;
using XCode.DataAccessLayer;
using XCode.Transform;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>使用SQLite最小复现验证可选扩展点，不依赖其它数据库。</summary>
[Collection("Database")]
public class PackageExtensionTests
{
    [Theory]
    [InlineData("id")]
    [InlineData("paging")]
    [InlineData("time")]
    public void Sync_QueryCallback_PreservesNullWithoutChangingDefaultReads(String algorithm)
    {
        using var scope = new Scope();
        var source = scope.Open("source");
        var target = scope.Open("target");
        var numeric = algorithm == "id";
        foreach (var dal in new[] { source, target })
            dal.Execute($"CREATE TABLE Rows(Id {(numeric ? "INTEGER" : "VARCHAR(30)")} NOT NULL PRIMARY KEY, Value INTEGER NULL, Created DATETIME NOT NULL)");
        source.Execute($"INSERT INTO Rows VALUES({(numeric ? "1" : "'1'")},NULL,'{DateTime.Now.AddSeconds(-5):yyyy-MM-dd HH:mm:ss}')");
        if (algorithm == "time") source.Execute("CREATE INDEX IX_Rows_Created ON Rows(Created)");
        var table = source.Db.CreateMetaData().GetTables().Single(t => t.TableName == "Rows");
        var package = new DbPackage { Dal = source, IgnoreError = false };
        Assert.Equal(algorithm == "id" ? typeof(IdExtracter) : algorithm == "time" ? typeof(TimeExtracter) : typeof(PagingExtracter), package.GetExtracter(table).GetType());
        Assert.Equal(0L, Convert.ToInt64(source.Query("SELECT Value FROM Rows").Rows.Single()[0]));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var calls = 0;
        package.QueryCallback = (actualSource, builder, row, size, token) =>
        {
            Assert.Same(source, actualSource);
            Assert.Equal(cancellation.Token, token);
            Assert.InRange(++calls, 1, 10);
            return ReadPage(actualSource, builder, row, size);
        };
        Assert.Equal(1, package.Sync(table, target.ConnName, false, cancellation.Token));
        Assert.InRange(calls, 1, 10);
        Assert.Equal(1, target.Session.ExecuteScalar<Int32>("SELECT COUNT(*) FROM Rows WHERE Value IS NULL"));
        Assert.Equal(0L, Convert.ToInt64(source.Query("SELECT Value FROM Rows").Rows.Single()[0]));
    }

    [Fact]
    public void Sync_CustomExtracter_KeepsItsOwnQueryAndSource()
    {
        using var scope = new Scope();
        var host = scope.Open("host");
        var external = scope.Open("external");
        var target = scope.Open("target");
        foreach (var dal in new[] { host, external, target }) dal.Execute("CREATE TABLE Rows(Id VARCHAR(30) NOT NULL PRIMARY KEY, Value INTEGER NULL)");
        external.Execute("INSERT INTO Rows VALUES('external',911)");
        var table = host.Db.CreateMetaData().GetTables().Single(t => t.TableName == "Rows");
        var package = new DbPackage
        {
            Dal = host,
            IgnoreError = false,
            QueryCallback = (_, _, _, _, _) => throw new InvalidOperationException("Custom extractor must own its query."),
            CreateExtracterCallback = _ => new PagingExtracter(external, "Rows", "Id"),
        };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Assert.Equal(1, package.Sync(table, target.ConnName, false, cancellation.Token));
        Assert.Equal(911, target.Session.ExecuteScalar<Int32>("SELECT Value FROM Rows"));
    }

    [Fact]
    public void Sync_UnsetCallback_PreservesExistingDefaultNullConversion()
    {
        using var scope = new Scope();
        var source = scope.Open("source");
        var target = scope.Open("target");
        foreach (var dal in new[] { source, target }) dal.Execute("CREATE TABLE Rows(Id INTEGER NOT NULL PRIMARY KEY, Value INTEGER NULL)");
        source.Execute("INSERT INTO Rows VALUES(1,NULL)");
        var table = source.Db.CreateMetaData().GetTables().Single(t => t.TableName == "Rows");
        var package = new DbPackage { Dal = source, IgnoreError = false };
        Assert.Null(package.QueryCallback);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Assert.Equal(1, package.Sync(table, target.ConnName, false, cancellation.Token));
        Assert.Equal(1, target.Session.ExecuteScalar<Int32>("SELECT COUNT(*) FROM Rows WHERE Value=0"));
    }

    [Theory]
    [InlineData("mixedname", "value")]
    [InlineData("MIXEDNAME", "VALUE")]
    [InlineData("mixedName", "vAlue")]
    public void WriteXml_Exact_RoundTripsPhysicalSpelling(String tableName, String columnName)
    {
        var table = DAL.CreateTable();
        table.Name = "MixedName";
        table.TableName = tableName;
        table.DbType = DatabaseType.SQLite;
        var column = table.CreateColumn();
        column.Name = "Value";
        column.ColumnName = columnName;
        column.DataType = typeof(Int32);
        table.Columns.Add(column);
        var text = new StringBuilder();
        using (var writer = XmlWriter.Create(text, new XmlWriterSettings { OmitXmlDeclaration = true }))
        {
            writer.WriteStartElement("Table");
            table.WriteXml(writer, NameFormats.Exact);
            writer.WriteEndElement();
        }
        Assert.Contains("TableName=\"" + tableName + "\"", text.ToString());
        Assert.Contains("ColumnName=\"" + columnName + "\"", text.ToString());
        var imported = DAL.Import("<EntityModel><Tables>" + text + "</Tables></EntityModel>").Single();
        Assert.Equal(tableName, imported.TableName);
        Assert.Equal(columnName, imported.Columns.Single().ColumnName);
    }

    [Theory]
    [InlineData(NameFormats.Default)]
    [InlineData(NameFormats.None)]
    public void WriteXml_ExistingModes_KeepTheirOriginalCaseHeuristic(NameFormats mode)
    {
        var table = DAL.CreateTable();
        table.Name = "MixedName";
        table.TableName = "mixedname";
        var text = new StringBuilder();
        using (var writer = XmlWriter.Create(text, new XmlWriterSettings { OmitXmlDeclaration = true }))
        {
            writer.WriteStartElement("Table");
            table.WriteXml(writer, mode);
            writer.WriteEndElement();
        }
        Assert.DoesNotContain("TableName=", text.ToString());
        Assert.Equal(4, (Int32)NameFormats.Underline);
    }

    private static DbTable ReadPage(DAL source, SelectBuilder builder, Int64 row, Int64 size)
    {
        using var connection = source.Db.Factory.CreateConnection()!;
        connection.ConnectionString = source.Db.ConnectionString;
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = source.PageSplit(builder.Clone(), row, size).ToString();
        command.CommandTimeout = 30;
        command.Parameters.AddRange(builder.Parameters.ToArray());
        using var reader = command.ExecuteReader();
        var result = new DbTable();
        result.ReadHeader(reader);
        for (var i = 0; i < size && reader.Read(); i++)
        {
            var values = new Object?[reader.FieldCount];
            for (var j = 0; j < values.Length; j++) values[j] = reader.IsDBNull(j) ? null : reader.GetValue(j);
            result.Rows.Add(values);
        }
        result.Total = result.Rows.Count;
        return result;
    }

    private sealed class Scope : IDisposable
    {
        private readonly String _directory = Path.Combine(Path.GetTempPath(), "xcode-extension-" + Guid.NewGuid().ToString("N"));
        private readonly List<DAL> _dals = new();
        public Scope() { Directory.CreateDirectory(_directory); GC.KeepAlive(SqliteFactory.Instance); }
        public DAL Open(String name)
        {
            var connection = "extensions_" + Guid.NewGuid().ToString("N");
            DAL.AddConnStr(connection, "Data Source=" + Path.Combine(_directory, name + ".sqlite") + ";Pooling=False", null, "SQLite");
            var dal = DAL.Create(connection);
            _dals.Add(dal);
            return dal;
        }
        public void Dispose()
        {
            foreach (var dal in _dals) { dal.Session.Dispose(); dal.Db.Dispose(); }
            SqliteConnection.ClearAllPools();
            var absolute = Path.GetFullPath(_directory);
            if (!absolute.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(absolute).StartsWith("xcode-extension-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected test directory.");
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
        }
    }
}
