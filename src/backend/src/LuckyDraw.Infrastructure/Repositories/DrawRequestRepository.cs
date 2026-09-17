using LuckyDraw.Application.Common;
using LuckyDraw.Application.Interfaces;
using LuckyDraw.Domain.Entities;
using LuckyDraw.Domain.Exceptions;
using LuckyDraw.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LuckyDraw.Infrastructure.Repositories;

/// <summary>抽奖流水仓储实现（MOD-05 幂等权：唯一索引 (UserId, IdempotencyKey) 为强一致底线）。</summary>
public class DrawRequestRepository : IDrawRequestRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>构造函数注入。</summary>
    public DrawRequestRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<bool> TryAddAsync(DrawRequest request, CancellationToken cancellationToken)
    {
        _dbContext.DrawRequests.Add(request);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (MySqlErrors.IsDuplicateKey(exception))
        {
            // 重复请求（含并发同键）：脱离跟踪，交由上层回滚并重放首次结果
            _dbContext.Entry(request).State = EntityState.Detached;
            return false;
        }
        catch (Exception exception) when (MySqlErrors.IsLockWaitTimeout(exception))
        {
            // 并发同键等待首个事务提交超时（1205）→ 409「请勿重复提交」（D-03）
            _dbContext.Entry(request).State = EntityState.Detached;
            throw new BusinessException(ErrorCodes.IdempotencyConflict, "请勿重复提交", ErrorCodes.IdempotencyConflict);
        }
        catch (Exception exception) when (MySqlErrors.IsDeadlock(exception))
        {
            // 死锁（1213）：事务已被 InnoDB 整体回滚，交由业务层重试整个事务
            _dbContext.Entry(request).State = EntityState.Detached;
            throw new TransientDataException("抽奖流水占位遇数据库死锁", exception);
        }
    }

    /// <inheritdoc />
    public async Task<DrawRequest?> GetByKeyAsync(int userId, string idempotencyKey, CancellationToken cancellationToken)
    {
        return await _dbContext.DrawRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveResultAsync(
        long drawRequestId,
        int prizeItemId,
        bool isWin,
        int remainingAttempts,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        // 刻意使用 ExecuteUpdateAsync：流水表无需实体级审计（D-08 已述），且避免多余的一次读
        try
        {
            await _dbContext.DrawRequests
                .Where(x => x.Id == drawRequestId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.PrizeItemId, prizeItemId)
                        .SetProperty(x => x.IsWin, isWin)
                        .SetProperty(x => x.RemainingAttempts, remainingAttempts)
                        .SetProperty(x => x.UpdateTime, nowUtc),
                    cancellationToken);
        }
        catch (Exception exception) when (MySqlErrors.IsTransient(exception))
        {
            // 死锁（1213）/ 锁等待超时（1205）：交由业务层重试整个事务（D-08）；
            // ExecuteUpdateAsync 不经过变更跟踪，无待脱离实体
            throw new TransientDataException("抽奖流水结果回填遇数据库瞬时错误", exception);
        }
    }
}
