namespace LuckyDraw.Application.Options;

/// <summary>
/// 限流配置（规范 8.6 / §5.1）：固定窗口、按分钟计数。
/// 登录另叠加「按提交用户名 5 次失败锁定 15 分钟」的账号级锁定（Redis 计数）。
/// </summary>
public class RateLimitOptions
{
    /// <summary>配置节名。</summary>
    public const string SectionName = "RateLimit";

    /// <summary>注册接口：按 IP 每分钟允许次数（默认 10）。</summary>
    public int RegisterPerMinute { get; set; } = 10;

    /// <summary>登录接口：按 IP 每分钟允许次数（默认 10）。</summary>
    public int LoginPerMinute { get; set; } = 10;

    /// <summary>抽奖接口：按登录用户每分钟允许次数（默认 10）。</summary>
    public int DrawPerMinute { get; set; } = 10;
}
