namespace LuckyDraw.Api.Common;

/// <summary>限流策略名（规范 8.6）。</summary>
public static class RateLimitPolicies
{
    /// <summary>注册：按客户端 IP 固定窗口限流。</summary>
    public const string Register = "auth-register";

    /// <summary>登录：按客户端 IP 固定窗口限流（叠加服务层的「按用户名 5 次失败锁定 15 分钟」）。</summary>
    public const string Login = "auth-login";

    /// <summary>抽奖：按已认证用户固定窗口限流（Should-S3）。</summary>
    public const string Draw = "draw";
}
