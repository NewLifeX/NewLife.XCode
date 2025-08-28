using System;
using XCode.Configuration;
using Xunit;

namespace XUnitTest.XCode.Configuration;

public class ShowInOptionTests
{
    private static void AssertOption(ShowInOption opt, TriState list, TriState detail, TriState add, TriState edit, TriState search)
    {
        Assert.Equal(list, opt.List);
        Assert.Equal(detail, opt.Detail);
        Assert.Equal(add, opt.AddForm);
        Assert.Equal(edit, opt.EditForm);
        Assert.Equal(search, opt.Search);
    }

    [Fact(DisplayName = "默认/空字符串 -> 全Auto")]
    public void Parse_DefaultOrEmpty()
    {
        AssertOption(ShowInOption.Parse(null), TriState.Auto, TriState.Auto, TriState.Auto, TriState.Auto, TriState.Auto);
        AssertOption(ShowInOption.Parse(String.Empty), TriState.Auto, TriState.Auto, TriState.Auto, TriState.Auto, TriState.Auto);
        AssertOption(ShowInOption.Parse("   "), TriState.Auto, TriState.Auto, TriState.Auto, TriState.Auto, TriState.Auto);
    }

    [Fact(DisplayName = "管道语法-基础")]
    public void Parse_Pipe_Basic()
    {
        // 顺序：List|Detail|AddForm|EditForm|Search
        var opt = ShowInOption.Parse("Y|N|A||");
        AssertOption(opt, TriState.Show, TriState.Hide, TriState.Auto, TriState.Auto, TriState.Auto);

        var opt2 = ShowInOption.Parse("Y|Y|N||A");
        AssertOption(opt2, TriState.Show, TriState.Show, TriState.Hide, TriState.Auto, TriState.Auto);
    }

    [Fact(DisplayName = "管道语法-短段自动补齐")]
    public void Parse_Pipe_Short()
    {
        var opt = ShowInOption.Parse("Y|N");
        AssertOption(opt, TriState.Show, TriState.Hide, TriState.Auto, TriState.Auto, TriState.Auto);
    }

    [Fact(DisplayName = "管道语法-仅设置EditForm隐藏")]
    public void Parse_Pipe_OnlyEditHide()
    {
        var opt = ShowInOption.Parse("|||N|");
        AssertOption(opt, TriState.Auto, TriState.Auto, TriState.Auto, TriState.Hide, TriState.Auto);
    }

    [Fact(DisplayName = "掩码语法-基础(1/0/A/?/-)")]
    public void Parse_Mask_Basic()
    {
        // 掩码正则仅允许 1/0/A/?/-，不允许 Y/N
        var opt = ShowInOption.Parse("10A-?");
        AssertOption(opt, TriState.Show, TriState.Hide, TriState.Auto, TriState.Auto, TriState.Auto);

        var allAuto1 = ShowInOption.Parse("-----");
        var allAuto2 = ShowInOption.Parse("AAAAA");
        AssertOption(allAuto1, TriState.Auto, TriState.Auto, TriState.Auto, TriState.Auto, TriState.Auto);
        AssertOption(allAuto2, TriState.Auto, TriState.Auto, TriState.Auto, TriState.Auto, TriState.Auto);
    }

    [Fact(DisplayName = "具名列表-显式显示")]
    public void Parse_Named_Show()
    {
        var opt = ShowInOption.Parse("List,Search");
        AssertOption(opt, TriState.Show, TriState.Auto, TriState.Auto, TriState.Auto, TriState.Show);
    }

    [Fact(DisplayName = "具名列表-显式隐藏")]
    public void Parse_Named_Hide()
    {
        var opt = ShowInOption.Parse("-EditForm,-Detail");
        AssertOption(opt, TriState.Auto, TriState.Hide, TriState.Auto, TriState.Hide, TriState.Auto);
    }

    [Fact(DisplayName = "具名列表-别名与大小写")]
    public void Parse_Named_Aliases_CaseInsensitive()
    {
        var opt = ShowInOption.Parse("l,S,ADD,EDIT,d");
        AssertOption(opt, TriState.Show, TriState.Show, TriState.Show, TriState.Show, TriState.Show);
    }

    [Fact(DisplayName = "具名列表-Form同时作用AddForm与EditForm")]
    public void Parse_Named_FormAffectsBothAddAndEdit()
    {
        var opt = ShowInOption.Parse("form");
        AssertOption(opt, TriState.Auto, TriState.Auto, TriState.Show, TriState.Show, TriState.Auto);
    }

    [Fact(DisplayName = "具名列表-宏All后覆盖")]
    public void Parse_Named_Macro_All_Then_Hide()
    {
        var opt = ShowInOption.Parse("All,-Detail");
        AssertOption(opt, TriState.Show, TriState.Hide, TriState.Show, TriState.Show, TriState.Show);
    }

    [Fact(DisplayName = "具名列表-宏None后覆盖")]
    public void Parse_Named_Macro_None_Then_Set()
    {
        var opt = ShowInOption.Parse("None,Search,Add");
        AssertOption(opt, TriState.Hide, TriState.Hide, TriState.Show, TriState.Hide, TriState.Show);
    }

    [Fact(DisplayName = "具名列表-宏Auto(等同不写)")]
    public void Parse_Named_Macro_Auto()
    {
        var opt = ShowInOption.Parse("Auto");
        AssertOption(opt, TriState.Auto, TriState.Auto, TriState.Auto, TriState.Auto, TriState.Auto);
    }

    [Fact(DisplayName = "具名列表-前缀!与+")]
    public void Parse_Named_Prefixes()
    {
        var hide = ShowInOption.Parse("!search");
        AssertOption(hide, TriState.Auto, TriState.Auto, TriState.Auto, TriState.Auto, TriState.Hide);

        var show = ShowInOption.Parse("+search");
        AssertOption(show, TriState.Auto, TriState.Auto, TriState.Auto, TriState.Auto, TriState.Show);
    }

    [Fact(DisplayName = "具名列表-顺序覆盖(后者生效)")]
    public void Parse_Named_OrderOverride()
    {
        var opt = ShowInOption.Parse("List,-List,List");
        AssertOption(opt, TriState.Show, TriState.Auto, TriState.Auto, TriState.Auto, TriState.Auto);
    }

    [Fact(DisplayName = "具名列表-空白与未知项忽略")]
    public void Parse_Named_Whitespace_Unknown_Ignored()
    {
        var opt = ShowInOption.Parse("  List ,  -Detail  ,  Unknown  ,  ");
        AssertOption(opt, TriState.Show, TriState.Hide, TriState.Auto, TriState.Auto, TriState.Auto);
    }
}