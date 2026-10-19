using System;

namespace XCode.Membership;

/// <summary>审计日志动作名。作为日志表 Action 字段的数据值，保持既有中文以兼容历史报表与查询。</summary>
public static class LogActions
{
    /// <summary>添加</summary>
    public const String Add = "添加";

    /// <summary>修改</summary>
    public const String Update = "修改";

    /// <summary>删除</summary>
    public const String Delete = "删除";

    /// <summary>登录</summary>
    public const String Login = "登录";

    /// <summary>自动登录</summary>
    public const String AutoLogin = "自动登录";

    /// <summary>注册</summary>
    public const String Register = "注册";

    /// <summary>修改密码</summary>
    public const String ChangePassword = "修改密码";
}
