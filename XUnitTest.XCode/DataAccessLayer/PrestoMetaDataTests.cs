using System;
using System.ComponentModel;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>Presto 元数据解析测试。information_schema 不提供长度与精度列，需要从 data_type 解析</summary>
public class PrestoMetaDataTests
{
    private static IDataColumn CreateColumn(String rawType)
    {
        var table = DAL.CreateTable();
        var column = table.CreateColumn();
        column.RawType = rawType;

        return column;
    }

    [Theory]
    [DisplayName("解析字符串类型长度")]
    [InlineData("varchar", -1)]
    [InlineData("varchar(50)", 50)]
    [InlineData("char", 1)]
    [InlineData("char(10)", 10)]
    [InlineData("array(integer)", -1)]
    [InlineData("map(varchar, integer)", -1)]
    [InlineData("row(a integer)", -1)]
    [InlineData("json", -1)]
    public void FixLength(String rawType, Int32 length)
    {
        var column = CreateColumn(rawType);

        PrestoMetaData.FixColumn(column);

        Assert.Equal(length, column.Length);
    }

    [Theory]
    [DisplayName("解析数值精度与小数位")]
    [InlineData("decimal", 38, 0)]
    [InlineData("decimal(20,2)", 20, 2)]
    [InlineData("numeric(10, 4)", 10, 4)]
    [InlineData("tinyint", 3, 0)]
    [InlineData("smallint", 5, 0)]
    [InlineData("integer", 10, 0)]
    [InlineData("bigint", 19, 0)]
    [InlineData("real", 12, 2)]
    [InlineData("double", 22, 4)]
    public void FixPrecision(String rawType, Int32 precision, Int32 scale)
    {
        var column = CreateColumn(rawType);

        PrestoMetaData.FixColumn(column);

        Assert.Equal(precision, column.Precision);
        Assert.Equal(scale, column.Scale);
    }

    [Theory]
    [DisplayName("映射数据类型")]
    [InlineData("timestamp", typeof(DateTime))]
    [InlineData("timestamp(3)", typeof(DateTime))]
    [InlineData("timestamp with time zone", typeof(DateTime))]
    [InlineData("date", typeof(DateTime))]
    [InlineData("time", typeof(TimeSpan))]
    [InlineData("decimal(20,2)", typeof(Decimal))]
    [InlineData("varchar(50)", typeof(String))]
    [InlineData("char(10)", typeof(String))]
    [InlineData("array(integer)", typeof(String))]
    [InlineData("map(varchar, integer)", typeof(String))]
    [InlineData("row(a integer)", typeof(String))]
    [InlineData("json", typeof(String))]
    public void MapDataType(String rawType, Type type)
    {
        var column = CreateColumn(rawType);

        Assert.Equal(type, new TestMetaData().Map(column));
    }

    private sealed class TestMetaData : PrestoMetaData
    {
        public Type? Map(IDataColumn column) => GetDataType(column);
    }
}
