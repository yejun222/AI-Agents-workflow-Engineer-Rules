using LuckyDraw.Domain.Enums;

namespace LuckyDraw.Domain.Entities;

/// <summary>奖池条目（MOD-02）。权重与库存为内部字段，绝不下发前端（FR-03-1 / D-07）。</summary>
public class PrizeItem : AuditableEntity
{
    /// <summary>稳定标识（种子与测试覆盖使用，不下发前端）。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>展示全名，如「三等奖 · 定制马克杯」。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>转盘扇区短名，如「三等奖」。</summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>条目类型：1=实物 / 2=虚拟 / 3=未中奖（豁免库存）。</summary>
    public PrizeItemType Type { get; set; }

    /// <summary>相对权重（整数，候选集内归一）。</summary>
    public int Weight { get; set; }

    /// <summary>库存；<see cref="PrizeItemType.NoPrize"/> 恒为 0 且不参与扣减。</summary>
    public int Stock { get; set; }

    /// <summary>扇区顺序（1 起）。</summary>
    public int DisplayOrder { get; set; }

    /// <summary>启用开关（FR-03-2：上下架不删除）。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>逻辑删除标记。</summary>
    public bool IsDeleted { get; set; }
}
