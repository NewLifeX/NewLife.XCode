#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using NewLife.Data;
using XCode;
using XCode.DataAccessLayer;
using XCode.Membership;
using XCode.Model;
using Xunit;

namespace XUnitTest.XCode.Entity;

/// <summary>累加字段（Addition）相关测试：EntityAddition in-memory 行为、CalcAdditionDiff 差值计算、BatchUpsert 降级 SQL 生成</summary>
public class AdditionTests
{
    #region 辅助

    /// <summary>通过反射调用 DbSession.CalcAdditionDiff（protected static）</summary>
    private static Object InvokeCalcAdditionDiff(IModel model, String fieldName, Type targetType)
    {
        var mi = typeof(DbSession).GetMethod(
            "CalcAdditionDiff",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            [typeof(IModel), typeof(String), typeof(Type)],
            null)!;

        return mi.Invoke(null, [model, fieldName, targetType])!;
    }

    /// <summary>
    /// 使用 User 实体建立 SQLite 内存库测试环境，
    /// 返回 IDisposable（CreateSplit）供 using 自动还原
    /// </summary>
    private static IDisposable SetupSqliteSession(String connName, out IEntitySession session)
    {
        var db = $"Data\\{connName}.db";
        DAL.AddConnStr(connName, $"Data Source={db}", null, "SQLite");

        var table = User.Meta.Table.DataTable.Clone() as IDataTable ?? throw new InvalidOperationException("无法克隆 User 数据表");
        table.TableName = $"User_{connName}";

        var split = User.Meta.CreateSplit(connName, table.TableName);
        session = User.Meta.Session;
        session.Dal.SetTables(table);
        session.Truncate();
        return split;
    }

    /// <summary>获取实体的 IEntity.Addition（显式接口实现，需强转）</summary>
    private static IEntityAddition GetAddition(IEntity entity) => entity.Addition;

    /// <summary>通过反射调用 EntityExtension.ResetAdditionSnapshots（private static）</summary>
    private static void InvokeResetAdditionSnapshots(IEnumerable<IEntity> list, ICollection<String> columns)
    {
        var mi = typeof(EntityExtension).GetMethod(
            "ResetAdditionSnapshots",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            [typeof(IEnumerable<IEntity>), typeof(ICollection<String>)],
            null)!;

        mi.Invoke(null, [list, columns]);
    }

    #endregion

    #region EntityAddition 纯内存行为

    [Fact(DisplayName = "Set 以当前值为旧基线，Get 返回 {cur, old} 对")]
    public void EntityAddition_Set_CapturesCurrentValueAsOldBaseline()
    {
        var user = new User { Ex1 = 100 };
        var addition = GetAddition(user);

        addition.Set(new[] { "Ex1" });

        var dfs = addition.Get();
        Assert.True(dfs.ContainsKey("Ex1"));
        Assert.Equal(100, dfs["Ex1"][0]);  // cur
        Assert.Equal(100, dfs["Ex1"][1]);  // old（初始与 cur 相同）
    }

    [Fact(DisplayName = "Set 后修改字段，Get 返回新 cur 与保留的 old")]
    public void EntityAddition_Get_ReturnsCurAndOldAfterModification()
    {
        var user = new User { Ex1 = 100 };
        var addition = GetAddition(user);
        addition.Set(new[] { "Ex1" });

        user.Ex1 = 150;

        var dfs = addition.Get();
        Assert.Equal(150, dfs["Ex1"][0]);  // cur = 150
        Assert.Equal(100, dfs["Ex1"][1]);  // old = 100（快照保留初始值）
    }

    [Fact(DisplayName = "未调用 Set 时 Get 返回空字典")]
    public void EntityAddition_Get_WithoutSet_ReturnsEmpty()
    {
        var user = new User { Ex1 = 50 };

        var dfs = GetAddition(user).Get();

        Assert.Empty(dfs);
    }

    [Fact(DisplayName = "Reset 将 old 基线推进到 cur，使差值归零")]
    public void EntityAddition_Reset_AdvancesOldBaselineToCurrent()
    {
        var user = new User { Ex1 = 100 };
        var addition = GetAddition(user);
        addition.Set(new[] { "Ex1" });
        user.Ex1 = 150;

        // Reset 前：diff = 50
        var dfsBefore = addition.Get();
        Assert.Equal(150, dfsBefore["Ex1"][0]);
        Assert.Equal(100, dfsBefore["Ex1"][1]);

        // 执行 Reset
        addition.Reset(dfsBefore);

        // Reset 后：old 推进到 150，diff = 0
        var dfsAfter = addition.Get();
        Assert.Equal(150, dfsAfter["Ex1"][0]);  // cur
        Assert.Equal(150, dfsAfter["Ex1"][1]);  // old 已推进
    }

    [Fact(DisplayName = "连续两次 Set 时快照以第二次 Set 时的当前值为 old")]
    public void EntityAddition_Set_Twice_ResetsByLatestCurrentValue()
    {
        var user = new User { Ex1 = 100 };
        var addition = GetAddition(user);
        addition.Set(new[] { "Ex1" });
        user.Ex1 = 150;

        // 第二次 Set，以当前值 150 为新基线
        addition.Set(new[] { "Ex1" });
        user.Ex1 = 200;

        var dfs = addition.Get();
        Assert.Equal(200, dfs["Ex1"][0]);  // cur
        Assert.Equal(150, dfs["Ex1"][1]);  // old = 第二次 Set 时的值
    }

    #endregion

    #region CalcAdditionDiff 差值计算

    [Fact(DisplayName = "CalcAdditionDiff — Int32 正差值（cur > old）")]
    public void CalcAdditionDiff_Int32_PositiveDiff()
    {
        var user = new User { Ex1 = 100 };
        GetAddition(user).Set(new[] { "Ex1" });
        user.Ex1 = 150;

        var diff = InvokeCalcAdditionDiff(user, "Ex1", typeof(Int32));

        Assert.Equal(50, (Int32)diff!);
    }

    [Fact(DisplayName = "CalcAdditionDiff — Int32 负差值（cur < old）")]
    public void CalcAdditionDiff_Int32_NegativeDiff()
    {
        var user = new User { Ex1 = 200 };
        GetAddition(user).Set(new[] { "Ex1" });
        user.Ex1 = 130;

        var diff = InvokeCalcAdditionDiff(user, "Ex1", typeof(Int32));

        Assert.Equal(-70, (Int32)diff!);
    }

    [Fact(DisplayName = "CalcAdditionDiff — cur == old 时返回 0")]
    public void CalcAdditionDiff_Int32_ZeroDiff()
    {
        var user = new User { Ex1 = 100 };
        GetAddition(user).Set(new[] { "Ex1" });
        // 不修改 Ex1，cur == old == 100

        var diff = InvokeCalcAdditionDiff(user, "Ex1", typeof(Int32));

        Assert.Equal(0, (Int32)diff!);
    }

    [Fact(DisplayName = "CalcAdditionDiff — Double 正差值")]
    public void CalcAdditionDiff_Double_PositiveDiff()
    {
        var user = new User { Ex3 = 1.5 };
        GetAddition(user).Set(new[] { "Ex3" });
        user.Ex3 = 3.0;

        var diff = InvokeCalcAdditionDiff(user, "Ex3", typeof(Double));

        Assert.Equal(1.5, (Double)diff!, 10);
    }

    [Fact(DisplayName = "CalcAdditionDiff — Double 负差值")]
    public void CalcAdditionDiff_Double_NegativeDiff()
    {
        var user = new User { Ex3 = 5.0 };
        GetAddition(user).Set(new[] { "Ex3" });
        user.Ex3 = 2.5;

        var diff = InvokeCalcAdditionDiff(user, "Ex3", typeof(Double));

        Assert.Equal(-2.5, (Double)diff!, 10);
    }

    [Fact(DisplayName = "CalcAdditionDiff — 无快照时抛出 InvalidOperationException")]
    public void CalcAdditionDiff_NoSnapshot_ThrowsInvalidOperationException()
    {
        var user = new User { Ex1 = 100 };
        // 未调用 Set，Addition 快照为空

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeCalcAdditionDiff(user, "Ex1", typeof(Int32)));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("Ex1", ex.InnerException!.Message);
    }

    [Fact(DisplayName = "CalcAdditionDiff — model 非 IEntity 时抛出 InvalidOperationException")]
    public void CalcAdditionDiff_NotIEntity_ThrowsInvalidOperationException()
    {
        // 创建一个纯 IModel（非 IEntity）的 mock
        var model = new PlainModel { Ex1 = 100 };

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeCalcAdditionDiff(model, "Ex1", typeof(Int32)));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("IEntity", ex.InnerException!.Message);
    }

    #endregion

    #region BatchUpdate — Addition Reset 逻辑

    [Fact(DisplayName = "BatchUpdate Reset 后快照基线推进，第二次调用差值应为零")]
    public void BatchUpdate_AdditionReset_EnsuresSecondCallHasZeroDiff()
    {
        // 模拟实体从 DB 加载后的状态
        var user = new User { Ex1 = 100 };
        var addition = GetAddition(user);
        addition.Set(new[] { "Ex1" });  // 基线 = 100

        user.Ex1 = 150;  // diff = 50

        // ----- 模拟 EntityExtension.BatchUpdate 在 DAL 调用后执行的 Reset 逻辑 -----
        var dfs = addition.Get();
        Assert.Equal(50, (Int32)dfs["Ex1"][0]! - (Int32)dfs["Ex1"][1]!);

        addition.Reset(dfs);  // 推进基线：old → 150

        // ----- 第二次 BatchUpdate 前计算差值，应为 0 -----
        var diff2 = InvokeCalcAdditionDiff(user, "Ex1", typeof(Int32));
        Assert.Equal(0, (Int32)diff2!);
    }

    [Fact(DisplayName = "BatchUpdate Reset 后继续累加时差值仅反映增量")]
    public void BatchUpdate_AdditionReset_IncrementalDiffAfterSecondModification()
    {
        var user = new User { Ex1 = 100 };
        var addition = GetAddition(user);
        addition.Set(new[] { "Ex1" });
        user.Ex1 = 150;  // 第一次增量 +50

        // 第一次 BatchUpdate 后 Reset
        var dfs1 = addition.Get();
        addition.Reset(dfs1);

        // 继续修改：再增加 30
        user.Ex1 = 180;

        // 第二次 BatchUpdate 前差值应为 30（仅本次增量）
        var diff = InvokeCalcAdditionDiff(user, "Ex1", typeof(Int32));
        Assert.Equal(30, (Int32)diff!);
    }

    [Fact(DisplayName = "BatchUpdate 仅推进本次 addColumns 对应快照，其它累加字段保持原基线")]
    public void BatchUpdate_AdditionReset_OnlyResetsSpecifiedColumns()
    {
        var user = new User { Ex1 = 100, Ex2 = 10 };
        var addition = GetAddition(user);
        addition.Set(new[] { "Ex1", "Ex2" });

        user.Ex1 = 150;
        user.Ex2 = 20;

        InvokeResetAdditionSnapshots(new[] { user }, new HashSet<String>(StringComparer.OrdinalIgnoreCase) { "Ex1" });

        var dfs = addition.Get();
        Assert.Equal(150, dfs["Ex1"][1]);
        Assert.Equal(10, dfs["Ex2"][1]);
    }

    #endregion

    #region BatchUpsert — 有快照时降级为覆写（SQLite SQL 验证）

    [Fact(DisplayName = "BatchUpsert 有 Addition 快照时 addColumns 降级为 updateColumns（SQL 使用 excluded 覆写）")]
    public void BatchUpsert_WithSnapshot_DowngradesAddColumnsToOverwrite()
    {
        var connName = "test_addition_upsert_snap";
        using var split = SetupSqliteSession(connName, out var session);

        var user = new User { ID = 1001, Name = "UpsertUser1", Ex1 = 100 };
        // 模拟从 DB 加载后的快照
        var addition = GetAddition(user);
        addition.Set(new[] { "Ex1" });
        user.Ex1 = 150;  // diff = 50，有快照

        String? capturedSql = null;
        DAL.LocalFilter = sql => capturedSql = sql;
        try
        {
            var option = new BatchOption { AddColumns = new HashSet<String> { "Ex1" } };
            var rs = new List<User> { user }.BatchUpsert(option, session);
            Assert.True(rs > 0);
        }
        finally
        {
            DAL.LocalFilter = null!;
        }

        Assert.NotNull(capturedSql);
        var dfs = addition.Get();
        Assert.Equal(150, dfs["Ex1"][1]);

        // 降级后应使用覆写语义：Ex1=excluded."Ex1"
        Assert.Contains("excluded.", capturedSql, StringComparison.OrdinalIgnoreCase);
        // 不应出现累加语义：Ex1="Ex1"+excluded."Ex1"
        Assert.DoesNotContain("+excluded", capturedSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "BatchUpsert 无 Addition 快照时 addColumns 保留累加语义（SQL 使用 field+excluded）")]
    public void BatchUpsert_WithoutSnapshot_KeepsAddColumnsAccumulation()
    {
        var connName = "test_addition_upsert_nosnap";
        using var split = SetupSqliteSession(connName, out var session);

        var user = new User { ID = 1002, Name = "UpsertUser2", Ex1 = 100 };
        // 不设置快照，模拟全新实体

        String? capturedSql = null;
        DAL.LocalFilter = sql => capturedSql = sql;
        try
        {
            var option = new BatchOption { AddColumns = new HashSet<String> { "Ex1" } };
            var rs = new List<User> { user }.BatchUpsert(option, session);
            Assert.True(rs > 0);
        }
        finally
        {
            DAL.LocalFilter = null!;
        }

        Assert.NotNull(capturedSql);
        // 无快照时应保留累加语义：Ex1="Ex1"+excluded."Ex1"
        Assert.Contains("+excluded", capturedSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "BatchUpsert 部分实体有快照时只降级有快照的字段，其余保留累加")]
    public void BatchUpsert_MixedSnapshot_DowngradesOnlySnapshotFields()
    {
        var connName = "test_addition_upsert_mixed";
        using var split = SetupSqliteSession(connName, out var session);

        // user1：Ex1 有快照，Ex2 无快照
        var user1 = new User { ID = 2001, Name = "MixUser1", Ex1 = 100, Ex2 = 10 };
        GetAddition(user1).Set(new[] { "Ex1" });
        user1.Ex1 = 150;

        // user2：无任何快照
        var user2 = new User { ID = 2002, Name = "MixUser2", Ex1 = 200, Ex2 = 20 };

        String? capturedSql = null;
        DAL.LocalFilter = sql => capturedSql = sql;
        try
        {
            var option = new BatchOption { AddColumns = new HashSet<String> { "Ex1", "Ex2" } };
            var rs = new List<User> { user1, user2 }.BatchUpsert(option, session);
            Assert.True(rs > 0);
        }
        finally
        {
            DAL.LocalFilter = null!;
        }

        Assert.NotNull(capturedSql);
        // Ex1 有快照 → 降级覆写：Ex1=excluded."Ex1"
        // Ex2 无快照 → 保留累加：Ex2="Ex2"+excluded."Ex2"
        Assert.Contains("excluded.", capturedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("+excluded", capturedSql, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}

/// <summary>仅实现 IModel（不实现 IEntity）的纯数据模型，用于测试 CalcAdditionDiff 的类型检查</summary>
file class PlainModel : IModel
{
    private readonly Dictionary<String, Object?> _data = new(StringComparer.OrdinalIgnoreCase);

    public Int32 Ex1
    {
        get => (Int32)(_data.GetValueOrDefault("Ex1") ?? 0);
        set => _data["Ex1"] = value;
    }

    public Object? this[String name]
    {
        get => _data.GetValueOrDefault(name);
        set => _data[name] = value;
    }
}

