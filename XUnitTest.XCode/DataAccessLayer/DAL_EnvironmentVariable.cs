using System;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer
{
    public class DAL_EnvironmentVariable
    {
        [Fact]
        public void Test1()
        {
            for (var i = 0; i < 10; i++)
            {
                Environment.SetEnvironmentVariable($"XCode_test{i}", $"DataSource=data\\test{i}.db;provider=sqlite");
            }

            var cs = DAL.ConnStrs;
            for (var i = 0; i < 10; i++)
            {
                Assert.True(cs.ContainsKey($"test{i}"));

                var dal = DAL.Create($"test{i}");
                var ts = dal.Tables;
                Assert.Equal(0, ts.Count);
            }
        }
    }
}