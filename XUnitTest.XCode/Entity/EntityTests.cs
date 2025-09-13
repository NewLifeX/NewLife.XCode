using System;
using NewLife.Reflection;
using NewLife.Security;
using XCode;
using XCode.Membership;
using Xunit;

namespace XUnitTest.XCode.Entity;

public class EntityTests
{
    [Fact]
    public void LongFieldTest()
    {
        var user = new User
        {
            Name = "StoneXXX",
            DisplayName = Rand.NextString(99),
        };
        //user.Insert();
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => user.Insert());
        Assert.Contains("[Name=StoneXXX]", ex.Message);
    }

    [Theory]
    [InlineData(0.100000001490116)]
    [InlineData(-0.100000001490116)]
    [InlineData(0.00000000100000001490116)]
    [InlineData(-0.00000000100000001490116)]
    [InlineData(-5.4569682106375694E-12)]
    public void CheckEqualBySmallDouble(Double n)
    {
        var user = new User();
        var entity = user as IEntity;
        user.SetValue("IsFromDatabase", true);

        // 0改为其它数
        {
            user.Ex3 = n;

            Assert.Equal(n, user.Ex3);
            Assert.True(entity.HasDirty);
            Assert.True(entity.Dirtys["Ex3"]);
        }

        // 微小变化，不能修改，不算脏数据
        {
            entity.Dirtys.Clear();
            user.Ex3 += 0.000000000000001;

            Assert.Equal(n, user.Ex3);
            Assert.False(entity.HasDirty);
            Assert.False(entity.Dirtys["Ex3"]);
        }

        // 其它数改为0
        {
            entity.Dirtys.Clear();
            user.Ex3 = 0;

            Assert.Equal(0, user.Ex3);
            Assert.True(entity.HasDirty);
            Assert.True(entity.Dirtys["Ex3"]);
        }

        // 随机修改
        {
            user.Ex3 = Rand.Next() / 10000000d;
            entity.Dirtys.Clear();
            user.Ex3 = n;

            Assert.Equal(n, user.Ex3);
            Assert.True(entity.HasDirty);
            Assert.True(entity.Dirtys["Ex3"]);
        }
    }

    [Theory]
    [InlineData(0.100000001490116)]
    [InlineData(-0.100000001490116)]
    [InlineData(0.00000000100000001490116)]
    [InlineData(-0.00000000100000001490116)]
    [InlineData(-5.4569682106375694E-12)]
    public void CheckEqualBySmallDecimal(Decimal n)
    {
        var pm = new Parameter();
        var entity = pm as IEntity;
        pm.SetValue("IsFromDatabase", true);

        // 0改为其它数
        {
            pm.Ex2 = n;

            Assert.Equal(n, pm.Ex2);
            Assert.True(entity.HasDirty);
            Assert.True(entity.Dirtys["Ex2"]);
        }

        // 微小变化，也能修改，也算脏数据
        {
            entity.Dirtys.Clear();
            var n2 = n + 0.000000000000001m;
            pm.Ex2 = n2;

            Assert.Equal(n2, pm.Ex2);
            Assert.True(entity.HasDirty);
            Assert.True(entity.Dirtys["Ex2"]);
        }

        // 其它数改为0
        {
            entity.Dirtys.Clear();
            pm.Ex2 = 0;

            Assert.Equal(0, pm.Ex2);
            Assert.True(entity.HasDirty);
            Assert.True(entity.Dirtys["Ex2"]);
        }

        // 随机修改
        {
            pm.Ex2 = Rand.Next() / 10000000M;
            entity.Dirtys.Clear();
            pm.Ex2 = n;

            Assert.Equal(n, pm.Ex2);
            Assert.True(entity.HasDirty);
            Assert.True(entity.Dirtys["Ex2"]);
        }
    }
}
