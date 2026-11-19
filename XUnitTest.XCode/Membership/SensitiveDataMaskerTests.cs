using System;
using System.Linq;
using XCode;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode;

/// <summary>审计日志敏感数据掩码测试</summary>
public class SensitiveDataMaskerTests
{
    /// <summary>暴露明细构造的测试日志提供者，避免写入数据库</summary>
    private class TestLogProvider : LogProvider
    {
        public String BuildRemark(String action, IEntity entity) => GetEntityRemark(action, entity);
    }

    [Fact(DisplayName = "JSON报文中的密钥字段被掩码")]
    public void Redact_JsonSecret()
    {
        var remark = "{\"Code\":\"StarServer\",\"Secret\":\"$md5$1793198435$93FCA6D1E7CF4C414E776F6B785EFCE6\",\"ClientId\":\"10.0.0.1@1234\"}";

        var rs = SensitiveDataMasker.Redact(remark);

        Assert.DoesNotContain("93FCA6D1", rs);
        Assert.Contains("\"Secret\":\"***\"", rs);
        Assert.Contains("\"Code\":\"StarServer\"", rs);
    }

    [Fact(DisplayName = "查询串中的密钥字段被掩码")]
    public void Redact_QueryStringSecret()
    {
        var rs = SensitiveDataMasker.Redact("login?code=node01&secret=abc123&clientId=10.0.0.1@1234");

        Assert.DoesNotContain("abc123", rs);
        Assert.Contains("secret=***", rs);
        Assert.Contains("code=node01", rs);
    }

    [Fact(DisplayName = "查询串中的下划线令牌字段被掩码")]
    public void Redact_QueryStringAccessToken()
    {
        var rs = SensitiveDataMasker.Redact("callback?access_token=eyJhbGciOiJIUzI1NiJ9.payload.sig&state=abc");

        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9", rs);
        Assert.Contains("access_token=***", rs);
        Assert.Contains("state=abc", rs);
    }

    [Fact(DisplayName = "JSON报文中的密码与令牌字段被掩码")]
    public void Redact_PasswordAndToken()
    {
        var rs = SensitiveDataMasker.Redact("{\"password\":\"p@ss\",\"access_token\":\"tok\",\"appSecret\":\"sec\",\"api_key\":\"k\"}");

        Assert.DoesNotContain("p@ss", rs);
        Assert.DoesNotContain("\"tok\"", rs);
        Assert.DoesNotContain("\"sec\"", rs);
        Assert.DoesNotContain("\"k\"", rs);
    }

    [Fact(DisplayName = "普通文本不改变")]
    public void Redact_PlainText()
    {
        const String remark = "Secret validation failed. [node01/10.0.0.1] login at 2026-11-14 20:00:00";

        Assert.Equal(remark, SensitiveDataMasker.Redact(remark));
    }

    [Fact(DisplayName = "空备注原样返回")]
    public void Redact_NullOrEmpty()
    {
        Assert.Null(SensitiveDataMasker.Redact(null));
        Assert.Equal("", SensitiveDataMasker.Redact(""));
    }

    [Theory(DisplayName = "凭据字段名识别")]
    [InlineData("Password", true)]
    [InlineData("pwd", true)]
    [InlineData("pass", true)]
    [InlineData("Secret", true)]
    [InlineData("app_secret", true)]
    [InlineData("AppSecret", true)]
    [InlineData("UserToken", true)]
    [InlineData("AccessToken", true)]
    [InlineData("access-token", true)]
    [InlineData("refresh_token", true)]
    [InlineData("ApiKey", true)]
    [InlineData("PrivateKey", true)]
    [InlineData("TokenCount", false)]
    [InlineData("Key", false)]
    [InlineData("Sign", false)]
    [InlineData("Remark", false)]
    public void IsSensitiveName_Cases(String name, Boolean expected) => Assert.Equal(expected, SensitiveDataMasker.IsSensitiveName(name));

    [Theory(DisplayName = "口令字段名识别")]
    [InlineData("Password", true)]
    [InlineData("old_pwd", true)]
    [InlineData("pass", true)]
    [InlineData("bypass", false)]
    [InlineData("AppSecret", false)]
    public void IsPasswordName_Cases(String name, Boolean expected) => Assert.Equal(expected, SensitiveDataMasker.IsPasswordName(name));

    [Fact(DisplayName = "实体字段按名称与只写标记识别")]
    public void IsSensitiveField_Cases()
    {
        var fields = User.Meta.Factory.Fields;

        Assert.True(SensitiveDataMasker.IsSensitiveField(fields.First(e => e.Name == "Password")));
        Assert.False(SensitiveDataMasker.IsSensitiveField(fields.First(e => e.Name == "Name")));
    }

    [Fact(DisplayName = "部分模式下密钥保留头尾")]
    public void Redact_PartialKeepsHeadAndTail()
    {
        var rs = SensitiveDataMasker.Redact("{\"Secret\":\"$md5$1793198435$93FCA6D1E7CF4C414E776F6B785EFCE6\"}", LogMaskModes.Partial);

        Assert.Contains("\"Secret\":\"$md5****FCE6\"", rs);
    }

    [Fact(DisplayName = "部分模式下查询串密钥保留头尾")]
    public void Redact_PartialQueryString()
    {
        var rs = SensitiveDataMasker.Redact("secret=abcdefghijkl&code=node01", LogMaskModes.Partial);

        Assert.Contains("secret=abcd****ijkl", rs);
        Assert.Contains("code=node01", rs);
    }

    [Fact(DisplayName = "部分模式下的短值与口令仍然全掩")]
    public void Redact_PartialShortValueAndPassword()
    {
        var rs = SensitiveDataMasker.Redact("{\"token\":\"abc12345\",\"password\":\"abcdefghijkl\"}", LogMaskModes.Partial);

        Assert.Contains("\"token\":\"***\"", rs);
        Assert.Contains("\"password\":\"***\"", rs);
    }

    [Fact(DisplayName = "部分模式下含转义序列的密钥全掩")]
    public void Redact_PartialJsonEscape()
    {
        var rs = SensitiveDataMasker.Redact("{\"Secret\":\"abc\\\"defghijklmn\"}", LogMaskModes.Partial);

        Assert.Contains("\"Secret\":\"***\"", rs);
    }

    [Fact(DisplayName = "仅口令模式不掩码密钥与令牌")]
    public void Redact_PasswordOnly()
    {
        var rs = SensitiveDataMasker.Redact("{\"Secret\":\"plain-secret-value\",\"access_token\":\"plain-token\",\"Password\":\"p@ssw0rd\"}", LogMaskModes.PasswordOnly);

        Assert.Contains("\"plain-secret-value\"", rs);
        Assert.Contains("\"plain-token\"", rs);
        Assert.Contains("\"Password\":\"***\"", rs);
    }

    [Fact(DisplayName = "关闭脱敏时备注原样返回")]
    public void Redact_Off()
    {
        const String remark = "{\"Secret\":\"abc\",\"Password\":\"p@ss\"}";

        Assert.Equal(remark, SensitiveDataMasker.Redact(remark, LogMaskModes.Off));
    }

    [Fact(DisplayName = "日志脱敏配置默认完全掩码")]
    public void Setting_LogMaskModeByDefault() => Assert.Equal(LogMaskModes.Full, new XCodeSetting().LogMaskMode);

    [Fact(DisplayName = "实体日志明细中的口令字段被掩码")]
    public void EntityRemark_MasksPassword()
    {
        var user = new User { Id = 9, Name = "tester", Password = "p@ssw0rd" };

        var remark = new TestLogProvider().BuildRemark("Insert", user);

        Assert.DoesNotContain("p@ssw0rd", remark);
        Assert.Contains("***", remark);
    }

    [Fact(DisplayName = "实体字段按键名与模式掩码")]
    public void MaskField_Cases()
    {
        var password = User.Meta.Factory.Fields.First(e => e.Name == "Password");

        Assert.Equal("***", SensitiveDataMasker.MaskField(password, "p@ssw0rd", LogMaskModes.Partial));
        Assert.Equal("***", SensitiveDataMasker.MaskField(password, "p@ssw0rd", LogMaskModes.PasswordOnly));
        Assert.Equal("p@ssw0rd", SensitiveDataMasker.MaskField(password, "p@ssw0rd", LogMaskModes.Off));
    }

    [Fact(DisplayName = "写日志时备注中的密钥被掩码")]
    public void CreateLog_RedactsRemark()
    {
        var log = new LogProvider().CreateLog("Test", "Login", true, "{\"Secret\":\"abc\"}");

        Assert.DoesNotContain("\"abc\"", log.Remark);
        Assert.Contains("\"Secret\":\"***\"", log.Remark);
    }
}
