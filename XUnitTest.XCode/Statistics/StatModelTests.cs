using System;
using System.Collections.Generic;
using XCode.Statistics;
using Xunit;

namespace XUnitTest.XCode.Statistics;

/// <summary>StatModel统计模型测试</summary>
public class StatModelTests
{
    #region GetDate
    [Fact(DisplayName = "GetDate_All返回最小日期")]
    public void GetDate_All_ReturnsMinDate()
    {
        var model = new StatModel { Time = new DateTime(2025, 6, 15, 14, 30, 45) };

        var result = model.GetDate(StatLevels.All);

        Assert.Equal(new DateTime(1, 1, 1), result);
    }

    [Fact(DisplayName = "GetDate_Year截断到年初")]
    public void GetDate_Year_TruncatesToYear()
    {
        var model = new StatModel { Time = new DateTime(2025, 6, 15, 14, 30, 45) };

        var result = model.GetDate(StatLevels.Year);

        Assert.Equal(new DateTime(2025, 1, 1), result);
    }

    [Fact(DisplayName = "GetDate_Month截断到月初")]
    public void GetDate_Month_TruncatesToMonth()
    {
        var model = new StatModel { Time = new DateTime(2025, 6, 15, 14, 30, 45) };

        var result = model.GetDate(StatLevels.Month);

        Assert.Equal(new DateTime(2025, 6, 1), result);
    }

    [Fact(DisplayName = "GetDate_Day截断到日")]
    public void GetDate_Day_TruncatesToDay()
    {
        var model = new StatModel { Time = new DateTime(2025, 6, 15, 14, 30, 45) };

        var result = model.GetDate(StatLevels.Day);

        Assert.Equal(new DateTime(2025, 6, 15), result);
    }

    [Fact(DisplayName = "GetDate_Hour截断到小时")]
    public void GetDate_Hour_TruncatesToHour()
    {
        var model = new StatModel { Time = new DateTime(2025, 6, 15, 14, 30, 45) };

        var result = model.GetDate(StatLevels.Hour);

        Assert.Equal(new DateTime(2025, 6, 15, 14, 0, 0), result);
    }

    [Fact(DisplayName = "GetDate_Minute截断到分钟")]
    public void GetDate_Minute_TruncatesToMinute()
    {
        var model = new StatModel { Time = new DateTime(2025, 6, 15, 14, 30, 45) };

        var result = model.GetDate(StatLevels.Minute);

        Assert.Equal(new DateTime(2025, 6, 15, 14, 30, 0), result);
    }

    [Fact(DisplayName = "GetDate_未知级别返回原时间")]
    public void GetDate_Unknown_ReturnsOriginal()
    {
        var dt = new DateTime(2025, 6, 15, 14, 30, 45);
        var model = new StatModel { Time = dt };

        var result = model.GetDate(StatLevels.Quarter);

        Assert.Equal(dt, result);
    }
    #endregion

    #region ToString
    [Fact(DisplayName = "ToString_All返回全局")]
    public void ToString_All_ReturnsGlobal()
    {
        var model = new StatModel { Level = StatLevels.All, Time = DateTime.Now };

        Assert.Equal("全局", model.ToString());
    }

    [Fact(DisplayName = "ToString_Year格式化年")]
    public void ToString_Year_FormatsYear()
    {
        var model = new StatModel { Level = StatLevels.Year, Time = new DateTime(2025, 6, 15) };

        Assert.Equal("2025", model.ToString());
    }

    [Fact(DisplayName = "ToString_Month格式化年月")]
    public void ToString_Month_FormatsYearMonth()
    {
        var model = new StatModel { Level = StatLevels.Month, Time = new DateTime(2025, 6, 15) };

        Assert.Equal("2025-06", model.ToString());
    }

    [Fact(DisplayName = "ToString_Day格式化年月日")]
    public void ToString_Day_FormatsDate()
    {
        var model = new StatModel { Level = StatLevels.Day, Time = new DateTime(2025, 6, 15) };

        Assert.Equal("2025-06-15", model.ToString());
    }

    [Fact(DisplayName = "ToString_Hour格式化到小时")]
    public void ToString_Hour_FormatsHour()
    {
        var model = new StatModel { Level = StatLevels.Hour, Time = new DateTime(2025, 6, 15, 14, 0, 0) };

        Assert.Equal("2025-06-15 14", model.ToString());
    }

    [Fact(DisplayName = "ToString_Minute格式化到分钟")]
    public void ToString_Minute_FormatsMinute()
    {
        var model = new StatModel { Level = StatLevels.Minute, Time = new DateTime(2025, 6, 15, 14, 30, 0) };

        Assert.Equal("2025-06-15 14:30", model.ToString());
    }

    [Fact(DisplayName = "ToString_未知级别返回级别名")]
    public void ToString_Unknown_ReturnsLevelName()
    {
        var model = new StatModel { Level = StatLevels.Quarter, Time = DateTime.Now };

        Assert.Equal("Quarter", model.ToString());
    }
    #endregion

    #region Equals与GetHashCode
    [Fact(DisplayName = "Equals_同一对象返回true")]
    public void Equals_SameReference_ReturnsTrue()
    {
        var model = new StatModel { Level = StatLevels.Day, Time = new DateTime(2025, 6, 15) };

        Assert.True(model.Equals(model));
    }

    [Fact(DisplayName = "Equals_相同Level和Time返回true")]
    public void Equals_SameLevelAndTime_ReturnsTrue()
    {
        var m1 = new StatModel { Level = StatLevels.Day, Time = new DateTime(2025, 6, 15) };
        var m2 = new StatModel { Level = StatLevels.Day, Time = new DateTime(2025, 6, 15) };

        Assert.True(m1.Equals(m2));
    }

    [Fact(DisplayName = "Equals_不同Level返回false")]
    public void Equals_DifferentLevel_ReturnsFalse()
    {
        var m1 = new StatModel { Level = StatLevels.Day, Time = new DateTime(2025, 6, 15) };
        var m2 = new StatModel { Level = StatLevels.Month, Time = new DateTime(2025, 6, 15) };

        Assert.False(m1.Equals(m2));
    }

    [Fact(DisplayName = "Equals_不同Time返回false")]
    public void Equals_DifferentTime_ReturnsFalse()
    {
        var m1 = new StatModel { Level = StatLevels.Day, Time = new DateTime(2025, 6, 15) };
        var m2 = new StatModel { Level = StatLevels.Day, Time = new DateTime(2025, 6, 16) };

        Assert.False(m1.Equals(m2));
    }

    [Fact(DisplayName = "Equals_null返回false")]
    public void Equals_Null_ReturnsFalse()
    {
        var model = new StatModel { Level = StatLevels.Day, Time = new DateTime(2025, 6, 15) };

        Assert.False(model.Equals(null));
    }

    [Fact(DisplayName = "Equals_非StatModel类型返回false")]
    public void Equals_DifferentType_ReturnsFalse()
    {
        var model = new StatModel { Level = StatLevels.Day, Time = new DateTime(2025, 6, 15) };

        Assert.False(model.Equals("not a model"));
    }

    [Fact(DisplayName = "GetHashCode_相等对象哈希相同")]
    public void GetHashCode_EqualObjects_SameHash()
    {
        var m1 = new StatModel { Level = StatLevels.Day, Time = new DateTime(2025, 6, 15) };
        var m2 = new StatModel { Level = StatLevels.Day, Time = new DateTime(2025, 6, 15) };

        Assert.Equal(m1.GetHashCode(), m2.GetHashCode());
    }
    #endregion

    #region Fill
    [Fact(DisplayName = "Fill_设置Level和Time")]
    public void Fill_SetsLevelAndTime()
    {
        var model = new StatModel { Time = new DateTime(2025, 6, 15, 14, 30, 45) };
        var ps = new Dictionary<String, String>
        {
            ["Level"] = "3", // Day
            ["Time"] = "2025-06-15"
        };

        model.Fill(ps);

        Assert.Equal(StatLevels.Day, model.Level);
        // Fill会调用GetDate(Level)格式化时间
        Assert.Equal(new DateTime(2025, 6, 15), model.Time);
    }

    [Fact(DisplayName = "Fill_Level无效时使用默认值")]
    public void Fill_InvalidLevel_UsesDefault()
    {
        var model = new StatModel { Time = new DateTime(2025, 6, 15, 14, 30, 45) };
        var ps = new Dictionary<String, String>
        {
            ["Level"] = "-1"
        };

        model.Fill(ps, StatLevels.Month);

        Assert.Equal(StatLevels.Month, model.Level);
    }

    [Fact(DisplayName = "Fill_Level缺失则抛KeyNotFound")]
    public void Fill_NoLevelKey_ThrowsKeyNotFoundException()
    {
        var model = new StatModel { Time = new DateTime(2025, 6, 15, 14, 30, 45) };
        var ps = new Dictionary<String, String>();

        Assert.Throws<KeyNotFoundException>(() => model.Fill(ps, StatLevels.Hour));
    }
    #endregion
}

/// <summary>StatModel泛型测试</summary>
public class StatModelGenericTests
{
    private class TestStatModel : StatModel<TestStatModel>
    {
        public Int32 Count { get; set; }

        public override void Copy(TestStatModel model)
        {
            base.Copy(model);
            Count = model.Count;
        }
    }

    [Fact(DisplayName = "Clone_创建相同副本")]
    public void Clone_CreatesCopy()
    {
        var model = new TestStatModel
        {
            Level = StatLevels.Day,
            Time = new DateTime(2025, 6, 15),
            Count = 42
        };

        var clone = model.Clone();

        Assert.NotSame(model, clone);
        Assert.Equal(model.Level, clone.Level);
        Assert.Equal(model.Time, clone.Time);
        Assert.Equal(42, clone.Count);
    }

    [Fact(DisplayName = "Split_分割为多个层级")]
    public void Split_CreatesMultipleLevels()
    {
        var model = new TestStatModel
        {
            Level = StatLevels.Day,
            Time = new DateTime(2025, 6, 15, 14, 30, 0),
            Count = 100
        };

        var list = model.Split(StatLevels.Day, StatLevels.Month, StatLevels.Year);

        Assert.Equal(3, list.Count);
        Assert.Equal(StatLevels.Day, list[0].Level);
        Assert.Equal(new DateTime(2025, 6, 15), list[0].Time);
        Assert.Equal(StatLevels.Month, list[1].Level);
        Assert.Equal(new DateTime(2025, 6, 1), list[1].Time);
        Assert.Equal(StatLevels.Year, list[2].Level);
        Assert.Equal(new DateTime(2025, 1, 1), list[2].Time);
    }

    [Fact(DisplayName = "Copy_复制基本属性")]
    public void Copy_CopiesProperties()
    {
        var source = new TestStatModel
        {
            Level = StatLevels.Hour,
            Time = new DateTime(2025, 6, 15, 14, 0, 0),
            Count = 55
        };

        var target = new TestStatModel();
        target.Copy(source);

        Assert.Equal(source.Level, target.Level);
        Assert.Equal(source.Time, target.Time);
        Assert.Equal(55, target.Count);
    }
}
