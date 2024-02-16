using NewLife.Data;
using XCode;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode;

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
        var ex = Assert.Throws<XCodeException>(() => SqlBuilder.BuildOrder(page, factory));
        Assert.Equal("实体类[User]不包含排序字段[name2]", ex.Message);
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
