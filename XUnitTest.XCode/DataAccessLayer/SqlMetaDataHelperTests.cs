using System;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>SQL 元数据辅助。各数据库方言的 tables/columns 查询共用</summary>
public class SqlMetaDataHelperTests
{
    [Fact]
    public void EscapeNamesQuotesAndEscapes()
    {
        Assert.Equal("", SqlMetaDataHelper.EscapeNames(null));
        Assert.Equal("", SqlMetaDataHelper.EscapeNames([]));
        Assert.Equal("'a','b''c'", SqlMetaDataHelper.EscapeNames(["a", "b'c", ""]));
    }

    [Fact]
    public void BuildWhereCombinesSchemaAndNames()
    {
        Assert.Equal("", SqlMetaDataHelper.BuildWhere(null, null));
        Assert.Equal(" where table_schema = 'public'", SqlMetaDataHelper.BuildWhere("public", null));
        Assert.Equal(" where table_schema = 'o''brien' and table_name in ('a','b')", SqlMetaDataHelper.BuildWhere("o'brien", ["a", "b"]));
    }

    [Fact]
    public void GetStringFieldTypeSwitchesAtLongTextLength()
    {
        var table = DAL.CreateTable();
        var field = table.CreateColumn();
        field.DataType = typeof(String);

        field.Length = 0;
        Assert.Equal("CLOB", SqlMetaDataHelper.GetStringFieldType(field, 4000, "CLOB"));

        field.Length = 4000;
        Assert.Equal("CLOB", SqlMetaDataHelper.GetStringFieldType(field, 4000, "CLOB"));

        field.Length = 50;
        Assert.Equal("VARCHAR(50)", SqlMetaDataHelper.GetStringFieldType(field, 4000, "CLOB"));
        Assert.Equal("VARCHAR2(50)", SqlMetaDataHelper.GetStringFieldType(field, 4000, "CLOB", "VARCHAR2"));
    }
}
