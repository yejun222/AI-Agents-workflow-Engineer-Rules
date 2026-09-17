using LuckyDraw.Domain.Enums;

namespace LuckyDraw.Application.Dtos;

/// <summary>
/// 抽奖候选集内部投影（含权重 / 库存，**绝不下发前端**；仅用于服务端加权随机与条件扣减）。
/// </summary>
public class DrawCandidateDto
{
    /// <summary>条目 Id。</summary>
    public int Id { get; set; }

    /// <summary>稳定标识（权重覆盖配置按它匹配）。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>奖品名称（中奖记录快照来源）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>条目类型。</summary>
    public PrizeItemType Type { get; set; }

    /// <summary>相对权重（整数）。</summary>
    public int Weight { get; set; }

    /// <summary>剩余库存（NoPrize 恒为 0）。</summary>
    public int Stock { get; set; }

    /// <summary>扇区顺序（累积区间取样按此顺序扫描）。</summary>
    public int DisplayOrder { get; set; }
}

/// <summary>剩余次数查询响应（API-06）。</summary>
public class DrawQuotaDto
{
    /// <summary>今日剩余次数。</summary>
    public int RemainingAttempts { get; set; }

    /// <summary>每日上限。</summary>
    public int DailyLimit { get; set; }

    /// <summary>下次重置时刻（次日 00:00 UTC+8 对应的 UTC 时刻，ISO 8601）。</summary>
    public DateTimeOffset ResetAt { get; set; }
}

/// <summary>执行抽奖响应（API-07）。</summary>
public class DrawResponseDto
{
    /// <summary>结果条目 Id（前端落点的唯一依据，FR-05-R10 / FR-06）。</summary>
    public int ItemId { get; set; }

    /// <summary>是否中奖。</summary>
    public bool IsWin { get; set; }

    /// <summary>最新剩余次数。</summary>
    public int RemainingAttempts { get; set; }
}
