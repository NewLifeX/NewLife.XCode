using System;
using System.Linq;
using NewLife;
using XCode;
using XCode.DataAccessLayer;
using XCode.Linq;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Linq;

/// <summary>LINQ 查询完整演示测试。使用SQLite数据库，覆盖 Query/Where/WhereIf/OrderBy/Skip/Take/Count/First/Include/FindAllWhereIf</summary>
[Collection("Database")]
public class LinqTests : IDisposable
{
    private String _connName;

    public LinqTests()
    {
        _connName = "LinqDemoTest";

        // SQLite 文件数据库
        var file = $"Data\\LinqDemo_{Guid.NewGuid():n}.db";
        var connStr = $"Data Source={file}";
        DAL.AddConnStr(_connName, connStr, null, "SQLite");

        // 显式建 Role 表并插入测试数据
        var dal = DAL.Create(_connName);
        dal.Execute(@"CREATE TABLE IF NOT EXISTS Role (
            ID INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Enable INTEGER DEFAULT 1,
            IsSystem INTEGER DEFAULT 0,
            Remark TEXT
        )");

        // 插入测试数据
        dal.Execute("INSERT INTO Role (Name,Enable,IsSystem) VALUES ('管理员',1,1)");
        dal.Execute("INSERT INTO Role (Name,Enable,IsSystem) VALUES ('用户',1,0)");
        dal.Execute("INSERT INTO Role (Name,Enable,IsSystem) VALUES ('游客',1,0)");
        dal.Execute("INSERT INTO Role (Name,Enable,IsSystem) VALUES ('审计员',0,1)");

        Role.Meta.ConnName = _connName;
    }

    public void Dispose()
    {
        Role.Meta.ConnName = "Membership";
    }

    // ==================== 基础查询 ====================

    [Fact]
    public void Query_ToList()
    {
        // 查询所有角色
        var list = Role.Query.ToList();

        Assert.NotEmpty(list);
        Assert.Equal(4, list.Count);
    }

    [Fact]
    public void Query_Count()
    {
        var count = Role.Query.Count();
        Assert.Equal(4, count);
    }

    [Fact]
    public void Query_FirstOrDefault()
    {
        var role = Role.Query.FirstOrDefault(r => r.Name == "管理员");
        Assert.NotNull(role);
        Assert.Equal("管理员", role.Name);
    }

    [Fact]
    public void Query_FirstOrDefault_NotFound()
    {
        var role = Role.Query.FirstOrDefault(r => r.Name == "not_exist_role");
        Assert.Null(role);
    }

    // ==================== WhereIf 动态筛选 ====================

    [Fact]
    public void WhereIf_部分条件激活()
    {
        // 模拟：名称筛选激活，ID筛选不激活
        var filterName = "管理员";
        var filterId = 0; // 不筛选

        var list = Role.Query
            .WhereIf(!filterName.IsNullOrEmpty(), r => r.Name == filterName)
            .WhereIf(filterId > 0, r => r.ID == filterId)
            .ToList();

        Assert.NotEmpty(list);
        Assert.All(list, r => Assert.Equal("管理员", r.Name));
    }

    [Fact]
    public void WhereIf_全部条件激活()
    {
        var list = Role.Query
            .WhereIf(true, r => r.Name.Contains("管理"))
            .WhereIf(true, r => r.Enable == true)
            .ToList();

        Assert.NotEmpty(list);
    }

    [Fact]
    public void WhereIf_无条件激活_等于全表()
    {
        var all = 4; // 显式插入4行
        var list = Role.Query
            .WhereIf(false, r => r.Name == "nonexistent")
            .WhereIf(false, r => r.ID < 0)
            .ToList();

        Assert.Equal(all, list.Count);
    }

    // ==================== 排序与分页 ====================

    [Fact]
    public void OrderBy_Skip_Take()
    {
        // 按ID倒序，跳过1条取2条
        var list = Role.Query.OrderByDescending(r => r.ID).Skip(1).Take(2).ToList();

        Assert.Equal(2, list.Count);
        Assert.True(list[0].ID > list[1].ID);
    }

    [Fact]
    public void OrderBy_多字段排序()
    {
        var list = Role.Query.OrderBy(r => r.Name).ThenByDescending(r => r.ID).ToList();

        Assert.NotEmpty(list);
    }

    // ==================== 复杂条件 ====================

    [Fact]
    public void ComplexQuery_Contains()
    {
        var list = Role.Query.Where(r => r.Name.Contains("管理")).ToList();
        Assert.NotEmpty(list);
    }

    [Fact]
    public void ComplexQuery_组合条件()
    {
        var list = Role.Query
            .Where(r => r.Enable == true)
            .Where(r => r.Name == "管理员" || r.Name == "用户")
            .ToList();

        Assert.True(list.Count >= 1);
    }

    // ==================== Include 预加载 ====================

    [Fact]
    public void Include_预加载关联缓存()
    {
        // Include 预加载自身缓存，不抛异常
        var list = Role.Query.Include(typeof(Role)).Where(r => r.Enable == true).ToList();
        Assert.NotEmpty(list);
    }

    [Fact]
    public void Include_链式多级预加载()
    {
        var list = Role.Query.Include(typeof(Role)).Include(typeof(Role)).Where(r => r.Enable == true).ToList();
        Assert.NotEmpty(list);
    }

    // ==================== FindAllWhereIf 实体级 ====================

    [Fact]
    public void FindAllWhereIf_正常筛选()
    {
        var filterName = "管理员";
        var onlyEnabled = true;

        var list = Role.FindAllWhereIf(
            (!filterName.IsNullOrEmpty(), Role._.Name == filterName),
            (onlyEnabled, Role._.Enable == true)
        );

        Assert.NotEmpty(list);
        Assert.All(list, r => Assert.Equal("管理员", r.Name));
    }

    [Fact]
    public void FindAllWhereIf_空条件等于全表()
    {
        var list = Role.FindAllWhereIf();
        Assert.NotEmpty(list);
    }

    [Fact]
    public void FindAllWhereIf_禁用条件被忽略()
    {
        var list = Role.FindAllWhereIf(
            (false, Role._.Name == "管理员"),
            (true, Role._.ID > 0)
        );

        Assert.Equal(4, list.Count);
    }

    // ==================== Query 属性 ====================

    [Fact]
    public void Query_类型正确()
    {
        var query = Role.Query;
        Assert.NotNull(query);
        Assert.IsType<EntityQueryable<Role>>(query);
    }
}
