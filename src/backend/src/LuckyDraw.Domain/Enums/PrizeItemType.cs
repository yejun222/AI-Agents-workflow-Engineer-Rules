namespace LuckyDraw.Domain.Enums;

/// <summary>奖池条目类型（枚举存 int，规范 5.2）。</summary>
public enum PrizeItemType
{
    /// <summary>实物奖品。</summary>
    Physical = 1,

    /// <summary>虚拟奖品。</summary>
    Virtual = 2,

    /// <summary>未中奖（「谢谢参与」）：豁免库存、不写中奖记录。</summary>
    NoPrize = 3
}
