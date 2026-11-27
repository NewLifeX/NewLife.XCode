using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;
using Xunit;

// 兼容期测试直接使用旧拦截器
#pragma warning disable CS0618

namespace XUnitTest.XCode.Entity;

public class EntityFilterTests : IDisposable
{
    private const String ConnName = "test_filter";
    private static readonly String _dbFile;

    static EntityFilterTests()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Data");
        Directory.CreateDirectory(dir);
        _dbFile = Path.Combine(dir, "FilterTest.db");
        DAL.AddConnStr(ConnName, $"Data Source={_dbFile}", null, "SQLite");
    }

    public EntityFilterTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        EntityFilters.Global.Items = [];
        FilterTestEntity.Meta.Filters.Items = [];
        TenantContext.Current = null;
        DataScopeContext.Current = null;

        try { FilterTestEntity.Meta.Session.Cache.Clear("test", true); } catch { }
        try { FilterTestEntity.Meta.SingleCache.Clear("test"); } catch { }
        try { FilterTestEntity.Meta.Session.Execute("Delete From FilterTestEntity"); } catch { }
    }

    #region 管道
    [Fact(DisplayName = "过滤器按Order正序前置、逆序后置")]
    public void Pipeline_Order_And_ReverseAfter()
    {
        var log = new List<String>();
        var f1 = new DelegateFilter(0)
        {
            Before = c => log.Add("f1-before"),
            After = c => log.Add("f1-after"),
        };
        var f2 = new DelegateFilter(0)
        {
            Before = c => log.Add("f2-before"),
            After = c => log.Add("f2-after"),
        };
        FilterTestEntity.Meta.Filters.Items = [f1, f2];

        var context = new EntityContext { Action = DataMethod.Insert, Factory = FilterTestEntity.Meta.Factory };
        FilterTestEntity.Meta.Filters.Execute(context, c =>
        {
            log.Add("action");
            c.AffectedRows = 1;
        });

        Assert.Equal(["f1-before", "f2-before", "action", "f2-after", "f1-after"], log);
        Assert.Equal(1, context.AffectedRows);
    }

    [Fact(DisplayName = "前置Cancel跳过真实操作但执行后置")]
    public void Pipeline_Cancel_SkipsAction()
    {
        var actioned = false;
        var aftered = false;
        var filter = new DelegateFilter
        {
            Before = c => c.Cancel = true,
            After = c => aftered = true,
        };
        FilterTestEntity.Meta.Filters.Items = [filter];

        var context = new EntityContext { Action = DataMethod.Insert, Factory = FilterTestEntity.Meta.Factory };
        FilterTestEntity.Meta.Filters.Execute(context, c =>
        {
            actioned = true;
            c.AffectedRows = 1;
        });

        Assert.False(actioned);
        Assert.True(aftered);
        Assert.Equal(0, context.AffectedRows);
    }

    [Fact(DisplayName = "后置过滤器处理异常后不再抛出")]
    public void Pipeline_ExceptionHandled()
    {
        var filter = new DelegateFilter
        {
            After = c =>
            {
                Assert.NotNull(c.Exception);
                c.ExceptionHandled = true;
                c.AffectedRows = 42;
            },
        };
        FilterTestEntity.Meta.Filters.Items = [filter];

        var context = new EntityContext { Action = DataMethod.Insert, Factory = FilterTestEntity.Meta.Factory };
        FilterTestEntity.Meta.Filters.Execute(context, c => throw new InvalidOperationException("boom"));

        Assert.Equal(42, context.AffectedRows);
    }

    [Fact(DisplayName = "后置过滤器未处理异常时原样抛出")]
    public void Pipeline_Exception_Throws()
    {
        var filter = new DelegateFilter { After = c => Assert.NotNull(c.Exception) };
        FilterTestEntity.Meta.Filters.Items = [filter];

        var context = new EntityContext { Action = DataMethod.Insert, Factory = FilterTestEntity.Meta.Factory };
        var ex = Assert.Throws<InvalidOperationException>(() => FilterTestEntity.Meta.Filters.Execute(context, c => throw new InvalidOperationException("boom")));

        Assert.Equal("boom", ex.Message);
    }

    [Fact(DisplayName = "SuppressAll范围内过滤器不执行")]
    public void Pipeline_SuppressAll()
    {
        var called = false;
        var filter = new DelegateFilter
        {
            Before = c => called = true,
            After = c => called = true,
        };
        FilterTestEntity.Meta.Filters.Items = [filter];

        var actioned = false;
        var context = new EntityContext { Action = DataMethod.Insert, Factory = FilterTestEntity.Meta.Factory };
        using (EntityFilterScope.SuppressAll())
        {
            FilterTestEntity.Meta.Filters.Execute(context, c =>
            {
                actioned = true;
                c.AffectedRows = 1;
            });
        }

        Assert.True(actioned);
        Assert.False(called);
    }

    [Fact(DisplayName = "直接调用 Valid 会补跑过滤器校验")]
    public void Valid_RunsFiltersWhenCalledDirectly()
    {
        var called = 0;
        FilterTestEntity.Meta.Filters.Items =
        [
            new TenantFilter(),
            new DelegateFilter { Before = c => called++ },
        ];
        TenantContext.Current = new TenantContext { TenantId = 1 };

        // 插入校验会补齐租户
        var entity = new FilterTestEntity { Name = "valid", UserId = 1 };
        Assert.True(entity.Valid(DataMethod.Insert));
        Assert.Equal(1, called);
        Assert.Equal(1, entity.TenantId);

        // 其它租户的数据校验不通过
        var other = new FilterTestEntity { Name = "other", TenantId = 2, UserId = 1 };
        Assert.False(other.Valid(DataMethod.Delete));
    }

    [Fact(DisplayName = "操作类型区分创建、写入与查询")]
    public void Context_DistinguishesActionKinds()
    {
        Assert.False(new EntityContext { Action = DataMethod.Create }.IsWrite);
        Assert.True(new EntityContext { Action = DataMethod.Insert }.IsWrite);
        Assert.True(new EntityContext { Action = DataMethod.Upsert }.IsWrite);
        Assert.True(new EntityContext { Action = DataMethod.Replace }.IsWrite);
        Assert.False(new EntityContext { Action = DataMethod.Insert }.IsQuery);

        Assert.True(new EntityContext { Action = DataMethod.Find }.IsQuery);
        Assert.True(new EntityContext { Action = DataMethod.FindSQL }.IsQuery);
    }
    #endregion

    #region 单实体写
    [Fact(DisplayName = "过滤器之间可通过上下文共享数据")]
    public void Insert_ContextItemsSharedBetweenFilters()
    {
        var filter1 = new DelegateFilter(-1)
        {
            Before = c => c["reason"] = "audit",
        };
        var filter2 = new DelegateFilter
        {
            Before = c =>
            {
                if (c.Action != DataMethod.Insert) return;
                Assert.Equal("audit", c["reason"]);
                c.Entity!["Name"] = "filtered";
            },
            After = c =>
            {
                if (c.Action != DataMethod.Insert) return;
                Assert.Equal(1, c.AffectedRows);
                Assert.True(c.Entity!["Id"].ToInt() > 0);
            },
        };
        FilterTestEntity.Meta.Filters.Items = [filter1, filter2];

        var entity = new FilterTestEntity { Name = "raw", TenantId = 1, UserId = 1 };
        var rs = entity.Insert();

        Assert.Equal(1, rs);
        Assert.Equal("filtered", entity.Name);
        Assert.Equal("filtered", FilterTestEntity.FindByKey(entity.Id)!.Name);
    }

    [Fact(DisplayName = "前置Cancel阻止插入")]
    public void Insert_Cancel_NoRow()
    {
        var filter = new DelegateFilter
        {
            Before = c =>
            {
                if (c.Action == DataMethod.Insert) c.Cancel = true;
            },
        };
        FilterTestEntity.Meta.Filters.Items = [filter];

        var entity = new FilterTestEntity { Name = "cancelled", TenantId = 1, UserId = 1 };
        var rs = entity.Insert();

        Assert.Equal(0, rs);
        Assert.Equal(0, FilterTestEntity.FindCount());
    }

    [Fact(DisplayName = "异步插入同样经过过滤器")]
    public async Task InsertAsync_WithFilter()
    {
        var filter = new DelegateFilter
        {
            Before = c =>
            {
                if (c.Action == DataMethod.Insert) c.Entity!["Name"] = "async-filtered";
            },
            After = c =>
            {
                if (c.Action == DataMethod.Insert) Assert.Equal(1, c.AffectedRows);
            },
        };
        FilterTestEntity.Meta.Filters.Items = [filter];

        var entity = new FilterTestEntity { Name = "raw", TenantId = 1, UserId = 1 };
        var rs = await entity.InsertAsync();

        Assert.Equal(1, rs);
        Assert.Equal("async-filtered", FilterTestEntity.FindByKey(entity.Id)!.Name);
    }
    #endregion

    #region 条件与批量写
    [Fact(DisplayName = "条件删除会把租户条件追加到 where")]
    public void Delete_Condition_AppliesTenantFilter()
    {
        var t1 = new FilterTestEntity { Name = "same", TenantId = 1, UserId = 1 };
        t1.Insert();
        var t2 = new FilterTestEntity { Name = "same", TenantId = 2, UserId = 2 };
        t2.Insert();

        TenantContext.Current = new TenantContext { TenantId = 1 };
        FilterTestEntity.Meta.Filters.Items = [new TenantFilter()];

        Assert.Equal(1, FilterTestEntity.Delete(FilterTestEntity._.Name == "same"));

        using (EntityFilterScope.SuppressAll())
        {
            Assert.Null(FilterTestEntity.FindByKey(t1.Id));
            Assert.NotNull(FilterTestEntity.FindByKey(t2.Id));
        }
    }

    [Fact(DisplayName = "条件删除会把数据权限条件追加到 where")]
    public void Delete_Condition_AppliesDataScopeFilter()
    {
        var u1 = new FilterTestEntity { Name = "u1", TenantId = 1, UserId = 1 };
        u1.Insert();
        var u2 = new FilterTestEntity { Name = "u2", TenantId = 1, UserId = 2 };
        u2.Insert();

        DataScopeContext.Current = new DataScopeContext { UserId = 1, DataScope = DataScopes.仅本人 };
        FilterTestEntity.Meta.Filters.Items = [new DataScopeFilter()];

        Assert.Equal(1, FilterTestEntity.Delete(FilterTestEntity._.TenantId == 1));

        using (EntityFilterScope.SuppressAll())
        {
            Assert.Null(FilterTestEntity.FindByKey(u1.Id));
            Assert.NotNull(FilterTestEntity.FindByKey(u2.Id));
        }
    }

    [Fact(DisplayName = "字符串条件更新会把租户条件追加到 where")]
    public void Update_StringCondition_AppliesTenantFilter()
    {
        var t1 = new FilterTestEntity { Name = "same", TenantId = 1, UserId = 1 };
        t1.Insert();
        var t2 = new FilterTestEntity { Name = "same", TenantId = 2, UserId = 2 };
        t2.Insert();

        TenantContext.Current = new TenantContext { TenantId = 1 };
        FilterTestEntity.Meta.Filters.Items = [new TenantFilter()];

        Assert.Equal(0, FilterTestEntity.Update("Name='changed'", $"Id={t2.Id}"));

        using (EntityFilterScope.SuppressAll())
        {
            Assert.Equal("same", FilterTestEntity.FindByKey(t2.Id)!.Name);
        }
    }

    [Fact(DisplayName = "字符串条件删除会把租户条件追加到 where")]
    public void Delete_StringCondition_AppliesTenantFilter()
    {
        var t1 = new FilterTestEntity { Name = "same", TenantId = 1, UserId = 1 };
        t1.Insert();
        var t2 = new FilterTestEntity { Name = "same", TenantId = 2, UserId = 2 };
        t2.Insert();

        TenantContext.Current = new TenantContext { TenantId = 1 };
        FilterTestEntity.Meta.Filters.Items = [new TenantFilter()];

        Assert.Equal(1, FilterTestEntity.Delete("Name='same'"));

        using (EntityFilterScope.SuppressAll())
        {
            Assert.Null(FilterTestEntity.FindByKey(t1.Id));
            Assert.NotNull(FilterTestEntity.FindByKey(t2.Id));
        }
    }

    [Fact(DisplayName = "静态条件更新直接执行，不经过过滤器")]
    public void Update_Condition_BypassesFilters()
    {
        var t1 = new FilterTestEntity { Name = "same", TenantId = 1, UserId = 1 };
        t1.Insert();
        var t2 = new FilterTestEntity { Name = "same", TenantId = 2, UserId = 2 };
        t2.Insert();

        TenantContext.Current = new TenantContext { TenantId = 1 };
        FilterTestEntity.Meta.Filters.Items = [new TenantFilter()];

        Assert.Equal(1, FilterTestEntity.Update(["Name"], ["changed"], ["Id"], [t2.Id]));

        using (EntityFilterScope.SuppressAll())
        {
            Assert.Equal("changed", FilterTestEntity.FindByKey(t2.Id)!.Name);
        }
    }

    [Fact(DisplayName = "原始SQL查询在租户过滤下直接拒绝")]
    public void Query_RawSql_Aborts()
    {
        TenantContext.Current = new TenantContext { TenantId = 1 };
        FilterTestEntity.Meta.Filters.Items = [new TenantFilter()];

        Assert.Throws<XCodeException>(() => FilterTestEntity.FindAll("Select * From FilterTestEntity"));

        // 显式放开后允许
        using (EntityFilterScope.Suppress<TenantFilter>())
        {
            FilterTestEntity.FindAll("Select * From FilterTestEntity");
        }
    }

    [Fact(DisplayName = "数据权限下静态条件更新直接执行")]
    public void DataScopeFilter_UpdateCondition_BypassesFilters()
    {
        var u1 = new FilterTestEntity { Name = "u1", TenantId = 1, UserId = 1 };
        u1.Insert();
        var u2 = new FilterTestEntity { Name = "u2", TenantId = 1, UserId = 2 };
        u2.Insert();

        DataScopeContext.Current = new DataScopeContext { UserId = 1, DataScope = DataScopes.仅本人 };
        FilterTestEntity.Meta.Filters.Items = [new DataScopeFilter()];

        Assert.Equal(1, FilterTestEntity.Update(["Name"], ["changed"], ["Id"], [u2.Id]));

        using (EntityFilterScope.SuppressAll())
        {
            Assert.Equal("changed", FilterTestEntity.FindByKey(u2.Id)!.Name);
        }
    }

    [Fact(DisplayName = "有键Upsert时租户为0被拒绝")]
    public void TenantFilter_UpsertWithKey_NoTenant_Aborts()
    {
        var existing = new FilterTestEntity { Name = "t2", TenantId = 2, UserId = 1 };
        existing.Insert();

        TenantContext.Current = new TenantContext { TenantId = 1 };
        FilterTestEntity.Meta.Filters.Items = [new TenantFilter()];

        var entity = new FilterTestEntity { Id = existing.Id, Name = "hijack", TenantId = 0, UserId = 1 };
        Assert.Throws<XCodeException>(() => ((IEntity)entity).Upsert());

        using (EntityFilterScope.Suppress<TenantFilter>())
        {
            var saved = FilterTestEntity.FindByKey(existing.Id)!;
            Assert.Equal(2, saved.TenantId);
            Assert.Equal("t2", saved.Name);
        }
    }

    [Fact(DisplayName = "静态条件插入直接执行，不经过过滤器")]
    public void ConditionInsert_BypassesFilters()
    {
        TenantContext.Current = new TenantContext { TenantId = 1 };
        DataScopeContext.Current = new DataScopeContext { UserId = 3, DataScope = DataScopes.仅本人 };
        FilterTestEntity.Meta.Filters.Items = [new TenantFilter(), new DataScopeFilter()];

        Assert.Equal(1, FilterTestEntity.Insert(["Name", "UserId"], ["cond", 0]));

        using (EntityFilterScope.SuppressAll())
        {
            Assert.Equal(1, FilterTestEntity.FindCount(FilterTestEntity._.Name == "cond"));
        }
    }

    [Fact(DisplayName = "批量插入每批只触发一次过滤器并补齐租户")]
    public void BatchInsert_OncePerBatch_And_FillTenant()
    {
        var count = 0;
        FilterTestEntity.Meta.Filters.Filters =
        [
            new TenantFilter(),
            new DelegateFilter { Before = c => { if (c.Action == DataMethod.Insert && c.Entities is { Count: 2 }) count++; } },
        ];

        TenantContext.Current = new TenantContext { TenantId = 1 };

        var list = new List<FilterTestEntity>
        {
            new() { Name = "a", UserId = 1 },
            new() { Name = "b", UserId = 1 },
        };
        var rs = list.BatchInsert();

        Assert.Equal(2, rs);
        Assert.Equal(1, count);
        Assert.Equal(2, FilterTestEntity.FindCount());

        using (EntityFilterScope.Suppress<TenantFilter>())
        {
            Assert.Equal(2, FilterTestEntity.FindCount(FilterTestEntity._.TenantId == 1));
        }
    }

    [Fact(DisplayName = "集合Insert每批只触发一次过滤器")]
    public void ListInsert_OncePerBatch()
    {
        var count = 0;
        FilterTestEntity.Meta.Filters.Filters =
        [
            new TenantFilter(),
            new DelegateFilter { Before = c => { if (c.Action == DataMethod.Insert) count++; } },
        ];

        TenantContext.Current = new TenantContext { TenantId = 1 };

        var list = new List<FilterTestEntity>
        {
            new() { Name = "a", UserId = 1 },
            new() { Name = "b", UserId = 1 },
        };
        var rs = list.Insert();

        Assert.Equal(2, rs);
        Assert.Equal(1, count);
        Assert.All(list, e => Assert.Equal(1, e.TenantId));
    }
    #endregion

    #region 查询与缓存
    [Fact(DisplayName = "查询与计数自动应用租户条件")]
    public void Query_AppliesTenantFilter()
    {
        new FilterTestEntity { Name = "t1", TenantId = 1, UserId = 1 }.Insert();
        new FilterTestEntity { Name = "t2", TenantId = 2, UserId = 2 }.Insert();

        TenantContext.Current = new TenantContext { TenantId = 1 };
        FilterTestEntity.Meta.Filters.Items = [new TenantFilter()];

        var list = FilterTestEntity.FindAll();

        Assert.Single(list);
        Assert.Equal(1, list[0].TenantId);
        Assert.Equal(1, FilterTestEntity.FindCount());
    }

    [Fact(DisplayName = "查询后置可以裁剪结果数据表")]
    public void Query_OnExecuted_CanFilterTable()
    {
        new FilterTestEntity { Name = "keep", TenantId = 1, UserId = 1 }.Insert();
        new FilterTestEntity { Name = "drop", TenantId = 1, UserId = 1 }.Insert();

        FilterTestEntity.Meta.Filters.Filters =
        [
            new DelegateFilter
            {
                After = c =>
                {
                    if (c.Action != DataMethod.FindAll || c.Result is not DbTable table) return;

                    var index = table.GetColumn(nameof(FilterTestEntity.Name));
                    if (index < 0) return;

                    for (var i = table.Rows.Count - 1; i >= 0; i--)
                    {
                        if (table.Rows[i][index] as String == "drop") table.Rows.RemoveAt(i);
                    }
                },
            },
        ];

        var list = FilterTestEntity.FindAll();

        Assert.Single(list);
        Assert.Equal("keep", list[0].Name);
    }

    [Fact(DisplayName = "缓存读取按租户过滤，缓存本身保留整表")]
    public void CacheQuery_FiltersByTenant()
    {
        new FilterTestEntity { Name = "t1", TenantId = 1, UserId = 1 }.Insert();
        new FilterTestEntity { Name = "t2", TenantId = 2, UserId = 2 }.Insert();

        TenantContext.Current = new TenantContext { TenantId = 1 };
        FilterTestEntity.Meta.Filters.Items = [new TenantFilter()];

        var list = FilterTestEntity.FindAllWithCache();

        Assert.Single(list);
        Assert.Equal(1, list[0].TenantId);
        Assert.Single(FilterTestEntity.FindAll());

        // 跨租户上下文可见自己的数据，缓存未被污染
        TenantContext.Current = new TenantContext { TenantId = 2 };
        var list2 = FilterTestEntity.FindAllWithCache();
        Assert.Single(list2);
        Assert.Equal(2, list2[0].TenantId);
    }

    [Fact(DisplayName = "单对象缓存按租户判断可见性")]
    public void CacheQuery_KeyFilteredByTenant()
    {
        var t1 = new FilterTestEntity { Name = "k1", TenantId = 1, UserId = 1 };
        t1.Insert();
        var t2 = new FilterTestEntity { Name = "k2", TenantId = 2, UserId = 2 };
        t2.Insert();

        TenantContext.Current = new TenantContext { TenantId = 1 };
        FilterTestEntity.Meta.Filters.Items = [new TenantFilter()];

        var mine = FilterTestEntity.FindByKeyWithCache(t1.Id);
        Assert.NotNull(mine);
        Assert.Equal(1, mine!.TenantId);

        Assert.Null(FilterTestEntity.FindByKeyWithCache(t2.Id));
    }

    [Fact(DisplayName = "缓存读取按数据权限过滤")]
    public void CacheQuery_FiltersByDataScope()
    {
        new FilterTestEntity { Name = "m1", TenantId = 1, UserId = 1 }.Insert();
        new FilterTestEntity { Name = "m2", TenantId = 1, UserId = 2 }.Insert();

        DataScopeContext.Current = new DataScopeContext { UserId = 1, DataScope = DataScopes.仅本人 };
        FilterTestEntity.Meta.Filters.Items = [new DataScopeFilter()];

        var list = FilterTestEntity.FindAllWithCache();

        Assert.Single(list);
        Assert.Equal(1, list[0].UserId);
    }
    #endregion

    #region 数据权限
    [Fact(DisplayName = "数据权限查询仅返回本人数据")]
    public void DataScopeFilter_Query_OnlyOwn()
    {
        new FilterTestEntity { Name = "u1", TenantId = 1, UserId = 1 }.Insert();
        new FilterTestEntity { Name = "u2", TenantId = 1, UserId = 2 }.Insert();

        DataScopeContext.Current = new DataScopeContext { UserId = 1, DataScope = DataScopes.仅本人 };
        FilterTestEntity.Meta.Filters.Items = [new DataScopeFilter()];

        var list = FilterTestEntity.FindAll();

        Assert.Single(list);
        Assert.Equal(1, list[0].UserId);
    }

    [Fact(DisplayName = "数据权限拒绝修改他人数据")]
    public void DataScopeFilter_UpdateOther_Aborts()
    {
        var own = new FilterTestEntity { Name = "u2", TenantId = 1, UserId = 2 };
        own.Insert();

        DataScopeContext.Current = new DataScopeContext { UserId = 1, DataScope = DataScopes.仅本人 };
        FilterTestEntity.Meta.Filters.Items = [new DataScopeFilter()];

        var entity = new FilterTestEntity { Id = own.Id, Name = "changed", TenantId = 1, UserId = 2 };

        Assert.Throws<XCodeException>(() => entity.Update());
    }

    [Fact(DisplayName = "数据权限插入自动填充用户")]
    public void DataScopeFilter_Insert_FillsUser()
    {
        DataScopeContext.Current = new DataScopeContext { UserId = 3, DataScope = DataScopes.仅本人 };
        FilterTestEntity.Meta.Filters.Items = [new DataScopeFilter()];

        var entity = new FilterTestEntity { Name = "auto", TenantId = 1 };
        entity.Insert();

        Assert.Equal(3, entity.UserId);
    }
    #endregion

    #region 旧拦截器迁移
    [Fact(DisplayName = "创建实体时执行Create过滤器")]
    public void CreateFilter_FillsTenantAndUser()
    {
        FilterTestEntity.Meta.Filters.Items = [new TenantFilter(), new DataScopeFilter()];
        TenantContext.Current = new TenantContext { TenantId = 9 };
        DataScopeContext.Current = new DataScopeContext { UserId = 7, DataScope = DataScopes.仅本人 };

        var entity = (FilterTestEntity)FilterTestEntity.Meta.Factory.Create(true);

        Assert.Equal(9, entity.TenantId);
        Assert.Equal(7, entity.UserId);
    }

    [Fact(DisplayName = "旧租户拦截器注册后自动进入过滤器管道并覆盖查询")]
    public void LegacyTenantInterceptor_AutoRegistersAsFilter()
    {
        using (EntityFilterScope.SuppressAll())
        {
            new FilterTestEntity { Name = "t1", TenantId = 1, UserId = 1 }.Insert();
            new FilterTestEntity { Name = "t2", TenantId = 2, UserId = 2 }.Insert();
        }

        TenantContext.Current = new TenantContext { TenantId = 1 };
        var legacy = new TenantInterceptor();
        FilterTestEntity.Meta.Filters.Add(legacy);
        WaitForFilter<TenantFilter>();

        Assert.Contains(FilterTestEntity.Meta.Filters.Items, e => e is TenantFilter);

        var list = FilterTestEntity.FindAll();
        Assert.Single(list);
        Assert.Equal(1, list[0].TenantId);

        var cached = FilterTestEntity.FindAllWithCache();
        // 缓存读取在结果阶段按租户过滤
        Assert.Single(cached);
        Assert.Equal(1, cached[0].TenantId);
    }

    [Fact(DisplayName = "旧租户拦截器适配后条件删除按租户过滤")]
    public void LegacyTenantInterceptor_ConditionDelete()
    {
        var t1 = new FilterTestEntity { Name = "t1", TenantId = 1, UserId = 1 };
        t1.Insert();
        var t2 = new FilterTestEntity { Name = "t2", TenantId = 2, UserId = 2 };
        t2.Insert();

        TenantContext.Current = new TenantContext { TenantId = 1 };
        var legacy = new TenantInterceptor();
        FilterTestEntity.Meta.Filters.Add(legacy);
        WaitForFilter<TenantFilter>();

        Assert.Equal(1, FilterTestEntity.Delete(FilterTestEntity._.UserId >= 0));

        using (EntityFilterScope.SuppressAll())
        {
            Assert.Null(FilterTestEntity.FindByKey(t1.Id));
            Assert.NotNull(FilterTestEntity.FindByKey(t2.Id));
        }
    }

    [Fact(DisplayName = "旧租户拦截器适配后越权插入抛异常")]
    public void LegacyTenantInterceptor_ViolationThrows()
    {
        TenantContext.Current = new TenantContext { TenantId = 1 };
        var legacy = new TenantInterceptor();
        FilterTestEntity.Meta.Filters.Add(legacy);
        WaitForFilter<TenantFilter>();

        var entity = new FilterTestEntity { Name = "hijack", TenantId = 2, UserId = 1 };
        Assert.Throws<XCodeException>(() => entity.Insert());

        using (EntityFilterScope.SuppressAll())
        {
            Assert.Equal(0, FilterTestEntity.FindCount());
        }
    }

    [Fact(DisplayName = "旧数据权限拦截器注册后自动补齐创建用户")]
    public void LegacyDataScopeInterceptor_AutoRegistersAndFillsCreate()
    {
        DataScopeContext.Current = new DataScopeContext { UserId = 5, DataScope = DataScopes.仅本人 };
        var legacy = new DataScopeInterceptor();
        FilterTestEntity.Meta.Filters.Add(legacy);
        WaitForFilter<DataScopeFilter>();

        Assert.Contains(FilterTestEntity.Meta.Filters.Items, e => e is DataScopeFilter);

        var entity = (FilterTestEntity)FilterTestEntity.Meta.Factory.Create(true);
        Assert.Equal(5, entity.UserId);
    }

    [Fact(DisplayName = "旧租户拦截器适配后可用TenantFilter抑制")]
    public void LegacyTenantInterceptor_SuppressNewFilter()
    {
        using (EntityFilterScope.SuppressAll())
        {
            new FilterTestEntity { Name = "t1", TenantId = 1, UserId = 1 }.Insert();
            new FilterTestEntity { Name = "t2", TenantId = 2, UserId = 2 }.Insert();
        }

        TenantContext.Current = new TenantContext { TenantId = 1 };
        var legacy = new TenantInterceptor();
        FilterTestEntity.Meta.Filters.Add(legacy);
        WaitForFilter<TenantFilter>();

        using (EntityFilterScope.Suppress<TenantFilter>())
        {
            Assert.Equal(2, FilterTestEntity.FindCount());
        }

        Assert.Equal(1, FilterTestEntity.FindCount());
    }

    [Fact(DisplayName = "旧拦截器Filter仍可过滤单条结果")]
    public void LegacyInterceptor_FilterSingleResult()
    {
        using (EntityFilterScope.SuppressAll())
        {
            new FilterTestEntity { Name = "blocked", TenantId = 1, UserId = 1 }.Insert();
            new FilterTestEntity { Name = "allowed", TenantId = 1, UserId = 1 }.Insert();
        }

        FilterTestEntity.Meta.Filters.Items = [new HiddenNameInterceptor()];

        var list = FilterTestEntity.FindAll(FilterTestEntity._.Name == "blocked" | FilterTestEntity._.Name == "allowed");
        Assert.Equal(2, list.Count);
        Assert.Null(FilterTestEntity.Meta.Filters.FilterEntity(list.First(e => e.Name == "blocked")));
        Assert.NotNull(FilterTestEntity.Meta.Filters.FilterEntity(list.First(e => e.Name == "allowed")));
    }

    [Fact(DisplayName = "Add 泛型同时支持过滤器与旧拦截器")]
    public void Add_Generic_SupportsFiltersAndInterceptors()
    {
        FilterTestEntity.Meta.Filters.Add<AddedFilter>();
        WaitForFilter<AddedFilter>();
        Assert.Contains(FilterTestEntity.Meta.Filters.Items, e => e is AddedFilter);

        FilterTestEntity.Meta.Filters.Add<HiddenNameInterceptor>();
        WaitForFilter<HiddenNameInterceptor>();
        Assert.Contains(FilterTestEntity.Meta.Filters.Items, e => e is HiddenNameInterceptor);
    }

    [Fact(DisplayName = "旧内置拦截器类名注册后等价于新过滤器")]
    public void LegacyTimeInterceptor_EqualsTimeFilter()
    {
        var filters = new EntityFilters(typeof(global::XCode.Membership.Log));
        filters.Add<TimeInterceptor>();

        for (var i = 0; i < 100 && filters.Filters.Length == 0; i++) System.Threading.Thread.Sleep(10);

        var filter = Assert.Single(filters.Filters);
        Assert.IsType<TimeInterceptor>(filter);
        Assert.IsAssignableFrom<TimeFilter>(filter);
        Assert.True(filter.Init(typeof(global::XCode.Membership.Log)));
    }

    [Fact(DisplayName = "查询过滤器之间可通过上下文共享数据")]
    public void Query_ContextItemsSharedBetweenFilters()
    {
        var seen = false;
        FilterTestEntity.Meta.Filters.Filters =
        [
            new DelegateFilter(-1)
            {
                Before = c =>
                {
                    if (c.Action == DataMethod.FindAll) c["reason"] = "query";
                },
            },
            new DelegateFilter
            {
                Before = c =>
                {
                    if (c.Action == DataMethod.FindAll) seen = c["reason"] as String == "query";
                },
            },
        ];

        FilterTestEntity.FindAll();

        Assert.True(seen);
    }

    [Fact(DisplayName = "内置实体使用新过滤器注册")]
    public void BuiltInEntities_UseNewFilters()
    {
        WaitForFilter<TenantFilter>(Role.Meta.Filters);
        WaitForFilter<DataScopeFilter>(User.Meta.Filters);
        WaitForFilter<TenantFilter>(Department.Meta.Filters);
        WaitForFilter<DataScopeFilter>(Department.Meta.Filters);
    }

    private static void WaitForFilter<TFilter>() where TFilter : IEntityFilter => WaitForFilter<TFilter>(FilterTestEntity.Meta.Filters);

    private static void WaitForFilter<TFilter>(EntityFilters filters) where TFilter : IEntityFilter
    {
        for (var i = 0; i < 100; i++)
        {
            if (filters.Items.Any(e => e is TFilter)) return;
            System.Threading.Thread.Sleep(10);
        }

        Assert.Contains(filters.Items, e => e is TFilter);
    }
    #endregion

    private class DelegateFilter : EntityFilter
    {
        public Action<EntityContext>? Before { get; set; }
        public Action<EntityContext>? After { get; set; }

        public DelegateFilter(Int32 order = 0)
        {
            Order = order;
        }

        public override void OnExecuting(EntityContext context) => Before?.Invoke(context);
        public override void OnExecuted(EntityContext context) => After?.Invoke(context);
    }

    private class HiddenNameInterceptor : EntityInterceptor
    {
        protected override Boolean OnFilter(IEntity entity) => entity["Name"] as String != "blocked";
    }

    private class AddedFilter : EntityFilter { }
}

/// <summary>过滤器测试实体</summary>
[Serializable]
[Description("过滤器测试实体")]
[BindTable("FilterTestEntity", Description = "过滤器测试实体", ConnName = "test_filter", DbType = DatabaseType.SQLite)]
public partial class FilterTestEntity : Entity<FilterTestEntity>, ITenantScope, IUserScope
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String? _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "")]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Int32 _TenantId;
    /// <summary>租户标识</summary>
    [DisplayName("租户标识")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TenantId", "租户标识", "")]
    public Int32 TenantId { get => _TenantId; set { if (OnPropertyChanging("TenantId", value)) { _TenantId = value; OnPropertyChanged("TenantId"); } } }

    private Int32 _UserId;
    /// <summary>用户标识</summary>
    [DisplayName("用户标识")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UserId", "用户标识", "")]
    public Int32 UserId { get => _UserId; set { if (OnPropertyChanging("UserId", value)) { _UserId = value; OnPropertyChanged("UserId"); } } }
    #endregion

    #region 获取/设置 字段值
    public override Object? this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "Name" => _Name,
            "TenantId" => _TenantId,
            "UserId" => _UserId,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Name": _Name = value?.ToString(); break;
                case "TenantId": _TenantId = value.ToInt(); break;
                case "UserId": _UserId = value.ToInt(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 字段名
    public partial class _
    {
        public static readonly Field Id = FindByName("Id");
        public static readonly Field Name = FindByName("Name");
        public static readonly Field TenantId = FindByName("TenantId");
        public static readonly Field UserId = FindByName("UserId");
        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }
    #endregion
}
