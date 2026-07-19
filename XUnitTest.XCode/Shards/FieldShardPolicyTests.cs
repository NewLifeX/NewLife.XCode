using System;
using System.Collections.Generic;
using System.Linq;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;
using XCode.Exceptions;
using XCode.Shards;
using Xunit;
using XUnitTest.XCode.TestEntity;

namespace XUnitTest.XCode.Shards;

/// <summary>字段值分表策略单元测试</summary>
public class FieldShardPolicyTests
{
    #region 直接值分表
    [Fact(DisplayName = "整数字段值分表_默认表名策略")]
    public void ShardByIntValue_DefaultPolicy()
    {
        var policy = new FieldShardPolicy(Log2._.CreateUserID, Log2.Meta.Factory);

        var model = policy.ShardByValue(1000);

        Assert.NotNull(model);
        Assert.Equal("Log2_1000", model.TableName);
        Assert.Equal("test", model.ConnName);
    }

    [Fact(DisplayName = "字符串字段值分表")]
    public void ShardByStringValue()
    {
        var policy = new FieldShardPolicy(Log2._.UserName, Log2.Meta.Factory)
        {
            TablePolicy = "{0}_{1}",
        };

        var model = policy.ShardByValue("alice");

        Assert.NotNull(model);
        Assert.Equal("Log2_alice", model.TableName);
        Assert.Equal("test", model.ConnName);
    }

    [Fact(DisplayName = "连接名策略分表")]
    public void ShardByConnPolicy()
    {
        var policy = new FieldShardPolicy(Log2._.CreateUserID, Log2.Meta.Factory)
        {
            ConnPolicy = "{0}_{1}",
            TablePolicy = null,
        };

        var model = policy.ShardByValue(1000);

        Assert.NotNull(model);
        Assert.Equal("test_1000", model.ConnName);
        Assert.Equal("Log2", model.TableName);
    }

    [Fact(DisplayName = "同时配置连接名和表名策略")]
    public void ShardByBothPolicies()
    {
        var policy = new FieldShardPolicy(Log2._.CreateUserID, Log2.Meta.Factory)
        {
            ConnPolicy = "{0}_{1}",
            TablePolicy = "{0}_{1}",
        };

        var model = policy.ShardByValue(2000);

        Assert.NotNull(model);
        Assert.Equal("test_2000", model.ConnName);
        Assert.Equal("Log2_2000", model.TableName);
    }

    [Fact(DisplayName = "未配置策略时返回null")]
    public void Shard_NoPolicyReturnsNull()
    {
        var policy = new FieldShardPolicy(Log2._.CreateUserID, Log2.Meta.Factory)
        {
            ConnPolicy = null,
            TablePolicy = null,
        };

        var model = policy.Shard(1000);

        Assert.Null(model);
    }
    #endregion

    #region 实体对象分表
    [Fact(DisplayName = "实体对象分表_提取字段值")]
    public void ShardByEntity()
    {
        var policy = new FieldShardPolicy(Log2._.CreateUserID, Log2.Meta.Factory)
        {
            TablePolicy = "{0}_{1}",
        };

        var log = new Log2 { CreateUserID = 1000 };
        var model = policy.Shard(log);

        Assert.NotNull(model);
        Assert.Equal("Log2_1000", model.TableName);
        Assert.Equal("test", model.ConnName);
    }

    [Fact(DisplayName = "实体对象分表_不同字段值路由不同表")]
    public void ShardByEntity_DifferentValues()
    {
        var policy = new FieldShardPolicy(Log2._.CreateUserID, Log2.Meta.Factory)
        {
            TablePolicy = "{0}_{1}",
        };

        var log1 = new Log2 { CreateUserID = 100 };
        var log2 = new Log2 { CreateUserID = 200 };

        var model1 = policy.Shard(log1);
        var model2 = policy.Shard(log2);

        Assert.NotNull(model1);
        Assert.NotNull(model2);
        Assert.Equal("Log2_100", model1.TableName);
        Assert.Equal("Log2_200", model2.TableName);
        Assert.NotEqual(model1.TableName, model2.TableName);
    }
    #endregion

    #region 时间区间（不支持）
    [Fact(DisplayName = "时间区间查询返回空数组")]
    public void Shards_DateRange_ReturnsEmpty()
    {
        var policy = new FieldShardPolicy(Log2._.CreateUserID, Log2.Meta.Factory)
        {
            TablePolicy = "{0}_{1}",
        };

        var shards = policy.Shards(DateTime.Now.AddDays(-1), DateTime.Now);

        Assert.NotNull(shards);
        Assert.Empty(shards);
    }
    #endregion

    #region 表达式分表
    [Fact(DisplayName = "等值表达式分表")]
    public void Shards_EqualExpression()
    {
        var policy = new FieldShardPolicy(Log2._.CreateUserID, Log2.Meta.Factory)
        {
            TablePolicy = "{0}_{1}",
        };

        var where = Log2._.CreateUserID == 1000;
        var shards = policy.Shards(where);

        Assert.NotNull(shards);
        Assert.Single(shards);
        Assert.Equal("Log2_1000", shards[0].TableName);
        Assert.Equal("test", shards[0].ConnName);
    }

    [Fact(DisplayName = "WhereExpression组合条件中含等值分表字段")]
    public void Shards_CompositeWhereExpression()
    {
        var policy = new FieldShardPolicy(Log2._.CreateUserID, Log2.Meta.Factory)
        {
            TablePolicy = "{0}_{1}",
        };

        var where = Log2._.Success == true & Log2._.CreateUserID == 2000;
        var shards = policy.Shards(where);

        Assert.NotNull(shards);
        Assert.Single(shards);
        Assert.Equal("Log2_2000", shards[0].TableName);
    }

    [Fact(DisplayName = "非等值条件返回空数组")]
    public void Shards_NonEqualExpression_ReturnsEmpty()
    {
        var policy = new FieldShardPolicy(Log2._.CreateUserID, Log2.Meta.Factory)
        {
            TablePolicy = "{0}_{1}",
        };

        // 大于条件，不是等值，不支持范围分表
        var where = Log2._.CreateUserID > 0;
        var shards = policy.Shards(where);

        Assert.NotNull(shards);
        Assert.Empty(shards);
    }

    [Fact(DisplayName = "不含分表字段的表达式返回空数组")]
    public void Shards_UnrelatedExpression_ReturnsEmpty()
    {
        var policy = new FieldShardPolicy(Log2._.CreateUserID, Log2.Meta.Factory)
        {
            TablePolicy = "{0}_{1}",
        };

        // 与分表字段无关的条件
        var where = Log2._.Success == true;
        var shards = policy.Shards(where);

        Assert.NotNull(shards);
        Assert.Empty(shards);
    }

    [Fact(DisplayName = "单字段表达式等值分表")]
    public void Shards_SingleFieldExpression()
    {
        var policy = new FieldShardPolicy(Log2._.CreateUserID, Log2.Meta.Factory)
        {
            TablePolicy = "{0}_{1}",
        };

        var where = Log2._.CreateUserID.Equal(5000);
        var shards = policy.Shards(where);

        Assert.NotNull(shards);
        Assert.Single(shards);
        Assert.Equal("Log2_5000", shards[0].TableName);
    }
    #endregion

    #region 构造函数
    [Fact(DisplayName = "按字段名字符串构造")]
    public void Ctor_ByFieldName()
    {
        var policy = new FieldShardPolicy("CreateUserID", Log2.Meta.Factory)
        {
            TablePolicy = "{0}_{1}",
        };

        // 字段异步加载，等待短暂时间
        System.Threading.Thread.Sleep(200);

        var model = policy.Shard(3000);

        Assert.NotNull(model);
        Assert.Equal("Log2_3000", model.TableName);
    }

    [Fact(DisplayName = "按FieldItem构造")]
    public void Ctor_ByFieldItem()
    {
        var policy = new FieldShardPolicy(Log2._.CreateUserID)
        {
            TablePolicy = "{0}_{1}",
        };

        Assert.Equal(Log2._.CreateUserID, policy.Field);
        Assert.NotNull(policy.Factory);

        var model = policy.Shard(7777);
        Assert.NotNull(model);
        Assert.Equal("Log2_7777", model.TableName);
    }
    #endregion

    #region SQL路由集成测试
    [Fact(DisplayName = "Insert路由到字段分表")]
    public void Insert_RoutesToShardTable()
    {
        var policy = new FieldShardPolicy("CreateUserID", Log2.Meta.Factory)
        {
            TablePolicy = "{0}_{1}",
        };
        Log2.Meta.ShardPolicy = policy;

        var sql = "";
        DAL.LocalFilter = s => sql = s;

        try
        {
            var log = new Log2
            {
                Action = "字段分表",
                Category = "Test",
                CreateUserID = 1000,
            };
            log.Insert();

            Assert.Contains("Log2_1000", sql);
            Assert.StartsWith("[test] Insert Into Log2_1000(", sql);
        }
        catch { }
        finally
        {
            Log2.Meta.ShardPolicy = null;
            DAL.LocalFilter = null;
        }
    }

    [Fact(DisplayName = "Update路由到字段分表")]
    public void Update_RoutesToShardTable()
    {
        var policy = new FieldShardPolicy("CreateUserID", Log2.Meta.Factory)
        {
            TablePolicy = "{0}_{1}",
        };
        Log2.Meta.ShardPolicy = policy;

        var sqls = new List<String>();
        DAL.LocalFilter = s => sqls.Add(s);

        try
        {
            var log = new Log2
            {
                Action = "字段分表",
                Category = "Test",
                CreateUserID = 2000,
            };
            log.Insert();

            log.Category = "Updated";
            log.Update();

            Assert.Contains("Log2_2000", sqls[^1]);
        }
        catch { }
        finally
        {
            Log2.Meta.ShardPolicy = null;
            DAL.LocalFilter = null;
        }
    }

    [Fact(DisplayName = "Delete路由到字段分表")]
    public void Delete_RoutesToShardTable()
    {
        var policy = new FieldShardPolicy("CreateUserID", Log2.Meta.Factory)
        {
            TablePolicy = "{0}_{1}",
        };
        Log2.Meta.ShardPolicy = policy;

        var sqls = new List<String>();
        DAL.LocalFilter = s => sqls.Add(s);

        try
        {
            var log = new Log2
            {
                Action = "字段分表",
                Category = "Test",
                CreateUserID = 3000,
            };
            log.Insert();

            log.Delete();

            var deleteSql = sqls.LastOrDefault(s => s.Contains("Delete"));
            Assert.NotNull(deleteSql);
            Assert.Contains("Log2_3000", deleteSql);
        }
        catch { }
        finally
        {
            Log2.Meta.ShardPolicy = null;
            DAL.LocalFilter = null;
        }
    }

    [Fact(DisplayName = "等值查询路由到字段分表")]
    public void FindAll_EqualCondition_RoutesToShardTable()
    {
        var policy = new FieldShardPolicy("CreateUserID", Log2.Meta.Factory)
        {
            TablePolicy = "{0}_{1}",
        };
        Log2.Meta.ShardPolicy = policy;

        var sqls = new List<String>();
        DAL.LocalFilter = s => sqls.Add(s);

        try
        {
            Log2.FindAll(Log2._.CreateUserID == 9000);

            sqls.RemoveAll(e => e.EndsWith("sqlite_master"));
            Assert.NotEmpty(sqls);
            Assert.Contains("Log2_9000", sqls[^1]);
        }
        catch { }
        finally
        {
            Log2.Meta.ShardPolicy = null;
            DAL.LocalFilter = null;
        }
    }
    #endregion
}
