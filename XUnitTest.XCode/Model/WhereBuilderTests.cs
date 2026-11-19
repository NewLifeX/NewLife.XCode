using System;
using System.Collections.Generic;
using System.Text;
using XCode;
using XCode.Membership;
using XCode.Model;
using Xunit;

namespace XUnitTest.XCode.Model
{
    public class WhereBuilderTests
    {
        [Fact(DisplayName = "普通表达式解析")]
        public void Parse()
        {
            var dic = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase)
            {
                ["userid"] = 1234
            };
            var builder = new WhereBuilder
            {
                Factory = Log.Meta.Factory,
                Expression = "CreateUserId={$userId}",
            };
            builder.SetData(dic);

            var exp = builder.GetExpression();
            Assert.NotNull(exp);

            var fe = exp as FieldExpression;
            Assert.NotNull(fe);
            Assert.Equal(Log._.CreateUserId, fe.Field);
            Assert.Equal("=", fe.Action);
            Assert.Equal(1234, fe.Value);
        }

        [Fact(DisplayName = "无数据源")]
        public void ParseNoData()
        {
            var builder = new WhereBuilder
            {
                Factory = Log.Meta.Factory,
                Expression = "CreateUserId={$userId}",
            };

            var ex = Assert.Throws<ArgumentException>("Data", () => builder.GetExpression());
            Assert.NotNull(ex);
        }

        [Fact(DisplayName = "数据源变量大小写")]
        public void ParseNoData2()
        {
            // 变量大小写
            var dic = new Dictionary<String, Object>()
            {
                ["userid"] = 1234
            };
            var builder = new WhereBuilder
            {
                Factory = Log.Meta.Factory,
                Expression = "CreateUserId={#userId}",
            };
            builder.SetData2(dic);

            // 变量大小写
            //var ex = Assert.Throws<ArgumentException>("Data2", () => builder.GetExpression());
            //Assert.NotNull(ex);

            var exp = builder.GetExpression();
            Assert.NotNull(exp);

            var fe = exp as FieldExpression;
            Assert.NotNull(fe);
            Assert.Equal(Log._.CreateUserId, fe.Field);
            Assert.Equal("=", fe.Action);
            //Assert.Null(fe.Value);
            Assert.Equal(1234, fe.Value);
        }

        [Fact(DisplayName = "无字段")]
        public void ParseNoField()
        {
            var builder = new WhereBuilder
            {
                Factory = Log.Meta.Factory,
                Expression = "UserId={$userId}",
            };

            var ex = Assert.Throws<XCodeException>(() => builder.GetExpression());
            Assert.NotNull(ex);
        }

        [Fact(DisplayName = "多层变量")]
        public void ParseVar()
        {
            var user = new User { Id = 1234 };
            var dic = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase)
            {
                ["user"] = user
            };
            var builder = new WhereBuilder
            {
                Factory = Log.Meta.Factory,
                Expression = "CreateUserId={$User.Id}",
            };
            builder.SetData(dic);

            var exp = builder.GetExpression();
            Assert.NotNull(exp);

            var fe = exp as FieldExpression;
            Assert.NotNull(fe);
            Assert.Equal(Log._.CreateUserId, fe.Field);
            Assert.Equal("=", fe.Action);
            Assert.Equal(1234, fe.Value);
        }

        [Fact(DisplayName = "高级或运算")]
        public void ParseOr()
        {
            var dic = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase)
            {
                ["SiteIds"] = new[] { 2, 4, 8, 16 },
                ["userid"] = 1234
            };
            var builder = new WhereBuilder
            {
                Factory = Log.Meta.Factory,
                Expression = "linkid in{#SiteIds} or CreateUserId={#userId}",
            };
            builder.SetData2(dic);

            var exp = builder.GetExpression();
            Assert.NotNull(exp);

            var where = exp as WhereExpression;
            Assert.NotNull(where);
            Assert.Equal(Operator.Or, where.Operator);

            var left = where.Left as InExpression;
            Assert.NotNull(left);
            Assert.Equal(Log._.LinkId, left.Field);
            Assert.Equal("{0} In({1})", left.Format);
            Assert.Equal(dic["SiteIds"], left.Value);
        }

        [Fact(DisplayName = "未支持表达式")]
        public void ParseUnknown()
        {
            var user = new User { Id = 1234 };
            var dic = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase)
            {
                ["user"] = user
            };
            var builder = new WhereBuilder
            {
                Factory = Log.Meta.Factory,
                Expression = "CreateUserId>={$User.Id}",
            };
            builder.SetData(dic);

            var ex = Assert.Throws<XCodeException>(() => builder.GetExpression());
            Assert.NotNull(ex);
        }

        [Theory]
        [InlineData("CreateUserId={$userId}")]
        [InlineData("CreateUserId!={#userId} or linkid in{#SiteIds}")]
        public void EvalSuccess(String exp)
        {
            var dic = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase)
            {
                ["SiteIds"] = new[] { 2, 4, 8, 16 },
                ["userid"] = 1234
            };
            var builder = new WhereBuilder
            {
                Factory = Log.Meta.Factory,
                Expression = exp,
            };
            builder.SetData(dic);
            builder.SetData2(dic);

            var log = new Log { LinkId = 8, CreateUserId = 1234 };

            var rs = builder.Eval(log);
            Assert.True(rs);
        }

        [Theory]
        [InlineData("CreateUserId=={$userId}")]
        [InlineData("CreateUserId=={#userId} or linkid in{#SiteIds}")]
        public void EvalFail(String exp)
        {
            var dic = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase)
            {
                ["SiteIds"] = new[] { 2, 4, 8, 16 },
                ["userid"] = 1234
            };
            var builder = new WhereBuilder
            {
                Factory = Log.Meta.Factory,
                Expression = exp,
            };
            builder.SetData(dic);
            builder.SetData2(dic);

            var log = new Log { LinkId = 17, CreateUserId = 1235 };

            var rs = builder.Eval(log);
            Assert.False(rs);
        }
    }
}