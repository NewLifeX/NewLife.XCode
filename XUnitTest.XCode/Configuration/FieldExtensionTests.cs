using System;
using NewLife;
using XCode;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Configuration;

public class FieldExtensionTests
{
    #region Between(DateTime, DateTime) - DateTime 字段
    [Fact(DisplayName = "DateTime字段_两端均为MinValue_返回空表达式")]
    public void Between_DateTime_BothMinValue_ReturnsEmpty()
    {
        var fi = User._.UpdateTime;
        var exp = fi.Between(DateTime.MinValue, DateTime.MinValue);
        Assert.True(exp.IsEmpty);
    }

    [Fact(DisplayName = "DateTime字段_仅有开始时间_返回大于等于")]
    public void Between_DateTime_OnlyStart_ReturnsGte()
    {
        var fi = User._.UpdateTime;
        var start = new DateTime(2025, 1, 1);
        var exp = fi.Between(start, DateTime.MinValue);

        // 仅有开始时，Between 返回 WhereExpression，实际 FieldExpression 在 Right 节点
        var we = Assert.IsType<WhereExpression>(exp);
        var fe = Assert.IsType<FieldExpression>(we.Right);
        Assert.Equal(fi, fe.Field);
        Assert.Equal(">=", fe.Action);
        Assert.Equal(start, fe.Value);
    }

    [Fact(DisplayName = "DateTime字段_仅有结束时间_返回小于")]
    public void Between_DateTime_OnlyEnd_ReturnsLt()
    {
        var fi = User._.UpdateTime;
        var end = new DateTime(2025, 6, 30);
        var exp = fi.Between(DateTime.MinValue, end);

        var fe = Assert.IsType<FieldExpression>(exp);
        Assert.Equal(fi, fe.Field);
        Assert.Equal("<", fe.Action);
        Assert.Equal(end, fe.Value);
    }

    [Fact(DisplayName = "DateTime字段_开始结束均为纯日期且相同_结束自动加一天")]
    public void Between_DateTime_SamePureDate_EndAddOneDay()
    {
        var fi = User._.UpdateTime;
        var date = new DateTime(2025, 3, 15);
        var exp = fi.Between(date, date);

        var we = Assert.IsType<WhereExpression>(exp);
        var left = Assert.IsType<FieldExpression>(we.Left);
        var right = Assert.IsType<FieldExpression>(we.Right);

        Assert.Equal(">=", left.Action);
        Assert.Equal(date, left.Value);
        Assert.Equal("<", right.Action);
        Assert.Equal(date.AddDays(1), right.Value);
    }

    [Fact(DisplayName = "DateTime字段_带时间的范围_结束不加一天")]
    public void Between_DateTime_RangeWithTime_NoAddDay()
    {
        var fi = User._.UpdateTime;
        var start = new DateTime(2025, 3, 15, 8, 0, 0);
        var end = new DateTime(2025, 3, 15, 20, 0, 0);
        var exp = fi.Between(start, end);

        var we = Assert.IsType<WhereExpression>(exp);
        var left = Assert.IsType<FieldExpression>(we.Left);
        var right = Assert.IsType<FieldExpression>(we.Right);

        Assert.Equal(">=", left.Action);
        Assert.Equal(start, left.Value);
        Assert.Equal("<", right.Action);
        Assert.Equal(end, right.Value); // 结束不加天
    }

    [Fact(DisplayName = "DateTime字段_不同纯日期区间_结束加一天")]
    public void Between_DateTime_DifferentPureDates_EndAddOneDay()
    {
        var fi = User._.UpdateTime;
        var start = new DateTime(2025, 1, 1);
        var end = new DateTime(2025, 1, 31);
        var exp = fi.Between(start, end);

        var we = Assert.IsType<WhereExpression>(exp);
        var right = Assert.IsType<FieldExpression>(we.Right);
        Assert.Equal("<", right.Action);
        Assert.Equal(end.AddDays(1), right.Value);
    }

    [Fact(DisplayName = "DateTime字段_不支持整数字段_抛出NotSupportedException")]
    public void Between_DateTime_IntField_ThrowsNotSupported()
    {
        var fi = User._.ID; // Int32 类型
        var start = new DateTime(2025, 1, 1);
        var ex = Assert.Throws<NotSupportedException>(() => fi.Between(start, DateTime.MinValue));
        Assert.Contains("Between", ex.Message);
        Assert.Contains("Int64", ex.Message);
    }
    #endregion

    #region Between(DateTime, DateTime) - String 字段
    [Fact(DisplayName = "String字段_两端均为MinValue_返回空表达式")]
    public void Between_DateTime_StringBothMinValue_ReturnsEmpty()
    {
        var fi = User._.Password;
        var exp = fi.Between(DateTime.MinValue, DateTime.MinValue);
        Assert.True(exp.IsEmpty);
    }

    [Fact(DisplayName = "String字段_无ItemType_使用ToFullString格式")]
    public void Between_DateTime_StringNoItemType_UsesToFullString()
    {
        var fi = User._.Password; // 无 ItemType
        var start = new DateTime(2025, 6, 1);
        var exp = fi.Between(start, DateTime.MinValue);

        var we = Assert.IsType<WhereExpression>(exp);
        var fe = Assert.IsType<FieldExpression>(we.Right);
        Assert.Equal(fi, fe.Field);
        Assert.Equal(">=", fe.Action);
        Assert.Equal(start.ToFullString(), fe.Value);
    }

    [Fact(DisplayName = "String字段_ItemType非date_使用ToFullString格式")]
    public void Between_DateTime_StringItemTypeNotDate_UsesToFullString()
    {
        var fi = User._.Mail; // ItemType = "mail"
        var start = new DateTime(2025, 6, 1);
        var exp = fi.Between(start, DateTime.MinValue);

        var we = Assert.IsType<WhereExpression>(exp);
        var fe = Assert.IsType<FieldExpression>(we.Right);
        Assert.Equal(">=", fe.Action);
        Assert.Equal(start.ToFullString(), fe.Value);
    }

    [Fact(DisplayName = "String字段_ItemType为date_使用yyyyMMdd格式")]
    public void Between_DateTime_StringItemTypeDate_UsesDateFormat()
    {
        var fi = User._.Mail; // 借用 Mail 字段，临时设置 ItemType = "date"
        var oldItemType = fi.Field?.ItemType;
        try
        {
            if (fi.Field != null)
                fi.Field.ItemType = "date";

            var start = new DateTime(2025, 6, 15);
            var exp = fi.Between(start, DateTime.MinValue);

            var we = Assert.IsType<WhereExpression>(exp);
            var fe = Assert.IsType<FieldExpression>(we.Right);
            Assert.Equal(">=", fe.Action);
            Assert.Equal("20250615", fe.Value);
        }
        finally
        {
            if (fi.Field != null)
                fi.Field.ItemType = oldItemType;
        }
    }

    [Fact(DisplayName = "String字段_仅有结束时间_返回小于")]
    public void Between_DateTime_StringOnlyEnd_ReturnsLt()
    {
        var fi = User._.Password;
        var end = new DateTime(2025, 12, 31);
        var exp = fi.Between(DateTime.MinValue, end);

        var fe = Assert.IsType<FieldExpression>(exp);
        Assert.Equal("<", fe.Action);
        Assert.Equal(end.ToFullString(), fe.Value);
    }

    [Fact(DisplayName = "String字段_区间_始终左闭右开不加一天")]
    public void Between_DateTime_StringRange_NoAddDay()
    {
        var fi = User._.Password;
        var start = new DateTime(2025, 3, 1);
        var end = new DateTime(2025, 3, 31);
        var exp = fi.Between(start, end);

        var we = Assert.IsType<WhereExpression>(exp);
        var left = Assert.IsType<FieldExpression>(we.Left);
        var right = Assert.IsType<FieldExpression>(we.Right);

        Assert.Equal(">=", left.Action);
        Assert.Equal(start.ToFullString(), left.Value);
        Assert.Equal("<", right.Action);
        Assert.Equal(end.ToFullString(), right.Value); // 字符串字段不加一天
    }
    #endregion

    #region Between(Int64, Int64)
    [Fact(DisplayName = "Int64字段_开始等于结束_生成等号表达式")]
    public void Between_Int64_StartEqualsEnd_GeneratesEqual()
    {
        var fi = Log._.ID; // Int64 类型
        var exp = fi.Between(1000L, 1000L);

        var fe = Assert.IsType<FieldExpression>(exp);
        Assert.Equal(fi, fe.Field);
        Assert.Equal("=", fe.Action);
        Assert.Equal(1000L, fe.Value);
    }

    [Fact(DisplayName = "Int64字段_开始大于结束_返回空表达式")]
    public void Between_Int64_StartGreaterThanEnd_ReturnsEmpty()
    {
        var fi = Log._.ID;
        var exp = fi.Between(2000L, 1000L);
        Assert.True(exp.IsEmpty);
    }

    [Fact(DisplayName = "Int64字段_区间_生成大于等于且小于等于")]
    public void Between_Int64_Range_GeneratesGteLte()
    {
        var fi = Log._.ID;
        var exp = fi.Between(100L, 200L);

        var we = Assert.IsType<WhereExpression>(exp);
        var left = Assert.IsType<FieldExpression>(we.Left);
        var right = Assert.IsType<FieldExpression>(we.Right);

        Assert.Equal(">=", left.Action);
        Assert.Equal(100L, left.Value);
        Assert.Equal("<=", right.Action);
        Assert.Equal(200L, right.Value);
    }

    [Fact(DisplayName = "Int64字段_字符串字段不支持_抛出NotSupportedException")]
    public void Between_Int64_StringField_ThrowsNotSupported()
    {
        var fi = User._.Name; // String 类型
        var ex = Assert.Throws<NotSupportedException>(() => fi.Between(100L, 200L));
        Assert.Contains("Between", ex.Message);
        Assert.Contains("Int16", ex.Message);
    }
    #endregion

    #region Between(Int32, Int32) - 委托给 Int64
    [Fact(DisplayName = "Int32字段_区间_两端闭合")]
    public void Between_Int32_Range_GeneratesGteLte()
    {
        var fi = User._.ID; // Int32 类型
        var exp = fi.Between(10, 20);

        var we = Assert.IsType<WhereExpression>(exp);
        var left = Assert.IsType<FieldExpression>(we.Left);
        var right = Assert.IsType<FieldExpression>(we.Right);

        Assert.Equal(">=", left.Action);
        Assert.Equal(10L, left.Value);
        Assert.Equal("<=", right.Action);
        Assert.Equal(20L, right.Value);
    }

    [Fact(DisplayName = "Int32字段_开始等于结束_生成等号")]
    public void Between_Int32_StartEqualsEnd_Equal()
    {
        var fi = User._.ID;
        var exp = fi.Between(5, 5);

        var fe = Assert.IsType<FieldExpression>(exp);
        Assert.Equal("=", fe.Action);
        Assert.Equal(5L, fe.Value);
    }
    #endregion

    #region Between(Double, Double)
    [Fact(DisplayName = "Double字段_开始等于结束_生成等号表达式")]
    public void Between_Double_StartEqualsEnd_GeneratesEqual()
    {
        var fi = User._.Ex3; // Double 类型
        var exp = fi.Between(3.14, 3.14);

        var fe = Assert.IsType<FieldExpression>(exp);
        Assert.Equal(fi, fe.Field);
        Assert.Equal("=", fe.Action);
        Assert.Equal(3.14, fe.Value);
    }

    [Fact(DisplayName = "Double字段_开始大于结束_返回空表达式")]
    public void Between_Double_StartGreaterThanEnd_ReturnsEmpty()
    {
        var fi = User._.Ex3;
        var exp = fi.Between(100.0, 50.0);
        Assert.True(exp.IsEmpty);
    }

    [Fact(DisplayName = "Double字段_区间_生成大于等于且小于等于")]
    public void Between_Double_Range_GeneratesGteLte()
    {
        var fi = User._.Ex3;
        var exp = fi.Between(1.5, 9.9);

        var we = Assert.IsType<WhereExpression>(exp);
        var left = Assert.IsType<FieldExpression>(we.Left);
        var right = Assert.IsType<FieldExpression>(we.Right);

        Assert.Equal(">=", left.Action);
        Assert.Equal(1.5, left.Value);
        Assert.Equal("<=", right.Action);
        Assert.Equal(9.9, right.Value);
    }

    [Fact(DisplayName = "Double字段_整数字段不支持_抛出NotSupportedException")]
    public void Between_Double_IntField_ThrowsNotSupported()
    {
        var fi = User._.ID; // Int32 类型
        var ex = Assert.Throws<NotSupportedException>(() => fi.Between(1.0, 2.0));
        Assert.Contains("Between", ex.Message);
        Assert.Contains("Single", ex.Message);
    }
    #endregion

    #region Between(Decimal, Decimal)
    [Fact(DisplayName = "Decimal字段_开始等于结束_生成等号表达式")]
    public void Between_Decimal_StartEqualsEnd_GeneratesEqual()
    {
        var fi = Parameter._.Ex2; // Decimal 类型
        var exp = fi.Between(9.99m, 9.99m);

        var fe = Assert.IsType<FieldExpression>(exp);
        Assert.Equal(fi, fe.Field);
        Assert.Equal("=", fe.Action);
        Assert.Equal(9.99m, fe.Value);
    }

    [Fact(DisplayName = "Decimal字段_开始大于结束_返回空表达式")]
    public void Between_Decimal_StartGreaterThanEnd_ReturnsEmpty()
    {
        var fi = Parameter._.Ex2;
        var exp = fi.Between(100m, 50m);
        Assert.True(exp.IsEmpty);
    }

    [Fact(DisplayName = "Decimal字段_区间_生成大于等于且小于等于")]
    public void Between_Decimal_Range_GeneratesGteLte()
    {
        var fi = Parameter._.Ex2;
        var exp = fi.Between(10.5m, 99.9m);

        var we = Assert.IsType<WhereExpression>(exp);
        var left = Assert.IsType<FieldExpression>(we.Left);
        var right = Assert.IsType<FieldExpression>(we.Right);

        Assert.Equal(">=", left.Action);
        Assert.Equal(10.5m, left.Value);
        Assert.Equal("<=", right.Action);
        Assert.Equal(99.9m, right.Value);
    }

    [Fact(DisplayName = "Decimal字段_字符串字段不支持_抛出NotSupportedException")]
    public void Between_Decimal_StringField_ThrowsNotSupported()
    {
        var fi = User._.Name; // String 类型
        var ex = Assert.Throws<NotSupportedException>(() => fi.Between(1m, 9m));
        Assert.Contains("Between", ex.Message);
        Assert.Contains("Single", ex.Message);
    }
    #endregion
}
