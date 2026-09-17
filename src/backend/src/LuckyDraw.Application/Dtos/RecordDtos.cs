namespace LuckyDraw.Application.Dtos;

/// <summary>我的中奖记录列表项（API-08）。</summary>
public class WinningRecordDto
{
    /// <summary>记录 Id。</summary>
    public int Id { get; set; }

    /// <summary>奖品名称（写入时快照）。</summary>
    public string PrizeName { get; set; } = string.Empty;

    /// <summary>
    /// 中奖时间（ISO 8601（UTC）；前端按 UTC+8 展示）。用 <see cref="DateTimeOffset"/> 而非 <see cref="DateTime"/>：
    /// MySQL `DATETIME` 读回为 `Kind = Unspecified`，序列化时不带时区标识（BUG-02），使「UTC」口径不自描述；
    /// 本字段与 API-06 的 `resetAt`（同为 UTC 时间）保持同一表达方式。
    /// </summary>
    public DateTimeOffset WinTime { get; set; }
}
