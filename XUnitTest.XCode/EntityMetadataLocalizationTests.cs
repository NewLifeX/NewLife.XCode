using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using NewLife;
using XCode;
using Xunit;

namespace XUnitTest.XCode;

/// <summary>内置实体元数据词表测试</summary>
[Collection("Localization")]
public class EntityMetadataLocalizationTests
{
    private const String UserDescription = "用户。用户帐号信息，以身份验证为中心，拥有多种角色，可加入多个租户";

    [Fact(DisplayName = "内置实体中文元数据可按显示名与描述翻译")]
    public void MembershipMetadata()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(XCodeSetting).Module.ModuleHandle);

        // 不变全球化模式下具名区域性不可用，译文按英文兜底
        try
        {
            if (CultureInfo.GetCultureInfo("zh-Hans").Name != "zh-Hans") return;
        }
        catch (CultureNotFoundException)
        {
            return;
        }

        var saved = Localization.Culture;
        try
        {
            Localization.Culture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("User", Localization.Translate("用户"));
            Assert.Equal("Name", Localization.Translate("名称"));
            Assert.Equal("View", Localization.Translate("查看"));
            Assert.Equal("User. User account information, centered on authentication, has multiple roles and can join multiple tenants", Localization.Translate(UserDescription));

            Localization.Culture = CultureInfo.GetCultureInfo("zh-TW");
            Assert.Equal("使用者", Localization.Translate("用户"));
            Assert.Equal("使用者。使用者帳號資訊，以身分驗證為中心，擁有多種角色，可加入多個租戶", Localization.Translate(UserDescription));

            Localization.Culture = CultureInfo.GetCultureInfo("zh-CN");
            Assert.Equal("用户", Localization.Translate("用户"));
            Assert.Equal(UserDescription, Localization.Translate(UserDescription));
        }
        finally
        {
            Localization.Culture = saved;
        }
    }
}
