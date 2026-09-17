namespace LuckyDraw.Application.Dtos;

/// <summary>
/// 奖池条目的对外投影（D-07 白名单）：**结构上不含** Weight / Stock / Code / IsEnabled，
/// 因此 AC-06「响应不含权重与库存字段」由类型系统保证，而非序列化忽略。
/// </summary>
public class PrizePoolItemDto
{
    /// <summary>条目 Id。</summary>
    public int Id { get; set; }

    /// <summary>展示全名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>转盘扇区短名。</summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>条目类型（1=实物 / 2=虚拟 / 3=未中奖）。</summary>
    public int Type { get; set; }

    /// <summary>扇区顺序（1 起）。</summary>
    public int DisplayOrder { get; set; }
}

/// <summary>奖池查询响应（API-05）。</summary>
public class PrizePoolResponse
{
    /// <summary>启用中的奖池条目（按 displayOrder 升序）。</summary>
    public IReadOnlyList<PrizePoolItemDto> Items { get; set; } = Array.Empty<PrizePoolItemDto>();
}
