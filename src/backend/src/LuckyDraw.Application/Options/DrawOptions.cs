namespace LuckyDraw.Application.Options;

/// <summary>
/// 抽奖配置节（D-06）：测试可控性配置集中于此，且由 <see cref="DrawOptionsValidator"/> 在启动时强校验，
/// 生产环境检测到确定性配置即拒绝启动（PRD R5 / 附录 B B1）。
/// </summary>
public class DrawOptions
{
    /// <summary>配置节名。</summary>
    public const string SectionName = "Draw";

    /// <summary>每人每日抽奖次数上限（默认 3）。</summary>
    public int DailyLimit { get; set; } = 3;

    /// <summary>确定性结果配置（仅 Development / Testing 环境允许开启）。</summary>
    public DeterministicOptions Deterministic { get; set; } = new();
}

/// <summary>确定性结果配置。</summary>
public class DeterministicOptions
{
    /// <summary>确定性开关（生产基线必须为 false 且覆盖项为空）。</summary>
    public bool Enabled { get; set; }

    /// <summary>用户名 → 指定条目 Code 的强制命中映射（用于 AC-08 / AC-09 / AC-13）。</summary>
    public List<ForcedResultOptions> ForcedResults { get; set; } = new();
}

/// <summary>单条强制命中映射。</summary>
public class ForcedResultOptions
{
    /// <summary>用户名（匹配时不区分大小写）。</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>目标奖池条目 Code（查无此条目时回退正常加权随机）。</summary>
    public string PrizeItemCode { get; set; } = string.Empty;
}
