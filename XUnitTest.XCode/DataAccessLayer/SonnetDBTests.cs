using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NewLife;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Model;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>通过正式 ADO.NET 驱动验证 SonnetDB 的 XCode 接入；每个用例使用独立嵌入式数据库。</summary>
[Collection("Database")]
public class SonnetDBTests
{
    [Fact]
    public void PrimitiveParametersPreserveValuesAndNulls()
    {
        using var scope = new DatabaseScope();
        var values = new Object[] { (SByte)(-7), (UInt16)65535, UInt32.MaxValue, (UInt64)Int64.MaxValue, '中' };
        foreach (var value in values)
        {
            var parameter = scope.Dal.Db.CreateParameter("value", value);
            var result = scope.Dal.Session.ExecuteScalar<Object>("SELECT @value", CommandType.Text, parameter);
            if (value is Char) Assert.Equal("中", result);
            else Assert.Equal(Convert.ToInt64(value), Convert.ToInt64(result));
        }
        var nullableGuid = scope.Dal.Db.CreateParameter("value", DBNull.Value, typeof(Guid));
        Assert.Same(DBNull.Value, nullableGuid.Value);
        Assert.Throws<NotSupportedException>(() => scope.Dal.Db.CreateParameter("value", UInt64.MaxValue));
    }

    [Fact]
    public void UpsertUsesFormattedPhysicalKeyColumns()
    {
        using var scope = new DatabaseScope();
        scope.Dal.Db.NameFormat = NameFormats.Underline;
        var table = CreateTable("ConflictRows");
        table.Columns[0].Name = table.Columns[0].ColumnName = "UserId";
        table.Fix();
        Assert.True(scope.Dal.Db.CreateMetaData().CreateTable(table));
        var columns = table.Columns.ToArray();
        var session = scope.Dal.Session;
        Assert.Equal(1, session.Insert(table, columns, new[] { new KeyRow(7, "before") }));
        Assert.Equal(1, session.Upsert(table, columns, new[] { "Name" }, null, new[] { new KeyRow(7, "after") }));
        var formatted = scope.Dal.Db.FormatName(table);
        Assert.Equal("after", session.ExecuteScalar<String>($"SELECT name FROM {formatted} WHERE user_id=7"));
        Assert.Equal(1, session.ExecuteScalar<Int32>($"SELECT COUNT(*) FROM {formatted}"));
    }

    [Fact]
    public void UnboundedStringsAndViewMetadataGuard()
    {
        using var scope = new DatabaseScope();
        var table = CreateTable("ViewSource");
        table.Columns.Single(column => column.ColumnName == "Name").Length = 0;
        var meta = scope.Dal.Db.CreateMetaData();
        Assert.True(meta.CreateTable(table));
        scope.Dal.Execute("INSERT INTO ViewSource(Name) VALUES('ordinary text')");
        var actual = Assert.Single(meta.GetTables(), item => item.TableName == "ViewSource");
        Assert.Equal("STRING", actual.Columns.Single(column => column.ColumnName == "Name").RawType);
        scope.Dal.Execute("CREATE VIEW ViewProjection AS SELECT Id,Name FROM ViewSource");
        Assert.Contains("ViewProjection", meta.GetTableNames());
        var error = Assert.Throws<NotSupportedException>(() => meta.GetTables());
        Assert.Contains("#261", error.Message);
    }

    [Fact(Skip = "🛠️ SonnetDB 尚需补齐视图声明元数据并正式发布：https://github.com/IoTSharp/SonnetDB/issues/261")]
    public void ViewMetadataRetainsDeclaredTypesWithoutReadingRows()
    {
        using var scope = new DatabaseScope();
        var meta = scope.Dal.Db.CreateMetaData();
        Assert.True(meta.CreateTable(CreateTable("ViewSource")));
        scope.Dal.Execute("CREATE VIEW ViewProjection AS SELECT Id,Name FROM ViewSource");
        var view = Assert.Single(meta.GetTables(), item => item.TableName == "ViewProjection");
        Assert.True(view.IsView);
        Assert.Equal(new[] { "Id", "Name" }, view.Columns.Select(column => column.ColumnName));
        Assert.Equal(typeof(Int64), view.Columns[0].DataType);
        Assert.Equal(typeof(String), view.Columns[1].DataType);
    }

    [Fact]
    public void FactoryAndDAL()
    {
        using var scope = new DatabaseScope();
        var dal = scope.Dal;
        Assert.Equal(DatabaseType.SonnetDB, dal.DbType);
        Assert.Equal(scope.ConnName, dal.ConnName);
        Assert.NotNull(dal.Db.Factory.CreateCommand());
        Assert.NotNull(dal.Db.Factory.CreateParameter());
        // The provider has no DbDataAdapter; XCode must supply its DataSet query path.
        Assert.Null(dal.Db.Factory.CreateDataAdapter());

        using var connection = dal.Db.OpenConnection();
        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Equal("SonnetDB.Data.SndbConnection", connection.GetType().FullName);
        Assert.NotEmpty(dal.Db.ServerVersion);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 123";
        Assert.Equal(123, Convert.ToInt32(command.ExecuteScalar()));
        var dataSet = dal.Select("SELECT 123 AS Value");
        Assert.Equal(123, Convert.ToInt32(dataSet.Tables[0].Rows[0][0]));
    }

    [Fact]
    public void AutomaticSchemaAndReverseMetadata()
    {
        using var scope = new DatabaseScope();
        var table = CreateTable("MetadataRows");
        var amount = AddColumn(table, "Amount", typeof(Decimal));
        amount.Precision = 18;
        amount.Scale = 4;
        var enabled = AddColumn(table, "Enabled", typeof(Boolean));
        enabled.DefaultValue = "true";
        var payload = AddColumn(table, "Payload", typeof(Byte[]), true);
        var time = AddColumn(table, "Created", typeof(DateTime), true);
        AddIndex(table, "IU_MetadataRows_Name_Amount", true, "Name", "Amount");
        var meta = scope.Dal.Db.CreateMetaData();
        meta.SetTables(Migration.Full, table);
        // Repeating the model check must leave the table usable and retain its indexes.
        meta.SetTables(Migration.Full, table);

        Assert.Contains(table.TableName, meta.GetTableNames());
        var actual = Assert.Single(meta.GetTables(), e => e.TableName == table.TableName);
        var id = Assert.Single(actual.Columns, e => e.ColumnName == "Id");
        Assert.True(id.PrimaryKey);
        Assert.True(id.Identity);
        Assert.False(id.Nullable);
        Assert.Equal(typeof(Int64), id.DataType);
        Assert.Equal(typeof(Decimal), actual.Columns.Single(e => e.ColumnName == amount.ColumnName).DataType);
        Assert.Equal(18, actual.Columns.Single(e => e.ColumnName == amount.ColumnName).Precision);
        Assert.Equal(4, actual.Columns.Single(e => e.ColumnName == amount.ColumnName).Scale);
        Assert.Equal(typeof(Byte[]), actual.Columns.Single(e => e.ColumnName == payload.ColumnName).DataType);
        Assert.Equal(typeof(DateTime), actual.Columns.Single(e => e.ColumnName == time.ColumnName).DataType);
        var actualEnabled = actual.Columns.Single(e => e.ColumnName == enabled.ColumnName);
        Assert.Equal(typeof(Boolean), actualEnabled.DataType);
        Assert.False(String.IsNullOrWhiteSpace(actualEnabled.DefaultValue));
        var unique = Assert.Single(actual.Indexes, e => e.Name == "IU_MetadataRows_Name_Amount");
        Assert.True(unique.Unique);
        Assert.Equal(new[] { "Name", "Amount" }, unique.Columns);

        scope.Dal.Execute("INSERT INTO MetadataRows (Name,Amount) VALUES ('default',1)");
        Assert.True(scope.Dal.Session.ExecuteScalar<Boolean>("SELECT Enabled FROM MetadataRows WHERE Name='default'"));
        Assert.True(meta.DropTable(table));
        Assert.DoesNotContain(table.TableName, meta.GetTableNames());
    }

    [Fact]
    public async Task IdentityDoesNotDependOnColumnOrder()
    {
        using var scope = new DatabaseScope();
        var table = CreateTable("IdentityRows");
        // A text field precedes the identity field in the physical table.
        var idColumn = table.Columns[0];
        table.Columns.RemoveAt(0);
        table.Columns.Add(idColumn);
        scope.Dal.Db.CreateMetaData().SetTables(Migration.Full, table);
        var id = scope.Dal.InsertAndGetIdentity("INSERT INTO IdentityRows (Name) VALUES ('first')");
        Assert.True(id > 0);
        Assert.Equal(id, scope.Dal.Session.ExecuteScalar<Int64>("SELECT Id FROM IdentityRows WHERE Name='first'"));
        var next = await scope.Dal.InsertAndGetIdentityAsync("INSERT INTO IdentityRows (Name) VALUES ('second')");
        Assert.True(next > id);
        Assert.Equal(next, scope.Dal.Session.ExecuteScalar<Int64>("SELECT Id FROM IdentityRows WHERE Name='second'"));
    }

    [Fact]
    public async Task EntityCRUDAndTypedRoundtrip()
    {
        using var scope = new DatabaseScope();
        using var split = scope.CreateEntitySplit();
        var entity = new SonnetTestRow
        {
            Name = "中文😀 O'Brien",
            Amount = 123456789.1234m,
            Payload = new Byte[] { 0, 1, 127, 128, 255 },
            Enabled = true,
            Created = new DateTime(2026, 10, 8, 12, 34, 56).AddMilliseconds(123),
            Remark = null
        };
        Assert.Equal(1, entity.Insert());
        Assert.True(entity.Id > 0);
        var loaded = Assert.Single(SonnetTestRow.FindAll());
        Assert.Equal(entity.Id, loaded.Id);
        Assert.Equal(entity.Name, loaded.Name);
        Assert.Equal(entity.Amount, loaded.Amount);
        Assert.Equal(entity.Payload, loaded.Payload);
        Assert.Equal(entity.Enabled, loaded.Enabled);
        Assert.Equal(entity.Created, loaded.Created);
        Assert.Null(loaded.Remark);

        loaded.Amount = -12.3456m;
        loaded.Remark = "更新";
        Assert.Equal(1, loaded.Update());
        var updated = Assert.Single(SonnetTestRow.FindAll());
        Assert.Equal(-12.3456m, updated.Amount);
        Assert.Equal("更新", updated.Remark);
        Assert.Equal(1, updated.Delete());
        Assert.Empty(SonnetTestRow.FindAll());

        var asyncRow = new SonnetTestRow { Name = "async", Amount = 0.0001m, Created = entity.Created };
        Assert.Equal(1, await asyncRow.InsertAsync());
        Assert.True(asyncRow.Id > entity.Id);
        asyncRow.Remark = "异步";
        Assert.Equal(1, await asyncRow.UpdateAsync());
        Assert.Equal("异步", Assert.Single(SonnetTestRow.FindAll()).Remark);
        Assert.Equal(1, await asyncRow.DeleteAsync());
        Assert.Empty(SonnetTestRow.FindAll());
    }

    [Fact]
    public async Task PagingAggregatesAndParameters()
    {
        using var scope = new DatabaseScope();
        using var split = scope.CreateEntitySplit();
        var rows = Enumerable.Range(1, 5).Select(i => new SonnetTestRow { Name = $"row{i}", Amount = i, Created = new DateTime(2026, 10, 8) }).ToList();
        Assert.Equal(5, rows.BatchInsert());
        var page = SonnetTestRow.FindAll((String)null, "Id ASC", null, 2, 2);
        Assert.Equal(new[] { "row3", "row4" }, page.Select(e => e.Name));
        var remaining = SonnetTestRow.FindAll((String)null, "Id ASC", null, 3, 0);
        Assert.Equal(new[] { "row4", "row5" }, remaining.Select(e => e.Name));

        var result = scope.Dal.Query($"SELECT COUNT(*), SUM(Amount) FROM {scope.TableName}");
        var aggregate = Assert.Single(result.Rows);
        Assert.Equal(5, Convert.ToInt32(aggregate[0]));
        Assert.Equal(15m, Convert.ToDecimal(aggregate[1]));

        const String value = "中文 ' OR 1=1 -- 😀";
        Assert.Equal(1, scope.Dal.Execute($"UPDATE {scope.TableName} SET Remark=@remark WHERE Name=@name", CommandType.Text,
            new Dictionary<String, Object> { ["remark"] = value, ["name"] = "row3" }));
        var selected = await scope.Dal.QueryAsync($"SELECT Name,Remark FROM {scope.TableName} WHERE Remark=@remark",
            new Dictionary<String, Object> { ["remark"] = value });
        var record = Assert.Single(selected.Rows);
        Assert.Equal("row3", record[0]);
        Assert.Equal(value, record[1]);
    }

    [Fact]
    public void BatchInsertIgnoreAndUpsert()
    {
        using var scope = new DatabaseScope();
        using var split = scope.CreateEntitySplit();
        var rows = new List<SonnetTestRow>
        {
            new() { Name = "first", Amount = 1m, Remark = "original", Created = new DateTime(2026, 10, 8) },
            new() { Name = "second", Amount = 2m, Created = new DateTime(2026, 10, 8) }
        };
        Assert.Equal(2, rows.BatchInsert());
        var firstId = SonnetTestRow.FindAll().Single(e => e.Name == "first").Id;
        var ignored = new List<SonnetTestRow>
        {
            new() { Name = "first", Amount = 99m },
            new() { Name = "third", Amount = 3m }
        };
        Assert.Equal(1, ignored.BatchInsertIgnore());
        Assert.Equal(1m, SonnetTestRow.FindAll().Single(e => e.Name == "first").Amount);

        var upserted = new List<SonnetTestRow>
        {
            new() { Name = "first", Amount = 4m, Remark = "upsert" },
            new() { Name = "fourth", Amount = 4m, Remark = "insert" }
        };
        Assert.True(upserted.BatchUpsert(new BatchOption { UpdateColumns = new[] { "Remark" }, AddColumns = new[] { "Amount" } }) > 0);
        var all = SonnetTestRow.FindAll();
        Assert.Equal(4, all.Count);
        var first = all.Single(e => e.Name == "first");
        Assert.Equal(firstId, first.Id);
        Assert.Equal(5m, first.Amount);
        Assert.Equal("upsert", first.Remark);
        Assert.Contains(all, e => e.Name == "fourth" && e.Amount == 4m && e.Remark == "insert");
    }

    [Fact]
    public void UnsupportedReplaceFailsClearly()
    {
        using var scope = new DatabaseScope();
        using var split = scope.CreateEntitySplit();
        var row = new SonnetTestRow { Name = "retained", Remark = "original" };
        Assert.Equal(1, row.Insert());
        var replaced = new List<SonnetTestRow> { new() { Name = "retained", Remark = "replacement" } };
        Assert.Throws<NotSupportedException>(() => replaced.BatchReplace());
        Assert.Equal("original", Assert.Single(SonnetTestRow.FindAll()).Remark);
    }

    [Fact(Skip = "🛠️ SonnetDB 尚需实现原子 REPLACE 并正式发布：https://github.com/IoTSharp/SonnetDB/issues/242")]
    public void BatchReplaceDeletesThenInserts()
    {
        using var scope = new DatabaseScope();
        using var split = scope.CreateEntitySplit();
        var original = new SonnetTestRow { Name = "first", Amount = 1m, Remark = "original", Created = new DateTime(2026, 10, 8) };
        Assert.Equal(1, original.Insert());
        var replaced = new List<SonnetTestRow> { new() { Name = "first", Amount = 10m, Created = original.Created } };
        Assert.True(replaced.BatchReplace(new BatchOption { FullInsert = true }) > 0);
        var replacement = Assert.Single(SonnetTestRow.FindAll());
        Assert.Equal(10m, replacement.Amount);
        Assert.Null(replacement.Remark);
        Assert.NotEqual(original.Id, replacement.Id);
    }

    [Fact]
    public void UnsupportedDecimalMigrationFailsClearly()
    {
        using var scope = new DatabaseScope();
        var table = CreateTable("DecimalGuardRows");
        var amount = AddColumn(table, "Amount", typeof(Decimal), true);
        amount.Precision = 18;
        amount.Scale = 4;
        var meta = scope.Dal.Db.CreateMetaData();
        meta.SetTables(Migration.Full, table);
        scope.Dal.Execute("INSERT INTO DecimalGuardRows (Name,Amount) VALUES ('retained',123.1234)");
        var extra = AddColumn(table, "Extra", typeof(Decimal), true);
        extra.Precision = 18;
        extra.Scale = 4;
        Assert.Throws<NotSupportedException>(() => meta.AddColumn(extra));
        var old = amount.Clone(table);
        amount.Precision = 20;
        amount.Scale = 6;
        Assert.Throws<NotSupportedException>(() => meta.AlterColumn(amount, old));
        Assert.Equal(123.1234m, scope.Dal.Session.ExecuteScalar<Decimal>("SELECT Amount FROM DecimalGuardRows"));
        Assert.DoesNotContain(meta.GetTables().Single(e => e.TableName == table.TableName).Columns, e => e.ColumnName == "Extra");
    }

    [Fact]
    public void ValuesThatWouldLosePrecisionFailClearly()
    {
        using var scope = new DatabaseScope();
        var table = new XTable("PrecisionGuardRows");
        var number = AddColumn(table, "Amount", typeof(Decimal));
        const Decimal unsafeAmount = 9007199254740993.123456789m;
        Assert.Throws<NotSupportedException>(() => scope.Dal.Db.CreateParameter("amount", unsafeAmount));
        Assert.Throws<NotSupportedException>(() => scope.Dal.Db.FormatValue(number, unsafeAmount));
        var timestamp = AddColumn(table, "Created", typeof(DateTime));
        var unsafeTime = new DateTime(2026, 10, 8, 12, 34, 56).AddTicks(1);
        Assert.Throws<NotSupportedException>(() => scope.Dal.Db.CreateParameter("time", unsafeTime));
        Assert.Throws<NotSupportedException>(() => scope.Dal.Db.FormatValue(timestamp, unsafeTime));
        var unsafeOffset = new DateTimeOffset(unsafeTime, TimeSpan.FromHours(8));
        Assert.Throws<NotSupportedException>(() => scope.Dal.Db.CreateParameter("offset", unsafeOffset));
        var unsigned = AddColumn(table, "Unsigned", typeof(UInt64));
        Assert.Throws<NotSupportedException>(() => scope.Dal.Db.CreateParameter("unsigned", UInt64.MaxValue));
        Assert.Throws<NotSupportedException>(() => scope.Dal.Db.FormatValue(unsigned, UInt64.MaxValue));
    }

    [Fact(Skip = "🛠️ SonnetDB 尚需修复高精度 decimal 与亚毫秒 DATETIME 保真并正式发布：https://github.com/IoTSharp/SonnetDB/issues/235 https://github.com/IoTSharp/SonnetDB/issues/236")]
    public void HighPrecisionValuesRoundtrip()
    {
        using var scope = new DatabaseScope();
        var table = CreateTable("HighPrecisionRows");
        var amount = AddColumn(table, "Amount", typeof(Decimal));
        amount.Precision = 38;
        amount.Scale = 20;
        AddColumn(table, "Created", typeof(DateTime));
        scope.Dal.Db.CreateMetaData().SetTables(Migration.Full, table);
        const Decimal value = 9007199254740993.123456789m;
        var time = new DateTime(2026, 10, 8, 12, 34, 56).AddTicks(1234567);
        scope.Dal.Execute("INSERT INTO HighPrecisionRows (Name,Amount,Created) VALUES (@name,@amount,@created)", CommandType.Text,
            new Dictionary<String, Object> { ["name"] = "precision", ["amount"] = value, ["created"] = time });
        var record = Assert.Single(scope.Dal.Query("SELECT Amount,Created FROM HighPrecisionRows").Rows);
        Assert.Equal(value, Convert.ToDecimal(record[0]));
        Assert.Equal(time, Convert.ToDateTime(record[1]));
    }

    [Fact(Skip = "🛠️ SonnetDB 尚需修复 DECIMAL ADD/ALTER 精度并正式发布：https://github.com/IoTSharp/SonnetDB/issues/241")]
    public void DecimalColumnMigrationRetainsPrecision()
    {
        using var scope = new DatabaseScope();
        var table = CreateTable("DecimalMigrationRows");
        var meta = scope.Dal.Db.CreateMetaData();
        meta.SetTables(Migration.Full, table);
        var amount = AddColumn(table, "Amount", typeof(Decimal), true);
        amount.Precision = 18;
        amount.Scale = 4;
        Assert.True(meta.AddColumn(amount));
        scope.Dal.Execute("INSERT INTO DecimalMigrationRows (Name,Amount) VALUES ('precision',123456.1234)");
        var old = amount.Clone(table);
        amount.Precision = 20;
        amount.Scale = 6;
        Assert.True(meta.AlterColumn(amount, old));
        Assert.Equal(123456.1234m, scope.Dal.Session.ExecuteScalar<Decimal>("SELECT Amount FROM DecimalMigrationRows"));
        var actual = meta.GetTables().Single(e => e.TableName == table.TableName).Columns.Single(e => e.ColumnName == "Amount");
        Assert.Equal(20, actual.Precision);
        Assert.Equal(6, actual.Scale);
    }

    [Fact]
    public void TransactionCommitAndRollback()
    {
        using var scope = new DatabaseScope();
        var table = CreateTable("TransactionRows");
        scope.Dal.Db.CreateMetaData().SetTables(Migration.Full, table);
        var session = scope.Dal.Session;
        session.BeginTransaction(IsolationLevel.ReadCommitted);
        session.Execute("INSERT INTO TransactionRows (Name) VALUES ('committed')");
        session.Commit();
        Assert.Equal(1, session.ExecuteScalar<Int32>("SELECT COUNT(*) FROM TransactionRows"));
        session.BeginTransaction(IsolationLevel.ReadCommitted);
        session.Execute("UPDATE TransactionRows SET Name='changed' WHERE Name='committed'");
        session.Execute("INSERT INTO TransactionRows (Name) VALUES ('rolled-back')");
        session.Rollback(false);
        Assert.Equal(1, session.ExecuteScalar<Int32>("SELECT COUNT(*) FROM TransactionRows"));
        Assert.Equal("committed", session.ExecuteScalar<String>("SELECT Name FROM TransactionRows"));
    }

    [Fact]
    public void SchemaEvolutionPreservesData()
    {
        using var scope = new DatabaseScope();
        var table = CreateTable("MigrationRows");
        var meta = scope.Dal.Db.CreateMetaData();
        meta.SetTables(Migration.Full, table);
        scope.Dal.Execute("INSERT INTO MigrationRows (Name) VALUES ('retained')");
        var extra = AddColumn(table, "Extra", typeof(String), true, 30);
        Assert.True(meta.AddColumn(extra));
        scope.Dal.Execute("UPDATE MigrationRows SET Extra='column-data' WHERE Name='retained'");
        var before = extra.Clone(table);
        extra.Nullable = false;
        extra.DefaultValue = "'default-extra'";
        Assert.True(meta.AlterColumn(extra, before));
        var index = AddIndex(table, "IX_MigrationRows_Extra", false, "Extra");
        Assert.True(meta.CreateIndex(index));
        var actual = meta.GetTables().Single(e => e.TableName == table.TableName);
        Assert.Contains(actual.Columns, e => e.ColumnName == "Extra");
        Assert.False(actual.Columns.Single(e => e.ColumnName == "Extra").Nullable);
        Assert.Contains("default-extra", actual.Columns.Single(e => e.ColumnName == "Extra").DefaultValue);
        Assert.Contains(actual.Indexes, e => e.Name == index.Name);
        Assert.Equal("column-data", scope.Dal.Session.ExecuteScalar<String>("SELECT Extra FROM MigrationRows"));
        scope.Dal.Execute("INSERT INTO MigrationRows (Name) VALUES ('default-check')");
        Assert.Equal("default-extra", scope.Dal.Session.ExecuteScalar<String>("SELECT Extra FROM MigrationRows WHERE Name='default-check'"));
        Assert.True(meta.DropIndex(index));
        Assert.True(meta.DropColumn(extra));
        actual = meta.GetTables().Single(e => e.TableName == table.TableName);
        Assert.DoesNotContain(actual.Columns, e => e.ColumnName == "Extra");
        Assert.DoesNotContain(actual.Indexes, e => e.Name == index.Name);
        Assert.Equal("retained", scope.Dal.Session.ExecuteScalar<String>("SELECT Name FROM MigrationRows WHERE Name='retained'"));
    }

    [Fact]
    public void UnsupportedLengthAndCommentsFailClearly()
    {
        using var scope = new DatabaseScope();
        var table = CreateTable("UnsupportedSchemaRows");
        var meta = scope.Dal.Db.CreateMetaData();
        meta.SetTables(Migration.Full, table);
        var name = table.Columns.Single(e => e.ColumnName == "Name");
        var old = name.Clone(table);
        name.Length = 200;
        Assert.Throws<NotSupportedException>(() => meta.AlterColumn(name, old));
        table.Description = "需要保留的表说明";
        name.Description = "需要保留的字段说明";
        Assert.Throws<NotSupportedException>(() => meta.AddTableDescription(table));
        Assert.Throws<NotSupportedException>(() => meta.AddColumnDescription(name));
    }

    [Fact]
    public void EntityLinqUsesFilteringOrderingAndPaging()
    {
        using var scope = new DatabaseScope();
        using var split = scope.CreateEntitySplit();
        var rows = Enumerable.Range(1, 5).Select(i => new SonnetTestRow { Name = $"linq{i}", Amount = i }).ToList();
        Assert.Equal(5, rows.BatchInsert());
        var selected = SonnetTestRow.Query.Where(e => e.Amount >= 2m).OrderByDescending(e => e.Amount).Skip(1).Take(2).ToList();
        Assert.Equal(new[] { "linq4", "linq3" }, selected.Select(e => e.Name));
        Assert.Equal(4, SonnetTestRow.Query.Count(e => e.Amount >= 2m));
    }

    [Fact]
    public void JoinAndWindowQueries()
    {
        using var scope = new DatabaseScope();
        var parents = CreateTable("JoinParents");
        var children = CreateTable("JoinChildren");
        AddColumn(children, "ParentId", typeof(Int64));
        var meta = scope.Dal.Db.CreateMetaData();
        meta.SetTables(Migration.Full, parents, children);
        var first = scope.Dal.InsertAndGetIdentity("INSERT INTO JoinParents (Name) VALUES ('first')");
        scope.Dal.InsertAndGetIdentity("INSERT INTO JoinParents (Name) VALUES ('second')");
        scope.Dal.Execute($"INSERT INTO JoinChildren (Name,ParentId) VALUES ('child',{first})");
        var joined = scope.Dal.Query("SELECT p.Name,c.Name FROM JoinParents p LEFT JOIN JoinChildren c ON p.Id=c.ParentId ORDER BY p.Id");
        Assert.Equal(2, joined.Rows.Count);
        Assert.Equal("first", joined.Rows[0][0]);
        Assert.Equal("child", joined.Rows[0][1]);
        Assert.Equal("second", joined.Rows[1][0]);
        Assert.True(joined.Rows[1][1] == null || joined.Rows[1][1] == DBNull.Value);
        var builder = new SelectBuilder { Table = "JoinParents", Column = "Name", OrderBy = "Id", Window = WindowFunction.RowNumber("Id") };
        var numbered = scope.Dal.Query(builder, 0, 0);
        Assert.Equal(2, numbered.Rows.Count);
        Assert.Equal(1, Convert.ToInt32(numbered.Rows[0][1]));
        Assert.Equal(2, Convert.ToInt32(numbered.Rows[1][1]));
    }

    [Fact]
    public void TablePrefixWorksForEntitySchemaAndCRUD()
    {
        using var scope = new DatabaseScope("prefix_");
        using var split = scope.CreateEntitySplit();
        var row = new SonnetTestRow { Name = "prefixed", Amount = 1m };
        Assert.Equal(1, row.Insert());
        Assert.Equal("prefixed", Assert.Single(SonnetTestRow.FindAll()).Name);
        var names = scope.Dal.Db.CreateMetaData().GetTableNames();
        Assert.Contains("prefix_" + scope.TableName, names);
        Assert.DoesNotContain(scope.TableName, names);
        row.Remark = "updated";
        Assert.Equal(1, row.Update());
        Assert.Equal("updated", Assert.Single(SonnetTestRow.FindAll()).Remark);
        Assert.Equal(1, row.Delete());
        Assert.Empty(SonnetTestRow.FindAll());
    }

    [Fact]
    public void BoundedEntityDeleteRespectsMaximumRows()
    {
        using var scope = new DatabaseScope();
        using var split = scope.CreateEntitySplit();
        var rows = Enumerable.Range(1, 5).Select(i => new SonnetTestRow { Name = $"delete{i}" }).ToList();
        Assert.Equal(5, rows.BatchInsert());
        Assert.Equal(2, SonnetTestRow.Delete(SonnetTestRow.Meta.Table.FindByName("Id") > 0, 2));
        Assert.Equal(3, SonnetTestRow.FindAll().Count);
    }

    [Fact]
    public void EntityDeleteEscapesApostrophesAndKeepsOtherRows()
    {
        using var scope = new DatabaseScope();
        using var split = scope.CreateEntitySplit();
        var rows = new List<SonnetTestRow> { new() { Name = "O'Brien" }, new() { Name = "OBrien" } };
        Assert.Equal(2, rows.BatchInsert());
        Assert.Equal(1, SonnetTestRow.Delete(SonnetTestRow.Meta.Table.FindByName("Name") == "O'Brien", 1));
        Assert.Equal("OBrien", Assert.Single(SonnetTestRow.FindAll()).Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnsupportedBoundedDeleteWithoutSinglePrimaryKeyFailsClearly(Boolean composite)
    {
        using var scope = new DatabaseScope();
        var table = CreateTable("DeleteGuardRows");
        var id = table.Columns.Single(e => e.ColumnName == "Id");
        id.Identity = false;
        id.PrimaryKey = composite;
        if (composite)
        {
            var other = AddColumn(table, "Other", typeof(Int64));
            other.PrimaryKey = true;
        }
        scope.Dal.Db.CreateMetaData().SetTables(Migration.Full, table);
        if (composite) scope.Dal.Execute("INSERT INTO DeleteGuardRows (Id,Name,Other) VALUES (1,'retained',2)");
        Assert.Throws<NotSupportedException>(() => scope.Dal.Db.BuildDeleteSql(table.TableName, "Id>0", 1));
        Assert.Equal(composite ? 1 : 0, scope.Dal.Session.ExecuteScalar<Int32>("SELECT COUNT(*) FROM DeleteGuardRows"));
    }

    [Fact]
    public void TruncateRetainsSchemaAndResetsIdentity()
    {
        using var scope = new DatabaseScope();
        var table = CreateTable("TruncateRows");
        scope.Dal.Db.CreateMetaData().SetTables(Migration.Full, table);
        var first = scope.Dal.InsertAndGetIdentity("INSERT INTO TruncateRows (Name) VALUES ('before')");
        scope.Dal.Session.Truncate(table.TableName);
        Assert.Equal(0, scope.Dal.Session.ExecuteScalar<Int32>("SELECT COUNT(*) FROM TruncateRows"));
        var next = scope.Dal.InsertAndGetIdentity("INSERT INTO TruncateRows (Name) VALUES ('after')");
        Assert.Equal(first, next);
        Assert.Contains(table.TableName, scope.Dal.Db.CreateMetaData().GetTableNames());
    }

    private sealed class KeyRow : NewLife.Data.IModel
    {
        private readonly Dictionary<String, Object> _values;
        public KeyRow(Int64 id, String name) => _values = new() { ["UserId"] = id, ["Name"] = name };
        public Object this[String name] { get => _values[name]; set => _values[name] = value; }
    }

    private static IDataTable CreateTable(String name)
    {
        var table = new XTable(name) { Name = name };
        var id = AddColumn(table, "Id", typeof(Int64));
        id.Identity = true;
        id.PrimaryKey = true;
        AddColumn(table, "Name", typeof(String), false, 100);
        return table;
    }

    private static IDataColumn AddColumn(IDataTable table, String name, Type type, Boolean nullable = false, Int32 length = 0)
    {
        var column = table.CreateColumn();
        column.Name = column.ColumnName = name;
        column.DataType = type;
        column.Nullable = nullable;
        column.Length = length;
        table.Columns.Add(column);
        return column;
    }

    private static IDataIndex AddIndex(IDataTable table, String name, Boolean unique, params String[] columns)
    {
        var index = table.CreateIndex();
        index.Name = name;
        index.Unique = unique;
        index.Columns = columns;
        table.Indexes.Add(index);
        return index;
    }

    private sealed class DatabaseScope : IDisposable
    {
        private readonly String _directory = Path.Combine(Path.GetTempPath(), "xcode-sonnetdb", Guid.NewGuid().ToString("N"));
        public String ConnName { get; } = "SonnetDB_" + Guid.NewGuid().ToString("N");
        public String TableName { get; } = "SonnetRows_" + Guid.NewGuid().ToString("N");
        public DAL Dal { get; }

        public DatabaseScope(String prefix = null)
        {
            Directory.CreateDirectory(_directory);
            DAL.AddConnStr(ConnName, $"Data Source={_directory};Migration=Full;TablePrefix={prefix}", null, "SonnetDB");
            Dal = DAL.Create(ConnName);
        }

        public IDisposable CreateEntitySplit()
        {
            var split = SonnetTestRow.Meta.CreateSplit(ConnName, TableName);
            var model = (IDataTable)SonnetTestRow.Meta.Table.DataTable.Clone();
            model.TableName = TableName;
            Dal.Db.CreateMetaData().SetTables(Migration.Full, model);
            return split;
        }

        public void Dispose()
        {
            Dal.Session.Dispose();
            Dal.Db.Dispose();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
    }
}

/// <summary>仅用于 SonnetDB 集成测试，避免修改 Membership 等全局实体的连接。</summary>
[BindTable("SonnetTestRow", ConnName = "SonnetDB_Test", DbType = DatabaseType.None)]
[BindIndex("IU_SonnetTestRow_Name", true, "Name")]
public class SonnetTestRow : Entity<SonnetTestRow>
{
    private Int64 _id;
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "", "")]
    public Int64 Id { get => _id; set { if (OnPropertyChanging(nameof(Id), value)) { _id = value; OnPropertyChanged(nameof(Id)); } } }

    private String _name;
    [DataObjectField(false, false, false, 100)]
    [BindColumn("Name", "", "")]
    public String Name { get => _name; set { if (OnPropertyChanging(nameof(Name), value)) { _name = value; OnPropertyChanged(nameof(Name)); } } }

    private Decimal _amount;
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Amount", "", "decimal(18,4)", Precision = 18, Scale = 4)]
    public Decimal Amount { get => _amount; set { if (OnPropertyChanging(nameof(Amount), value)) { _amount = value; OnPropertyChanged(nameof(Amount)); } } }

    private Byte[] _payload;
    [DataObjectField(false, false, true, 0)]
    [BindColumn("Payload", "", "")]
    public Byte[] Payload { get => _payload; set { if (OnPropertyChanging(nameof(Payload), value)) { _payload = value; OnPropertyChanged(nameof(Payload)); } } }

    private Boolean _enabled;
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enabled", "", "")]
    public Boolean Enabled { get => _enabled; set { if (OnPropertyChanging(nameof(Enabled), value)) { _enabled = value; OnPropertyChanged(nameof(Enabled)); } } }

    private DateTime _created;
    [DataObjectField(false, false, true, 0)]
    [BindColumn("Created", "", "")]
    public DateTime Created { get => _created; set { if (OnPropertyChanging(nameof(Created), value)) { _created = value; OnPropertyChanged(nameof(Created)); } } }

    private String _remark;
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Remark", "", "")]
    public String Remark { get => _remark; set { if (OnPropertyChanging(nameof(Remark), value)) { _remark = value; OnPropertyChanged(nameof(Remark)); } } }

    public override Object this[String name]
    {
        get => name switch
        {
            nameof(Id) => _id,
            nameof(Name) => _name,
            nameof(Amount) => _amount,
            nameof(Payload) => _payload,
            nameof(Enabled) => _enabled,
            nameof(Created) => _created,
            nameof(Remark) => _remark,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case nameof(Id): _id = Convert.ToInt64(value); break;
                case nameof(Name): _name = value as String; break;
                case nameof(Amount): _amount = Convert.ToDecimal(value); break;
                case nameof(Payload): _payload = value as Byte[]; break;
                case nameof(Enabled): _enabled = Convert.ToBoolean(value); break;
                case nameof(Created): _created = value == null || value == DBNull.Value ? default : Convert.ToDateTime(value); break;
                case nameof(Remark): _remark = value as String; break;
                default: base[name] = value; break;
            }
        }
    }
}
