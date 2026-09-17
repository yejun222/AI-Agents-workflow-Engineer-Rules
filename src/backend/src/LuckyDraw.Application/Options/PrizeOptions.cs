namespace LuckyDraw.Application.Options;

/// <summary>
/// 奖池测试配置节：权重覆盖（AC-14 / AC-16 的统计场景）。
/// 库存**不提供运行时覆盖**（会绕过条件更新语义，A5 已确认）。
/// </summary>
public class PrizeOptions
{
    /// <summary>配置节名。</summary>
    public const string SectionName = "Prize";

    /// <summary>条目 Code → 权重覆盖值；生产基线必须为空对象。</summary>
    public Dictionary<string, int> WeightOverrides { get; set; } = new();
}
