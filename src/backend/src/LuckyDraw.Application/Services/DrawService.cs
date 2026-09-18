using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LuckyDraw.Application.Common;
using LuckyDraw.Application.Dtos;
using LuckyDraw.Application.Interfaces;
using LuckyDraw.Application.Options;
using LuckyDraw.Domain.Entities;
using LuckyDraw.Domain.Enums;
using LuckyDraw.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LuckyDraw.Application.Services;

/// <summary>
/// 抽奖服务（MOD-03，事务宿主）：幂等 → 扣次 → 加权随机 → 条件扣库存 → 中奖记录 → 审计，
/// 全流程在同一 READ COMMITTED 事务内完成（D-01 / D-02 / D-03 / D-08）。
/// </summary>
public class DrawService : IDrawService
{
    /// <summary>库存条件扣减失败后的最大重抽轮数（D-01）。</summary>
    private const int MaxStockRetryRounds = 3;

    /// <summary>
    /// 数据库瞬时错误（死锁 1213 / 锁等待超时 1205）下整个事务的最大**尝试**次数 = 2（即重试 1 次）。
    /// 数值对齐 `docs/30-architecture.md` D-08「异常与重试」（原文「整个事务重试 1 次」）；
    /// 单次尝试最坏吃满服务端 `innodb_lock_wait_timeout`（默认 50s），故用户最坏等待 ≈ 2 × 50s = 100s。
    /// </summary>
    private const int MaxTransactionAttempts = 2;

    /// <summary>抽奖请求体恒为空对象，其规范化哈希用于幂等键复用体的完整性校验（D-03）。</summary>
    private static readonly string EmptyRequestHash =
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("{}")));

    private readonly IUserDrawQuotaRepository _quotaRepository;
    private readonly IPrizeRepository _prizeRepository;
    private readonly IDrawRequestRepository _drawRequestRepository;
    private readonly IWinningRecordRepository _winningRecordRepository;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IAuditService _auditService;
    private readonly ITransactionManager _transactionManager;
    private readonly IRandomSource _randomSource;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<DrawOptions> _drawOptions;
    private readonly IOptions<PrizeOptions> _prizeOptions;
    private readonly ILogger<DrawService> _logger;

    /// <summary>构造函数注入。</summary>
    public DrawService(
        IUserDrawQuotaRepository quotaRepository,
        IPrizeRepository prizeRepository,
        IDrawRequestRepository drawRequestRepository,
        IWinningRecordRepository winningRecordRepository,
        IIdempotencyStore idempotencyStore,
        IAuditService auditService,
        ITransactionManager transactionManager,
        IRandomSource randomSource,
        TimeProvider timeProvider,
        IOptions<DrawOptions> drawOptions,
        IOptions<PrizeOptions> prizeOptions,
        ILogger<DrawService> logger)
    {
        _quotaRepository = quotaRepository;
        _prizeRepository = prizeRepository;
        _drawRequestRepository = drawRequestRepository;
        _winningRecordRepository = winningRecordRepository;
        _idempotencyStore = idempotencyStore;
        _auditService = auditService;
        _transactionManager = transactionManager;
        _randomSource = randomSource;
        _timeProvider = timeProvider;
        _drawOptions = drawOptions;
        _prizeOptions = prizeOptions;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DrawQuotaDto> GetQuotaAsync(int userId, CancellationToken cancellationToken)
    {
        var drawDate = _timeProvider.GetDrawDateUtc8();
        var usedCount = await _quotaRepository.GetUsedCountAsync(userId, drawDate, cancellationToken);
        var dailyLimit = _drawOptions.Value.DailyLimit;

        return new DrawQuotaDto
        {
            RemainingAttempts = Math.Max(0, dailyLimit - usedCount),
            DailyLimit = dailyLimit,
            ResetAt = _timeProvider.GetNextResetAtUtc()
        };
    }

    /// <inheritdoc />
    public async Task<DrawResponseDto> DrawAsync(
        int userId,
        string userName,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var hasKey = !string.IsNullOrWhiteSpace(idempotencyKey);

        // 重复请求快速重放（Redis，可降级；正确性由 DrawRequest 唯一索引兜底）
        if (hasKey)
        {
            var cached = await _idempotencyStore.TryGetAsync(userId, idempotencyKey!, cancellationToken);
            if (cached is not null)
            {
                _logger.LogInformation("幂等结果缓存命中，重放首次结果：UserId={UserId}", userId);
                return cached;
            }
        }

        // 死锁 / 锁等待超时（1213 / 1205）：InnoDB 会回滚**整个事务**（牺牲者），因此只能重试整个事务；
        // 重试是幂等安全的 —— 事务回滚后流水占位一并消失，重试会重新抢占同一幂等键
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ExecuteDrawAsync(userId, userName, idempotencyKey, hasKey, cancellationToken);
            }
            catch (TransientDataException exception) when (attempt >= MaxTransactionAttempts)
            {
                // D-08「异常与重试」承诺：重试仍未成功 → CRITICAL 告警。
                // 终态错误码经用户裁决保持 `1001`「系统繁忙」（架构侧同步修订 D-08「异常与重试」的表述，本仓不动该契约文件）
                // REV-21（CHG-18）：计数口径为「重试次数」= 尝试次数 - 1，与同日志的 WRN「第 N 次」及 D-08
                // （30-architecture.md D-08「异常与重试」原文「整个事务重试 1 次」）自洽 —— 传 attempt - 1，勿改回 attempt。
                // 守护断言：DrawServiceTests.DrawAsync_WhenTransientErrorPersists_CriticalLogReportsRetryCountNotAttempts
                _logger.LogCritical(
                    exception,
                    "抽奖事务重试 {RetryCount} 次（共 {Attempts} 次尝试）后仍遇数据库瞬时错误（死锁 / 锁等待超时），已放弃本次抽奖：UserId={UserId}",
                    attempt - 1,
                    attempt,
                    userId);
                throw new BusinessException(ErrorCodes.SystemBusy, "系统繁忙，请稍后重试");
            }
            catch (TransientDataException exception)
            {
                _logger.LogWarning(
                    exception,
                    "抽奖事务遇数据库瞬时错误，重试整个事务（第 {Attempt} 次）：UserId={UserId}",
                    attempt,
                    userId);
            }
        }
    }

    /// <summary>单次抽奖事务：幂等占位 → 扣次 → 抽奖 → 扣库存 → 写记录 → 写审计 → 提交。</summary>
    private async Task<DrawResponseDto> ExecuteDrawAsync(
        int userId,
        string userName,
        string? idempotencyKey,
        bool hasKey,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var drawDate = _timeProvider.GetDrawDateUtc8();
        var dailyLimit = _drawOptions.Value.DailyLimit;

        await using var scope = await _transactionManager.BeginAsync(cancellationToken);
        try
        {
            // ① 抢占幂等权（锁序第一步，不可与扣次调换，D-08）
            var request = new DrawRequest
            {
                UserId = userId,
                IdempotencyKey = hasKey ? idempotencyKey : null,
                RequestHash = hasKey ? EmptyRequestHash : null,
                IsWin = false,
                RemainingAttempts = 0,
                CreateTime = now,
                UpdateTime = now
            };

            if (!await _drawRequestRepository.TryAddAsync(request, cancellationToken))
            {
                // 重复请求：回滚本事务后重放首次结果（D-03）
                await TryRollbackAsync(scope, cancellationToken);
                return await ReplayFirstResultAsync(userId, idempotencyKey!, cancellationToken);
            }

            // ② 条件扣次（影响 0 行 = 次数已用尽）
            var consumed = await _quotaRepository.TryConsumeAsync(userId, drawDate, dailyLimit, now, cancellationToken);
            if (!consumed)
            {
                throw new BusinessException(ErrorCodes.QuotaExhausted, "今日抽奖次数已用完，明日 0 点重置");
            }

            var usedCount = await _quotaRepository.GetUsedCountAsync(userId, drawDate, cancellationToken);
            var remainingAttempts = Math.Max(0, dailyLimit - usedCount);

            // ③④⑤ 候选集 → 加权随机 → 条件扣库存（失败重抽，≤3 轮）
            var picked = await PickCandidateAsync(userName, now, cancellationToken);
            var isWin = picked.Type != PrizeItemType.NoPrize;

            // ⑥ 中奖记录（冗余奖品名称快照，D-10）
            if (isWin)
            {
                await _winningRecordRepository.AddAsync(
                    new WinningRecord
                    {
                        UserId = userId,
                        PrizeItemId = picked.Id,
                        PrizeName = picked.Name,
                        CreateTime = now,
                        UpdateTime = now
                    },
                    cancellationToken);
            }

            // ⑦ 审计（同事务：审计失败 = 抽奖失败，宁可拒绝也不留无痕操作，D-08 / A2）
            await _auditService.LogAsync(
                new AuditEntry(
                    userId,
                    "draw",
                    "Draw",
                    request.Id.ToString(),
                    JsonSerializer.Serialize(new { prizeItemId = picked.Id, isWin, remaining = remainingAttempts })),
                cancellationToken);

            // ⑧ 回填流水结果（幂等重放的唯一来源）
            await _drawRequestRepository.SaveResultAsync(request.Id, picked.Id, isWin, remainingAttempts, now, cancellationToken);

            await scope.CommitAsync(cancellationToken);

            var result = new DrawResponseDto
            {
                ItemId = picked.Id,
                IsWin = isWin,
                RemainingAttempts = remainingAttempts
            };

            // ⑩ 结果缓存（best effort，失败不回滚业务事务）
            if (hasKey)
            {
                await _idempotencyStore.SetAsync(userId, idempotencyKey!, result, cancellationToken);
            }

            _logger.LogInformation(
                "抽奖完成：UserId={UserId} PrizeItemId={PrizeItemId} IsWin={IsWin} Remaining={Remaining}",
                userId,
                picked.Id,
                isWin,
                remainingAttempts);

            return result;
        }
        catch
        {
            await TryRollbackAsync(scope, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// 尽力回滚：死锁场景事务已被 InnoDB 回滚，此时再显式回滚可能抛错，
    /// 不得让回滚异常掩盖原始业务 / 瞬时异常。
    /// </summary>
    private async Task TryRollbackAsync(ITransactionScope scope, CancellationToken cancellationToken)
    {
        try
        {
            await scope.RollbackAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "回滚事务失败（事务可能已被数据库回滚）");
        }
    }

    /// <summary>重放首次结果：读取已提交的流水行并原样返回（不重复消耗次数）。</summary>
    private async Task<DrawResponseDto> ReplayFirstResultAsync(int userId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var existing = await _drawRequestRepository.GetByKeyAsync(userId, idempotencyKey, cancellationToken);
        if (existing is null || !string.Equals(existing.RequestHash, EmptyRequestHash, StringComparison.Ordinal))
        {
            // 唯一索引等待超时（并发同键尚未提交）或请求体哈希不一致 → 409「请勿重复提交」
            throw new BusinessException(ErrorCodes.IdempotencyConflict, "请勿重复提交", ErrorCodes.IdempotencyConflict);
        }

        var result = new DrawResponseDto
        {
            ItemId = existing.PrizeItemId ?? 0,
            IsWin = existing.IsWin,
            RemainingAttempts = existing.RemainingAttempts
        };

        await _idempotencyStore.SetAsync(userId, idempotencyKey, result, cancellationToken);
        return result;
    }

    /// <summary>构造候选集并完成抽取与库存扣减；库存被并发抽空时重读候选集重抽。</summary>
    private async Task<DrawCandidateDto> PickCandidateAsync(string userName, DateTime nowUtc, CancellationToken cancellationToken)
    {
        for (var round = 1; round <= MaxStockRetryRounds; round++)
        {
            var candidates = await _prizeRepository.GetDrawCandidatesAsync(cancellationToken);
            if (candidates.Count == 0)
            {
                throw new BusinessException(ErrorCodes.NoCandidate, "奖品已抽完，请稍后再来");
            }

            var chosen = ChooseCandidate(candidates, userName);

            // 「谢谢参与」不占库存，无需扣减
            if (chosen.Type == PrizeItemType.NoPrize)
            {
                return chosen;
            }

            if (await _prizeRepository.TryDecrementStockAsync(chosen.Id, nowUtc, cancellationToken))
            {
                return chosen;
            }

            _logger.LogWarning(
                "库存条件扣减影响 0 行（并发抽空），重读候选集重抽：PrizeItemId={PrizeItemId} 第 {Round} 轮",
                chosen.Id,
                round);
        }

        throw new BusinessException(ErrorCodes.NoCandidate, "奖品已抽完，请稍后再来");
    }

    /// <summary>选取结果条目：命中确定性配置则直接返回该条目，否则走整数权重的累积区间取样（D-04）。</summary>
    private DrawCandidateDto ChooseCandidate(IReadOnlyList<DrawCandidateDto> candidates, string userName)
    {
        var deterministic = _drawOptions.Value.Deterministic;
        if (deterministic.Enabled)
        {
            var forced = deterministic.ForcedResults.FirstOrDefault(
                item => string.Equals(item.UserName, userName, StringComparison.OrdinalIgnoreCase));

            if (forced is not null)
            {
                var target = candidates.FirstOrDefault(
                    item => string.Equals(item.Code, forced.PrizeItemCode, StringComparison.OrdinalIgnoreCase));

                if (target is not null)
                {
                    return target;
                }

                // 测试配置错误不产生 500：记 warning 并回退正常加权随机
                _logger.LogWarning(
                    "确定性配置中的 prizeItemCode 未命中候选集（{PrizeItemCode}），回退正常加权随机",
                    forced.PrizeItemCode);
            }
        }

        return WeightedPick(candidates);
    }

    /// <summary>整数权重 + 累积区间取样 + 加密级随机源（D-04）。</summary>
    private DrawCandidateDto WeightedPick(IReadOnlyList<DrawCandidateDto> candidates)
    {
        var totalWeight = 0;
        foreach (var candidate in candidates)
        {
            totalWeight += ResolveWeight(candidate);
        }

        if (totalWeight <= 0)
        {
            // 权重全为 0 属配置错误：回退为末位候选，避免除零与不确定行为
            _logger.LogWarning("候选集权重总和为 0，回退为末位候选（请检查 Prize:WeightOverrides 配置）");
        }

        return WeightedSampler.Pick(candidates, ResolveWeight, _randomSource);
    }

    /// <summary>解析条目权重（测试环境可被 Prize:WeightOverrides 覆盖，D-06）。</summary>
    private int ResolveWeight(DrawCandidateDto candidate)
    {
        var weight = _prizeOptions.Value.WeightOverrides.TryGetValue(candidate.Code, out var overridden)
            ? overridden
            : candidate.Weight;

        return Math.Max(0, weight);
    }
}
