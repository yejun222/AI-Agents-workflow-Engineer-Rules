using FluentAssertions;
using LuckyDraw.Application.Common;
using LuckyDraw.UnitTests.TestDoubles;

namespace LuckyDraw.UnitTests.Common;

/// <summary>
/// D-05 / FR-07：抽奖日按 UTC+8 自然日判定，跨日重置不依赖系统时区、不信任客户端时间（AC-11）。
/// </summary>
public class ClockExtensionsTests
{
    /// <summary>UTC+8 当日 23:59:59 仍属前一日。</summary>
    [Fact]
    public void GetDrawDateUtc8_JustBeforeMidnight_KeepsPreviousDay()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 16, 15, 59, 59, TimeSpan.Zero));

        clock.GetDrawDateUtc8().Should().Be(new DateOnly(2026, 9, 17).AddDays(-1));
    }

    /// <summary>UTC 16:00 整即 UTC+8 次日 00:00，抽奖日翻页。</summary>
    [Fact]
    public void GetDrawDateUtc8_AtUtc16_StartsNewDay()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 16, 16, 0, 0, TimeSpan.Zero));

        clock.GetDrawDateUtc8().Should().Be(new DateOnly(2026, 9, 17));
    }

    /// <summary>非 UTC 时刻入参（带偏移）先归一到 UTC 再换算，结果与 UTC 表示一致。</summary>
    [Fact]
    public void GetDrawDateUtc8_WithOffsetInput_IsOffsetAgnostic()
    {
        var utc = new FixedTimeProvider(new DateTimeOffset(2026, 9, 16, 16, 30, 0, TimeSpan.Zero));
        var local = new FixedTimeProvider(new DateTimeOffset(2026, 9, 17, 0, 30, 0, TimeSpan.FromHours(8)));

        local.GetDrawDateUtc8().Should().Be(utc.GetDrawDateUtc8());
    }

    /// <summary>下次重置时刻 = 次日 00:00 UTC+8 对应的 UTC 时刻（前端倒计时 / 文案依据）。</summary>
    [Fact]
    public void GetNextResetAtUtc_ReturnsNextLocalMidnightInUtc()
    {
        // UTC 2026-09-16T20:10Z = UTC+8 2026-09-17 04:10 → 下次重置为 UTC+8 2026-09-18 00:00 = UTC 2026-09-17T16:00Z
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 16, 20, 10, 0, TimeSpan.Zero));

        clock.GetNextResetAtUtc().Should().Be(new DateTimeOffset(2026, 9, 17, 16, 0, 0, TimeSpan.Zero));
    }

    /// <summary>重置时刻晚于当前时刻，且间隔不超过 24 小时。</summary>
    [Fact]
    public void GetNextResetAtUtc_IsWithinOneDay()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 17, 0, 0, 0, TimeSpan.Zero));

        var resetAt = clock.GetNextResetAtUtc();

        resetAt.Should().BeAfter(clock.GetUtcNow());
        (resetAt - clock.GetUtcNow()).Should().BeLessThanOrEqualTo(TimeSpan.FromDays(1));
    }
}
