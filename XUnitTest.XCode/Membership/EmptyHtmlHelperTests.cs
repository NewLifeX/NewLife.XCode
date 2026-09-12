using System;
using NewLife;
using NewLife.Security;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Membership;

/// <summary>空富文本清理测试。覆盖判定逻辑与用户实体保存时的自动清理，操作共享 Membership 库需与其他数据库测试串行</summary>
[Collection("Database")]
public class EmptyHtmlHelperTests
{
    [Theory(DisplayName = "判断空富文本：仅空段落/换行/空白视为空")]
    [InlineData("<p><br></p>", true)]
    [InlineData("<p><br/></p>", true)]
    [InlineData("<p>&nbsp;</p>", true)]
    [InlineData("<p></p>", true)]
    [InlineData("<p>hello</p>", false)]
    [InlineData("<p><img src=\"a.png\" /></p>", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsEmptyHtmlTest(String? html, Boolean expected)
    {
        Assert.Equal(expected, EmptyHtmlHelper.IsEmptyHtml(html));
    }

    [Fact(DisplayName = "保存用户时自动清空空富文本备注")]
    public void ClearEmptyHtmlRemark()
    {
        var user = new User
        {
            Name = Rand.NextString(16),
            Remark = "<p><br></p>",
        };
        user.Insert();

        // 插入时即被清理
        var user2 = User.FindByKey(user.ID);
        Assert.True(user2.Remark.IsNullOrEmpty());

        // 绕过实体直接写库制造脏值，再通过更新触发清理
        User.Update($"Remark='<p><br></p>'", $"ID={user.ID}");
        user2 = User.FindByKey(user.ID);
        Assert.Equal("<p><br></p>", user2.Remark);

        user2.Update();

        var user3 = User.FindByKey(user.ID);
        Assert.True(user3.Remark.IsNullOrEmpty());
    }
}
