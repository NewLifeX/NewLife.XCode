using System;
using XCode;
using XCode.Shards;
using Xunit;
using XUnitTest.XCode.TestEntity;

namespace XUnitTest.XCode.EntityTests;

/// <summary>分表查询条件不足时的严格模式校验</summary>
[Collection("Database")]
public class ShardPolicyStrictTests
{
    [Fact(DisplayName = "时间分表查询缺少时间字段条件时抛异常")]
    public void Shards_WithoutTimeField_Throws()
    {
        var shard = new TimeShardPolicy("RegisterTime", User2.Meta.Factory)
        {
            TablePolicy = "{0}_{1:yyyyMM}",
        };

        // 推不出时间分片时不能静默只查基准表，否则其它月份分表的数据全部漏掉
        Assert.Throws<XCodeException>(() => shard.Shards(User2._.Name == "stone"));
    }

    [Fact(DisplayName = "时间下界晚于当前时间时仍能算出差分片")]
    public void Shards_FutureLowerBound_KeepsShard()
    {
        var shard = new TimeShardPolicy("RegisterTime", User2.Meta.Factory)
        {
            TablePolicy = "{0}_{1:yyyyMM}",
        };

        // 预约、计划类数据的时间在将来，此时“结束时间=当前时间”会早于下界，
        // 上下界倒挂不能把下界所在分片丢掉
        var start = new DateTime(2030, 6, 10);
        var models = shard.Shards(User2._.RegisterTime >= start);

        var model = Assert.Single(models);
        Assert.EndsWith("_203006", model.TableName);
    }
}
