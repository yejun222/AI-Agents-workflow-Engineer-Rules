using System.Security.Cryptography;
using LuckyDraw.Application.Interfaces;

namespace LuckyDraw.Infrastructure.Random;

/// <summary>
/// 生产随机源（D-04）：使用加密级 <see cref="RandomNumberGenerator"/>，不可预测；
/// 刻意不使用 <c>Random.Shared</c>（种子可推断 = 抽奖结果可被预测）。
/// </summary>
public class SystemRandomSource : IRandomSource
{
    /// <inheritdoc />
    public int NextInt(int exclusiveMax)
    {
        if (exclusiveMax <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exclusiveMax), "随机上界必须为正整数。");
        }

        return RandomNumberGenerator.GetInt32(exclusiveMax);
    }
}
