using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewLife;
using NewLife.Data;
using NewLife.Security;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

/// <summary>保存模式测试。覆盖 Insert/Upsert/InsertIgnore/Replace 四种模式（M3-4）</summary>
[Collection("Database")]
public class SaveModesTests : IDisposable
{
    private readonly DAL _dal;
    private readonly String _file;

    public SaveModesTests()
    {
        _file = $"Data\\save_modes_{Rand.NextString(6)}.db".GetFullPath();
        _file.EnsureDirectory(true);
        DAL.AddConnStr("save_modes", $"Data Source={_file}", null, "SQLite");
        _dal = DAL.Create("save_modes");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_file)) File.Delete(_file);
        }
        catch { }
    }

    private IDataTable CreateTable()
    {
        var table = DAL.CreateTable();
        table.TableName = "sm_user";

        var id = table.CreateColumn();
        id.Name = "ID";
        id.ColumnName = "ID";
        id.DataType = typeof(Int32);
        id.PrimaryKey = true;
        table.Columns.Add(id);

        var name = table.CreateColumn();
        name.Name = "Name";
        name.ColumnName = "Name";
        name.DataType = typeof(String);
        name.Length = 50;
        table.Columns.Add(name);

        var num = table.CreateColumn();
        num.Name = "Num";
        num.ColumnName = "Num";
        num.DataType = typeof(Int32);
        table.Columns.Add(num);

        _dal.Db.CreateMetaData().CreateTable(table);
        return table;
    }

    private class SmModel : IModel
    {
        private readonly Dictionary<String, Object> _dic = new(StringComparer.OrdinalIgnoreCase);

        public Object this[String key] { get => _dic.TryGetValue(key, out var v) ? v : null; set => _dic[key] = value; }

        public SmModel(Int32 id, String name, Int32 num = 0) { _dic["ID"] = id; _dic["Name"] = name; _dic["Num"] = num; }
    }

    [Fact(DisplayName = "保存模式枚举值")]
    public void EnumValues()
    {
        Assert.Equal(0, (Int32)SaveModes.Insert);
        Assert.Equal(1, (Int32)SaveModes.Upsert);
        Assert.Equal(2, (Int32)SaveModes.InsertIgnore);
        Assert.Equal(3, (Int32)SaveModes.Replace);
    }

    [Theory(DisplayName = "InsertBuilder 四种模式生成对应SQL")]
    [InlineData(SaveModes.Insert, "Insert Into")]
    [InlineData(SaveModes.Upsert, "Upsert Into")]
    [InlineData(SaveModes.InsertIgnore, "Insert Ignore Into")]
    [InlineData(SaveModes.Replace, "Replace Into")]
    public void BuilderSql(SaveModes mode, String prefix)
    {
        var table = DAL.CreateTable();
        table.TableName = "sm_user";
        var id = table.CreateColumn();
        id.Name = "ID";
        id.ColumnName = "ID";
        id.DataType = typeof(Int32);
        id.PrimaryKey = true;
        table.Columns.Add(id);
        var name = table.CreateColumn();
        name.Name = "Name";
        name.ColumnName = "Name";
        name.DataType = typeof(String);
        name.Length = 50;
        table.Columns.Add(name);

        var builder = new InsertBuilder { Mode = mode };
        var sql = builder.GetSql(_dal.Db, table, table.Columns.ToArray(), new SmModel(1, "Stone"));
        Assert.NotNull(sql);
        Assert.StartsWith(prefix + " ", sql);
    }

    [Fact(DisplayName = "SQLite Insert 插入数据")]
    public void SqliteInsert()
    {
        var table = CreateTable();
        var columns = table.Columns.ToArray();

        var n = _dal.Session.Insert(table, columns, [new SmModel(1, "Stone"), new SmModel(2, "NewLife")]);
        Assert.Equal(2, n);

        var count = _dal.Session.QueryCount("select * from sm_user");
        Assert.Equal(2L, count);
    }

    [Fact(DisplayName = "SQLite InsertIgnore 重复主键跳过")]
    public void SqliteInsertIgnore()
    {
        var table = CreateTable();
        var columns = table.Columns.ToArray();

        _dal.Session.Insert(table, columns, [new SmModel(1, "Stone")]);

        // 主键1已存在被忽略，主键3插入成功
        var n = _dal.Session.InsertIgnore(table, columns, [new SmModel(1, "Ignore"), new SmModel(3, "NewLife")]);
        Assert.Equal(1, n);

        var count = _dal.Session.QueryCount("select * from sm_user");
        Assert.Equal(2L, count);

        Assert.Equal("Stone", _dal.QuerySingle<String>("select Name from sm_user where ID=1"));
        Assert.Equal("NewLife", _dal.QuerySingle<String>("select Name from sm_user where ID=3"));
    }

    [Fact(DisplayName = "SQLite Replace 重复主键整行替换")]
    public void SqliteReplace()
    {
        var table = CreateTable();
        var columns = table.Columns.ToArray();

        _dal.Session.Insert(table, columns, [new SmModel(1, "Stone", 10)]);

        // 主键1已存在，整行替换
        var n = _dal.Session.Replace(table, columns, [new SmModel(1, "Replace", 20)]);
        Assert.Equal(1, n);

        var count = _dal.Session.QueryCount("select * from sm_user");
        Assert.Equal(1L, count);

        Assert.Equal("Replace", _dal.QuerySingle<String>("select Name from sm_user where ID=1"));
        Assert.Equal(20, _dal.QuerySingle<Int32>("select Num from sm_user where ID=1"));
    }

    [Fact(DisplayName = "SQLite Upsert 混合插入更新")]
    public void SqliteUpsert()
    {
        var table = CreateTable();
        var columns = table.Columns.ToArray();

        _dal.Session.Insert(table, columns, [new SmModel(1, "Stone", 10)]);

        // 主键1更新Name，主键2插入
        var n = _dal.Session.Upsert(table, columns, ["Name"], null, [new SmModel(1, "Upsert", 99), new SmModel(2, "NewLife", 20)]);
        Assert.Equal(2, n);

        var count = _dal.Session.QueryCount("select * from sm_user");
        Assert.Equal(2L, count);
        Assert.Equal("Upsert", _dal.QuerySingle<String>("select Name from sm_user where ID=1"));
        Assert.Equal(10, _dal.QuerySingle<Int32>("select Num from sm_user where ID=1"));
        Assert.Equal("NewLife", _dal.QuerySingle<String>("select Name from sm_user where ID=2"));
    }

    [Fact(DisplayName = "SQLite Upsert 增量更新列")]
    public void SqliteUpsertAdd()
    {
        var table = CreateTable();
        var columns = table.Columns.ToArray();

        _dal.Session.Insert(table, columns, [new SmModel(1, "Stone", 10)]);

        // 主键1已存在，Num 增量累加
        var n = _dal.Session.Upsert(table, columns, null, ["Num"], [new SmModel(1, "Stone", 5)]);
        Assert.Equal(1, n);

        Assert.Equal(15, _dal.QuerySingle<Int32>("select Num from sm_user where ID=1"));
    }
}
