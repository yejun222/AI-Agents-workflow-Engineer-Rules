namespace LuckyDraw.Domain.Entities;

/// <summary>
/// 可审计实体基类：统一承载自增主键与创建 / 更新时间，
/// 时间由 SaveChanges 拦截器按 UTC 统一赋值（架构 4.7）。
/// </summary>
public abstract class AuditableEntity
{
    /// <summary>自增主键。</summary>
    public int Id { get; set; }

    /// <summary>创建时间（UTC）。</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（UTC）。</summary>
    public DateTime UpdateTime { get; set; }
}
