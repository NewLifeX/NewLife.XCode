using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.Data;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>Presto 数组类型解析与值转换测试</summary>
public class PrestoArrayTests
{
    [Theory]
    [DisplayName("解析一层数组元素类型")]
    [InlineData("array(varchar)", "varchar")]
    [InlineData("array(varchar(50))", "varchar(50)")]
    [InlineData("array(decimal(10,2))", "decimal(10,2)")]
    [InlineData("ARRAY(integer)", "integer")]
    [InlineData("array(array(integer))", null)]
    [InlineData("array(map(varchar, integer))", null)]
    [InlineData("map(varchar, integer)", null)]
    [InlineData("varchar", null)]
    public void TryParseArray(String rawType, String? elementType)
    {
        var result = PrestoArray.TryParseArray(rawType, out var element);

        Assert.Equal(elementType != null, result);
        Assert.Equal(elementType, result ? element : null);
    }

    [Theory]
    [DisplayName("数组元素映射到 CLR 类型")]
    [InlineData("array(varchar)", typeof(String))]
    [InlineData("array(char(10))", typeof(String))]
    [InlineData("array(smallint)", typeof(Int16))]
    [InlineData("array(integer)", typeof(Int32))]
    [InlineData("array(bigint)", typeof(Int64))]
    [InlineData("array(double)", typeof(Double))]
    [InlineData("array(decimal(10,2))", typeof(Decimal))]
    [InlineData("array(boolean)", typeof(Boolean))]
    public void TryGetElementType(String rawType, Type elementType)
    {
        Assert.True(PrestoArray.TryGetElementType(rawType, out var type));
        Assert.Equal(elementType, type);
    }

    [Theory]
    [DisplayName("不支持的数组元素类型")]
    [InlineData("array(timestamp)")]
    [InlineData("array(real)")]
    [InlineData("array(varbinary)")]
    [InlineData("array(array(integer))")]
    [InlineData("array(map(varchar, integer))")]
    public void UnsupportedElementType(String rawType)
    {
        Assert.False(PrestoArray.TryGetElementType(rawType, out _));
    }

    [Fact]
    [DisplayName("Object 数组转换为强类型数组")]
    public void ConvertObjectArray()
    {
        Assert.True(PrestoArray.TryConvert(new Object[] { "a", "b" }, typeof(String), out var str));
        Assert.Equal(new String[] { "a", "b" }, str);

        Assert.True(PrestoArray.TryConvert(new Object[] { 1, 2L, "3" }, typeof(Int64), out var nums));
        Assert.Equal(new Int64[] { 1, 2, 3 }, nums);
    }

    [Fact]
    [DisplayName("JSON 文本转换为强类型数组")]
    public void ConvertJsonText()
    {
        Assert.True(PrestoArray.TryConvert("[\"a\",\"b\"]", typeof(String), out var str));
        Assert.Equal(new String[] { "a", "b" }, str);

        Assert.True(PrestoArray.TryConvert("[1,2,3]", typeof(Int32), out var nums));
        Assert.Equal(new Int32[] { 1, 2, 3 }, nums);
    }

    [Fact]
    [DisplayName("结构化复杂类型还原为 JSON 文本")]
    public void ToJson()
    {
        Assert.Equal("[\"a\",\"b\"]", PrestoArray.ToJson(new Object[] { "a", "b" }));
        Assert.Equal("{\"a\":1}", PrestoArray.ToJson(new System.Collections.Generic.Dictionary<String, Object?> { ["a"] = 1 }));
        Assert.Equal("text", PrestoArray.ToJson("text"));
    }

    [Fact]
    [DisplayName("按列原始类型归一化数组与复杂类型")]
    public void NormalizeColumns()
    {
        var table = new DbTable
        {
            Columns = ["Tags", "Detail", "Name"],
            Types = [typeof(Object), typeof(Object), typeof(String)],
            Rows =
            [
                new Object?[]
                {
                    new Object[] { "a", "b" },
                    new Dictionary<String, Object?> { ["x"] = 1 },
                    "n",
                }
            ],
        };

        PrestoSession.NormalizeArrays(table, ["array(varchar)", "map(varchar, integer)", "varchar"]);

        Assert.Equal(typeof(String[]), table.Types[0]);
        Assert.Equal(new String[] { "a", "b" }, table.Rows[0][0]);
        Assert.Equal(typeof(String), table.Types[1]);
        Assert.Equal("{\"x\":1}", table.Rows[0][1]);
        Assert.Equal(typeof(String), table.Types[2]);
        Assert.Equal("n", table.Rows[0][2]);
    }
}
