using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.Navigation;

/// <summary>SelectBuilder Join 测试</summary>
[Collection("Database")]
public class SelectBuilderJoinTests
{
    [Fact(DisplayName = "Join_Empty_NotInToString")]
    public void Join_Empty_NotInToString()
    {
        var sb = new SelectBuilder
        {
            Table = "User",
            Where = "Id>0",
        };

        var sql = sb.ToString();
        Assert.Contains("From User", sql);
        Assert.DoesNotContain("JOIN", sql);
    }

    [Fact(DisplayName = "Join_LeftJoin_InToString")]
    public void Join_LeftJoin_InToString()
    {
        var sb = new SelectBuilder
        {
            Table = "User",
            Where = "Id>0",
            Join = "LEFT JOIN Role ON User.RoleId = Role.Id",
        };

        var sql = sb.ToString();
        Assert.Contains("From User", sql);
        Assert.Contains("LEFT JOIN Role ON User.RoleId = Role.Id", sql);
    }

    [Fact(DisplayName = "Join_MultipleJoins_AllInToString")]
    public void Join_MultipleJoins_AllInToString()
    {
        var sb = new SelectBuilder
        {
            Table = "User",
            Where = "Id>0",
            Join = "LEFT JOIN Role r ON User.RoleId = r.Id LEFT JOIN Dept d ON User.DeptId = d.Id",
        };

        var sql = sb.ToString();
        Assert.Contains("LEFT JOIN Role r", sql);
        Assert.Contains("LEFT JOIN Dept d", sql);
    }

    [Fact(DisplayName = "Clone_CopiesJoin")]
    public void Clone_CopiesJoin()
    {
        var sb = new SelectBuilder
        {
            Table = "User",
            Join = "LEFT JOIN Role ON User.RoleId = Role.Id",
        };

        var clone = sb.Clone();
        Assert.Equal("LEFT JOIN Role ON User.RoleId = Role.Id", clone.Join);
    }
}
