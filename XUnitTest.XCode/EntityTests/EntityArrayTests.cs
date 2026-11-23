using System;
using System.ComponentModel;
using System.Linq;
using NewLife;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.EntityTests;

/// <summary>数组字段实体读写与表达式测试</summary>
public class EntityArrayTests
{
    [Fact]
    [DisplayName("数组字段内容比较与克隆深拷贝")]
    public void CheckEqualAndClone()
    {
        var entity = new ArrayTestEntity
        {
            Id = 1,
            Tags = ["a", "b"],
            Numbers = [1, 2],
        };

        Assert.True(EntityBase.CheckEqual(entity.Tags, new[] { "a", "b" }));
        Assert.False(EntityBase.CheckEqual(entity.Tags, new[] { "a" }));

        var clone = entity.CloneEntity();
        Assert.NotSame(entity.Tags, clone.Tags);
        Assert.NotSame(entity.Numbers, clone.Numbers);
        Assert.Equal(entity.Tags, clone.Tags);

        clone.Tags![0] = "changed";
        clone.Numbers![0] = 9;
        Assert.Equal("a", entity.Tags![0]);
        Assert.Equal(1, entity.Numbers![0]);
    }

    [Fact]
    [DisplayName("索引器把 Object 数组转换为强类型数组")]
    public void SetItemConvertArray()
    {
        var entity = new ArrayTestEntity
        {
            Id = 1,
        };

        entity["Tags"] = new Object[] { "a", "b" };
        entity["Numbers"] = new Object[] { 1, 2L };

        Assert.Equal(new String[] { "a", "b" }, entity.Tags);
        Assert.Equal(new Int64[] { 1, 2 }, entity.Numbers);
    }

    [Fact]
    [DisplayName("Presto 数组包含表达式")]
    public void ArrayContainsPresto()
    {
        DAL.AddConnStr("ArrayPresto", "Host=localhost;Port=8080;Catalog=hive;Schema=default;Arrays=On", null, "Presto");
        var db = DAL.Create("ArrayPresto").Db;
        var field = ArrayTestEntity.Meta.Table.Fields.First(e => e.Name == "Tags");

        var sql = field.ArrayContains("a").GetString(db, null);

        Assert.Contains("contains(", sql);
        Assert.Contains("'a'", sql);
    }

    [Fact]
    [DisplayName("PostgreSQL 数组包含表达式")]
    public void ArrayContainsPostgreSQL()
    {
        DAL.AddConnStr("ArrayPg", "Server=localhost;Port=5432;Database=test;User ID=postgres;Password=test", null, "PostgreSQL");
        var db = DAL.Create("ArrayPg").Db;
        var field = ArrayTestEntity.Meta.Table.Fields.First(e => e.Name == "Tags");

        var sql = field.ArrayContains("a").GetString(db, null);

        Assert.Contains("@>", sql);
        Assert.Contains("ARRAY['a']", sql);
        Assert.Contains("::varchar[]", sql);
    }

    [Fact]
    [DisplayName("非数组字段不能使用数组包含表达式")]
    public void ArrayContainsRejectsNonArray()
    {
        var field = ArrayTestEntity.Meta.Table.Fields.First(e => e.Name == "Id");

        Assert.Throws<NotSupportedException>(() => field.ArrayContains(1));
    }
}

/// <summary>数组字段测试实体</summary>
[Serializable]
[BindTable("ArrayTestEntity", Description = "数组测试", ConnName = "ArrayTest", DbType = DatabaseType.SQLite)]
public class ArrayTestEntity : Entity<ArrayTestEntity>
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DataObjectField(true, false, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String[]? _Tags;
    /// <summary>标签</summary>
    [DataObjectField(false, false, true, 0)]
    [BindColumn("Tags", "标签", "varchar[]")]
    public String[]? Tags { get => _Tags; set { if (OnPropertyChanging("Tags", value)) { _Tags = value; OnPropertyChanged("Tags"); } } }

    private Int64[]? _Numbers;
    /// <summary>数字</summary>
    [DataObjectField(false, false, true, 0)]
    [BindColumn("Numbers", "数字", "bigint[]")]
    public Int64[]? Numbers { get => _Numbers; set { if (OnPropertyChanging("Numbers", value)) { _Numbers = value; OnPropertyChanged("Numbers"); } } }
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
            "Tags" => _Tags,
            "Numbers" => _Numbers,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Tags": _Tags = ValidHelper.ToStringArray(value); break;
                case "Numbers": _Numbers = ValidHelper.ToInt64Array(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion
}
