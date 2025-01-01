using System;
using System.Collections.Generic;
using System.Linq;
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

[Collection("Database")]
[TestCaseOrderer("NewLife.UnitTest.PriorityOrderer", "NewLife.UnitTest")]
public class ShardTests
{
    public ShardTests()
    {
        var tracer = new DefaultTracer { MaxSamples = 1000, Log = XTrace.Log };
        DAL.GlobalTracer ??= tracer;

        DAL.AddConnStr("mysql", "Server=.;Port=3306;Database=membership;Uid=root;Pwd=root", null, "mysql");
        DAL.AddConnStr("mysql_underline", "Server=.;Port=3306;Database=membership_underline;Uid=root;Pwd=root;NameFormat=underline", null, "mysql");
    }

    //[Fact]
    //public void SplitTestSQLite()
    //{
    //    User2.Meta.ShardTableName = e => $"User_{e.RegisterTime:yyyyMM}";

    //    var user = new User2
    //    {
    //        Name = "Stone",
    //        DisplayName = "大石头",
    //        Enable = true,

    //        RegisterTime = new DateTime(2020, 8, 22),
    //        UpdateTime = new DateTime(2020, 9, 1),
    //    };
    //    User2.Meta.CreateShard(user);

    //    var factory = User2.Meta.Factory;
    //    var session = User2.Meta.Session;

    //    var sql = factory.Persistence.GetSql(session, user, DataObjectMethodType.Insert);
    //    Assert.Equal(@"Insert Into User_202008(Name,Password,DisplayName,Sex,Mail,Mobile,Code,Avatar,RoleID,RoleIds,DepartmentID,Online,Enable,Logins,LastLogin,LastLoginIP,RegisterTime,RegisterIP,Ex1,Ex2,Ex3,Ex4,Ex5,Ex6,UpdateUser,UpdateUserID,UpdateIP,UpdateTime,Remark) Values('Stone',null,'大石头',0,null,null,null,null,0,null,0,0,1,0,null,null,'2020-08-22 00:00:00',null,0,0,0,null,null,null,null,0,null,'2020-09-01 00:00:00',null)", sql);

    //    user.ID = 2;
    //    sql = factory.Persistence.GetSql(session, user, DataObjectMethodType.Update);
    //    Assert.Equal(@"Update User_202008 Set Name='Stone',DisplayName='大石头',Enable=1,RegisterTime='2020-08-22 00:00:00',UpdateTime='2020-09-01 00:00:00' Where ID=2", sql);

    //    sql = factory.Persistence.GetSql(session, user, DataObjectMethodType.Delete);
    //    Assert.Equal(@"Delete From User_202008 Where ID=2", sql);

    //    // 恢复现场，避免影响其它测试用例
    //    User2.Meta.ShardTableName = null;
    //}

    [TestOrder(10)]
    [Fact]
    public void ShardTestSQLite()
    {
        // 配置自动分表策略，一般在实体类静态构造函数中配置
        var shard = new TimeShardPolicy("RegisterTime", User2.Meta.Factory)
        {
            //Field = User2._.RegisterTime,
            TablePolicy = "{0}_{1:yyyyMM}",
        };
        User2.Meta.ShardPolicy = shard;

        // 拦截Sql
        var sql = "";
        DAL.LocalFilter = s => sql = s;

        var user = new User2
        {
            Name = Rand.NextString(8),

            RegisterTime = new DateTime(2020, 8, 22),
            UpdateTime = new DateTime(2020, 9, 1),
        };

        // 添删改查全部使用新表名
        user.Insert();
        Assert.StartsWith(@"[test] Insert Into User2_202008(", sql);

        user.DisplayName = Rand.NextString(16);
        user.Update();
        Assert.StartsWith(@"[test] Update User2_202008 Set", sql);

        user.Delete();
        Assert.StartsWith(@"[test] Delete From User2_202008 Where", sql);

        // 恢复现场，避免影响其它测试用例
        User2.Meta.ShardPolicy = null;
    }

    [TestOrder(20)]
    [Fact]
    public void ShardTestSQLite2()
    {
        // 配置自动分表策略，一般在实体类静态构造函数中配置
        var shard = new TimeShardPolicy("ID", Log2.Meta.Factory)
        {
            //Field = Log2._.ID,
            ConnPolicy = "{0}_{1:yyyy}",
            TablePolicy = "{0}_{1:yyyyMMdd}",
        };
        Log2.Meta.ShardPolicy = shard;

        // 拦截Sql，仅为了断言，非业务代码
        var sqls = new List<String>();
        DAL.LocalFilter = s => sqls.Add(s);

        var time = DateTime.Now;
        var log = new Log2
        {
            Action = "分表",
            Category = Rand.NextString(8),

            CreateTime = time,
        };

        // 添删改查全部使用新表名
        log.Insert();
        Assert.StartsWith($"[test_{time:yyyy}] Insert Into Log2_{time:yyyyMMdd}(", sqls[^1]);

        log.Category = Rand.NextString(16);
        log.Update();
        Assert.StartsWith($"[test_{time:yyyy}] Update Log2_{time:yyyyMMdd} Set", sqls[^1]);

        log.Delete();
        Assert.StartsWith($"[test_{time:yyyy}] Delete From Log2_{time:yyyyMMdd} Where", sqls[^1]);

        var list = Log2.Search(null, null, -1, null, -1, time.AddHours(-24), time, null, new PageParameter { PageSize = 100 });
        sqls.RemoveAll(e => e.EndsWith("sqlite_master"));
        Assert.StartsWith($"[test_{time.AddHours(-24):yyyy}] Select * From Log2_{time.AddHours(-24):yyyyMMdd} Where", sqls[^2]);
        Assert.StartsWith($"[test_{time:yyyy}] Select * From Log2_{time.AddHours(-00):yyyyMMdd} Where", sqls[^1]);

        list = Log2.Search(null, null, -1, null, -1, time.AddHours(-24), time, null, new PageParameter { PageIndex = 2, PageSize = 100 });
        sqls.RemoveAll(e => e.EndsWith("sqlite_master"));
        Assert.StartsWith($"[test_{time.AddHours(-24):yyyy}] Select * From Log2_{time.AddHours(-24):yyyyMMdd} Where", sqls[^3]);
        Assert.StartsWith($"[test_{time.AddHours(-24):yyyy}] Select Count(*) From Log2_{time.AddHours(-24):yyyyMMdd} Where", sqls[^2]);
        Assert.StartsWith($"[test_{time:yyyy}] Select * From Log2_{time.AddHours(-00):yyyyMMdd} Where", sqls[^1]);

        // 恢复现场，避免影响其它测试用例
        Log2.Meta.ShardPolicy = null;
    }

    [TestOrder(30)]
    [Fact]
    public void FindById()
    {
        // 配置自动分表策略，一般在实体类静态构造函数中配置
        var shard = new TimeShardPolicy("ID", Log2.Meta.Factory)
        {
            ConnPolicy = "{0}_{1:yyyy}",
            TablePolicy = "{0}_{1:yyyyMMdd}",
        };
        Log2.Meta.ShardPolicy = shard;

        // 拦截Sql，仅为了断言，非业务代码
        var sqls = new List<String>();
        DAL.LocalFilter = s => sqls.Add(s);

        var time = DateTime.Now;
        var log = new Log2
        {
            Action = "分表",
            Category = Rand.NextString(8),

            CreateTime = time,
        };

        // 添删改查全部使用新表名
        log.Insert();
        Assert.StartsWith($"[test_{time:yyyy}] Insert Into Log2_{time:yyyyMMdd}(", sqls[^1]);

        var log2 = Log2.FindByID(log.ID);
        Assert.StartsWith($"[test_{time:yyyy}] Select * From Log2_{time:yyyyMMdd} Where ID=" + log.ID, sqls[^1]);

        // 恢复现场，避免影响其它测试用例
        Log2.Meta.ShardPolicy = null;
    }

    [TestOrder(40)]
    [Fact]
    public void SearchDates()
    {
        // 配置自动分表策略，一般在实体类静态构造函数中配置
        var shard = new TimeShardPolicy("ID", Log2.Meta.Factory)
        {
            ConnPolicy = "{0}_{1:yyyy}",
            TablePolicy = "{0}_{1:yyyyMMdd}",
        };
        Log2.Meta.ShardPolicy = shard;

        // 插入一点数据
        var snow = Log2.Meta.Factory.Snow;
        var now = DateTime.Now;
        var log = new Log2 { ID = snow.NewId(now.AddDays(-2)) };
        log.Insert();
        log = new Log2 { ID = snow.NewId(now.AddDays(-1)) };
        log.Insert();
        log = new Log2 { ID = snow.NewId(now.AddDays(-0)) };
        log.Insert();
        log = new Log2 { ID = snow.NewId(now.AddDays(-3)) };
        log.Insert();

        // 拦截Sql，仅为了断言，非业务代码
        var sqls = new List<String>();
        DAL.LocalFilter = s => sqls.Add(s);

        var time = DateTime.Now;
        var start = time.AddDays(-3);

        // 遍历分表查询
        XTrace.WriteLine("AutoShard FindCount ({0}, {1})", start, time);
        Log2.Meta.AutoShard(start, time, () => Log2.FindCount()).ToArray();
        sqls.RemoveAll(e => e.EndsWith("sqlite_master"));
        Assert.StartsWith($"[test_{time.AddDays(-3):yyyy}] Select Count(*) From Log2_{time.AddDays(-3):yyyyMMdd}", sqls[^4]);
        Assert.StartsWith($"[test_{time.AddDays(-2):yyyy}] Select Count(*) From Log2_{time.AddDays(-2):yyyyMMdd}", sqls[^3]);
        Assert.StartsWith($"[test_{time.AddDays(-1):yyyy}] Select Count(*) From Log2_{time.AddDays(-1):yyyyMMdd}", sqls[^2]);
        Assert.StartsWith($"[test_{time:yyyy}] Select Count(*) From Log2_{time.AddDays(-0):yyyyMMdd}", sqls[^1]);

        // 在多表中进行分页查询
        XTrace.WriteLine("Search Page");
        var list = Log2.Search(null, null, -1, null, -1, start, time, null, new PageParameter { PageSize = 10000 });
        sqls.RemoveAll(e => e.EndsWith("sqlite_master"));
        Assert.StartsWith($"[test_{time.AddDays(-3):yyyy}] Select * From Log2_{time.AddDays(-3):yyyyMMdd} Where ID>=", sqls[^4]);
        Assert.StartsWith($"[test_{time.AddDays(-2):yyyy}] Select * From Log2_{time.AddDays(-2):yyyyMMdd} Where ID>=", sqls[^3]);
        Assert.StartsWith($"[test_{time.AddDays(-1):yyyy}] Select * From Log2_{time.AddDays(-1):yyyyMMdd} Where ID>=", sqls[^2]);
        Assert.StartsWith($"[test_{time:yyyy}] Select * From Log2_{time.AddDays(-0):yyyyMMdd} Where ID>=", sqls[^1]);

        // 查询第二页
        XTrace.WriteLine("Search Page2");
        list = Log2.Search(null, null, -1, null, -1, start, time, null, new PageParameter { PageIndex = 2, PageSize = 10000 });
        sqls.RemoveAll(e => e.EndsWith("sqlite_master"));
        Assert.StartsWith($"[test_{time.AddDays(-3):yyyy}] Select * From Log2_{time.AddDays(-3):yyyyMMdd} Where ID>=", sqls[^7]);
        Assert.StartsWith($"[test_{time.AddDays(-3):yyyy}] Select Count(*) From Log2_{time.AddDays(-3):yyyyMMdd} Where ID>=", sqls[^6]);
        Assert.StartsWith($"[test_{time.AddDays(-2):yyyy}] Select * From Log2_{time.AddDays(-2):yyyyMMdd} Where ID>=", sqls[^5]);
        Assert.StartsWith($"[test_{time.AddDays(-2):yyyy}] Select Count(*) From Log2_{time.AddDays(-2):yyyyMMdd} Where ID>=", sqls[^4]);
        Assert.StartsWith($"[test_{time.AddDays(-1):yyyy}] Select * From Log2_{time.AddDays(-1):yyyyMMdd} Where ID>=", sqls[^3]);
        Assert.StartsWith($"[test_{time.AddDays(-1):yyyy}] Select Count(*) From Log2_{time.AddDays(-1):yyyyMMdd} Where ID>=", sqls[^2]);
        Assert.StartsWith($"[test_{time:yyyy}] Select * From Log2_{time.AddDays(-0):yyyyMMdd} Where ID>=", sqls[^1]);

        // 在多表中进行分页查询（倒序）
        XTrace.WriteLine("Search Page Reverse");
        list = Log2.Search(null, null, -1, null, -1, start, time, null, new PageParameter { PageSize = 10000, Sort = "id", Desc = true });
        sqls.RemoveAll(e => e.EndsWith("sqlite_master"));
        Assert.StartsWith($"[test_{time:yyyy}] Select * From Log2_{time.AddDays(-0):yyyyMMdd} Where ID>=", sqls[^4]);
        Assert.StartsWith($"[test_{time.AddDays(-1):yyyy}] Select * From Log2_{time.AddDays(-1):yyyyMMdd} Where ID>=", sqls[^3]);
        Assert.StartsWith($"[test_{time.AddDays(-2):yyyy}] Select * From Log2_{time.AddDays(-2):yyyyMMdd} Where ID>=", sqls[^2]);
        Assert.StartsWith($"[test_{time.AddDays(-3):yyyy}] Select * From Log2_{time.AddDays(-3):yyyyMMdd} Where ID>=", sqls[^1]);

        // 日期倒序
        time = DateTime.Today;
        start = time.AddDays(-3);
        XTrace.WriteLine("AutoShard start={0} end={1}", time, start);
        Log2.Meta.AutoShard(time, start, () => Log2.FindCount()).ToArray();
        sqls.RemoveAll(e => e.EndsWith("sqlite_master"));
        Assert.StartsWith($"[test_{time.AddDays(-1):yyyy}] Select Count(*) From Log2_{time.AddDays(-1):yyyyMMdd}", sqls[^3]);
        Assert.StartsWith($"[test_{time.AddDays(-2):yyyy}] Select Count(*) From Log2_{time.AddDays(-2):yyyyMMdd}", sqls[^2]);
        Assert.StartsWith($"[test_{time.AddDays(-3):yyyy}] Select Count(*) From Log2_{time.AddDays(-3):yyyyMMdd}", sqls[^1]);

        time = DateTime.Today;
        start = time.AddDays(-3);
        time = time.AddSeconds(1);
        XTrace.WriteLine("AutoShard start={0} end={1}", time, start);
        Log2.Meta.AutoShard(time, start, () => Log2.FindCount()).ToArray();
        sqls.RemoveAll(e => e.EndsWith("sqlite_master"));
        Assert.StartsWith($"[test_{time:yyyy}] Select Count(*) From Log2_{time.AddDays(-0):yyyyMMdd}", sqls[^4]);
        Assert.StartsWith($"[test_{time.AddDays(-1):yyyy}] Select Count(*) From Log2_{time.AddDays(-1):yyyyMMdd}", sqls[^3]);
        Assert.StartsWith($"[test_{time.AddDays(-2):yyyy}] Select Count(*) From Log2_{time.AddDays(-2):yyyyMMdd}", sqls[^2]);
        Assert.StartsWith($"[test_{time.AddDays(-3):yyyy}] Select Count(*) From Log2_{time.AddDays(-3):yyyyMMdd}", sqls[^1]);

        // 恢复现场，避免影响其它测试用例
        Log2.Meta.ShardPolicy = null;
    }

    [TestOrder(110)]
    [Fact]
    public void SearchAutoShard()
    {
        // 配置自动分表策略，一般在实体类静态构造函数中配置
        var shard = new TimeShardPolicy("ID", Log2.Meta.Factory)
        {
            ConnPolicy = "{0}_{1:yyyy}",
            TablePolicy = "{0}_{1:yyyyMMdd}",
        };
        Log2.Meta.ShardPolicy = shard;

        // 插入一点数据
        var snow = Log2.Meta.Factory.Snow;
        var now = DateTime.Now;
        var log = new Log2 { ID = snow.NewId(now.AddDays(-2)), Success = true };
        log.Insert();
        log = new Log2 { ID = snow.NewId(now.AddDays(-1)), Success = true };
        log.Insert();
        log = new Log2 { ID = snow.NewId(now.AddDays(-0)), Success = true };
        log.Insert();

        // 拦截Sql，仅为了断言，非业务代码
        var sqls = new List<String>();
        DAL.LocalFilter = s => sqls.Add(s);

        var time = DateTime.Now;
        var start = time.AddDays(-2);

        // 自动分表查询，在指定时间区间内执行多次查询
        XTrace.WriteLine("AutoShard FindAll ({0}, {1})", start, time);
        var list = Log2.Meta.AutoShard(start, time, () => Log2.FindAll(Log2._.Success == true)).SelectMany(e => e).ToList();
        sqls.RemoveAll(e => e.EndsWith("sqlite_master"));
        Assert.StartsWith($"[test_{time.AddDays(-2):yyyy}] Select * From Log2_{time.AddDays(-2):yyyyMMdd} Where Success=1", sqls[^3]);
        Assert.StartsWith($"[test_{time.AddDays(-1):yyyy}] Select * From Log2_{time.AddDays(-1):yyyyMMdd} Where Success=1", sqls[^2]);
        Assert.StartsWith($"[test_{time:yyyy}] Select * From Log2_{time.AddDays(0):yyyyMMdd} Where Success=1", sqls[^1]);

        // 倒序查询
        XTrace.WriteLine("AutoShard FindAll ({0}, {1})", time, start);
        list = Log2.Meta.AutoShard(time, start, () => Log2.FindAll(Log2._.Success == true)).SelectMany(e => e).ToList();
        sqls.RemoveAll(e => e.EndsWith("sqlite_master"));
        Assert.StartsWith($"[test_{time:yyyy}] Select * From Log2_{time.AddDays(0):yyyyMMdd} Where Success=1", sqls[^3]);
        Assert.StartsWith($"[test_{time.AddDays(-1):yyyy}] Select * From Log2_{time.AddDays(-1):yyyyMMdd} Where Success=1", sqls[^2]);
        Assert.StartsWith($"[test_{time.AddDays(-2):yyyy}] Select * From Log2_{time.AddDays(-2):yyyyMMdd} Where Success=1", sqls[^1]);

        // 枚举数查询，只有在遍历时才真正执行查询
        var idx = 1;
        XTrace.WriteLine("AutoShard Start");
        var es = Log2.Meta.AutoShard(start, time, () => Log2.FindAll(Log2._.Success == true));
        XTrace.WriteLine("AutoShard Ready");
        foreach (var item in es)
        {
            XTrace.WriteLine("AutoShard idx={0} count={1}", idx++, item.Count);
        }
        XTrace.WriteLine("AutoShard End");

        // 恢复现场，避免影响其它测试用例
        Log2.Meta.ShardPolicy = null;
    }

    [TestOrder(120)]
    [Fact]
    public void SearchAutoShard2()
    {
        // 配置自动分表策略，一般在实体类静态构造函数中配置
        var shard = new TimeShardPolicy("ID", Log2.Meta.Factory)
        {
            ConnPolicy = "{0}_{1:yyyy}",
            TablePolicy = "{0}_{1:yyyyMMdd}",
        };
        Log2.Meta.ShardPolicy = shard;

        // 拦截Sql，仅为了断言，非业务代码
        var sqls = new List<String>();
        DAL.LocalFilter = s => sqls.Add(s);

        var time = DateTime.Now;
        var start = time.AddDays(-2);

        // SelectCount遍历
        XTrace.WriteLine("AutoShard SelectCount({0}, {1})", start, time);
        Log2.Meta.AutoShard(start, time, () => Log2.FindCount()).ToArray();
        sqls.RemoveAll(e => e.EndsWith("sqlite_master"));
        Assert.StartsWith($"[test_{time.AddDays(-2):yyyy}] Select Count(*) From Log2_{time.AddDays(-2):yyyyMMdd}", sqls[^3]);
        Assert.StartsWith($"[test_{time.AddDays(-1):yyyy}] Select Count(*) From Log2_{time.AddDays(-1):yyyyMMdd}", sqls[^2]);
        Assert.StartsWith($"[test_{time:yyyy}] Select Count(*) From Log2_{time.AddDays(-0):yyyyMMdd}", sqls[^1]);

        // 第一个命中，不查后面
        XTrace.WriteLine("FirstOrDefault");
        var list = Log2.Meta.AutoShard(start, time, () => Log2.FindAll(Log2._.Success == true)).FirstOrDefault(e => e.Count > 0);
        Assert.StartsWith($"[test_{time.AddDays(-2):yyyy}] Select * From Log2_{time.AddDays(-2):yyyyMMdd} Where Success=1", sqls[^1]);

        // 倒过来查第一个命中
        XTrace.WriteLine("FirstOrDefault");
        list = Log2.Meta.AutoShard(time, start, () => Log2.FindAll(Log2._.Success == true)).FirstOrDefault(e => e.Count > 0);
        Assert.StartsWith($"[test_{time:yyyy}] Select * From Log2_{time.AddDays(-0):yyyyMMdd} Where Success=1", sqls[^1]);

        // 查所有
        XTrace.WriteLine("SelectMany");
        list = Log2.Meta.AutoShard(start, time, () => Log2.FindAll(Log2._.Success == true)).SelectMany(e => e).ToList();
        sqls.RemoveAll(e => e.EndsWith("sqlite_master"));
        Assert.StartsWith($"[test_{time.AddDays(-2):yyyy}] Select * From Log2_{time.AddDays(-2):yyyyMMdd} Where Success=1", sqls[^3]);
        Assert.StartsWith($"[test_{time.AddDays(-1):yyyy}] Select * From Log2_{time.AddDays(-1):yyyyMMdd} Where Success=1", sqls[^2]);
        Assert.StartsWith($"[test_{time:yyyy}] Select * From Log2_{time.AddDays(-0):yyyyMMdd} Where Success=1", sqls[^1]);

        // 恢复现场，避免影响其它测试用例
        Log2.Meta.ShardPolicy = null;
    }

    [TestOrder(130)]
    [Fact]
    public void SearchAutoShard3()
    {
        // 配置自动分表策略，一般在实体类静态构造函数中配置
        var shard = new TimeShardPolicy("ID", Log2.Meta.Factory)
        {
            ConnPolicy = "{0}_{1:yyyy}",
            TablePolicy = "{0}_{1:yyyyMMdd}",
        };
        Log2.Meta.ShardPolicy = shard;

        // 拦截Sql，仅为了断言，非业务代码
        var sqls = new List<String>();
        DAL.LocalFilter = s => sqls.Add(s);

        var time = DateTime.Now;
        var start = time.AddDays(-2);

        // 遍历SelectCount
        XTrace.WriteLine("AutoShard SelectCount({0}, {1})", start, time);
        Log2.Meta.AutoShard(start, time, () => Log2.FindCount()).ToArray();
        sqls.RemoveAll(e => e.EndsWith("sqlite_master"));
        Assert.StartsWith($"[test_{time.AddDays(-2):yyyy}] Select Count(*) From Log2_{time.AddDays(-2):yyyyMMdd}", sqls[^3]);
        Assert.StartsWith($"[test_{time.AddDays(-1):yyyy}] Select Count(*) From Log2_{time.AddDays(-1):yyyyMMdd}", sqls[^2]);
        Assert.StartsWith($"[test_{time:yyyy}] Select Count(*) From Log2_{time.AddDays(-0):yyyyMMdd}", sqls[^1]);

        // 倒序FindAll
        XTrace.WriteLine("AutoShard FindAll({0}, {1})", time, start);
        var list = Log2.Meta.AutoShard(time, start, () => Log2.FindAll(Log2._.Success == true)).SelectMany(e => e).ToList();
        sqls.RemoveAll(e => e.EndsWith("sqlite_master"));
        Assert.StartsWith($"[test_{time:yyyy}] Select * From Log2_{time.AddDays(-0):yyyyMMdd} Where Success=1", sqls[^3]);
        Assert.StartsWith($"[test_{time.AddDays(-1):yyyy}] Select * From Log2_{time.AddDays(-1):yyyyMMdd} Where Success=1", sqls[^2]);
        Assert.StartsWith($"[test_{time.AddDays(-2):yyyy}] Select * From Log2_{time.AddDays(-2):yyyyMMdd} Where Success=1", sqls[^1]);

        // 恢复现场，避免影响其它测试用例
        Log2.Meta.ShardPolicy = null;
    }

    [TestOrder(140)]
    [Fact(Skip = "跳过")]
    public void SearchAutoShard4()
    {
        // 配置自动分表策略，一般在实体类静态构造函数中配置
        var shard = new TimeShardPolicy("ID", Log2.Meta.Factory)
        {
            ConnPolicy = "{0}_{1:yyyy}",
            TablePolicy = "{0}_{1:yyyyMMdd}",
        };

        var start = DateTime.Today;
        var end = DateTime.Today;
        Log2.Meta.AutoShard(start, end, session =>
        {
            try
            {
                return session.Execute($"Drop Table {session.FormatedTableName}");
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
                return 0;
            }
        }
        );
    }

    [Fact]
    public void ExpressionShards()
    {
        var policy = new TimeShardPolicy(Log2._.CreateTime, Log2.Meta.Factory)
        {
            TablePolicy = "{0}_{1:yyyyMMdd}",
        };

        var start = "2024-05-29".ToDateTime();
        var end = start.AddDays(1);
        //var fi = Log2.Meta.Factory.Table.FindByName("ID");
        var fi = policy.Field;
        var where = fi >= start & fi < end;

        var shards = policy.Shards(where);
        Assert.NotNull(shards);
        Assert.Single(shards);
        Assert.Equal("Log2_20240529", shards[0].TableName);
    }

    [Fact]
    public void 跨年Shards()
    {
        var policy = new TimeShardPolicy(Log2._.CreateTime, Log2.Meta.Factory)
        {
            TablePolicy = "{0}_{1:yyyy}",
            Step = TimeSpan.FromDays(365),
        };

        var start = "2023-12-31".ToDateTime();
        var end = DateTime.Today;
        var fi = policy.Field;
        var where = fi >= start & fi < end;

        var shards = policy.Shards(where);
        Assert.NotNull(shards);
        Assert.Equal(2, shards.Length);
        Assert.Equal("Log2_2023", shards[0].TableName);
        Assert.Equal("Log2_2024", shards[1].TableName);
    }

    [Fact]
    public void 跨月Shards()
    {
        var policy = new TimeShardPolicy(Log2._.CreateTime, Log2.Meta.Factory)
        {
            TablePolicy = "{0}_{1:yyyyMM}",
            Step = TimeSpan.FromDays(31),
        };

        // 起止都是整数日期，末尾加1天
        var start = "2024/7/25 00:00:00".ToDateTime();
        var end = "2024/8/1 00:00:00".ToDateTime();
        var fi = policy.Field;
        //var where = fi >= start & fi < end;
        var where = fi.Between(start, end);

        var shards = policy.Shards(where);
        Assert.NotNull(shards);
        Assert.Equal(2, shards.Length);
        Assert.Equal("Log2_202407", shards[0].TableName);
        Assert.Equal("Log2_202408", shards[1].TableName);
    }

    [Fact]
    public void 单日Shards()
    {
        var policy = new TimeShardPolicy(Log2._.CreateTime, Log2.Meta.Factory)
        {
            TablePolicy = "{0}_{1:yyyyMM}",
            Step = TimeSpan.FromDays(31),
        };

        // 起止都是整数日期，末尾加1天
        var start = "2024/7/25 00:00:00".ToDateTime();
        var fi = policy.Field;
        var where = fi.Equal(start);

        var shards = policy.Shards(where);
        Assert.NotNull(shards);
        Assert.Equal(1, shards.Length);
        Assert.Equal("Log2_202407", shards[0].TableName);
    }
}