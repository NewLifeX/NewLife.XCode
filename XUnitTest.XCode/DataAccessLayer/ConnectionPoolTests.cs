using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Collections;
using NewLife.Log;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>连接池单元测试</summary>
public class ConnectionPoolTests
{
    [Fact(DisplayName = "构造默认值检查")]
    public void DefaultValues()
    {
        using var pool = new ConnectionPool();
        var min = Environment.ProcessorCount;
        if (min < 2) min = 2;
        if (min > 8) min = 8;

        Assert.Equal(min, pool.Min);
        Assert.Equal(1000, pool.Max);
        Assert.Equal(30, pool.IdleTime);
        Assert.Equal("ConnectionPool", pool.Name);
    }

    [Fact(DisplayName = "Execute模式-工厂为null时抛出异常")]
    public void Execute_NullFactory_Throws()
    {
        using var pool = new ConnectionPool
        {
            ConnectionString = "Data Source=:memory:"
        };

        // Factory 为 null，调用 Get() 时 ConnectionPool.OnCreate 检测到 null 后抛出 Exception
        Assert.Throws<Exception>(() => pool.Execute(conn => conn.State));
    }

    [Fact(DisplayName = "借出归还基本流程")]
    public async Task GetReturn_BasicFlow()
    {
        // 使用 SQLite 内存数据库测试借出归还流程
        var db = DbFactory.Create(DatabaseType.SQLite);
        if (db?.Factory == null) return; // 无 SQLite 驱动时跳过

        using var pool = new ConnectionPool
        {
            Factory = db.Factory,
            ConnectionString = "Data Source=:memory:",
            Max = 10,
            Min = 1,
        };

        // 同步借出
        var conn = pool.Get();
        Assert.NotNull(conn);
        Assert.Equal(ConnectionState.Open, conn.State);
        Assert.Equal(1, pool.BusyCount);
        Assert.Equal(0, pool.FreeCount);

        // 归还
        pool.Return(conn);
        Assert.Equal(0, pool.BusyCount);
        Assert.Equal(1, pool.FreeCount);

        // 再次借出（应复用）
        var conn2 = pool.Get();
        Assert.NotNull(conn2);
        Assert.Equal(1, pool.BusyCount);
        Assert.Equal(0, pool.FreeCount);
        pool.Return(conn2);
    }

    [Fact(DisplayName = "异步借出归还")]
    public async Task GetReturnAsync_BasicFlow()
    {
        var db = DbFactory.Create(DatabaseType.SQLite);
        if (db?.Factory == null) return;

        using var pool = new ConnectionPool
        {
            Factory = db.Factory,
            ConnectionString = "Data Source=:memory:",
            Max = 10,
            Min = 1,
        };

        // 异步借出
        var conn = await pool.GetAsync();
        Assert.NotNull(conn);
        Assert.Equal(ConnectionState.Open, conn.State);
        Assert.Equal(1, pool.BusyCount);

        // 归还
        pool.Return(conn);
        Assert.Equal(0, pool.BusyCount);
        Assert.Equal(1, pool.FreeCount);
    }

    [Fact(DisplayName = "OnReturn-连接关闭时被销毁")]
    public void OnReturn_ClosedConnection_Destroyed()
    {
        var db = DbFactory.Create(DatabaseType.SQLite);
        if (db?.Factory == null) return;

        using var pool = new ConnectionPool
        {
            Factory = db.Factory,
            ConnectionString = "Data Source=:memory:",
            Max = 10,
            Min = 1,
        };

        // 借出
        var conn = pool.Get();
        Assert.Equal(ConnectionState.Open, conn.State);

        // 关闭连接
        conn.Close();

        // 归还 - OnReturn 返回 false，连接被销毁，不进入空闲池
        pool.Return(conn);
        Assert.Equal(0, pool.FreeCount);
    }

    [Fact(DisplayName = "多连接借出归还")]
    public void MultipleGetReturn()
    {
        var db = DbFactory.Create(DatabaseType.SQLite);
        if (db?.Factory == null) return;

        using var pool = new ConnectionPool
        {
            Factory = db.Factory,
            ConnectionString = "Data Source=:memory:",
            Max = 10,
            Min = 1,
        };

        var connections = new System.Collections.Generic.List<DbConnection>();
        for (var i = 0; i < 5; i++)
        {
            connections.Add(pool.Get());
        }
        Assert.Equal(5, pool.BusyCount);

        foreach (var conn in connections)
        {
            pool.Return(conn);
        }
        Assert.Equal(0, pool.BusyCount);
        Assert.Equal(5, pool.FreeCount);
    }

    [Fact(DisplayName = "超过最大值抛异常")]
    public void ExceedsMax_Throws()
    {
        var db = DbFactory.Create(DatabaseType.SQLite);
        if (db?.Factory == null) return;

        using var pool = new ConnectionPool
        {
            Factory = db.Factory,
            ConnectionString = "Data Source=:memory:",
            Max = 2,
            Min = 0,
            WaitTimeout = TimeSpan.Zero, // 不等待，立即抛异常
        };

        var conn1 = pool.Get();
        Assert.NotNull(conn1);
        var conn2 = pool.Get();
        Assert.NotNull(conn2);

        // 第三个应抛 PoolFullException
        Assert.Throws<PoolFullException>(() => pool.Get());

        // 清理
        pool.Return(conn1);
        pool.Return(conn2);
    }

    [Fact(DisplayName = "Async超过最大值抛异常")]
    public async Task ExceedsMaxAsync_Throws()
    {
        var db = DbFactory.Create(DatabaseType.SQLite);
        if (db?.Factory == null) return;

        using var pool = new ConnectionPool
        {
            Factory = db.Factory,
            ConnectionString = "Data Source=:memory:",
            Max = 2,
            Min = 0,
            WaitTimeout = TimeSpan.Zero,
        };

        var conn1 = await pool.GetAsync();
        Assert.NotNull(conn1);
        var conn2 = await pool.GetAsync();
        Assert.NotNull(conn2);

        // 第三个应抛 PoolFullException
        await Assert.ThrowsAsync<PoolFullException>(() => pool.GetAsync());

        pool.Return(conn1);
        pool.Return(conn2);
    }

    [Fact(DisplayName = "Execute模式-执行成功后归还")]
    public void Execute_Success_ReturnsToPool()
    {
        var db = DbFactory.Create(DatabaseType.SQLite);
        if (db?.Factory == null) return;

        using var pool = new ConnectionPool
        {
            Factory = db.Factory,
            ConnectionString = "Data Source=:memory:",
            Max = 10,
            Min = 1,
        };

        var result = pool.Execute(conn =>
        {
            Assert.Equal(ConnectionState.Open, conn.State);
            Assert.Equal(1, pool.BusyCount);
            return 42;
        });

        Assert.Equal(42, result);
        // 执行完毕后连接应归还到池中
        Assert.Equal(0, pool.BusyCount);
        Assert.Equal(1, pool.FreeCount);
    }

    [Fact(DisplayName = "Execute模式-异常时归还连接")]
    public void Execute_Exception_ReturnsToPool()
    {
        var db = DbFactory.Create(DatabaseType.SQLite);
        if (db?.Factory == null) return;

        using var pool = new ConnectionPool
        {
            Factory = db.Factory,
            ConnectionString = "Data Source=:memory:",
            Max = 10,
            Min = 1,
        };

        Assert.Throws<InvalidOperationException>(() =>
            pool.Execute<int>(conn =>
            {
                throw new InvalidOperationException("测试异常");
            })
        );

        // 异常后连接应归还
        Assert.Equal(0, pool.BusyCount);
        Assert.Equal(1, pool.FreeCount);
    }
}
