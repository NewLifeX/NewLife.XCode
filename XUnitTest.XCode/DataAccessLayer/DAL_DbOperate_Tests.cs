using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using NewLife.Data;
using NewLife.Security;
using XCode;
using XCode.DataAccessLayer;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

public class DAL_DbOperate_Tests
{
    public DAL_DbOperate_Tests()
    {
        EntityFactory.InitEntity(typeof(User));
    }

    [Fact]
    public void PageSplit()
    {
        var dal = User.Meta.Session.Dal;

        var sql = "select * from user where name=@name order by id";
        var sb = new SelectBuilder(sql);
        var rs = dal.PageSplit(sb, 5, 7);
        Assert.NotNull(rs);

        var sql2 = rs.ToString();
        Assert.Equal("Select * From user Where name=@name Order By id limit 5, 7", sql2);

        rs = dal.PageSplit(sb, 0, 7);
        sql2 = rs.ToString();
        Assert.Equal("Select * From user Where name=@name Order By id limit 7", sql2);

        rs = dal.PageSplit(sb, -1, 7);
        sql2 = rs.ToString();
        Assert.Equal("Select * From user Where name=@name Order By id limit 7", sql2);

        Assert.ThrowsAny<NotSupportedException>(() => rs = dal.PageSplit(sb, 1, -7));

        rs = dal.PageSplit(sb, -1, -7);
        sql2 = rs.ToString();
        Assert.Equal("Select * From user Where name=@name Order By id", sql2);
    }

    [Fact]
    public void Select()
    {
        var dal = User.Meta.Session.Dal;

        var ds = dal.Select("select * from user");
        Assert.NotNull(ds);
        Assert.True(ds.Tables[0].Rows.Count > 0);

        var sb = new SelectBuilder("select * from user");
        ds = dal.Select(sb, 55, 7);
        Assert.NotNull(ds);
        Assert.True(ds.Tables[0].Rows.Count == 0);

        var p = dal.Db.CreateParameter("name", "admin");
        ds = dal.Select("select * from user where name=@name", CommandType.Text, p);
        Assert.NotNull(ds);
        Assert.True(ds.Tables[0].Rows.Count == 1);

        ds = dal.Select("select * from user where name=@name", CommandType.Text, new { name = "admin" }.ToDictionary());
        Assert.NotNull(ds);
        Assert.True(ds.Tables[0].Rows.Count == 1);
    }

    [Fact]
    public void SelectCount()
    {
        var dal = User.Meta.Session.Dal;

        var count = dal.SelectCount("select * from user", CommandType.Text);
        Assert.True(count > 0);

        var p = dal.Db.CreateParameter("name", "admin");
        count = dal.SelectCount("select * from user where name=@name", CommandType.Text, p);
        Assert.True(count > 0);

        var sb = new SelectBuilder("select * from user");
        count = dal.SelectCount(sb);
        Assert.True(count > 0);
    }

    [Fact]
    public void Query()
    {
        var dal = User.Meta.Session.Dal;

        var dt = dal.Query("select * from user");
        Assert.NotNull(dt);
        Assert.True(dt.Rows.Count > 0);

        dt = dal.Query("select * from user where name=@name", new { name = "admin" }.ToDictionary());
        Assert.NotNull(dt);
        Assert.Single(dt.Rows);

        dt = dal.Query("select * from user where name=@name1234", new { name1234 = "admin" }.ToDictionary());
        Assert.NotNull(dt);
        Assert.Single(dt.Rows);

        var sb = new SelectBuilder("select * from user");
        dt = dal.Query(sb, 55, 7);
        Assert.NotNull(dt);
        Assert.True(dt.Rows.Count == 0);
    }

    [Fact]
    public void Execute()
    {
        var dal = User.Meta.Session.Dal;

        var id = dal.InsertAndGetIdentity($"insert into user(name) values('{Rand.NextString(8)}')");
        Assert.True(id > 0);

        var rs = dal.Execute("update user set enable=1 where id=" + id);
        Assert.True(rs > 0);

        rs = dal.Execute("delete from user where id=" + id);
        Assert.True(rs > 0);
    }

    [Fact]
    public void ExecuteWithParameter()
    {
        var dal = User.Meta.Session.Dal;

        var p = dal.Db.CreateParameter("name", Rand.NextString(8));
        var id = dal.InsertAndGetIdentity("insert into user(name) values(@name)", CommandType.Text, p);
        Assert.True(id > 0);

        var ps = dal.Db.CreateParameter("id", id);
        var rs = dal.Execute("update user set enable=1 where id=@id", CommandType.Text, ps);
        Assert.True(rs > 0);

        rs = dal.Execute("update user set code=@code where id=@id", CommandType.Text, new { id, code = Rand.NextString(8) }.ToDictionary());
        Assert.True(rs > 0);

        rs = dal.Execute("delete from user where id=@id", CommandType.Text, ps);
        Assert.True(rs > 0);
    }

    [Fact]
    public void ExecuteWithTimeout()
    {
        var dal = User.Meta.Session.Dal;

        var rs = dal.Execute("delete from user where id>=10", 15);
        Assert.True(rs >= 0);

        rs = dal.Execute("delete from user where id>=10", 0);
        Assert.True(rs >= 0);
    }

    [Fact]
    public void ExecuteScalar()
    {
        var dal = User.Meta.Session.Dal;

        var id = dal.ExecuteScalar<Int64>("select id from user where name=@name", CommandType.Text, new { name = "admin" }.ToDictionary());
        Assert.True(id > 0);

        id = dal.ExecuteScalar<Int64>("select id from user", CommandType.Text, null);
        Assert.True(id > 0);
    }

    //[Fact]
    //public async void SelectAsync()
    //{
    //    var dal = User.Meta.Session.Dal;

    //    var ds = await dal.SelectAsync("select * from user");
    //    Assert.NotNull(ds);
    //    Assert.True(ds.Tables[0].Rows.Count > 0);

    //    var sb = new SelectBuilder("select * from user");
    //    ds = await dal.SelectAsync(sb, 55, 7);
    //    Assert.NotNull(ds);
    //    Assert.True(ds.Tables[0].Rows.Count == 0);

    //    var p = dal.Db.CreateParameter("name", "admin");
    //    ds = await dal.SelectAsync("select * from user where name=@name", CommandType.Text, p);
    //    Assert.NotNull(ds);
    //    Assert.True(ds.Tables[0].Rows.Count == 1);

    //    ds = await dal.SelectAsync("select * from user where name=@name", CommandType.Text, new { name = "admin" }.ToDictionary());
    //    Assert.NotNull(ds);
    //    Assert.True(ds.Tables[0].Rows.Count == 1);
    //}

    [Fact]
    public async void SelectCountAsync()
    {
        var dal = User.Meta.Session.Dal;

        var count = await dal.SelectCountAsync("select * from user", CommandType.Text);
        Assert.True(count > 0);

        var p = dal.Db.CreateParameter("name", "admin");
        count = await dal.SelectCountAsync("select * from user where name=@name", CommandType.Text, p);
        Assert.True(count > 0);

        var sb = new SelectBuilder("select * from user");
        count = await dal.SelectCountAsync(sb);
        Assert.True(count > 0);
    }

    [Fact]
    public async void QueryAsync()
    {
        var dal = User.Meta.Session.Dal;

        var dt = await dal.QueryAsync("select * from user");
        Assert.NotNull(dt);
        Assert.True(dt.Rows.Count > 0);

        dt = await dal.QueryAsync("select * from user where name=@name", new { name = "admin" }.ToDictionary());
        Assert.NotNull(dt);
        Assert.Single(dt.Rows);

        dt = await dal.QueryAsync("select * from user where name=@name1234", new { name1234 = "admin" }.ToDictionary());
        Assert.NotNull(dt);
        Assert.Single(dt.Rows);

        var sb = new SelectBuilder("select * from user");
        dt = await dal.QueryAsync(sb, 55, 7);
        Assert.NotNull(dt);
        Assert.True(dt.Rows.Count == 0);
    }

    [Fact]
    public async void ExecuteAsync()
    {
        var dal = User.Meta.Session.Dal;

        var id = await dal.InsertAndGetIdentityAsync($"insert into user(name) values('{Rand.NextString(8)}')");
        Assert.True(id > 0);

        var rs = await dal.ExecuteAsync("update user set enable=1 where id=" + id);
        Assert.True(rs > 0);

        rs = await dal.ExecuteAsync("delete from user where id=" + id);
        Assert.True(rs > 0);
    }

    [Fact]
    public async void ExecuteAsyncWithParameter()
    {
        var dal = User.Meta.Session.Dal;

        var p = dal.Db.CreateParameter("name", Rand.NextString(8));
        var id = await dal.InsertAndGetIdentityAsync("insert into user(name) values(@name)", CommandType.Text, p);
        Assert.True(id > 0);

        var ps = dal.Db.CreateParameter("id", id);
        var rs = await dal.ExecuteAsync("update user set enable=1 where id=@id", CommandType.Text, ps);
        Assert.True(rs > 0);

        rs = await dal.ExecuteAsync("update user set code=@code where id=@id", CommandType.Text, new { id, code = Rand.NextString(8) }.ToDictionary());
        Assert.True(rs > 0);

        rs = await dal.ExecuteAsync("delete from user where id=@id", CommandType.Text, ps);
        Assert.True(rs > 0);
    }

    [Fact]
    public async void ExecuteAsyncWithTimeout()
    {
        var dal = User.Meta.Session.Dal;

        var rs = await dal.ExecuteAsync("delete from user where id>=10", 15);
        Assert.True(rs >= 0);

        rs = await dal.ExecuteAsync("delete from user where id>=10", 0);
        Assert.True(rs >= 0);
    }

    [Fact]
    public async void ExecuteScalarAsync()
    {
        var dal = User.Meta.Session.Dal;

        var id = await dal.ExecuteScalarAsync<Int64>("select id from user where name=@name", CommandType.Text, new { name = "admin" }.ToDictionary());
        Assert.True(id > 0);

        id = await dal.ExecuteScalarAsync<Int64>("select id from user", CommandType.Text, null);
        Assert.True(id > 0);
    }
}