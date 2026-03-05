using System;
using XCode.Statistics;
using Xunit;

namespace XUnitTest.XCode.Statistics;

/// <summary>StatLevels枚举测试</summary>
public class StatLevelsTests
{
    [Fact(DisplayName = "StatLevels_All值为0")]
    public void All_IsZero()
    {
        Assert.Equal(0, (Int32)StatLevels.All);
    }

    [Fact(DisplayName = "StatLevels_Year值为1")]
    public void Year_IsOne()
    {
        Assert.Equal(1, (Int32)StatLevels.Year);
    }

    [Fact(DisplayName = "StatLevels_Month值为2")]
    public void Month_IsTwo()
    {
        Assert.Equal(2, (Int32)StatLevels.Month);
    }

    [Fact(DisplayName = "StatLevels_Day值为3")]
    public void Day_IsThree()
    {
        Assert.Equal(3, (Int32)StatLevels.Day);
    }

    [Fact(DisplayName = "StatLevels_Hour值为4")]
    public void Hour_IsFour()
    {
        Assert.Equal(4, (Int32)StatLevels.Hour);
    }

    [Fact(DisplayName = "StatLevels_Minute值为5")]
    public void Minute_IsFive()
    {
        Assert.Equal(5, (Int32)StatLevels.Minute);
    }

    [Fact(DisplayName = "StatLevels_Quarter值为11")]
    public void Quarter_IsEleven()
    {
        Assert.Equal(11, (Int32)StatLevels.Quarter);
    }

    [Theory(DisplayName = "StatLevels_所有值唯一")]
    [InlineData(StatLevels.All)]
    [InlineData(StatLevels.Year)]
    [InlineData(StatLevels.Month)]
    [InlineData(StatLevels.Day)]
    [InlineData(StatLevels.Hour)]
    [InlineData(StatLevels.Minute)]
    [InlineData(StatLevels.Quarter)]
    public void AllValues_AreDefined(StatLevels level)
    {
        Assert.True(Enum.IsDefined(typeof(StatLevels), level));
    }
}

/// <summary>StatModes枚举测试</summary>
public class StatModesTests
{
    [Fact(DisplayName = "StatModes_Max为1")]
    public void Max_IsOne()
    {
        Assert.Equal(1, (Int32)StatModes.Max);
    }

    [Fact(DisplayName = "StatModes_Min为2")]
    public void Min_IsTwo()
    {
        Assert.Equal(2, (Int32)StatModes.Min);
    }

    [Fact(DisplayName = "StatModes_Avg为3")]
    public void Avg_IsThree()
    {
        Assert.Equal(3, (Int32)StatModes.Avg);
    }

    [Fact(DisplayName = "StatModes_Sum为4")]
    public void Sum_IsFour()
    {
        Assert.Equal(4, (Int32)StatModes.Sum);
    }

    [Fact(DisplayName = "StatModes_Count为5")]
    public void Count_IsFive()
    {
        Assert.Equal(5, (Int32)StatModes.Count);
    }
}
