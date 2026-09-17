namespace LuckyDraw.Domain.Entities;

/// <summary>
/// 每日抽奖次数聚合行（MOD-03 / D-02）：每人每日一行，唯一索引 (UserId, DrawDate)；
/// 跨日重置 = 新的一天自然出现新行，无需定时任务。
/// </summary>
public class UserDrawQuota : AuditableEntity
{
    /// <summary>用户 Id。</summary>
    public int UserId { get; set; }

    /// <summary>抽奖自然日（服务端时钟按 UTC+8 口径换算）。</summary>
    public DateOnly DrawDate { get; set; }

    /// <summary>当日已消耗次数（未中奖同样计入，FR-04）。</summary>
    public int UsedCount { get; set; }
}
