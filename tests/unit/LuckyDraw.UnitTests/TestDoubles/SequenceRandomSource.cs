using LuckyDraw.Application.Interfaces;

namespace LuckyDraw.UnitTests.TestDoubles;

/// <summary>
/// 脚本化随机源（D-04）：按队列依次返回指定命中值，队列耗尽即抛错——
/// 避免「实现多掷了一次随机」被静默放过。上限以实际入参校验。
/// </summary>
public sealed class SequenceRandomSource : IRandomSource
{
    private readonly Queue<int> _hits;

    /// <summary>用命中序列构造随机源。</summary>
    /// <param name="hits">依次返回的命中值（落入 [0, exclusiveMax)）。</param>
    public SequenceRandomSource(params int[] hits)
    {
        _hits = new Queue<int>(hits);
    }

    /// <summary>记录每次调用收到的 exclusiveMax，便于断言权重总和。</summary>
    public List<int> ObservedMaxima { get; } = new();

    /// <inheritdoc />
    public int NextInt(int exclusiveMax)
    {
        ObservedMaxima.Add(exclusiveMax);

        if (_hits.Count == 0)
        {
            throw new InvalidOperationException($"随机源调用次数超出脚本（第 {ObservedMaxima.Count} 次，exclusiveMax={exclusiveMax}）");
        }

        var hit = _hits.Dequeue();
        if (hit < 0 || hit >= exclusiveMax)
        {
            throw new InvalidOperationException($"脚本命中值 {hit} 超出 [0, {exclusiveMax}) 区间");
        }

        return hit;
    }
}
