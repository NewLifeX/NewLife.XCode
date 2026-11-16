using System;
using System.Data;
using XCode.DataAccessLayer;
using XCode.Exceptions;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

[Collection("Database")]
public class SqlExceptionInnerTests
{
    [Fact(DisplayName = "SQL 异常保留内层异常，供重试与错误分类判断")]
    public void Execute_SqlError_KeepsInnerException()
    {
        var dal = DAL.Create("test");
        var ss = dal.Session;

        var ex = Assert.Throws<XSqlException>(() => ss.Execute("Select * From NoSuchTable_XCode_12345", CommandType.Text));

        Assert.NotNull(ex.InnerException);
        Assert.False(String.IsNullOrEmpty(ex.InnerException!.Message));
    }
}
