namespace LuckyDraw.UnitTests.TestDoubles;

/// <summary>固定时钟（D-05）：替代 BCL <see cref="TimeProvider"/> 以构造跨日场景（AC-11）。</summary>
/// <param name="utcNow">固定的 UTC 时刻。</param>
public sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    /// <summary>当前固定时刻。</summary>
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    /// <summary>返回固定 UTC 时刻。</summary>
    public override DateTimeOffset GetUtcNow() => UtcNow;
}
