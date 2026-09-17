using System.Net;
using System.Text.Json;
using FluentAssertions;
using LuckyDraw.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace LuckyDraw.IntegrationTests;

/// <summary>
/// 并发与一致性（FR-08）：D-01 库存零超发、D-02 次数不超扣、D-03 幂等重放（含数据库唯一索引路径）。
/// 覆盖 AC-12 / AC-13，是与「真实 MySQL」绑定的正确性卡点（编译通过不作为依据）。
/// </summary>
[Collection(IntegrationCollection.Name)]
public class ConcurrencyTests(IntegrationFixture fixture)
{
    /// <summary>AC-13 / D-02：同一用户 10 个并发请求（各自幂等键）只能消耗 3 次，超出的全部 1501。</summary>
    [Fact]
    public async Task ConcurrentDraws_BySameUser_NeverExceedDailyLimit()
    {
        await fixture.ResetAsync();
        var token = await fixture.RegisterAsync();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => fixture.DrawAsync(token, Guid.NewGuid().ToString())));

        var envelopes = new List<IntegrationFixture.Envelope<IntegrationFixture.DrawData>>();
        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            envelopes.Add(await IntegrationFixture.ReadDrawAsync(response));
        }

        envelopes.Count(envelope => envelope.Code == 0).Should().Be(3);
        envelopes.Count(envelope => envelope.Code == 1501).Should().Be(7);

        var usedCount = await fixture.QueryAsync(db => db.Database
            .SqlQueryRaw<int>("SELECT `UsedCount` AS `Value` FROM `UserDrawQuota`")
            .SingleAsync());
        usedCount.Should().Be(3);
    }

    /// <summary>
    /// REV-19（CHG-11 回归）：同用户高并发（含死锁 / 锁等待可能的路径）**不得出现 500 / 系统内部错误**——
    /// 瞬时错误按 D-08 整事务重试，重试耗尽也必须降级为 `1001`；业务结果只允许 0 / 1001 / 1501 / 409。
    /// </summary>
    [Fact]
    public async Task ConcurrentDraws_BySameUser_NeverSurfaceServerError()
    {
        await fixture.ResetAsync();
        var token = await fixture.RegisterAsync();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 12).Select(_ => fixture.DrawAsync(token, Guid.NewGuid().ToString())));

        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().NotContain("系统内部错误");
            body.Should().NotContain("\"code\":500");

            using var document = JsonDocument.Parse(body);
            var code = document.RootElement.GetProperty("code").GetInt32();
            new[] { 0, ErrorCodes.SystemBusy, ErrorCodes.QuotaExhausted, ErrorCodes.IdempotencyConflict }
                .Should().Contain(code, "瞬时错误必须被降级为 1001，不得透出 500");
        }

        // 成功次数仍受每日上限约束（重试不代表多扣次）
        var usedCount = await fixture.QueryAsync(db => db.Database
            .SqlQueryRaw<int>("SELECT `UsedCount` AS `Value` FROM `UserDrawQuota`")
            .SingleAsync());
        usedCount.Should().Be(3);
    }

    /// <summary>AC-13 / D-01：10 名用户并发抽取同一奖品（库存 3）恰好中出 3 次，库存归零不为负。</summary>
    [Fact]
    public async Task ConcurrentDraws_OnScarcePrize_NeverOversellStock()
    {
        await fixture.ResetAsync();
        // 权重全部倾斜到一等奖（库存 3），其余为 0：并发下条件扣减是唯一防线
        await fixture.BiasKeyboardWeightAsync();

        var tokens = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            tokens.Add(await fixture.RegisterAsync());
        }

        var responses = await Task.WhenAll(
            tokens.Select(token => fixture.DrawAsync(token, Guid.NewGuid().ToString())));
        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var keyboardWins = await fixture.QueryAsync(db => db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS `Value` FROM `WinningRecord` WHERE `PrizeItemId` = 1")
            .SingleAsync());
        keyboardWins.Should().Be(3);

        var keyboardStock = await fixture.QueryAsync(db => db.Database
            .SqlQueryRaw<int>("SELECT `Stock` AS `Value` FROM `PrizeItem` WHERE `Code` = 'prize-keyboard'")
            .SingleAsync());
        keyboardStock.Should().Be(0);

        var negativeStock = await fixture.QueryAsync(db => db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS `Value` FROM `PrizeItem` WHERE `Stock` < 0")
            .SingleAsync());
        negativeStock.Should().Be(0);
    }

    /// <summary>AC-12 / D-03：同一幂等键并发提交 → 结果一致且只消耗一次（数据库唯一索引兜底）。</summary>
    [Fact]
    public async Task ConcurrentDraws_WithSameIdempotencyKey_ConsumeQuotaOnce()
    {
        await fixture.ResetAsync();
        var token = await fixture.RegisterAsync();
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 3).Select(_ => fixture.DrawAsync(token, key)));

        var envelopes = new List<IntegrationFixture.Envelope<IntegrationFixture.DrawData>>();
        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            envelopes.Add(await IntegrationFixture.ReadDrawAsync(response));
        }

        var succeeded = envelopes.Where(envelope => envelope.Code == 0).ToList();
        succeeded.Should().HaveCount(3, "重复提交必须重放首次结果，而不是报错或重复扣次");

        succeeded.Select(envelope => envelope.Data!.ItemId).Distinct().Should().HaveCount(1);
        succeeded.Select(envelope => envelope.Data!.IsWin).Distinct().Should().HaveCount(1);
        succeeded.Select(envelope => envelope.Data!.RemainingAttempts).Distinct().Should().HaveCount(1);

        var usedCount = await fixture.QueryAsync(db => db.Database
            .SqlQueryRaw<int>("SELECT `UsedCount` AS `Value` FROM `UserDrawQuota`")
            .SingleAsync());
        usedCount.Should().Be(1);
    }

    /// <summary>
    /// AC-12 / D-03：清空 Redis 后重放同一幂等键（模拟缓存失效降级），
    /// 结果必须与首次一致 —— 正确性来自 `DrawRequest` 唯一索引，Redis 只是加速层。
    /// </summary>
    [Fact]
    public async Task ReplayAfterCacheFlush_ReturnsSameResultFromDatabase()
    {
        await fixture.ResetAsync();
        var token = await fixture.RegisterAsync();
        var key = Guid.NewGuid().ToString();

        var first = await IntegrationFixture.ReadDrawAsync(await fixture.DrawAsync(token, key));
        first.Code.ShouldBeSuccess();

        // 仅清 Redis，数据库流水保留：走「唯一索引冲突 → 读取首次结果」路径
        await fixture.ResetIdempotencyCacheAsync();

        var replay = await IntegrationFixture.ReadDrawAsync(await fixture.DrawAsync(token, key));

        replay.Code.ShouldBeSuccess();
        replay.Data!.ItemId.Should().Be(first.Data!.ItemId);
        replay.Data.IsWin.Should().Be(first.Data.IsWin);
        replay.Data.RemainingAttempts.Should().Be(first.Data.RemainingAttempts);

        var usedCount = await fixture.QueryAsync(db => db.Database
            .SqlQueryRaw<int>("SELECT `UsedCount` AS `Value` FROM `UserDrawQuota`")
            .SingleAsync());
        usedCount.Should().Be(1, "重放不得重复消耗次数");
    }
}
