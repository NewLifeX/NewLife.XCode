using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

[Collection("Database")]
public class DbCommandFilterTests : IDisposable
{
    private readonly String _connName;
    private readonly String _fileName;

    public DbCommandFilterTests()
    {
        _connName = "_unit_test_db_filter_" + Guid.NewGuid().ToString("N");
        _fileName = Path.Combine(Path.GetTempPath(), _connName + ".db");
        DAL.AddConnStr(_connName, $"Data Source={_fileName}", null, "SQLite");
        DbCommandFilters.Filters = [];
    }

    public void Dispose()
    {
        DbCommandFilters.Filters = [];

        try { if (File.Exists(_fileName)) File.Delete(_fileName); } catch { }
    }

    [Fact(DisplayName = "命令过滤器可以改写SQL")]
    public void Filter_CanRewriteSql()
    {
        var filter = new DelegateDbCommandFilter
        {
            Before = context => context.Sql = "Select 2",
        };
        DbCommandFilters.Filters = [filter];

        var session = DAL.Create(_connName).Session;

        Assert.Equal(2, session.ExecuteScalar<Int32>("Select 1"));
    }

    [Fact(DisplayName = "命令过滤器按Order正序执行、逆序后置")]
    public void Filter_Order_And_ReverseAfter()
    {
        var log = new List<String>();
        DbCommandFilters.Filters =
        [
            new DelegateDbCommandFilter(0)
            {
                Before = _ => log.Add("f1-before"),
                After = _ => log.Add("f1-after"),
            },
            new DelegateDbCommandFilter(1)
            {
                Before = _ => log.Add("f2-before"),
                After = _ => log.Add("f2-after"),
            },
        ];

        var session = DAL.Create(_connName).Session;
        session.ExecuteScalar<Int32>("Select 1");

        Assert.Equal(["f1-before", "f2-before", "f2-after", "f1-after"], log);
    }

    [Fact(DisplayName = "命令过滤器取消后不执行真实SQL")]
    public void Filter_Cancel_SkipsCommand()
    {
        DbCommandFilters.Filters =
        [
            new DelegateDbCommandFilter
            {
                Before = context =>
                {
                    context.Cancel = true;
                    context.Handled = true;
                    context.Result = 42;
                },
            },
        ];

        var session = DAL.Create(_connName).Session;

        Assert.Equal(42, session.ExecuteScalar<Int32>("Select 1"));
    }

    [Fact(DisplayName = "SuppressAll范围内命令过滤器不执行")]
    public void Filter_SuppressAll()
    {
        var called = false;
        DbCommandFilters.Filters =
        [
            new DelegateDbCommandFilter
            {
                Before = _ => called = true,
            },
        ];

        var session = DAL.Create(_connName).Session;
        Int32 value;
        using (DbCommandFilterScope.SuppressAll())
        {
            value = session.ExecuteScalar<Int32>("Select 3");
        }

        Assert.Equal(3, value);
        Assert.False(called);
    }

    [Fact(DisplayName = "命令过滤器可以处理异常并返回结果")]
    public void Filter_ExceptionHandled()
    {
        DbCommandFilters.Filters =
        [
            new DelegateDbCommandFilter
            {
                Before = _ => throw new InvalidOperationException("boom"),
                After = context =>
                {
                    Assert.NotNull(context.Exception);
                    context.ExceptionHandled = true;
                    context.Result = 7;
                },
            },
        ];

        var session = DAL.Create(_connName).Session;

        Assert.Equal(7, session.ExecuteScalar<Int32>("Select 1"));
    }

    [Fact(DisplayName = "命令过滤器可以读取执行耗时")]
    public void Filter_Duration()
    {
        var duration = TimeSpan.Zero;
        DbCommandFilters.Filters =
        [
            new DelegateDbCommandFilter
            {
                After = context => duration = context.Duration,
            },
        ];

        var session = DAL.Create(_connName).Session;

        Assert.Equal(1, session.ExecuteScalar<Int32>("Select 1"));
        Assert.True(duration > TimeSpan.Zero);
    }

    [Fact(DisplayName = "异步命令同样经过过滤器")]
    public async Task Filter_Async()
    {
        DbCommandFilters.Filters =
        [
            new DelegateDbCommandFilter
            {
                Before = context => context.Sql = "Select 4",
            },
        ];

        var session = (DbSession)DAL.Create(_connName).Session;

        Assert.Equal(4, await session.ExecuteScalarAsync<Int32>("Select 1"));
    }

    private class DelegateDbCommandFilter : DbCommandFilter
    {
        public DelegateDbCommandFilter(Int32 order = 0) => Order = order;

        public Action<DbCommandContext>? Before { get; set; }
        public Action<DbCommandContext>? After { get; set; }

        public override void OnExecuting(DbCommandContext context) => Before?.Invoke(context);
        public override void OnExecuted(DbCommandContext context) => After?.Invoke(context);
    }
}
