using LuckyDraw.Application.Interfaces;
using LuckyDraw.Domain.Entities;
using LuckyDraw.Domain.Exceptions;
using LuckyDraw.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LuckyDraw.Infrastructure.Repositories;

/// <summary>
/// 每日次数仓储实现（MOD-03 / D-02）：扣次走条件 UPDATE，行不存在时插入，
/// 并发首抽靠唯一索引 (UserId, DrawDate) 兜底后回到条件更新（最多 3 轮）。
/// </summary>
public class UserDrawQuotaRepository : IUserDrawQuotaRepository
{
    /// <summary>并发首抽的最大重试轮数。</summary>
    private const int MaxRounds = 3;

    private readonly AppDbContext _dbContext;

    /// <summary>构造函数注入。</summary>
    public UserDrawQuotaRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<int> GetUsedCountAsync(int userId, DateOnly drawDate, CancellationToken cancellationToken)
    {
        var usedCount = await _dbContext.UserDrawQuotas
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.DrawDate == drawDate)
            .Select(x => (int?)x.UsedCount)
            .FirstOrDefaultAsync(cancellationToken);

        return usedCount ?? 0;
    }

    /// <inheritdoc />
    public async Task<bool> TryConsumeAsync(
        int userId,
        DateOnly drawDate,
        int dailyLimit,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        for (var round = 1; round <= MaxRounds; round++)
        {
            try
            {
                // 条件 UPDATE：影响 0 行 = 行不存在或次数已达上限
                var affected = await _dbContext.UserDrawQuotas
                    .Where(x => x.UserId == userId && x.DrawDate == drawDate && x.UsedCount < dailyLimit)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(x => x.UsedCount, x => x.UsedCount + 1)
                            .SetProperty(x => x.UpdateTime, nowUtc),
                        cancellationToken);

                if (affected == 1)
                {
                    return true;
                }

                var rowExists = await _dbContext.UserDrawQuotas
                    .AsNoTracking()
                    .AnyAsync(x => x.UserId == userId && x.DrawDate == drawDate, cancellationToken);

                if (rowExists)
                {
                    // 行存在但条件不满足 → 次数确实已用尽
                    return false;
                }

                // 当日首次抽奖：插入新行（并发时唯一键冲突 → 回到条件更新）
                var quota = new UserDrawQuota
                {
                    UserId = userId,
                    DrawDate = drawDate,
                    UsedCount = 1,
                    CreateTime = nowUtc,
                    UpdateTime = nowUtc
                };

                _dbContext.UserDrawQuotas.Add(quota);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException exception) when (MySqlErrors.IsDuplicateKey(exception))
            {
                // 并发首抽：脱离跟踪后重试条件更新，避免脏实体被后续 SaveChanges 重复插入
                DetachPendingQuotas();
            }
            catch (Exception exception) when (MySqlErrors.IsTransient(exception))
            {
                // 死锁 / 锁等待超时：InnoDB 已回滚整个事务，交由业务层重试整个事务（禁止就地重试单条语句）
                DetachPendingQuotas();
                throw new TransientDataException("每日次数条件扣减遇数据库瞬时错误", exception);
            }
        }

        return false;
    }

    /// <summary>把尚未落库的配额行脱离跟踪（并发冲突 / 瞬时错误后重试前必须清理）。</summary>
    private void DetachPendingQuotas()
    {
        foreach (var entry in _dbContext.ChangeTracker.Entries<UserDrawQuota>().ToList())
        {
            if (entry.State == EntityState.Added)
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}
