using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using NewLife;
using XCode;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode;

/// <summary>实体审计日志明细的本地化测试</summary>
/// <remarks>
/// 日志明细的字段名原先取属性名（DisplayName/Online 等英文标识），中文界面下仍然显示英文。
/// 现改为取字段元数据显示名并经 L.T 翻译：源文为中文时中文界面原样输出、英文界面回退英文，
/// 源文为英文时由各应用词表提供译文。主键保留属性名，保持 Id=xxx 的机器可读约定。
/// </remarks>
[Collection("Localization")]
public class LogProviderRemarkTests
{
    /// <summary>暴露明细构造的测试日志提供者，避免写入数据库</summary>
    private class TestLogProvider : LogProvider
    {
        public String BuildRemark(String action, IEntity entity, String error = null) => GetEntityRemark(action, entity, error);
    }

    [Fact(DisplayName = "实体日志明细的字段名按当前语言取显示名")]
    public void Remark_UsesLocalizedDisplayName()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(XCodeSetting).Module.ModuleHandle);

        // 不变全球化模式下具名区域性不可用，跳过
        try
        {
            if (CultureInfo.GetCultureInfo("zh-Hans").Name != "zh-Hans") return;
        }
        catch (CultureNotFoundException)
        {
            return;
        }

        var user = new User { Id = 2, DisplayName = "测试", Online = true };
        var provider = new TestLogProvider();

        var saved = Localization.Culture;
        try
        {
            // 中文界面：元数据源文即中文，字段显示名不再是属性名
            Localization.Culture = CultureInfo.GetCultureInfo("zh-CN");
            var remark = provider.BuildRemark("Insert", user);
            Assert.Contains("Id=2", remark);
            Assert.Contains("昵称=测试", remark);
            Assert.Contains("在线=True", remark);
            Assert.DoesNotContain("DisplayName=", remark);
            Assert.DoesNotContain("Online=", remark);

            // 英文界面：同一个字段名走词表翻译
            Localization.Culture = CultureInfo.GetCultureInfo("en-US");
            var en = provider.BuildRemark("Insert", user);
            Assert.Contains("Nickname=测试", en);
            Assert.Contains("Online=True", en);
        }
        finally
        {
            Localization.Culture = saved;
        }
    }

    [Fact(DisplayName = "日志关联编号支持 Id= 前缀与多字段备注")]
    public void CreateLog_ParsesLinkId()
    {
        var provider = new LogProvider();

        // 明细以 Id= 开头，后跟逗号分隔的其它字段
        var log = provider.CreateLog("测试", "Insert", true, "Id=12,昵称=测试");
        Assert.Equal(12L, log.LinkId);
        Assert.Equal("Id=12,昵称=测试", log.Remark);

        // 兼容旧版本 ID= 前缀
        var legacy = provider.CreateLog("测试", "Insert", true, "ID=34,Name=old");
        Assert.Equal(34L, legacy.LinkId);

        // 无关联编号时不解析
        var none = provider.CreateLog("测试", "Insert", true, "Key=1");
        Assert.Equal(0L, none.LinkId);
    }
}
