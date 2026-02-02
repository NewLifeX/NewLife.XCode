using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using NewLife;
using NewLife.Log;
using XCode.DataAccessLayer;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

[Collection("Database")]
public class DALTests
{
    [Fact]
    public void LoadConfig()
    {
        var ds = new ConcurrentDictionary<String, DbInfo>(StringComparer.OrdinalIgnoreCase);

        DAL.LoadConfig(ds);

        Assert.True(ds.ContainsKey("MSSQL"));

        var di = ds["MSSQL"];
        Assert.Equal("MSSQL", di.Name);
        Assert.Equal("Data Source=.;Initial Catalog=master;Integrated Security=SSPI", di.ConnectionString);
        Assert.Equal("XCode.DataAccessLayer.SqlServer", di.Type.FullName);
        Assert.Equal("System.Data.SqlClient", di.Provider);
    }

    [Fact]
    public void LoadAppSettings()
    {
        var ds = new ConcurrentDictionary<String, DbInfo>(StringComparer.OrdinalIgnoreCase);
        DAL.LoadAppSettings("appsettings.json", ds);

        Assert.True(ds.ContainsKey("sqlserver"));

        var di = ds["sqlserver"];
        Assert.Equal("sqlserver", di.Name);
        Assert.Equal("Server=127.0.0.1;Database=Membership;Uid=root;Pwd=root;", di.ConnectionString);
        Assert.Equal("XCode.DataAccessLayer.SqlServer", di.Type.FullName);
        Assert.Equal("SqlServer", di.Provider);
    }

    [Fact]
    public void LoadAppSettings2()
    {
        var ds = new ConcurrentDictionary<String, DbInfo>(StringComparer.OrdinalIgnoreCase);
        DAL.LoadAppSettings("appsettings.json", ds);

        Assert.True(ds.ContainsKey("sqlite"));

        var di = ds["sqlite"];
        Assert.Equal("sqlite", di.Name);
        Assert.Equal("Data Source=Data\\Membership.db;provider=sqlite", di.ConnectionString);
        Assert.Equal("XCode.DataAccessLayer.SQLite", di.Type.FullName);
        Assert.Equal("sqlite", di.Provider);
    }

    [Fact]
    public void LoadAppSettingsWithProtected()
    {
        var ds = new ConcurrentDictionary<String, DbInfo>(StringComparer.OrdinalIgnoreCase);
        DAL.LoadAppSettings("appsettings.json", ds);

        Assert.True(ds.ContainsKey("MySqlWithProtected"));

        var dal = DAL.Create("MySqlWithProtected");
        Assert.Equal("KeyOfProtected", dal.ProtectedKey.Secret.ToStr());

        Assert.Equal("Server=.;Membership=mysql;Uid=root;Pwd=$AES$xpZ49nBk5UscLCFyCx_BUg;provider=mysql", dal.ConnStr);
        Assert.Equal("Server=127.0.0.1;Membership=mysql;Uid=root;Pwd=Pass@word;CharSet=utf8mb4", dal.Db.ConnectionString);
    }

    [Fact]
    public void LoadEnvironmentVariable()
    {
        var ds = new ConcurrentDictionary<String, DbInfo>(StringComparer.OrdinalIgnoreCase);
        var envs = Environment.GetEnvironmentVariables();
        envs.Add("XCode_pgsql", "Server=.;Database=master;Uid=root;Pwd=root;provider=PostgreSql");

        DAL.LoadEnvironmentVariable(ds, envs);

        Assert.True(ds.ContainsKey("pgsql"));

        var di = ds["pgsql"];
        Assert.Equal("pgsql", di.Name);
        Assert.Equal("Server=.;Database=master;Uid=root;Pwd=root;provider=PostgreSql", di.ConnectionString);
        Assert.Equal("XCode.DataAccessLayer.PostgreSQL", di.Type.FullName);
        Assert.Equal("PostgreSql", di.Provider);
    }

    [Fact]
    public void NullableType()
    {
        var type = typeof(Int32?);
        Assert.Equal(typeof(Int32?), type);
        Assert.NotEqual(typeof(Int32), type);

        var type2 = Nullable.GetUnderlyingType(type);
        Assert.Equal(typeof(Int32), type2);

        var type3 = Nullable.GetUnderlyingType(type2);
        Assert.Null(type3);
    }

    [Theory]
    [InlineData("select * from user", "user")]
    [InlineData("select * from 'user' where id=123", "user")]
    [InlineData("select * from `user` where id=123", "user")]
    [InlineData("select * from \"user\" where id=123", "user")]
    [InlineData("select * from [user] where id=123", "user")]
    [InlineData("select * from member.user", "user")]
    [InlineData("select * from `member`.'user' where id=123", "user")]
    [InlineData("select * from \"member\".`user` where id=123", "user")]
    [InlineData("select * from [member].\"user\" where id=123", "user")]
    [InlineData("select * from 'member'.[user] where id=123", "user")]
    [InlineData("insert into \"member\".`user`(xxx)", "user")]
    [InlineData("update \"member\".`user` set xxx", "user")]
    [InlineData("select * from 'member'.[user] left join data.[role] on user.roleid=role.id where id=123", "user,role")]
    [InlineData("truncate table \"member\".`user`", "user")]
    public void GetTables(String sql, String tableName)
    {
        var ts = DAL.GetTables(sql, false);
        Assert.NotNull(ts);

        var tables = tableName.Split(",");
        Assert.Equal(tables.Length, ts.Length);
        Assert.Equal(tableName, ts.Join(","));
    }
}