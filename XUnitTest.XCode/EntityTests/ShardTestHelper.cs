using System;
using XCode.DataAccessLayer;
using XCode.Shards;

namespace XUnitTest.XCode.EntityTests;

/// <summary>分表测试辅助。清理历史分表数据，避免多次运行后表格撑满分页查询。</summary>
internal static class ShardTestHelper
{
    /// <summary>清空指定时间对应的分表数据</summary>
    /// <param name="shard">分表策略</param>
    /// <param name="time">时间</param>
    public static void ClearShard(TimeShardPolicy shard, DateTime time)
    {
        if (shard.Shard(time) is not { } model) return;

        var dal = DAL.Create(model.ConnName);
        if (dal.TableExists(model.TableName)) dal.Execute($"Delete From {model.TableName}");
    }
}
