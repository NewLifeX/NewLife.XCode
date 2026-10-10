using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using NewLife;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>自动迁移测试实体</summary>
[Serializable]
[BindTable("MigrationTestEntity", Description = "自动迁移测试", ConnName = "MigrationTest", DbType = DatabaseType.SQLite)]
public class MigrationTestEntity : Entity<MigrationTestEntity>
{
    #region 属性
    private Int32 _ID;
    /// <summary>编号</summary>
    [DataObjectField(true, true, false, 0)]
    [BindColumn("ID", "编号", "")]
    public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Int32 _Count;
    /// <summary>次数</summary>
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Count", "次数", "")]
    public Int32 Count { get => _Count; set { if (OnPropertyChanging("Count", value)) { _Count = value; OnPropertyChanged("Count"); } } }

    private Int32 _Weight;
    /// <summary>权重</summary>
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Weight", "权重", "")]
    public Int32 Weight { get => _Weight; set { if (OnPropertyChanging("Weight", value)) { _Weight = value; OnPropertyChanged("Weight"); } } }

    private String _Remark;
    /// <summary>备注</summary>
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Remark", "备注", "")]
    public String Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object this[String name]
    {
        get => name switch
        {
            "ID" => _ID,
            "Name" => _Name,
            "Count" => _Count,
            "Weight" => _Weight,
            "Remark" => _Remark,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "ID": _ID = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Count": _Count = value.ToInt(); break;
                case "Weight": _Weight = value.ToInt(); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion
}

/// <summary>自动迁移（CodeFirst）DDL 行为测试。使用 SQLite 临时库验证各迁移模式的建表/加字段/删字段语义。</summary>
[Collection("Database")]
public class MigrationTests
{
    private const String _connName = "MigrationTest";
    private const String _table = "MigrationTestEntity";

    static MigrationTests()
    {
        var dbFile = Path.Combine(Path.GetTempPath(), $"MigrationTest_{Guid.NewGuid():n}.db");
        DAL.AddConnStr(_connName, $"Data Source={dbFile}", null, "SQLite");
    }

    /// <summary>按给定 DDL 重建表</summary>
    private static void ResetTable(String createSql)
    {
        var dal = DAL.Create(_connName);
        dal.Execute($"Drop Table If Exists {_table}");
        if (!createSql.IsNullOrEmpty()) dal.Execute(createSql);
    }

    /// <summary>以指定迁移模式应用实体模型</summary>
    private static void Apply(Migration mode)
    {
        var dal = DAL.Create(_connName);
        dal.Db.Migration = mode;
        dal.SetTables(MigrationTestEntity.Meta.Table.DataTable);
    }

    private static String[] DbColumns()
    {
        // 直连元数据查询，绕过 DAL.Tables 的异步缓存
        var tables = DAL.Create(_connName).Db.CreateMetaData().GetTables();
        var tb = tables.FirstOrDefault(e => e.TableName.EqualIgnoreCase(_table));
        return tb == null ? [] : tb.Columns.Select(e => e.Name).OrderBy(e => e).ToArray();
    }

    [Fact(DisplayName = "On 模式：缺失表自动创建并补齐全部字段")]
    public void On_CreatesMissingTable()
    {
        ResetTable(null);

        Apply(Migration.On);

        Assert.Equal(new[] { "Count", "ID", "Name", "Remark", "Weight" }, DbColumns());

        // 重复应用应当幂等
        Apply(Migration.On);
        Assert.Equal(new[] { "Count", "ID", "Name", "Remark", "Weight" }, DbColumns());
    }

    [Fact(DisplayName = "On 模式：新增字段自动补齐，已有多余字段不删除")]
    public void On_AddsMissingColumns_KeepsExtra()
    {
        ResetTable($"Create Table {_table} (ID INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT, Extra TEXT)");

        Apply(Migration.On);

        var columns = DbColumns();
        Assert.Contains("Count", columns);
        Assert.Contains("Weight", columns);
        Assert.Contains("Remark", columns);
        Assert.Contains("Extra", columns);
    }

    [Fact(DisplayName = "Full 模式：删除模型外的多余字段")]
    public void Full_DropsExtraColumn()
    {
        ResetTable($"Create Table {_table} (ID INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT, Extra TEXT)");

        Apply(Migration.Full);

        var columns = DbColumns();
        Assert.Contains("Count", columns);
        Assert.DoesNotContain("Extra", columns);
    }

    [Fact(DisplayName = "Off 模式：完全不做任何变更")]
    public void Off_Skips()
    {
        ResetTable($"Create Table {_table} (ID INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT)");

        Apply(Migration.Off);

        Assert.Equal(new[] { "ID", "Name" }, DbColumns());
    }

    [Fact(DisplayName = "ReadOnly 模式：只检查差异不执行 DDL")]
    public void ReadOnly_Skips()
    {
        ResetTable($"Create Table {_table} (ID INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT)");

        Apply(Migration.ReadOnly);

        Assert.Equal(new[] { "ID", "Name" }, DbColumns());
    }

    [Fact(DisplayName = "表级配置只能收紧全局模式，不能放宽")]
    public void TableLevel_TightensOnly()
    {
        var table = MigrationTestEntity.Meta.Table.DataTable;
        try
        {
            // 全局 On，表级 Off → 收紧，不执行
            table.Properties[nameof(Migration)] = nameof(Migration.Off);
            ResetTable($"Create Table {_table} (ID INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT)");
            Apply(Migration.On);
            Assert.Equal(new[] { "ID", "Name" }, DbColumns());

            // 全局 Off，表级 Full → 不能放宽，仍然不执行
            table.Properties[nameof(Migration)] = nameof(Migration.Full);
            Apply(Migration.Off);
            Assert.Equal(new[] { "ID", "Name" }, DbColumns());

            // 全局 Full，表级 On → 收紧为只加字段，不删多余字段
            table.Properties[nameof(Migration)] = nameof(Migration.On);
            ResetTable($"Create Table {_table} (ID INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT, Extra TEXT)");
            Apply(Migration.Full);
            var columns = DbColumns();
            Assert.Contains("Count", columns);
            Assert.Contains("Extra", columns);
        }
        finally
        {
            table.Properties.Remove(nameof(Migration));
        }
    }
}
