using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NewLife.Data;
using NewLife.Log;
using XCode.DataAccessLayer;
using XCode.Services;
using Xunit;

namespace XUnitTest.XCode.Services;

/// <summary>DbServer集成测试。服务端启动SQLite，客户端通过Network虚拟数据库进行远程操作</summary>
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class DbServerIntegrationTests : IAsyncLifetime
{
    private DbServer? _server;
    private Int32 _port;
    private String? _serverUrl;
    private String? _dbFilePath;
    private const String TestToken = "integration_test_token";
    private readonly String _testDb = $"IntegrationTest_{Guid.NewGuid():N}";

    /// <summary>异步初始化：启动DbServer</summary>
    public async Task InitializeAsync()
    {
        // 使用随机可用端口
        _port = GetAvailablePort();
        _serverUrl = $"http://127.0.0.1:{_port}";

        // 使用临时文件作为SQLite数据库，避免内存数据库在连接关闭后丢失
        _dbFilePath = Path.Combine(Path.GetTempPath(), $"{_testDb}.db");
        DAL.AddConnStr(_testDb, $"Data Source={_dbFilePath}", null, "SQLite");

        // 创建DbServer并配置令牌
        _server = new DbServer()
        {
            Port = _port,
            Log = XTrace.Log,
        };
        _server.Service.Tokens[TestToken] = [_testDb];

        // 异步启动服务器，给予足够时间启动
        var startTask = Task.Run(() => _server.Start());
        await Task.Delay(1000);

        // 准备测试数据
        await PrepareTestTable();
    }

    /// <summary>异步清理：停止DbServer</summary>
    public async Task DisposeAsync()
    {
        if (_server != null)
        {
            _server.Stop("Integration test completed");
            _server.Dispose();
            await Task.Delay(500);
        }

        // 清理临时数据库文件
        try { if (_dbFilePath != null && File.Exists(_dbFilePath)) File.Delete(_dbFilePath); } catch { }
    }

    [Fact(DisplayName = "服务器启动成功")]
    public void Server_StartsSuccessfully()
    {
        Assert.NotNull(_server);
        Assert.True(_server.Active);
        Assert.Equal(_port, _server.Port);
    }

    [Fact(DisplayName = "客户端连接并登录")]
    public async Task Client_ConnectsAndLogins()
    {
        using var client = new DbClient(_serverUrl, _testDb, TestToken);
        client.Open();

        var info = await client.LoginAsync();

        Assert.NotNull(info);
        Assert.Equal(DatabaseType.SQLite, info.DbType);
    }

    [Fact(DisplayName = "查询操作")]
    public async Task Query_ReturnsData()
    {
        using var client = new DbClient(_serverUrl, _testDb, TestToken);
        client.Open();
        await client.LoginAsync();

        // 查询所有数据
        var result = await client.QueryAsync("SELECT * FROM TestTable", null);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Rows);
        Assert.Equal(3, result.Rows.Count);
    }

    [Fact(DisplayName = "执行更新操作")]
    public async Task Execute_UpdateData()
    {
        using var client = new DbClient(_serverUrl, _testDb, TestToken);
        client.Open();
        await client.LoginAsync();

        // 更新数据
        var affectedRows = await client.ExecuteAsync(
            "UPDATE TestTable SET Value = Value + 10 WHERE Id = 1",
            null
        );

        Assert.Equal(1, affectedRows);

        // 验证更新结果
        var result = await client.QueryAsync(
            "SELECT Value FROM TestTable WHERE Id = 1",
            null
        );

        Assert.NotNull(result);
        Assert.Single(result.Rows);
        Assert.Equal(110L, Convert.ToInt64(result.Rows[0][0]));
    }

    [Fact(DisplayName = "插入并返回自增ID")]
    public async Task InsertAndGetIdentity_ReturnsNewId()
    {
        using var client = new DbClient(_serverUrl, _testDb, TestToken);
        client.Open();
        await client.LoginAsync();

        // 插入新记录并获取ID
        var newId = await client.InsertAndGetIdentityAsync(
            "INSERT INTO TestTable (Name, Value) VALUES (@name, @value)",
            new Dictionary<String, Object?> { { "@name", "NewRecord" }, { "@value", 999 } }
        );

        Assert.True(newId > 0);

        // 验证插入的记录
        var result = await client.QueryAsync(
            "SELECT Name, Value FROM TestTable WHERE Id = @id",
            new Dictionary<String, Object?> { { "@id", newId } }
        );

        Assert.NotNull(result);
        Assert.Single(result.Rows);
        Assert.Equal("NewRecord", result.Rows[0][0]?.ToString());
        Assert.Equal(999L, Convert.ToInt64(result.Rows[0][1]));
    }

    [Fact(DisplayName = "快速查询行数")]
    public async Task QueryCountFast_ReturnsRowCount()
    {
        using var client = new DbClient(_serverUrl, _testDb, TestToken);
        client.Open();
        await client.LoginAsync();

        var count = await client.QueryCountAsync("TestTable");

        // 至少包含初始3条数据和插入的新记录
        Assert.True(count >= 3);
    }

    [Fact(DisplayName = "通过NetworkDb远程操作")]
    public async Task NetworkDb_RemoteOperations()
    {
        // 配置Network虚拟数据库连接到服务器
        var remoteDb = $"RemoteDb_{Guid.NewGuid():N}";
        DAL.AddConnStr(remoteDb, 
            $"Server={_serverUrl};Database={_testDb};Password={TestToken};Provider=Network",
            null, 
            "Network"
        );

        var dal = DAL.Create(remoteDb);
        Assert.NotNull(dal);
        Assert.Equal(DatabaseType.Network, dal.DbType);

        // 执行查询
        var result = dal.Query("SELECT * FROM TestTable", null);
        Assert.NotNull(result);
        Assert.True(result.Rows.Count >= 3);

        // 执行更新
        var affectedRows = dal.Execute(
            "UPDATE TestTable SET Value = 555 WHERE Id = 2",
            CommandType.Text
        );
        Assert.Equal(1, affectedRows);

        // 验证更新
        result = dal.Query("SELECT Value FROM TestTable WHERE Id = 2", null);
        Assert.Single(result.Rows);
        Assert.Equal(555L, Convert.ToInt64(result.Rows[0][0]));
    }

    [Fact(DisplayName = "参数化查询")]
    public async Task ParameterizedQuery_WorksCorrectly()
    {
        await ResetTestData();

        using var client = new DbClient(_serverUrl, _testDb, TestToken);
        client.Open();
        await client.LoginAsync();

        var parameters = new Dictionary<String, Object?>
        {
            { "@value", 100 }
        };

        var result = await client.QueryAsync(
            "SELECT * FROM TestTable WHERE Value = @value",
            parameters
        );

        Assert.NotNull(result);
        Assert.Single(result.Rows);
        Assert.Equal("Alice", result.Rows[0][1]?.ToString());
    }

    [Fact(DisplayName = "无效令牌被拒绝")]
    public async Task InvalidToken_IsRejected()
    {
        using var client = new DbClient(_serverUrl, _testDb, "invalid_token");
        client.Open();

        // 服务端将 UnauthorizedAccessException 转为 ApiException，客户端收到 HttpRequestException
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.LoginAsync()
        );
    }

    [Fact(DisplayName = "多个并发请求")]
    public async Task ConcurrentRequests_AllSucceed()
    {
        using var client = new DbClient(_serverUrl, _testDb, TestToken);
        client.Open();
        await client.LoginAsync();

        // 并发执行多个查询（仅查询存在的3条初始记录）
        var tasks = Enumerable.Range(1, 3)
            .Select(i => client.QueryAsync($"SELECT * FROM TestTable WHERE Id = {i}", null))
            .ToList();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(3, results.Length);
        Assert.All(results, r => Assert.NotNull(r));
    }

    #region 辅助方法

    /// <summary>获取可用的端口</summary>
    private static Int32 GetAvailablePort()
    {
        var listener = new System.Net.Sockets.TcpListener(
            System.Net.IPAddress.Loopback,
            0
        );
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>初始化测试表</summary>
    private async Task PrepareTestTable()
    {
        var dal = DAL.Create(_testDb);

        // 创建测试表
        dal.Execute(@"
            CREATE TABLE IF NOT EXISTS TestTable (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Value INTEGER NOT NULL
            )", CommandType.Text);

        // 清空表
        dal.Execute("DELETE FROM TestTable", CommandType.Text);

        // 插入测试数据
        dal.Execute("INSERT INTO TestTable (Name, Value) VALUES ('Alice', 100)", CommandType.Text);
        dal.Execute("INSERT INTO TestTable (Name, Value) VALUES ('Bob', 200)", CommandType.Text);
        dal.Execute("INSERT INTO TestTable (Name, Value) VALUES ('Charlie', 300)", CommandType.Text);

        await Task.CompletedTask;
    }

    /// <summary>重置测试数据为初始状态</summary>
    private async Task ResetTestData()
    {
        var dal = DAL.Create(_testDb);

        // 清空表
        dal.Execute("DELETE FROM TestTable", CommandType.Text);

        // 重新插入初始测试数据
        dal.Execute("INSERT INTO TestTable (Name, Value) VALUES ('Alice', 100)", CommandType.Text);
        dal.Execute("INSERT INTO TestTable (Name, Value) VALUES ('Bob', 200)", CommandType.Text);
        dal.Execute("INSERT INTO TestTable (Name, Value) VALUES ('Charlie', 300)", CommandType.Text);

        await Task.CompletedTask;
    }

    #endregion
}
