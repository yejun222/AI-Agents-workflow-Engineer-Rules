using LuckyDraw.Application.Interfaces;

namespace LuckyDraw.Application.Common;

/// <summary>
/// 整数权重累积区间取样（D-04）：P(i) = wᵢ / Σw，区间左闭右开（<c>hit &lt; cumulative</c>）。
/// 抽出自 <see cref="IRandomSource"/>（生产为加密级随机，测试可注入确定性实现），
/// 与抽奖服务解耦以便对区间边界做单元验证。
/// </summary>
internal static class WeightedSampler
{
    /// <summary>按权重取样；权重总和为 0 时回退末位候选（调用方负责告警，避免除零与不确定行为）。</summary>
    /// <typeparam name="T">候选条目类型。</typeparam>
    /// <param name="items">候选集（决定区间顺序，调用方保证非空）。</param>
    /// <param name="weightOf">权重解析委托（负值按 0 处理）。</param>
    /// <param name="random">随机源。</param>
    public static T Pick<T>(IReadOnlyList<T> items, Func<T, int> weightOf, IRandomSource random)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("候选集不能为空", nameof(items));
        }

        var totalWeight = 0;
        foreach (var item in items)
        {
            totalWeight += Math.Max(0, weightOf(item));
        }

        if (totalWeight <= 0)
        {
            return items[^1];
        }

        var hit = random.NextInt(totalWeight);
        var cumulative = 0;
        foreach (var item in items)
        {
            cumulative += Math.Max(0, weightOf(item));
            if (hit < cumulative)
            {
                return item;
            }
        }

        // 理论不可达（hit < totalWeight 必落在某个区间内），保留兜底避免越界
        return items[^1];
    }
}
