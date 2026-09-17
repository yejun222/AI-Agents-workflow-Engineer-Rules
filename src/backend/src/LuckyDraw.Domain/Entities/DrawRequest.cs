namespace LuckyDraw.Domain.Entities;

/// <summary>
/// 抽奖请求流水（MOD-05 幂等权 / 审计内部流水，不提供任何查询接口）。
/// 正确性底线：唯一索引 (UserId, IdempotencyKey)；成功才落行（失败整事务回滚）。
/// </summary>
public class DrawRequest
{
    /// <summary>自增主键（高速增长表，bigint）。</summary>
    public long Id { get; set; }

    /// <summary>用户 Id；幂等键作用域 = 用户。</summary>
    public int UserId { get; set; }

    /// <summary>幂等键（可空：缺键 = 普通新请求，MySQL 唯一索引允许多个 NULL）。</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>请求体规范化 SHA-256；不一致 → 409（R1 下正常不可达）。</summary>
    public string? RequestHash { get; set; }

    /// <summary>结果条目 Id（未中奖也写，指向「谢谢参与」）。</summary>
    public int? PrizeItemId { get; set; }

    /// <summary>是否中奖。</summary>
    public bool IsWin { get; set; }

    /// <summary>首次响应中的剩余次数（重放时原样返回）。</summary>
    public int RemainingAttempts { get; set; }

    /// <summary>创建时间（UTC）。</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（UTC）。</summary>
    public DateTime UpdateTime { get; set; }
}
