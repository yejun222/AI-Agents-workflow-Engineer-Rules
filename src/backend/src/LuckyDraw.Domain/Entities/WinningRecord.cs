namespace LuckyDraw.Domain.Entities;

/// <summary>中奖记录（MOD-04）。只增不改：无更新 / 删除路径（FR-09-2）。</summary>
public class WinningRecord : AuditableEntity
{
    /// <summary>用户 Id；恒为当前登录用户，接口不接受 userId 参数。</summary>
    public int UserId { get; set; }

    /// <summary>奖品条目 Id（可追溯）。</summary>
    public int PrizeItemId { get; set; }

    /// <summary>奖品名称快照（D-10：写入时从奖品表带出，历史记录不受改名影响）。</summary>
    public string PrizeName { get; set; } = string.Empty;

    /// <summary>逻辑删除标记（记录不做物理删除）。</summary>
    public bool IsDeleted { get; set; }
}
