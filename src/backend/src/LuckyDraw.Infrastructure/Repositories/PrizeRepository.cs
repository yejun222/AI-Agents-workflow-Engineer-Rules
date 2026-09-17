using LuckyDraw.Application.Dtos;
using LuckyDraw.Application.Interfaces;
using LuckyDraw.Domain.Enums;
using LuckyDraw.Domain.Exceptions;
using LuckyDraw.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LuckyDraw.Infrastructure.Repositories;

/// <summary>奖池仓储实现（MOD-02 / MOD-03）。</summary>
public class PrizeRepository : IPrizeRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>构造函数注入。</summary>
    public PrizeRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PrizePoolItemDto>> GetEnabledItemsAsync(CancellationToken cancellationToken)
    {
        // D-07：白名单投影 —— 权重 / 库存 / Code / IsEnabled 结构上不存在于 DTO
        return await _dbContext.PrizeItems
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new PrizePoolItemDto
            {
                Id = x.Id,
                Name = x.Name,
                ShortName = x.ShortName,
                Type = (int)x.Type,
                DisplayOrder = x.DisplayOrder
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DrawCandidateDto>> GetDrawCandidatesAsync(CancellationToken cancellationToken)
    {
        // 候选集实时读取、不缓存：库存归零需即时剔除（FR-05-R3 / AC-14）
        return await _dbContext.PrizeItems
            .AsNoTracking()
            .Where(x => x.IsEnabled && (x.Type == PrizeItemType.NoPrize || x.Stock > 0))
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new DrawCandidateDto
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                Type = x.Type,
                Weight = x.Weight,
                Stock = x.Stock,
                DisplayOrder = x.DisplayOrder
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> TryDecrementStockAsync(int prizeItemId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        try
        {
            // D-01：条件 UPDATE + 影响行数判断，库存不足天然不超发（正确性底线落 MySQL）
            var affected = await _dbContext.PrizeItems
                .Where(x => x.Id == prizeItemId && x.Stock > 0)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.Stock, x => x.Stock - 1)
                        .SetProperty(x => x.UpdateTime, nowUtc),
                    cancellationToken);

            return affected == 1;
        }
        catch (Exception exception) when (MySqlErrors.IsTransient(exception))
        {
            // 死锁 / 锁等待超时：交由业务层重试整个事务（D-01 库存零超发由条件更新保证，重试安全）
            throw new TransientDataException("库存条件扣减遇数据库瞬时错误", exception);
        }
    }
}
