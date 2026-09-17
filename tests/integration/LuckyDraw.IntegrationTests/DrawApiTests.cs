using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LuckyDraw.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace LuckyDraw.IntegrationTests;

/// <summary>
/// API-05 / API-06 / API-07 / API-08 端到端：奖池、剩余次数、抽奖、我的中奖记录。
/// 覆盖 AC-06 / AC-07 / AC-10 / AC-15 与 D-07（权重库存不下发）。
/// </summary>
[Collection(IntegrationCollection.Name)]
public class DrawApiTests(IntegrationFixture fixture)
{
    /// <summary>AC-06 / D-07：奖池返回 5 个条目、按 displayOrder 升序，且**响应中不含 weight / stock**。</summary>
    [Fact]
    public async Task GetPrizes_ReturnsPoolWithoutWeightOrStock()
    {
        await fixture.ResetAsync();
        var token = await fixture.RegisterAsync();

        var response = await fixture.SendAsync(HttpMethod.Get, "/api/v1/prizes", token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(raw);
        document.RootElement.GetProperty("code").GetInt32().ShouldBeSuccess();

        var items = document.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCount(5);
        items.Select(item => item.GetProperty("displayOrder").GetInt32()).Should().BeInAscendingOrder();
        items.Should().Contain(item => item.GetProperty("name").GetString() == "谢谢参与");

        raw.ToLowerInvariant().Should().NotContain("weight");
        raw.ToLowerInvariant().Should().NotContain("stock");
    }

    /// <summary>AC-07：初始剩余 3 次，重置时刻在未来（次日 00:00 UTC+8）。</summary>
    [Fact]
    public async Task GetQuota_InitiallyReturnsDailyLimit()
    {
        await fixture.ResetAsync();
        var token = await fixture.RegisterAsync();

        var response = await fixture.SendAsync(HttpMethod.Get, "/api/v1/draw/quota", token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<IntegrationFixture.Envelope<IntegrationFixture.QuotaData>>(IntegrationFixture.JsonOptions);
        envelope!.Code.ShouldBeSuccess();
        envelope.Data!.RemainingAttempts.Should().Be(3);
        envelope.Data.DailyLimit.Should().Be(3);
        envelope.Data.ResetAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    /// <summary>AC-07 / FR-04：抽奖后剩余次数立即下降 1，且结果条目来自奖池。</summary>
    [Fact]
    public async Task Draw_DecrementsRemainingAttempts()
    {
        await fixture.ResetAsync();
        var token = await fixture.RegisterAsync();

        var response = await fixture.DrawAsync(token, Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await IntegrationFixture.ReadDrawAsync(response);
        envelope.Code.ShouldBeSuccess();
        envelope.Data!.ItemId.Should().BeInRange(1, 5);
        envelope.Data.RemainingAttempts.Should().Be(2);

        var quotaResponse = await fixture.SendAsync(HttpMethod.Get, "/api/v1/draw/quota", token);
        var quota = await quotaResponse.Content.ReadFromJsonAsync<IntegrationFixture.Envelope<IntegrationFixture.QuotaData>>(IntegrationFixture.JsonOptions);
        quota!.Data!.RemainingAttempts.Should().Be(2);
    }

    /// <summary>AC-10：当日 3 次用尽后第 4 次返回 1501「今日抽奖次数已用完，明日 0 点重置」。</summary>
    [Fact]
    public async Task Draw_AfterLimitExhausted_Reports1501()
    {
        await fixture.ResetAsync();
        var token = await fixture.RegisterAsync();

        for (var i = 0; i < 3; i++)
        {
            var response = await fixture.DrawAsync(token, Guid.NewGuid().ToString());
            var envelope = await IntegrationFixture.ReadDrawAsync(response);
            envelope.Code.ShouldBeSuccess();
        }

        var fourth = await fixture.DrawAsync(token, Guid.NewGuid().ToString());

        fourth.StatusCode.Should().Be(HttpStatusCode.OK);
        var fourthEnvelope = await IntegrationFixture.ReadDrawAsync(fourth);
        fourthEnvelope.Code.Should().Be(ErrorCodes.QuotaExhausted);
        fourthEnvelope.Message.Should().Be("今日抽奖次数已用完，明日 0 点重置");

        // 次数用尽不产生额外记录（仅 3 次消耗）
        var used = await fixture.QueryAsync(db => db.Database
            .SqlQueryRaw<int>("SELECT `UsedCount` AS `Value` FROM `UserDrawQuota`")
            .ToListAsync());
        used.Should().ContainSingle().Which.Should().Be(3);
    }

    /// <summary>AC-08 / AC-15：必中场景下写中奖记录，记录接口只返回本人数据且字段完整。</summary>
    [Fact]
    public async Task Draw_OnGuaranteedWin_WritesRecordVisibleToOwnerOnly()
    {
        await fixture.ResetAsync();
        // 键盘权重压到绝对优势（其余为 0）：抽必定落在一等奖
        await fixture.BiasKeyboardWeightAsync();
        var (_, ownerToken) = await fixture.RegisterUserAsync();
        var (_, otherToken) = await fixture.RegisterUserAsync();

        var draw = await IntegrationFixture.ReadDrawAsync(await fixture.DrawAsync(ownerToken, Guid.NewGuid().ToString()));
        draw.Code.ShouldBeSuccess();
        draw.Data!.IsWin.Should().BeTrue();
        draw.Data.ItemId.Should().Be(1);

        var recordsResponse = await fixture.SendAsync(HttpMethod.Get, "/api/v1/records?pageIndex=1&pageSize=10", ownerToken);
        var raw = await recordsResponse.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(raw);
        document.RootElement.GetProperty("code").GetInt32().ShouldBeSuccess();

        var data = document.RootElement.GetProperty("data");
        data.GetProperty("totalCount").GetInt32().Should().Be(1);
        var item = data.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("prizeName").GetString().Should().Be("一等奖 · 机械键盘");
        // BUG-02：winTime 为 ISO 8601（UTC），必须自描述时区标识（`Z` / `+00:00`），
        // 否则消费者按本地时间解析会产生 8 小时偏差（架构 §5.1 时间口径 / API-08）
        var winTime = item.GetProperty("winTime").GetString()!;
        winTime.Should().NotBeNullOrWhiteSpace();
        (winTime.EndsWith('Z') || winTime.EndsWith("+00:00", StringComparison.Ordinal))
            .Should().BeTrue($"winTime 须携带 UTC 时区标识，实际为 {winTime}");
        item.GetProperty("winTime").GetDateTimeOffset().Offset.Should().Be(TimeSpan.Zero);

        // 越权隔离：他人记录不可见（FR-09-1）
        var otherResponse = await fixture.SendAsync(HttpMethod.Get, "/api/v1/records", otherToken);
        var otherRaw = await otherResponse.Content.ReadAsStringAsync();
        using var otherDocument = JsonDocument.Parse(otherRaw);
        otherDocument.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    /// <summary>AC-15：无中奖记录时返回空列表（前端展示空态文案）。</summary>
    [Fact]
    public async Task GetRecords_WhenEmpty_ReturnsEmptyPage()
    {
        await fixture.ResetAsync();
        var token = await fixture.RegisterAsync();

        var response = await fixture.SendAsync(HttpMethod.Get, "/api/v1/records", token);

        var raw = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(raw);
        document.RootElement.GetProperty("code").GetInt32().ShouldBeSuccess();
        document.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().Should().Be(0);
        document.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    /// <summary>规范 6.2：pageSize 超上限被压到 100，pageIndex 非法值回落 1（不回 400）。</summary>
    [Fact]
    public async Task GetRecords_WithIllegalPaging_ClampsInsteadOfRejecting()
    {
        await fixture.ResetAsync();
        var token = await fixture.RegisterAsync();

        var response = await fixture.SendAsync(HttpMethod.Get, "/api/v1/records?pageIndex=0&pageSize=1000", token);

        var raw = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(raw);
        var data = document.RootElement.GetProperty("data");
        data.GetProperty("pageIndex").GetInt32().Should().Be(1);
        data.GetProperty("pageSize").GetInt32().Should().Be(PageQuery.MaxPageSize);
    }

    /// <summary>抽奖审计必须落库（FR-05-R8 / D-08：不留无审计的操作）。</summary>
    [Fact]
    public async Task Draw_WritesAuditLog()
    {
        await fixture.ResetAsync();
        var token = await fixture.RegisterAsync();

        await fixture.DrawAsync(token, Guid.NewGuid().ToString());

        var auditCount = await fixture.QueryAsync(db => db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS `Value` FROM `AuditLog` WHERE `Module` = 'draw' AND `OperationType` = 'Draw'")
            .SingleAsync());
        auditCount.Should().Be(1);

        // 审计内容不得包含密码 / token 痕迹
        var auditJson = await fixture.QueryAsync(db => db.Database
            .SqlQueryRaw<string>("SELECT `AfterJson` AS `Value` FROM `AuditLog` WHERE `Module` = 'draw' LIMIT 1")
            .SingleAsync());
        auditJson.ToLowerInvariant().Should().NotContain("password");
        auditJson.ToLowerInvariant().Should().NotContain("token");
    }

    /// <summary>
    /// REV-04 回归：非法 `Idempotency-Key`（非 UUID）→ HTTP 200 + 1002，不得落 500；
    /// 且不产生任何业务副作用（不扣次、不写流水）。
    /// </summary>
    [Fact]
    public async Task Draw_WithMalformedIdempotencyKey_Returns1002WithoutSideEffects()
    {
        await fixture.ResetAsync();
        var token = await fixture.RegisterAsync();

        var response = await fixture.DrawAsync(token, "not-a-uuid");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await IntegrationFixture.ReadDrawAsync(response);
        envelope.Code.Should().Be(ErrorCodes.ValidationFailed);
        envelope.Message.Should().Be("Idempotency-Key 格式非法（应为 UUID）");

        var used = await fixture.QueryAsync(db => db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS `Value` FROM `DrawRequest`")
            .SingleAsync());
        used.Should().Be(0);
    }

    /// <summary>
    /// REV-04 回归：超长 `Idempotency-Key`（&gt;64 字符，varchar(64) 溢出）→ HTTP 200 + 1002，
    /// 不得由 MySQL「Data too long」(1406) 兜底成 500。
    /// </summary>
    [Fact]
    public async Task Draw_WithOverlongIdempotencyKey_Returns1002InsteadOfServerError()
    {
        await fixture.ResetAsync();
        var token = await fixture.RegisterAsync();

        var response = await fixture.DrawAsync(token, new string('a', 200));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await IntegrationFixture.ReadDrawAsync(response);
        envelope.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    /// <summary>REV-04 对照：合法 UUID 键照常成功（校验不误伤正常路径）。</summary>
    [Fact]
    public async Task Draw_WithValidUuidIdempotencyKey_Succeeds()
    {
        await fixture.ResetAsync();
        var token = await fixture.RegisterAsync();

        var envelope = await IntegrationFixture.ReadDrawAsync(
            await fixture.DrawAsync(token, Guid.NewGuid().ToString()));

        envelope.Code.ShouldBeSuccess();
    }
}
