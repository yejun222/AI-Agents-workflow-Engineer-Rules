namespace LuckyDraw.Application.Common;

/// <summary>
/// 时间口径换算（D-05 / 架构 4.7）：「今日」按 UTC+8 自然日判定，不使用系统时区数据库
/// （中国无夏令时，固定偏移最稳）；自然日换算收敛在本文件一处。
/// </summary>
public static class ClockExtensions
{
    /// <summary>UTC+8 固定偏移。</summary>
    public static readonly TimeSpan ChinaOffset = TimeSpan.FromHours(8);

    /// <summary>取服务端时钟对应的 UTC+8 自然日（抽奖次数按日聚合的键）。</summary>
    /// <param name="timeProvider">注入的时钟（BCL <see cref="TimeProvider"/>）。</param>
    public static DateOnly GetDrawDateUtc8(this TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var localNow = timeProvider.GetUtcNow().ToOffset(ChinaOffset);
        return DateOnly.FromDateTime(localNow.DateTime);
    }

    /// <summary>取下一次次数重置时刻（次日 00:00 UTC+8）对应的 UTC 时刻，供前端引导文案使用。</summary>
    /// <param name="timeProvider">注入的时钟。</param>
    public static DateTimeOffset GetNextResetAtUtc(this TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var localNow = timeProvider.GetUtcNow().ToOffset(ChinaOffset);
        var nextLocalMidnight = new DateTimeOffset(localNow.Date.AddDays(1), ChinaOffset);
        return nextLocalMidnight.ToUniversalTime();
    }
}
