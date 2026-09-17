using LuckyDraw.Application.Common;
using LuckyDraw.Application.Dtos;
using LuckyDraw.Domain.Entities;

namespace LuckyDraw.Application.Interfaces;

/// <summary>用户仓储（MOD-01）。</summary>
public interface IUserRepository
{
    /// <summary>按用户名查询（不区分大小写；唯一性判定必须 <c>IgnoreQueryFilters()</c>，规范 4.4）。</summary>
    Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken);

    /// <summary>按 Id 查询。</summary>
    Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken);

    /// <summary>判断用户名是否已被占用（含软删除记录，<c>IgnoreQueryFilters()</c>）。</summary>
    Task<bool> ExistsByUserNameAsync(string userName, CancellationToken cancellationToken);

    /// <summary>新增用户并返回带自增主键的实体。</summary>
    Task<User> AddAsync(User user, CancellationToken cancellationToken);
}

/// <summary>奖池仓储（MOD-02 / MOD-03）。</summary>
public interface IPrizeRepository
{
    /// <summary>查询启用中的奖池条目（白名单投影，不含权重 / 库存）。</summary>
    Task<IReadOnlyList<PrizePoolItemDto>> GetEnabledItemsAsync(CancellationToken cancellationToken);

    /// <summary>读取抽奖候选集投影（含权重 / 库存，仅服务端使用；库存状态实时读取，不缓存）。</summary>
    Task<IReadOnlyList<DrawCandidateDto>> GetDrawCandidatesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 条件扣减库存（D-01）：<c>UPDATE ... SET Stock = Stock - 1 WHERE Id = @id AND Stock &gt; 0</c>。
    /// 返回 false 表示影响 0 行（并发下已被抽空），调用方须重读候选集重抽。
    /// </summary>
    Task<bool> TryDecrementStockAsync(int prizeItemId, DateTime nowUtc, CancellationToken cancellationToken);
}

/// <summary>每日次数聚合行仓储（MOD-03 / D-02）。</summary>
public interface IUserDrawQuotaRepository
{
    /// <summary>查询当日已消耗次数（无行 = 0 消耗）。</summary>
    Task<int> GetUsedCountAsync(int userId, DateOnly drawDate, CancellationToken cancellationToken);

    /// <summary>
    /// 条件扣次（D-02）：<c>UPDATE ... SET UsedCount = UsedCount + 1 WHERE ... AND UsedCount &lt; @limit</c>；
    /// 行不存在时插入（并发首抽唯一键冲突则回到条件更新，最多 3 轮）。
    /// 返回 false 表示次数已用尽（影响 0 行）。
    /// </summary>
    Task<bool> TryConsumeAsync(int userId, DateOnly drawDate, int dailyLimit, DateTime nowUtc, CancellationToken cancellationToken);
}

/// <summary>抽奖请求流水仓储（MOD-05 幂等权，唯一索引为正确性底线）。</summary>
public interface IDrawRequestRepository
{
    /// <summary>
    /// 插入抽奖流水（事务第一条写入，抢占幂等键执行权）。
    /// 返回 false 表示唯一索引冲突（重复请求）——调用方回滚后读取首次结果重放。
    /// </summary>
    Task<bool> TryAddAsync(DrawRequest request, CancellationToken cancellationToken);

    /// <summary>按 (UserId, IdempotencyKey) 读取首次流水。</summary>
    Task<DrawRequest?> GetByKeyAsync(int userId, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>回填抽奖结果（itemId / isWin / remaining；刻意不走变更跟踪，避免实体级审计噪音）。</summary>
    Task SaveResultAsync(long drawRequestId, int prizeItemId, bool isWin, int remainingAttempts, DateTime nowUtc, CancellationToken cancellationToken);
}

/// <summary>中奖记录仓储（MOD-04）。记录只读：不提供更新 / 删除路径。</summary>
public interface IWinningRecordRepository
{
    /// <summary>新增中奖记录。</summary>
    Task AddAsync(WinningRecord record, CancellationToken cancellationToken);

    /// <summary>分页查询本人中奖记录（按中奖时间倒序）。</summary>
    Task<PageResult<WinningRecordDto>> QueryAsync(int userId, PageQuery query, CancellationToken cancellationToken);
}
