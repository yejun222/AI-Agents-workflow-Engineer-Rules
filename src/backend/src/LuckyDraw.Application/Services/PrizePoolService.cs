using LuckyDraw.Application.Dtos;
using LuckyDraw.Application.Interfaces;

namespace LuckyDraw.Application.Services;

/// <summary>
/// 奖池查询服务（MOD-02）：只输出启用中的条目，且 DTO 结构上不含权重 / 库存（D-07）。
/// </summary>
public class PrizePoolService : IPrizePoolService
{
    private readonly IPrizeRepository _prizeRepository;

    /// <summary>构造函数注入。</summary>
    public PrizePoolService(IPrizeRepository prizeRepository)
    {
        _prizeRepository = prizeRepository;
    }

    /// <inheritdoc />
    public async Task<PrizePoolResponse> GetPoolAsync(CancellationToken cancellationToken)
    {
        var items = await _prizeRepository.GetEnabledItemsAsync(cancellationToken);
        return new PrizePoolResponse { Items = items };
    }
}
