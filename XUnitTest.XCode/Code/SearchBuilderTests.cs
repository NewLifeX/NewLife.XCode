using System.Collections.Generic;
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
}
