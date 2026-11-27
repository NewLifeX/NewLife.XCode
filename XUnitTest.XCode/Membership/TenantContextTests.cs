using System;
using System.ComponentModel;
using System.Threading.Tasks;
using NewLife;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;
using Xunit;

// 兼容期测试直接使用旧拦截器
#pragma warning disable CS0618

namespace XUnitTest.XCode.Membership;

/// <summary>租户上下文和租户模块测试</summary>
[DisplayName("租户上下文测试")]
public class TenantContextTests : IDisposable
{
    public TenantContextTests()
    {
        // 每个测试前清理上下文
        TenantContext.Current = null!;
    }

    public void Dispose()
    {
        // 清理上下文
        TenantContext.Current = null!;
    }

    #region TenantContext 静态属性测试
    [Fact]
    [DisplayName("Current属性设置和获取")]
    public void Current_SetAndGet()
    {
        // Arrange
        var ctx = new TenantContext { TenantId = 123 };

        // Act
        TenantContext.Current = ctx;

        // Assert
        Assert.Same(ctx, TenantContext.Current);
        Assert.Equal(123, TenantContext.Current.TenantId);
    }

    [Fact]
    [DisplayName("Current为null时返回null")]
    public void Current_WhenNull_ReturnsNull()
    {
        // Arrange
        TenantContext.Current = null!;

        // Act & Assert
        Assert.Null(TenantContext.Current);
    }

    [Fact]
    [DisplayName("CurrentId在有上下文时返回TenantId")]
    public void CurrentId_WhenHasContext_ReturnsTenantId()
    {
        // Arrange
        TenantContext.Current = new TenantContext { TenantId = 456 };

        // Act & Assert
        Assert.Equal(456, TenantContext.CurrentId);
    }

    [Fact]
    [DisplayName("CurrentId在无上下文时返回0")]
    public void CurrentId_WhenNoContext_ReturnsZero()
    {
        // Arrange
        TenantContext.Current = null!;

        // Act & Assert
        Assert.Equal(0, TenantContext.CurrentId);
    }
    #endregion


    #region TenantSourceHelper.ApplyTenant 测试
    [Fact]
    [DisplayName("ApplyTenant_有租户上下文时添加条件")]
    public void ApplyTenant_WithContext_AddsCondition()
    {
        // Arrange
        TenantContext.Current = new TenantContext { TenantId = 123 };
        var where = new WhereExpression();

        // Act
        var result = where.ApplyTenant<TenantTestEntity>();

        // Assert
        var sql = result.ToString();
        Assert.Contains("TenantId", sql);
        Assert.Contains("123", sql);
    }

    [Fact]
    [DisplayName("ApplyTenant_无租户上下文时不添加条件")]
    public void ApplyTenant_NoContext_DoesNotAddCondition()
    {
        // Arrange
        TenantContext.Current = null!;
        var where = new WhereExpression();

        // Act
        var result = where.ApplyTenant<TenantTestEntity>();

        // Assert
        Assert.True(result.IsEmpty);
    }

    [Fact]
    [DisplayName("ApplyTenant_TenantId为0时不添加条件")]
    public void ApplyTenant_TenantIdZero_DoesNotAddCondition()
    {
        // Arrange
        TenantContext.Current = new TenantContext { TenantId = 0 };
        var where = new WhereExpression();

        // Act
        var result = where.ApplyTenant<TenantTestEntity>();

        // Assert
        Assert.True(result.IsEmpty);
    }

    [Fact]
    [DisplayName("ApplyTenant_非ITenantScope实体不添加条件")]
    public void ApplyTenant_NonTenantScopeEntity_DoesNotAddCondition()
    {
        // Arrange
        TenantContext.Current = new TenantContext { TenantId = 123 };
        var where = new WhereExpression();

        // Act
        var result = where.ApplyTenant<NonTenantTestEntity>();

        // Assert
        Assert.True(result.IsEmpty);
    }

    [Fact]
    [DisplayName("ApplyTenant_通过工厂添加条件")]
    public void ApplyTenant_WithFactory_AddsCondition()
    {
        // Arrange
        TenantContext.Current = new TenantContext { TenantId = 456 };
        var where = new WhereExpression();
        var factory = TenantTestEntity.Meta.Factory;

        // Act
        var result = where.ApplyTenant(factory);

        // Assert
        var sql = result.ToString();
        Assert.Contains("TenantId", sql);
        Assert.Contains("456", sql);
    }

    [Fact]
    [DisplayName("ApplyTenant_工厂为null时返回原条件")]
    public void ApplyTenant_NullFactory_ReturnsOriginal()
    {
        // Arrange
        var where = new WhereExpression();

        // Act
        var result = where.ApplyTenant(null!);

        // Assert
        Assert.Same(where, result);
    }
    #endregion

    #region TenantSourceHelper.GetTenantField 测试
    [Fact]
    [DisplayName("GetTenantField_默认字段名")]
    public void GetTenantField_DefaultFieldName()
    {
        // Arrange
        var factory = TenantTestEntity.Meta.Factory;

        // Act
        var field = TenantSourceHelper.GetTenantField(factory);

        // Assert
        Assert.NotNull(field);
        Assert.Equal("TenantId", field.Name);
    }

    [Fact]
    [DisplayName("GetTenantField_自定义字段名")]
    public void GetTenantField_CustomFieldName()
    {
        // Arrange
        var factory = CustomTenantFieldEntity.Meta.Factory;

        // Act
        var field = TenantSourceHelper.GetTenantField(factory);

        // Assert
        Assert.NotNull(field);
        Assert.Equal("MyTenantId", field.Name);
    }

    [Fact]
    [DisplayName("GetTenantField_工厂为null返回null")]
    public void GetTenantField_NullFactory_ReturnsNull()
    {
        // Act
        var field = TenantSourceHelper.GetTenantField(null!);

        // Assert
        Assert.Null(field);
    }
    #endregion

    #region 多线程测试
    [Fact]
    [DisplayName("Current在多线程下隔离")]
    public async Task Current_ThreadIsolation()
    {
        // Arrange
        TenantContext.Current = new TenantContext { TenantId = 1 };
        var thread2TenantId = 0;

        // Act
        var task = System.Threading.Tasks.Task.Run(() =>
        {
            TenantContext.Current = new TenantContext { TenantId = 2 };
            thread2TenantId = TenantContext.CurrentId;
        });
        await task;

        // Assert
        Assert.Equal(1, TenantContext.CurrentId); // 主线程仍然是 1
        Assert.Equal(2, thread2TenantId); // 子线程是 2
    }
    #endregion
}

#region 测试用实体类
/// <summary>实现 ITenantScope 的测试实体</summary>
[Serializable]
[DataObject]
[Description("租户测试实体")]
[BindTable("TenantTestEntity", Description = "租户测试实体", ConnName = "test_tenant", DbType = DatabaseType.None)]
public partial class TenantTestEntity : Entity<TenantTestEntity>, ITenantScope
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
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object? this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "Name" => _Name,
            "TenantId" => _TenantId,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "TenantId": _TenantId = value.ToInt(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 字段名
    /// <summary>取得租户测试实体字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>租户标识</summary>
        public static readonly Field TenantId = FindByName("TenantId");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }
    #endregion
}

/// <summary>未实现 ITenantScope 的测试实体</summary>
[Serializable]
[DataObject]
[Description("非租户测试实体")]
[BindTable("NonTenantTestEntity", Description = "非租户测试实体", ConnName = "test_tenant", DbType = DatabaseType.None)]
public partial class NonTenantTestEntity : Entity<NonTenantTestEntity>
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
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object? this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "Name" => _Name,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 字段名
    /// <summary>取得非租户测试实体字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }
    #endregion
}

/// <summary>实现 IDataScopeFieldProvider 的自定义租户字段实体</summary>
[Serializable]
[DataObject]
[Description("自定义租户字段实体")]
[BindTable("CustomTenantFieldEntity", Description = "自定义租户字段实体", ConnName = "test_tenant", DbType = DatabaseType.None)]
public partial class CustomTenantFieldEntity : Entity<CustomTenantFieldEntity>, ITenantScope, IDataScopeFieldProvider
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

    private Int32 _MyTenantId;
    /// <summary>自定义租户标识</summary>
    [DisplayName("自定义租户标识")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MyTenantId", "自定义租户标识", "")]
    public Int32 MyTenantId { get => _MyTenantId; set { if (OnPropertyChanging("MyTenantId", value)) { _MyTenantId = value; OnPropertyChanged("MyTenantId"); } } }
    #endregion

    #region ITenantScope
    Int32 ITenantScope.TenantId { get => MyTenantId; set => MyTenantId = value; }
    #endregion

    #region IDataScopeFieldProvider
    /// <summary>获取用户字段</summary>
    public FieldItem? GetUserField() => null;

    /// <summary>获取部门字段</summary>
    public FieldItem? GetDepartmentField() => null;

    /// <summary>获取租户字段</summary>
    public FieldItem? GetTenantField() => Meta.Table.FindByName("MyTenantId");
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object? this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "Name" => _Name,
            "MyTenantId" => _MyTenantId,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "MyTenantId": _MyTenantId = value.ToInt(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 字段名
    /// <summary>取得自定义租户字段实体字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>自定义租户标识</summary>
        public static readonly Field MyTenantId = FindByName("MyTenantId");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }
    #endregion
}
#endregion
