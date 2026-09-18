using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using LuckyDraw.Application.Common;
using LuckyDraw.Application.Dtos;
using LuckyDraw.Application.Interfaces;
using LuckyDraw.Application.Options;
using LuckyDraw.Application.Services;
using LuckyDraw.Domain.Entities;
using LuckyDraw.Domain.Enums;
using LuckyDraw.Domain.Exceptions;
using LuckyDraw.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LuckyDraw.UnitTests.Services;

/// <summary>
/// 抽奖服务业务分支（D-01 库存重抽 / D-02 扣次 / D-03 幂等重放 / D-04 加权随机 / D-05 跨日）。
/// 仓储与时钟均为替身，真实并发与时序由集成测试覆盖（见 tests/integration/**）。
/// </summary>
public class DrawServiceTests
{
    private const int UserId = 7;
    private const string UserName = "alice_01";

    /// <summary>幂等重放依赖的请求体哈希（与 DrawService 内部常量同源）。</summary>
    private static readonly string EmptyRequestHash =
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("{}")));

    /// <summary>次数用尽：抛 1501、事务回滚、不写记录（FR-04 / FR-07 / AC-10）。</summary>
    [Fact]
    public async Task DrawAsync_WhenQuotaExhausted_Throws1501AndRollsBack()
    {
        var harness = new Harness();
        harness.Quota.TryConsumeAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var act = async () => await harness.Service.DrawAsync(UserId, UserName, "key-1", CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<BusinessException>()).Which;
        exception.Code.Should().Be(1501);
        exception.Message.Should().Be("今日抽奖次数已用完，明日 0 点重置");
        await harness.Scope.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await harness.Records.DidNotReceive().AddAsync(Arg.Any<WinningRecord>(), Arg.Any<CancellationToken>());
    }

    /// <summary>同一幂等键重复提交：重放首次结果，不重复扣次、不重抽（FR-08-4 / AC-12）。</summary>
    [Fact]
    public async Task DrawAsync_OnDuplicateKey_ReplaysPersistedResult()
    {
        var harness = new Harness();
        harness.DrawRequests.TryAddAsync(Arg.Any<DrawRequest>(), Arg.Any<CancellationToken>()).Returns(false);
        harness.DrawRequests.GetByKeyAsync(UserId, "key-1", Arg.Any<CancellationToken>()).Returns(new DrawRequest
        {
            Id = 99,
            UserId = UserId,
            IdempotencyKey = "key-1",
            RequestHash = EmptyRequestHash,
            PrizeItemId = 4,
            IsWin = true,
            RemainingAttempts = 2
        });

        var result = await harness.Service.DrawAsync(UserId, UserName, "key-1", CancellationToken.None);

        result.ItemId.Should().Be(4);
        result.IsWin.Should().BeTrue();
        result.RemainingAttempts.Should().Be(2);
        await harness.Quota.DidNotReceive().TryConsumeAsync(
            Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await harness.Idempotency.Received(1).SetAsync(UserId, "key-1", Arg.Any<DrawResponseDto>(), Arg.Any<CancellationToken>());
    }

    /// <summary>首次请求尚未落定（并发同键 / 请求体不一致）：返回 409「请勿重复提交」，不泄露内部状态。</summary>
    [Fact]
    public async Task DrawAsync_OnDuplicateKeyWithoutSettledResult_ThrowsConflict()
    {
        var harness = new Harness();
        harness.DrawRequests.TryAddAsync(Arg.Any<DrawRequest>(), Arg.Any<CancellationToken>()).Returns(false);
        harness.DrawRequests.GetByKeyAsync(UserId, "key-1", Arg.Any<CancellationToken>()).Returns((DrawRequest?)null);

        var act = async () => await harness.Service.DrawAsync(UserId, UserName, "key-1", CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<BusinessException>()).Which;
        exception.Code.Should().Be(409);
        exception.Message.Should().Be("请勿重复提交");
    }

    /// <summary>确定性配置命中「谢谢参与」：只扣次、不扣库存、不写中奖记录（FR-05-R6 / AC-09）。</summary>
    [Fact]
    public async Task DrawAsync_WithForcedNoPrize_ConsumesQuotaOnly()
    {
        var harness = new Harness(deterministic: new DeterministicOptions
        {
            Enabled = true,
            ForcedResults = [new ForcedResultOptions { UserName = UserName, PrizeItemCode = "no-prize" }]
        });

        var result = await harness.Service.DrawAsync(UserId, UserName, "key-1", CancellationToken.None);

        result.ItemId.Should().Be(5);
        result.IsWin.Should().BeFalse();
        result.RemainingAttempts.Should().Be(2);
        await harness.Prizes.DidNotReceive().TryDecrementStockAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await harness.Records.DidNotReceive().AddAsync(Arg.Any<WinningRecord>(), Arg.Any<CancellationToken>());
        await harness.Scope.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>确定性配置命中一等奖：扣库存 + 写记录 + 审计同事务（FR-05-R5/R8 / AC-08）。</summary>
    [Fact]
    public async Task DrawAsync_WithForcedWin_DecrementsStockAndWritesRecord()
    {
        var harness = new Harness(deterministic: new DeterministicOptions
        {
            Enabled = true,
            ForcedResults = [new ForcedResultOptions { UserName = UserName, PrizeItemCode = "prize-keyboard" }]
        });

        var result = await harness.Service.DrawAsync(UserId, UserName, "key-1", CancellationToken.None);

        result.ItemId.Should().Be(1);
        result.IsWin.Should().BeTrue();
        await harness.Prizes.Received(1).TryDecrementStockAsync(1, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await harness.Records.Received(1).AddAsync(
            Arg.Is<WinningRecord>(record => record.PrizeItemId == 1 && record.PrizeName == "一等奖 · 机械键盘"),
            Arg.Any<CancellationToken>());
        await harness.Audit.Received(1).LogAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
        await harness.DrawRequests.Received(1).SaveResultAsync(
            Arg.Any<long>(), 1, true, 2, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    /// <summary>加权随机命中区间上界：hit=0 落首个候选（AC-16 的确定性投影）。</summary>
    [Fact]
    public async Task DrawAsync_UsesWeightedSampling_WithConfiguredWeights()
    {
        var harness = new Harness(hits: [0]);

        var result = await harness.Service.DrawAsync(UserId, UserName, "key-1", CancellationToken.None);

        result.ItemId.Should().Be(1);
        harness.Random.Should().BeOfType<SequenceRandomSource>()
            .Which.ObservedMaxima.Should().Equal(100);
    }

    /// <summary>权重覆盖（测试环境）：把「谢谢参与」权重压 0 后必中实物奖（FR-11-2）。</summary>
    [Fact]
    public async Task DrawAsync_WithWeightOverride_ExcludesZeroWeightItem()
    {
        var harness = new Harness(hits: [0], weightOverrides: new Dictionary<string, int> { ["no-prize"] = 0 });

        var result = await harness.Service.DrawAsync(UserId, UserName, "key-1", CancellationToken.None);

        result.ItemId.Should().Be(1);
        result.IsWin.Should().BeTrue();
    }

    /// <summary>库存被并发抽空（条件扣减影响 0 行）：重读候选集重抽，最终成功（D-01）。</summary>
    [Fact]
    public async Task DrawAsync_WhenStockDecrementConflicts_RetriesWithFreshCandidates()
    {
        var harness = new Harness(hits: [0, 0]);
        harness.Prizes.TryDecrementStockAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(false, true);

        var result = await harness.Service.DrawAsync(UserId, UserName, "key-1", CancellationToken.None);

        result.IsWin.Should().BeTrue();
        await harness.Prizes.Received(2).GetDrawCandidatesAsync(Arg.Any<CancellationToken>());
        await harness.Scope.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>连续 3 轮扣减均失败：抛 1502「奖品已抽完」并回滚（D-01 上限）。</summary>
    [Fact]
    public async Task DrawAsync_WhenStockAlwaysConflicts_Throws1502AfterMaxRounds()
    {
        var harness = new Harness(hits: [0, 0, 0]);
        harness.Prizes.TryDecrementStockAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var act = async () => await harness.Service.DrawAsync(UserId, UserName, "key-1", CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<BusinessException>()).Which;
        exception.Code.Should().Be(1502);
        await harness.Prizes.Received(3).GetDrawCandidatesAsync(Arg.Any<CancellationToken>());
        await harness.Scope.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>候选集为空（全部奖品库存耗尽且无「谢谢参与」）：抛 1502（FR-05-R3 / AC-14）。</summary>
    [Fact]
    public async Task DrawAsync_WhenNoCandidates_Throws1502()
    {
        var harness = new Harness(hits: [0]);
        harness.Prizes.GetDrawCandidatesAsync(Arg.Any<CancellationToken>()).Returns(new List<DrawCandidateDto>());

        var act = async () => await harness.Service.DrawAsync(UserId, UserName, "key-1", CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<BusinessException>()).Which;
        exception.Code.Should().Be(1502);
        exception.Message.Should().Be("奖品已抽完，请稍后再来");
    }

    /// <summary>剩余次数：跨日（UTC+8 零点）后按新自然日查询，历史消耗不影响新一天（FR-07 / AC-11）。</summary>
    [Fact]
    public async Task GetQuotaAsync_AfterUtc8Midnight_QueriesNewDay()
    {
        var harness = new Harness(now: new DateTimeOffset(2026, 9, 16, 16, 0, 0, TimeSpan.Zero));
        harness.Quota.GetUsedCountAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(0);

        var quota = await harness.Service.GetQuotaAsync(UserId, CancellationToken.None);

        await harness.Quota.Received(1).GetUsedCountAsync(UserId, new DateOnly(2026, 9, 17), Arg.Any<CancellationToken>());
        quota.RemainingAttempts.Should().Be(3);
        quota.DailyLimit.Should().Be(3);
        // 此刻为 UTC+8 2026-09-17 00:00，下次重置落在 UTC+8 2026-09-18 00:00（= UTC 2026-09-17T16:00Z）
        quota.ResetAt.Should().Be(new DateTimeOffset(2026, 9, 17, 16, 0, 0, TimeSpan.Zero));
    }

    /// <summary>已消耗超出上限（历史数据异常）时剩余次数不为负。</summary>
    [Fact]
    public async Task GetQuotaAsync_ClampsRemainingAtZero()
    {
        var harness = new Harness();
        harness.Quota.GetUsedCountAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(5);

        var quota = await harness.Service.GetQuotaAsync(UserId, CancellationToken.None);

        quota.RemainingAttempts.Should().Be(0);
    }

    /// <summary>未携带幂等键：不查缓存、不写缓存，正常消耗次数（FR-08-4）。</summary>
    [Fact]
    public async Task DrawAsync_WithoutIdempotencyKey_SkipsIdempotencyStore()
    {
        var harness = new Harness(hits: [0]);

        await harness.Service.DrawAsync(UserId, UserName, null, CancellationToken.None);

        await harness.Idempotency.DidNotReceive().TryGetAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await harness.Idempotency.DidNotReceive().SetAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<DrawResponseDto>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// REV-19（CHG-11 回归）：数据库瞬时错误（1213 死锁 / 1205 锁等待）按 D-08 重试**整个事务**；
    /// 第二次成功时结果正常返回、失败事务已回滚、只写一次审计（不因重试产生重复副作用）。
    /// </summary>
    [Fact]
    public async Task DrawAsync_WhenTransientErrorThenSuccess_RetriesWholeTransaction()
    {
        var harness = new Harness(hits: [0]);
        harness.DrawRequests.TryAddAsync(Arg.Any<DrawRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new TransientDataException("死锁（1213）"),
                _ => Task.FromResult(true));

        var result = await harness.Service.DrawAsync(UserId, UserName, "key-1", CancellationToken.None);

        result.RemainingAttempts.Should().Be(2);
        await harness.Transactions.Received(2).BeginAsync(Arg.Any<CancellationToken>());
        await harness.Scope.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await harness.Scope.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await harness.Audit.Received(1).LogAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// REV-19（CHG-11 回归）：瞬时错误持续存在时按 D-08 只重试 **1 次**（共 2 次尝试，CHG-17 对齐架构 D-08「异常与重试」），
    /// 耗尽后转为 `1001`「系统繁忙，请稍后重试」，不得把数据库异常透出为 500，也不得产生任何审计 / 记录副作用。
    /// </summary>
    [Fact]
    public async Task DrawAsync_WhenTransientErrorPersists_DegradesTo1001AfterRetryBudget()
    {
        var harness = new Harness(hits: [0]);
        harness.DrawRequests.TryAddAsync(Arg.Any<DrawRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new TransientDataException("锁等待超时（1205）"));

        var act = async () => await harness.Service.DrawAsync(UserId, UserName, "key-1", CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<BusinessException>()).Which;
        exception.Code.Should().Be(ErrorCodes.SystemBusy);
        exception.Message.Should().Be("系统繁忙，请稍后重试");

        await harness.Transactions.Received(2).BeginAsync(Arg.Any<CancellationToken>());
        await harness.Audit.DidNotReceive().LogAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
        await harness.Records.DidNotReceive().AddAsync(Arg.Any<WinningRecord>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// REV-21（CHG-18 守护断言）：重试预算耗尽的终态 **CRITICAL 告警计数口径**必须按「重试次数」报数 ——
    /// 重试次数 = 尝试次数 - 1，当前预算（2 次尝试）下 = **1**，与 D-08「整个事务重试 1 次」
    /// （`docs/30-architecture.md` D-08「异常与重试」）及同一次抽奖日志中 WRN 行的「第 1 次」自洽。
    /// 常量被改（预算漂移）或文案改回传 <c>attempt</c>（把尝试次数记成重试次数）时，本用例必须失败。
    /// </summary>
    [Fact]
    public async Task DrawAsync_WhenTransientErrorPersists_CriticalLogReportsRetryCountNotAttempts()
    {
        var harness = new Harness(hits: [0], logger: Substitute.For<ILogger<DrawService>>());
        harness.DrawRequests.TryAddAsync(Arg.Any<DrawRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new TransientDataException("锁等待超时（1205）"));

        var act = async () => await harness.Service.DrawAsync(UserId, UserName, "key-1", CancellationToken.None);

        await act.Should().ThrowAsync<BusinessException>();

        var attempts = harness.Transactions.ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(ITransactionManager.BeginAsync));
        var message = SingleCriticalLogMessage(harness.Logger);

        // 守护点 ①：当前重试预算 = 2 次尝试（DrawService 私有常量 MaxTransactionAttempts）。
        // 该常量若调整，必须同步复核 D-08「重试 1 次」并显式更新本断言 —— 不允许静默漂移。
        attempts.Should().Be(2, "重试预算（MaxTransactionAttempts）变更时必须同步复核 D-08 并更新本断言");
        // 守护点 ②：告警报的必须是「重试次数 = 尝试次数 - 1 = 1」；改回传 attempt 会输出「重试 2 次」并在此失败
        message.Should().Contain(
            $"重试 {attempts - 1} 次（共 {attempts} 次尝试）",
            "CRITICAL 告警必须报「重试次数 = 尝试次数 - 1」，不得把尝试次数原样当成重试次数输出（REV-21）");
    }

    /// <summary>
    /// BUG-05：抽奖事务**后半段写入点**（⑥ 中奖记录 / ⑦ 审计 / ⑧ 回填流水）的瞬时错误必须进入
    /// 整事务重试链——首次失败回滚、第二次成功返回，且副作用不重复（只提交一次、只写一条审计）。
    /// 与集成测试 <c>TransientErrorClassificationTests</c> 互补：那边证明真实 1205 → `TransientDataException`，
    /// 这边证明该异常会被重试而不是直接冒泡。
    /// </summary>
    /// <param name="writePoint">注入瞬时错误的写入点：winning-record（⑥）/ audit-log（⑦）/ save-result（⑧）。</param>
    [Theory]
    [InlineData("winning-record")]
    [InlineData("audit-log")]
    [InlineData("save-result")]
    public async Task DrawAsync_WhenLateWritePointHitsTransientError_RetriesWholeTransaction(string writePoint)
    {
        // 失败点在 ⑤ 之后，重试会重新抽取 → 需要两次随机命中（脚本耗尽会抛错，不允许多掷）
        var harness = new Harness(hits: [0, 0]);
        var transient = new TransientDataException($"{writePoint} 遇数据库瞬时错误（死锁 1213 / 锁等待超时 1205）");

        switch (writePoint)
        {
            case "winning-record":
                harness.Records.AddAsync(Arg.Any<WinningRecord>(), Arg.Any<CancellationToken>())
                    .Returns(_ => throw transient, _ => Task.CompletedTask);
                break;
            case "audit-log":
                harness.Audit.LogAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
                    .Returns(_ => throw transient, _ => Task.CompletedTask);
                break;
            default:
                harness.DrawRequests.SaveResultAsync(
                        Arg.Any<long>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<DateTime>(),
                        Arg.Any<CancellationToken>())
                    .Returns(_ => throw transient, _ => Task.CompletedTask);
                break;
        }

        var result = await harness.Service.DrawAsync(UserId, UserName, "key-1", CancellationToken.None);

        // 结果由第二次尝试给出；两次尝试各自完整走完自己的事务（首次被回滚、第二次提交）
        result.ItemId.Should().Be(1);
        result.IsWin.Should().BeTrue();
        result.RemainingAttempts.Should().Be(2);
        await harness.Transactions.Received(2).BeginAsync(Arg.Any<CancellationToken>());
        await harness.Scope.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await harness.Scope.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        // ⑥ 每次尝试都会写中奖记录（首次随事务回滚）。替身不带事务语义，**不产生重复行**由集成测试的行数断言承担
        await harness.Records.Received(2).AddAsync(Arg.Any<WinningRecord>(), Arg.Any<CancellationToken>());
        // ⑦ 的调用次数取决于失败点：失败点在 ⑥ 时首次尝试走不到审计，其余两种情况两次尝试都会走到
        await harness.Audit.Received(writePoint == "winning-record" ? 1 : 2)
            .LogAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// BUG-05：后半段写入点的瞬时错误**持续存在**时同样受重试上限约束
    /// （D-08 的 1 次重试 / 共 2 次尝试，CHG-17 对齐后 → `1001`），
    /// 不得让 ⑥⑦⑧ 的错误越过重试链直接冒泡为 500。
    /// </summary>
    /// <param name="writePoint">注入瞬时错误的写入点：winning-record（⑥）/ audit-log（⑦）/ save-result（⑧）。</param>
    [Theory]
    [InlineData("winning-record")]
    [InlineData("audit-log")]
    [InlineData("save-result")]
    public async Task DrawAsync_WhenLateWritePointTransientErrorPersists_DegradesTo1001(string writePoint)
    {
        // 每次尝试都会重新抽取（失败点在 ⑤ 之后），2 次尝试 = 2 次随机命中
        var harness = new Harness(hits: [0, 0]);
        var transient = new TransientDataException($"{writePoint} 遇数据库瞬时错误（死锁 1213 / 锁等待超时 1205）");

        switch (writePoint)
        {
            case "winning-record":
                harness.Records.AddAsync(Arg.Any<WinningRecord>(), Arg.Any<CancellationToken>())
                    .Returns<Task>(_ => throw transient);
                break;
            case "audit-log":
                harness.Audit.LogAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
                    .Returns<Task>(_ => throw transient);
                break;
            default:
                harness.DrawRequests.SaveResultAsync(
                        Arg.Any<long>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<DateTime>(),
                        Arg.Any<CancellationToken>())
                    .Returns<Task>(_ => throw transient);
                break;
        }

        var act = async () => await harness.Service.DrawAsync(UserId, UserName, "key-1", CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<BusinessException>()).Which;
        exception.Code.Should().Be(ErrorCodes.SystemBusy);
        exception.Message.Should().Be("系统繁忙，请稍后重试");
        await harness.Transactions.Received(2).BeginAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// REV-19：回滚本身抛异常时，**原始业务异常不得被掩盖**（回滚失败只记 Debug 日志）。
    /// 用 1501（次数用尽）构造：它发生在事务内、且必须原样上抛为 1501。
    /// </summary>
    [Fact]
    public async Task DrawAsync_WhenRollbackFails_OriginalExceptionIsNotMasked()
    {
        var harness = new Harness(hits: [0]);
        harness.Quota.TryConsumeAsync(
                Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(false);
        harness.Scope.RollbackAsync(Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("连接已断开，无法回滚"));

        var act = async () => await harness.Service.DrawAsync(UserId, UserName, "key-1", CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<BusinessException>()).Which;
        exception.Code.Should().Be(1501);
        exception.Message.Should().Be("今日抽奖次数已用完，明日 0 点重置");
        await harness.Scope.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 取出替身 logger 记录的**唯一一条** CRITICAL 日志的正文（经 logger 自带的 formatter 委托渲染，与落盘文案一致）。
    /// 断言「有且仅有一条」同时防「告警漏打」与「告警重复」两类回归。
    /// </summary>
    /// <param name="logger">注入 <see cref="DrawService"/> 的日志替身。</param>
    private static string SingleCriticalLogMessage(ILogger<DrawService> logger)
    {
        var criticalCalls = logger.ReceivedCalls()
            .Select(call => call.GetArguments())
            .Where(arguments => arguments.Length == 5 && arguments[0] is LogLevel.Critical)
            .ToList();

        criticalCalls.Should().HaveCount(1, "重试预算耗尽时必须有且仅有一条 CRITICAL 告警（D-08）");

        var arguments = criticalCalls[0];
        var formatter = (Delegate)arguments[4]!;
        return (string)formatter.DynamicInvoke(arguments[2], arguments[3])!;
    }

    /// <summary>抽奖服务测试宿主：仓储 / 缓存 / 审计 / 时钟 / 随机源全部为可控替身。</summary>
    private sealed class Harness
    {
        public Harness(
            int[]? hits = null,
            int dailyLimit = 3,
            DeterministicOptions? deterministic = null,
            Dictionary<string, int>? weightOverrides = null,
            DateTimeOffset? now = null,
            ILogger<DrawService>? logger = null)
        {
            Random = new SequenceRandomSource(hits ?? []);
            Clock = new FixedTimeProvider(now ?? new DateTimeOffset(2026, 9, 17, 2, 0, 0, TimeSpan.Zero));
            Logger = logger ?? NullLogger<DrawService>.Instance;

            Transactions.BeginAsync(Arg.Any<CancellationToken>()).Returns(Scope);
            Scope.CommitAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            Scope.RollbackAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

            Quota.TryConsumeAsync(
                    Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                .Returns(true);
            Quota.GetUsedCountAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(1);

            DrawRequests.TryAddAsync(Arg.Any<DrawRequest>(), Arg.Any<CancellationToken>()).Returns(true);
            DrawRequests.SaveResultAsync(
                    Arg.Any<long>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            Prizes.GetDrawCandidatesAsync(Arg.Any<CancellationToken>()).Returns(DefaultCandidates());
            Prizes.TryDecrementStockAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);

            Idempotency.TryGetAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns((DrawResponseDto?)null);
            Idempotency.SetAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<DrawResponseDto>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            Audit.LogAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            Records.AddAsync(Arg.Any<WinningRecord>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

            Service = new DrawService(
                Quota,
                Prizes,
                DrawRequests,
                Records,
                Idempotency,
                Audit,
                Transactions,
                Random,
                Clock,
                Options.Create(new DrawOptions
                {
                    DailyLimit = dailyLimit,
                    Deterministic = deterministic ?? new DeterministicOptions()
                }),
                Options.Create(new PrizeOptions { WeightOverrides = weightOverrides ?? new Dictionary<string, int>() }),
                Logger);
        }

        public IUserDrawQuotaRepository Quota { get; } = Substitute.For<IUserDrawQuotaRepository>();

        public IPrizeRepository Prizes { get; } = Substitute.For<IPrizeRepository>();

        public IDrawRequestRepository DrawRequests { get; } = Substitute.For<IDrawRequestRepository>();

        public IWinningRecordRepository Records { get; } = Substitute.For<IWinningRecordRepository>();

        public IIdempotencyStore Idempotency { get; } = Substitute.For<IIdempotencyStore>();

        public IAuditService Audit { get; } = Substitute.For<IAuditService>();

        public ITransactionManager Transactions { get; } = Substitute.For<ITransactionManager>();

        public ITransactionScope Scope { get; } = Substitute.For<ITransactionScope>();

        public IRandomSource Random { get; }

        public FixedTimeProvider Clock { get; }

        public DrawService Service { get; }

        /// <summary>日志替身（默认 <see cref="NullLogger{T}"/>；需核验告警文案的用例可注入替身）。</summary>
        public ILogger<DrawService> Logger { get; }

        /// <summary>默认奖池候选集（权重 / 库存按 FR-03 默认值，顺序即扇区顺序）。</summary>
        private static List<DrawCandidateDto> DefaultCandidates() =>
        [
            new() { Id = 1, Code = "prize-keyboard", Name = "一等奖 · 机械键盘", Type = PrizeItemType.Physical, Weight = 1, Stock = 3, DisplayOrder = 1 },
            new() { Id = 2, Code = "prize-earbuds", Name = "二等奖 · 蓝牙耳机", Type = PrizeItemType.Physical, Weight = 3, Stock = 10, DisplayOrder = 2 },
            new() { Id = 3, Code = "prize-mug", Name = "三等奖 · 定制马克杯", Type = PrizeItemType.Physical, Weight = 10, Stock = 50, DisplayOrder = 3 },
            new() { Id = 4, Code = "prize-coupon", Name = "幸运奖 · 平台优惠券", Type = PrizeItemType.Virtual, Weight = 20, Stock = 200, DisplayOrder = 4 },
            new() { Id = 5, Code = "no-prize", Name = "谢谢参与", Type = PrizeItemType.NoPrize, Weight = 66, Stock = 0, DisplayOrder = 5 }
        ];
    }
}
