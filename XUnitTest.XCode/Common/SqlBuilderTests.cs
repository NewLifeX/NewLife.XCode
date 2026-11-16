using System.Globalization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode;

[Collection("Localization")]
public class SqlBuilderTests
{
    [Fact]
    public void BuildOrder()
    {
        var factory = User.Meta.Factory;

        // 使用Sort，此时Desc有效
        var page = new PageParameter { Sort = "Name", Desc = false };
        var orderby = SqlBuilder.BuildOrder(page, factory);
        Assert.Equal("Name", orderby);

        page = new PageParameter { Sort = "Name", Desc = true };
        orderby = SqlBuilder.BuildOrder(page, factory);
        Assert.Equal("Name Desc", orderby);

        page = new PageParameter { Sort = "Name,Code", Desc = false };
        orderby = SqlBuilder.BuildOrder(page, factory);
        Assert.Equal("Name,Code", orderby);

        page = new PageParameter { Sort = "Name,Code", Desc = true };
        orderby = SqlBuilder.BuildOrder(page, factory);
        Assert.Equal("Name Desc,Code Desc", orderby);

        page = new PageParameter { Sort = "Name desc\n,\nCode", Desc = false };
        orderby = SqlBuilder.BuildOrder(page, factory);
        Assert.Equal("Name Desc,Code", orderby);

        page = new PageParameter { Sort = "Name desc\n,\nCode", Desc = true };
        orderby = SqlBuilder.BuildOrder(page, factory);
        Assert.Equal("Name Desc,Code Desc", orderby);

        page = new PageParameter { Sort = "name asc\n,\ncode", Desc = true };
        orderby = SqlBuilder.BuildOrder(page, factory);
        Assert.Equal("Name,Code Desc", orderby);

        page = new PageParameter { Sort = "name2 asc\n,\ncode", Desc = true };
        var savedCulture = Localization.Culture;
        try
        {
            // 异常消息在构造时按当前语言翻译，必须在触发异常前切换语言
            if (!LocalizationTestHelper.IsInvariantGlobalization) Localization.Culture = CultureInfo.GetCultureInfo("zh-Hans");

            var ex = Assert.Throws<XCodeException>(() => SqlBuilder.BuildOrder(page, factory));
            if (!LocalizationTestHelper.IsInvariantGlobalization) Assert.Equal("实体类[User]不包含排序字段[name2]", ex.Message);
        }
        finally
        {
            Localization.Culture = savedCulture;
        }
    }

    [Fact(DisplayName = "排序子句只接受字段名与升降序标记")]
    public void GetOrderBy_ValidatesInput()
    {
        var page = new PageParameter { Sort = "Name" };
        Assert.Equal("Name", page.GetOrderBy());

        page = new PageParameter { Sort = "Name", Desc = true };
        Assert.Equal("Name Desc", page.GetOrderBy());

        page = new PageParameter { Sort = "name desc\n,\nCode" };
        Assert.Equal("name desc\n,\nCode", page.GetOrderBy());

        page = new PageParameter { OrderBy = "t.Name Asc, [Code] Desc" };
        Assert.Equal("t.Name Asc, [Code] Desc", page.GetOrderBy());

        // 子查询、注释、分号与函数调用一律拒绝，防止排序位注入
        foreach (var orderby in new[] { "Id; drop table user", "(select 1)", "Id --", "Id/*x*/", "rand()" })
        {
            page = new PageParameter { Sort = orderby };
            Assert.Throws<XCodeException>(() => page.GetOrderBy());

            page = new PageParameter { OrderBy = orderby };
            Assert.Throws<XCodeException>(() => page.GetOrderBy());
        }
    }

    [Fact]
    public void BuildOrder2()
    {
        var factory = User.Meta.Factory;

        // 使用OrderBy，此时Desc无效
        var page = new PageParameter { OrderBy = "Name", Desc = false };
        var orderby = SqlBuilder.BuildOrder(page, factory);
        Assert.Equal("Name", orderby);

        //page = new PageParameter { OrderBy = "Name", Desc = true };
        //orderby = SqlBuilder.BuildOrder(page, factory);
        //Assert.Equal("Name Desc", orderby);

        page = new PageParameter { OrderBy = "Name,Code", Desc = false };
        orderby = SqlBuilder.BuildOrder(page, factory);
        Assert.Equal("Name,Code", orderby);

        page = new PageParameter { OrderBy = "Name,Code", Desc = true };
        orderby = SqlBuilder.BuildOrder(page, factory);
        Assert.Equal("Name,Code", orderby);

        //page = new PageParameter { OrderBy = "Name desc\n,\nCode", Desc = false };
        //orderby = SqlBuilder.BuildOrder(page, factory);
        //Assert.Equal("Name Desc,Code", orderby);

        //page = new PageParameter { OrderBy = "Name desc\n,\nCode", Desc = true };
        //orderby = SqlBuilder.BuildOrder(page, factory);
        //Assert.Equal("Name Desc,Code", orderby);

        //page = new PageParameter { OrderBy = "name asc\n,\ncode", Desc = true };
        //orderby = SqlBuilder.BuildOrder(page, factory);
        //Assert.Equal("Name,Code", orderby);

        //page = new PageParameter { OrderBy = "name2 asc\n,\ncode", Desc = true };
        //var ex = Assert.Throws<XCodeException>(() => SqlBuilder.BuildOrder(page, factory));
        //Assert.Equal("实体类[User]不包含排序字段[name2]", ex.Message);
    }
}
