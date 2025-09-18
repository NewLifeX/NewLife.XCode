using System;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Security;
using NewLife.UnitTest;
using XCode;
using XCode.DataAccessLayer;
using XCode.Shards;
using Xunit;
using XUnitTest.XCode.TestEntity;

namespace XUnitTest.XCode.EntityTests;

/// <summary>测试SaveAsync分表会话问题</summary>
public class SaveAsyncShardSessionTest
{
    public SaveAsyncShardSessionTest()
    {
        // 设置分表策略，按日期分表
        var policy = new TimeShardPolicy(Log2._.CreateTime, Log2.Meta.Factory)
        {
            TablePolicy = "{0}_{1:yyyyMMdd}",
            Step = TimeSpan.FromDays(1),
        };
        Log2.Meta.ShardPolicy = policy;
    }

    [Fact]
    public void SaveAsync应该使用分表后的Session队列()
    {
        // 拦截SQL语句以验证行为
        var sqls = new List<String>();
        DAL.LocalFilter = s => { if (!s.Contains("sqlite_master")) sqls.Add(s); };

        // 创建测试数据，时间设为昨天以触发分表
        var testTime = DateTime.Today.AddDays(-1);
        var log = new Log2
        {
            ID = testTime.Ticks, // 使用时间戳作为ID避免冲突
            Category = "Test",
            Action = "SaveAsyncTest",
            CreateTime = testTime
        };

        // 清空之前的SQL记录
        sqls.Clear();
        
        // 调用SaveAsync，这应该会：
        // 1. 创建分表会话
        // 2. 使用分表后的Session.Queue而不是默认的
        var result = log.SaveAsync();
        
        // 验证返回结果
        Assert.True(result, "SaveAsync应该成功返回true");
        
        // 等待一小段时间让异步队列处理
        System.Threading.Thread.Sleep(100);
        
        // 验证SQL是否包含正确的分表表名
        var expectedTableName = $"Log2_{testTime:yyyyMMdd}";
        var insertSql = sqls.FirstOrDefault(s => s.Contains("Insert") && s.Contains("Log2_"));
        
        Assert.NotNull(insertSql);
        Assert.Contains(expectedTableName, insertSql);
        
        // 清理
        DAL.LocalFilter = null;
    }

    [Fact] 
    public void SaveAsync在已有分表会话中应该使用当前Session队列()
    {
        var sqls = new List<String>();
        DAL.LocalFilter = s => { if (!s.Contains("sqlite_master")) sqls.Add(s); };

        var testTime = DateTime.Today.AddDays(-2);
        
        // 手动创建分表会话
        using var split = Log2.Meta.CreateShard(testTime);
        
        var log = new Log2
        {
            ID = testTime.Ticks + 1000, // 避免ID冲突
            Category = "Test", 
            Action = "InShardTest",
            CreateTime = testTime
        };

        sqls.Clear();
        
        // 在分表会话中调用SaveAsync
        var result = log.SaveAsync();
        
        Assert.True(result);
        
        // 等待处理
        System.Threading.Thread.Sleep(100);
        
        // 验证使用了正确的分表表名
        var expectedTableName = $"Log2_{testTime:yyyyMMdd}";
        var insertSql = sqls.FirstOrDefault(s => s.Contains("Insert") && s.Contains("Log2_"));
        
        Assert.NotNull(insertSql);
        Assert.Contains(expectedTableName, insertSql);
        
        DAL.LocalFilter = null;
    }
}