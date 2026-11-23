using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using NewLife.Data;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>PostgreSQL/VastBase 数组读写与 DDL 测试</summary>
public class PostgreSqlArrayTests
{
    private static IDataTable CreateTable(DatabaseType dbType)
    {
        var table = DAL.CreateTable();
        table.Name = "ArrayModel";
        table.TableName = "ArrayModel";
        table.DbType = dbType;

        var id = table.CreateColumn();
        id.Name = "Id";
        id.ColumnName = "Id";
        id.DataType = typeof(Int32);
        id.PrimaryKey = true;
        id.Nullable = false;
        table.Columns.Add(id);

        return table;
    }

    private static IDataColumn CreateArrayColumn(IDataTable table, Type type, String rawType, Int32 length = 0)
    {
        var column = table.CreateColumn();
        column.Name = "Tags";
        column.ColumnName = "Tags";
        column.DataType = type;
        column.RawType = rawType;
        column.Length = length;
        column.Nullable = true;
        table.Columns.Add(column);

        return column;
    }

    [Theory]
    [DisplayName("PostgreSQL/VastBase 数组写入字面量")]
    [InlineData(DatabaseType.PostgreSQL)]
    [InlineData(DatabaseType.VastBase)]
    public void FormatArrayValue(DatabaseType dbType)
    {
        var db = DbFactory.Create(dbType);
        Assert.NotNull(db);

        var table = CreateTable(dbType);
        var tags = CreateArrayColumn(table, typeof(String[]), "varchar[]");

        Assert.Equal("ARRAY['a','b']::varchar[]", db.FormatValue(tags, new[] { "a", "b" }));
        Assert.Equal("ARRAY[]::varchar[]", db.FormatValue(tags, Array.Empty<String>()));

        tags.Nullable = false;
        Assert.Equal("ARRAY[]::varchar[]", db.FormatValue(tags, null));
        tags.Nullable = true;
        Assert.Equal("NULL", db.FormatValue(tags, null));

        var nums = CreateArrayColumn(table, typeof(Int64[]), "bigint[]");
        Assert.Equal("ARRAY[1,2]::bigint[]", db.FormatValue(nums, new Int64[] { 1, 2 }));
        Assert.Equal("ARRAY[]::bigint[]", db.FormatValue(nums, Array.Empty<Int64>()));

        var flags = CreateArrayColumn(table, typeof(Boolean[]), "boolean[]");
        Assert.Equal("ARRAY[true,false]::boolean[]", db.FormatValue(flags, new Boolean[] { true, false }));

        // 原始类型带长度/精度时，写入字面量保留精度，避免被数据库推断成无精度类型
        tags.RawType = "varchar(50)[]";
        tags.Length = 50;
        Assert.Equal("ARRAY['a']::varchar(50)[]", db.FormatValue(tags, new[] { "a" }));
        Assert.Equal("ARRAY[]::varchar(50)[]", db.FormatValue(tags, Array.Empty<String>()));

        var amounts = CreateArrayColumn(table, typeof(Decimal[]), "numeric(10,2)[]");
        Assert.Equal("ARRAY[1.25]::numeric(10,2)[]", db.FormatValue(amounts, new[] { 1.25m }));

        Assert.Throws<NotSupportedException>(() => db.FormatValue(tags, "a"));
    }

    [Theory]
    [DisplayName("PostgreSQL/VastBase 数组建表/加列/改列 DDL")]
    [InlineData(DatabaseType.PostgreSQL)]
    [InlineData(DatabaseType.VastBase)]
    public void ArrayDdl(DatabaseType dbType)
    {
        var db = DbFactory.Create(dbType);
        Assert.NotNull(db);
        using var meta = db.CreateMetaData();

        var table = CreateTable(dbType);
        var tags = CreateArrayColumn(table, typeof(String[]), null, 50);

        var create = meta.GetSchemaSQL(DDLSchema.CreateTable, table);
        Assert.Contains("varchar(50)[]", create);

        var add = meta.GetSchemaSQL(DDLSchema.AddColumn, tags);
        Assert.Contains("varchar(50)[]", add);

        // 反向工程回来的原始类型优先用于改列
        tags.RawType = "varchar(50)[]";
        var old = tags.Clone(table);
        old.DataType = typeof(String);
        old.RawType = "varchar(20)[]";
        var alter = meta.GetSchemaSQL(DDLSchema.AlterColumn, tags, old);
        Assert.Contains("varchar(50)[]", alter);
    }

    [Theory]
    [DisplayName("information_schema 的 ARRAY/udt_name 还原为数组元素类型")]
    [InlineData("_text", typeof(String[]))]
    [InlineData("_varchar", typeof(String[]))]
    [InlineData("_int2", typeof(Int16[]))]
    [InlineData("_int4", typeof(Int32[]))]
    [InlineData("_int8", typeof(Int64[]))]
    [InlineData("_float8", typeof(Double[]))]
    [InlineData("_numeric", typeof(Decimal[]))]
    [InlineData("_bool", typeof(Boolean[]))]
    [InlineData("_timestamptz", typeof(DateTime[]))]
    [InlineData("_time", typeof(TimeSpan[]))]
    [InlineData("_uuid", typeof(Guid[]))]
    public void PostgreSqlUdtArrayType(String udtName, Type type)
    {
        var table = DAL.CreateTable();
        var field = table.CreateColumn();
        field.RawType = "ARRAY";
        field.Nullable = true;

        var dt = new DataTable();
        dt.Columns.Add("UDT_NAME", typeof(String));
        var row = dt.NewRow();
        row["UDT_NAME"] = udtName;
        dt.Rows.Add(row);

        new TestPostgreSqlMetaData().Fix(field, row);

        Assert.EndsWith("[]", field.RawType);
        Assert.Equal(type, field.DataType);
    }

    [Fact]
    [DisplayName("数组元素保留 numeric 精度与长度")]
    public void PostgreSqlArrayTypeKeepsPrecision()
    {
        var table = DAL.CreateTable();
        var field = table.CreateColumn();
        field.RawType = "ARRAY";
        field.Precision = 10;
        field.Scale = 2;
        field.Length = 50;

        Assert.Equal("numeric(10,2)[]", PostgreSQLMetaData.FormatArrayType("numeric", field));
        Assert.Equal("varchar(50)[]", PostgreSQLMetaData.FormatArrayType("varchar", field));
    }

    [Fact]
    [DisplayName("批量插入数组字段使用方言字面量而不是参数")]
    public void InsertBuilderUsesArrayLiteral()
    {
        var db = DbFactory.Create(DatabaseType.PostgreSQL);
        Assert.NotNull(db);

        var table = CreateTable(DatabaseType.PostgreSQL);
        var tags = CreateArrayColumn(table, typeof(String[]), "varchar[]");

        var data = new DbTable
        {
            Columns = ["Tags"],
            Types = [typeof(String[])],
            Rows = [new Object?[] { new String[] { "a", "b" } }],
        };
        var builder = new InsertBuilder { UseParameter = true };

        var sql = builder.GetSql(db, table, [tags], data.GetRow(0));

        Assert.Contains("ARRAY['a','b']::varchar[]", sql);
        Assert.Null(builder.Parameters);
    }

    [Theory]
    [DisplayName("VastBase 的 ARRAY/udt_name 还原为数组元素类型")]
    [InlineData("_text", typeof(String[]))]
    [InlineData("_int8", typeof(Int64[]))]
    [InlineData("_timestamptz", typeof(DateTime[]))]
    [InlineData("_time", typeof(TimeSpan[]))]
    [InlineData("_uuid", typeof(Guid[]))]
    public void VastBaseUdtArrayType(String udtName, Type type)
    {
        var table = DAL.CreateTable();
        var field = table.CreateColumn();
        field.RawType = "ARRAY";
        field.Nullable = true;

        var dt = new DataTable();
        dt.Columns.Add("UDT_NAME", typeof(String));
        var row = dt.NewRow();
        row["UDT_NAME"] = udtName;
        dt.Rows.Add(row);

        new TestVastBaseMetaData().Fix(field, row);

        Assert.EndsWith("[]", field.RawType);
        Assert.Equal(type, field.DataType);
    }

    private sealed class TestPostgreSqlMetaData : PostgreSQLMetaData
    {
        public void Fix(IDataColumn field, DataRow row) => FixField(field, row);
    }

    private sealed class TestVastBaseMetaData : VastBaseMetaData
    {
        public void Fix(IDataColumn field, DataRow row) => FixField(field, row);
    }
}
