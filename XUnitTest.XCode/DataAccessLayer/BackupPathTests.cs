using System;
using System.IO;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.DataAccessLayer;

public class BackupPathTests
{
    [Fact(DisplayName = "备份路径拒绝目录穿越与目录外绝对路径")]
    public void Ensure_RejectsTraversal()
    {
        Assert.Throws<XCodeException>(() => BackupPathHelper.Ensure("../evil.bak"));
        Assert.Throws<XCodeException>(() => BackupPathHelper.Ensure(@"..\..\evil.bak"));
        Assert.Throws<XCodeException>(() => BackupPathHelper.Ensure(Path.Combine(Path.GetTempPath(), "evil.bak")));
    }

    [Fact(DisplayName = "备份路径允许备份目录内的文件名")]
    public void Ensure_AcceptsRelative()
    {
        var path = BackupPathHelper.Ensure("demo.bak");

        Assert.True(Path.IsPathRooted(path));
        Assert.EndsWith("demo.bak", path);
    }

    [Fact(DisplayName = "SQL 字面量与标识符转义")]
    public void Quote_EscapesSpecialChars()
    {
        Assert.Equal("'a''b'", BackupPathHelper.Quote("a'b"));
        Assert.Equal("[a]]b]", BackupPathHelper.QuoteName("a]b"));
    }
}
