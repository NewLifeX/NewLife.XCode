using System;
using XCode;
using XCode.Exceptions;
using Xunit;

namespace XUnitTest.XCode.Exceptions;

/// <summary>XCodeException异常测试</summary>
public class XCodeExceptionTests
{
    [Fact(DisplayName = "构造_无参")]
    public void Ctor_Default()
    {
        var ex = new XCodeException();

        Assert.NotNull(ex);
        Assert.Null(ex.InnerException);
    }

    [Fact(DisplayName = "构造_消息")]
    public void Ctor_Message()
    {
        var ex = new XCodeException("测试消息");

        Assert.Equal("测试消息", ex.Message);
    }

    [Fact(DisplayName = "构造_格式化消息")]
    public void Ctor_FormatMessage()
    {
        var ex = new XCodeException("错误{0}号", 42);

        Assert.Contains("42", ex.Message);
    }

    [Fact(DisplayName = "构造_消息和内部异常")]
    public void Ctor_MessageAndInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new XCodeException("外部", inner);

        Assert.Equal("外部", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact(DisplayName = "构造_仅内部异常")]
    public void Ctor_InnerOnly()
    {
        var inner = new ArgumentException("arg error");
        var ex = new XCodeException(inner);

        Assert.Contains("arg error", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }
}

/// <summary>EntityException异常测试</summary>
public class EntityExceptionTests
{
    [Fact(DisplayName = "构造_无参")]
    public void Ctor_Default()
    {
        var ex = new EntityException();

        Assert.NotNull(ex);
    }

    [Fact(DisplayName = "构造_消息")]
    public void Ctor_Message()
    {
        var ex = new EntityException("实体错误");

        Assert.Equal("实体错误", ex.Message);
    }

    [Fact(DisplayName = "构造_格式化消息")]
    public void Ctor_FormatMessage()
    {
        var ex = new EntityException("实体{0}错误", "User");

        Assert.Contains("User", ex.Message);
    }

    [Fact(DisplayName = "构造_消息和内部异常")]
    public void Ctor_MessageAndInner()
    {
        var inner = new Exception("inner");
        var ex = new EntityException("实体错误", inner);

        Assert.Equal("实体错误", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact(DisplayName = "构造_仅内部异常")]
    public void Ctor_InnerOnly()
    {
        var inner = new Exception("root cause");
        var ex = new EntityException(inner);

        Assert.Contains("root cause", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }
}

/// <summary>XDbException异常测试</summary>
public class XDbExceptionTests
{
    [Fact(DisplayName = "构造_数据库参数")]
    public void Ctor_Database()
    {
        var ex = new XDbException(null!);

        Assert.NotNull(ex);
        Assert.Null(ex.Database);
    }

    [Fact(DisplayName = "构造_数据库和消息")]
    public void Ctor_DatabaseAndMessage()
    {
        var ex = new XDbException(null!, "数据库错误");

        Assert.Equal("数据库错误", ex.Message);
    }

    [Fact(DisplayName = "构造_数据库消息和内部异常_无DB对象")]
    public void Ctor_NullDb_MessageAndInner()
    {
        var inner = new Exception("inner error");
        var ex = new XDbException(null!, "数据库错误", inner);

        Assert.Contains("数据库错误", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact(DisplayName = "构造_仅内部异常_无DB对象")]
    public void Ctor_NullDb_InnerOnly()
    {
        var inner = new Exception("root");
        var ex = new XDbException(null!, inner);

        Assert.Contains("root", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }
}

/// <summary>XDbSessionException异常测试</summary>
public class XDbSessionExceptionTests
{
    [Fact(DisplayName = "构造_null会话")]
    public void Ctor_NullSession()
    {
        var ex = new XDbSessionException(null!);

        Assert.NotNull(ex);
        Assert.Null(ex.Session);
    }

    [Fact(DisplayName = "构造_null会话和消息")]
    public void Ctor_NullSession_Message()
    {
        var ex = new XDbSessionException(null!, "会话错误");

        Assert.Equal("会话错误", ex.Message);
    }
}

/// <summary>XDbMetaDataException异常测试</summary>
public class XDbMetaDataExceptionTests
{
    // XDbMetaDataException需要非null的IMetaData参数才能正常构造
    // 由于IMetaData.Database是必需的，这里只测试基本行为
    [Fact(DisplayName = "类型继承关系")]
    public void TypeInheritance()
    {
        Assert.True(typeof(XDbMetaDataException).IsSubclassOf(typeof(XDbException)));
    }
}
