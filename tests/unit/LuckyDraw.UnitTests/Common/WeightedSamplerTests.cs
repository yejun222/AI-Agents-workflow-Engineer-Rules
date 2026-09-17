using FluentAssertions;
using LuckyDraw.Application.Common;
using LuckyDraw.UnitTests.TestDoubles;

namespace LuckyDraw.UnitTests.Common;

/// <summary>
/// D-04 加权随机：整数权重累积区间取样（P(i) = wᵢ / Σw，区间左闭右开）。
/// 覆盖默认奖池权重 1 / 3 / 10 / 20 / 66 的**边界命中值**与分布形态（AC-16 的单元级前置）。
/// </summary>
public class WeightedSamplerTests
{
    /// <summary>默认奖池权重（FR-03 默认值）。</summary>
    private static readonly int[] DefaultWeights = [1, 3, 10, 20, 66];

    private static List<string> Candidates() =>
        ["keyboard", "earbuds", "mug", "coupon", "no-prize"];

    private static string PickAt(int hit)
    {
        var candidates = Candidates();
        return WeightedSampler.Pick(candidates, item => DefaultWeights[candidates.IndexOf(item)], new SequenceRandomSource(hit));
    }

    /// <summary>命中值 0 落在首个区间（P(0) = 1/100）。</summary>
    [Fact]
    public void Pick_AtZero_ReturnsFirstCandidate()
    {
        PickAt(0).Should().Be("keyboard");
    }

    /// <summary>累积区间边界：hit = 累积上界 - 1 命中当前条目，hit = 累积上界 命中下一条目。</summary>
    [Theory]
    [InlineData(0, "keyboard")]
    [InlineData(1, "earbuds")]
    [InlineData(3, "earbuds")]
    [InlineData(4, "mug")]
    [InlineData(13, "mug")]
    [InlineData(14, "coupon")]
    [InlineData(33, "coupon")]
    [InlineData(34, "no-prize")]
    [InlineData(99, "no-prize")]
    public void Pick_AtCumulativeBoundaries_ReturnsExpectedCandidate(int hit, string expected)
    {
        PickAt(hit).Should().Be(expected);
    }

    /// <summary>权重总和作为随机源上界（exclusiveMax = Σw），保证取样不越界。</summary>
    [Fact]
    public void Pick_UsesTotalWeightAsExclusiveMax()
    {
        var candidates = Candidates();
        var random = new SequenceRandomSource(42);

        WeightedSampler.Pick(candidates, item => DefaultWeights[candidates.IndexOf(item)], random);

        random.ObservedMaxima.Should().Equal(100);
    }

    /// <summary>权重为 0 的条目永不被命中（库存耗尽剔除后仍保留扇区但不可中出，FR-03-1 / AC-14）。</summary>
    [Fact]
    public void Pick_WithZeroWeight_NeverSelectsThatCandidate()
    {
        var candidates = Candidates();
        var weights = new[] { 1, 3, 10, 20, 0 };

        for (var hit = 0; hit < 34; hit++)
        {
            WeightedSampler.Pick(candidates, item => weights[candidates.IndexOf(item)], new SequenceRandomSource(hit))
                .Should().NotBe("no-prize");
        }
    }

    /// <summary>权重总和非正时回退末位候选（配置错误兜底，不抛异常、不越界）。</summary>
    [Fact]
    public void Pick_WithAllZeroWeights_ReturnsLastCandidate()
    {
        var candidates = Candidates();

        var picked = WeightedSampler.Pick(candidates, _ => 0, new SequenceRandomSource());

        picked.Should().Be("no-prize");
    }

    /// <summary>负权重按 0 处理（配置容错）。</summary>
    [Fact]
    public void Pick_WithNegativeWeight_TreatsAsZero()
    {
        var candidates = new List<string> { "a", "b" };

        var picked = WeightedSampler.Pick(candidates, item => item == "a" ? -5 : 10, new SequenceRandomSource(0));

        picked.Should().Be("b");
    }

    /// <summary>分布形态：10 万次取样频率与理论概率偏差 &lt; 1.5 个百分点（AC-16 统计口径的单元级近似）。</summary>
    [Fact]
    public void Pick_Distribution_MatchesConfiguredWeights()
    {
        var candidates = Candidates();
        const int samples = 100_000;
        var random = new System.Random(20260917);
        var counts = new Dictionary<string, int>();

        foreach (var candidate in candidates)
        {
            counts[candidate] = 0;
        }

        var sequence = new CountingRandomSource(random);
        for (var i = 0; i < samples; i++)
        {
            var picked = WeightedSampler.Pick(candidates, item => DefaultWeights[candidates.IndexOf(item)], sequence);
            counts[picked]++;
        }

        for (var index = 0; index < candidates.Count; index++)
        {
            var expected = DefaultWeights[index] / 100d;
            var actual = counts[candidates[index]] / (double)samples;
            Math.Abs(actual - expected).Should().BeLessThan(0.015, $"条目 {candidates[index]} 的理论概率为 {expected:P0}");
        }
    }

    /// <summary>候选集为空属调用方错误（服务层已提前拦截并返回 1502）。</summary>
    [Fact]
    public void Pick_WithEmptyCandidates_Throws()
    {
        var act = () => WeightedSampler.Pick(new List<string>(), _ => 1, new SequenceRandomSource(0));

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>可复现的伪随机源（仅测试统计形态使用，生产实现为加密级随机）。</summary>
    private sealed class CountingRandomSource(System.Random random) : Application.Interfaces.IRandomSource
    {
        /// <inheritdoc />
        public int NextInt(int exclusiveMax) => random.Next(exclusiveMax);
    }
}
