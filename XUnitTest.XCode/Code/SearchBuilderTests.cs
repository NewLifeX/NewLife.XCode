using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using XCode.Code;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.Code;

public class SearchBuilderTests
{
    private readonly IList<IDataTable> _tables;

    public SearchBuilderTests()
    {
        var option = new BuilderOption();
        _tables = ClassBuilder.LoadModels(@"..\..\XUnitTest.XCode\Code\Member.xml", option, out _);
    }

    [Fact]
    public void ReservedKeywordParameterEscapesName()
    {
        var table = _tables.First(e => e.Name == "Parameter");
        var builder = new SearchBuilder(table) { Nullable = true };

        var columns = builder.GetColumns();
        Assert.Contains(columns, c => c.Name == "Readonly");

        var parameters = builder.GetParameters(columns, true);
        var keywordParam = parameters.FirstOrDefault(p => p.Description == "只读标记。用于验证关键字处理");

        Assert.NotNull(keywordParam);
        Assert.Equal("@readonly", keywordParam!.ParameterName);
    }

    [Fact]
    [DisplayName("不可搜索类型被过滤，可搜索类型保留")]
    public void UnsupportedColumnTypesExcluded()
    {
        var table = DAL.CreateTable();
        table.Name = "Order";
        table.TableName = "Order";
        table.Description = "订单";

        var name = table.CreateColumn();
        name.Name = "Name";
        name.ColumnName = "Name";
        name.DataType = typeof(String);
        table.Columns.Add(name);

        var amount = table.CreateColumn();
        amount.Name = "Amount";
        amount.ColumnName = "Amount";
        amount.DataType = typeof(Double);
        table.Columns.Add(amount);

        var remark = table.CreateColumn();
        remark.Name = "Remark";
        remark.ColumnName = "Remark";
        remark.DataType = typeof(Byte[]);
        table.Columns.Add(remark);

        var guid = table.CreateColumn();
        guid.Name = "Code2";
        guid.ColumnName = "Code2";
        guid.DataType = typeof(Guid);
        table.Columns.Add(guid);

        var di = table.CreateIndex();
        di.Columns = ["Name", "Amount", "Remark", "Code2"];
        di.Fix();
        table.Indexes.Add(di);

        Assert.True(SearchBuilder.IsSearchable(name));
        Assert.True(SearchBuilder.IsSearchable(amount));
        Assert.False(SearchBuilder.IsSearchable(remark));
        Assert.False(SearchBuilder.IsSearchable(guid));

        var builder = new SearchBuilder(table);
        var columns = builder.GetColumns();
        Assert.Contains(columns, c => c.Name == "Name");
        Assert.Contains(columns, c => c.Name == "Amount");
        Assert.DoesNotContain(columns, c => c.Name == "Remark");
        Assert.DoesNotContain(columns, c => c.Name == "Code2");
    }
}
