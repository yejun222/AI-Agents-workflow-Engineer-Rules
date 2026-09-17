namespace LuckyDraw.Domain.Entities;

/// <summary>
/// 审计日志（MOD-06，规范第十章字段集）。只增不改：禁止 Update / Delete；
/// 无 FK、无 UpdateTime / IsDeleted（专门约定优先于通用要求）。
/// </summary>
public class AuditLog
{
    /// <summary>自增主键（bigint，只增表）。</summary>
    public long Id { get; set; }

    /// <summary>操作人 Id；系统动作为 NULL。</summary>
    public int? OperatorId { get; set; }

    /// <summary>操作时间（UTC）。</summary>
    public DateTime OperateTime { get; set; }

    /// <summary>客户端 IP（反代后为还原的真实 IP）。</summary>
    public string? IpAddress { get; set; }

    /// <summary>UserAgent（超长截断存储）。</summary>
    public string? UserAgent { get; set; }

    /// <summary>模块：auth / draw。</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>操作类型：Register / Login / LoginFail / Logout / RefreshReuse / Draw 等。</summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>目标对象（如 DrawRequest.Id / UserId）。</summary>
    public string? TargetObject { get; set; }

    /// <summary>变更前 JSON（已脱敏）。</summary>
    public string? BeforeJson { get; set; }

    /// <summary>变更后 JSON（已脱敏）。</summary>
    public string? AfterJson { get; set; }
}
