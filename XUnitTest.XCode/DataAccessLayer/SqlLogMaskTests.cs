using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

[Collection("Database")]
public class SqlLogMaskTests
{
    [Fact(DisplayName = "SQL 日志按字段名脱敏凭据参数")]
    public void GetSql_MasksCredentialParameters()
    {
        var dal = DAL.Create("test");
        var ss = (DbSession)dal.Session;
        using var cmd = ss.CreateCommand("Update User Set Password=@Password, Name=@Name");
        cmd.Parameters.Add(dal.Db.CreateParameter("@Password", "secret123"));
        cmd.Parameters.Add(dal.Db.CreateParameter("@Name", "Stone"));

        var sql = ss.GetSql(cmd);

        Assert.NotNull(sql);
        Assert.Contains("Password=***", sql);
        Assert.Contains("Name=Stone", sql);
        Assert.DoesNotContain("secret123", sql);
    }

    [Fact(DisplayName = "关闭 SQL 日志脱敏后保留原值")]
    public void GetSql_MaskDisabled_KeepsValue()
    {
        var setting = XCodeSetting.Current;
        var old = setting.MaskSqlLog;
        setting.MaskSqlLog = false;
        try
        {
            var dal = DAL.Create("test");
            var ss = (DbSession)dal.Session;
            using var cmd = ss.CreateCommand("Update User Set Password=@Password");
            cmd.Parameters.Add(dal.Db.CreateParameter("@Password", "secret123"));

            var sql = ss.GetSql(cmd);

            Assert.NotNull(sql);
            Assert.Contains("secret123", sql);
        }
        finally
        {
            setting.MaskSqlLog = old;
        }
    }
}
