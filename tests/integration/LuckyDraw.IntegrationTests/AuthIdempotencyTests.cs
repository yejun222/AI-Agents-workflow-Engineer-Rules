using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LuckyDraw.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace LuckyDraw.IntegrationTests;

/// <summary>
/// API-01 / API-02 的 `Idempotency-Key` 语义（D-15 / REV-06）：
/// 同键重放不产生第二次业务副作用、请求体不一致 → 409、格式非法 → 1002、并发同键不重复建用户。
/// </summary>
[Collection(IntegrationCollection.Name)]
public class AuthIdempotencyTests(IntegrationFixture fixture)
{
    /// <summary>D-15-4：同键重复注册 —— 只建一个用户、只写一条 Register 审计，两次都拿到可用凭证（重放重新签发）。</summary>
    [Fact]
    public async Task Register_WithSameKey_ReplaysWithoutSecondSideEffect()
    {
        await fixture.ResetAsync();
        var userName = IntegrationFixture.GenerateUserName();
        var key = Guid.NewGuid().ToString();
        var body = new
        {
            userName,
            password = IntegrationFixture.ValidPassword,
            confirmPassword = IntegrationFixture.ValidPassword
        };

        var first = await PostAsync("/api/v1/auth/register", body, key);
        var second = await PostAsync("/api/v1/auth/register", body, key);

        (await first.DescribeAsync()).Should().Contain("\"code\":0");
        (await second.DescribeAsync()).Should().Contain("\"code\":0");

        var userCount = await fixture.QueryAsync(db => db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS `Value` FROM `User` WHERE `UserName` = {0}", userName)
            .SingleAsync());
        userCount.Should().Be(1);

        var registerAuditCount = await fixture.QueryAsync(db => db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS `Value` FROM `AuditLog` WHERE `Module` = 'auth' AND `OperationType` = 'Register'")
            .SingleAsync());
        registerAuditCount.Should().Be(1, "重放路径禁止重复审计（D-15-4）");

        // 重放必须重新签发凭证（不是复用首次的 token 字符串）
        var firstToken = await ReadAccessTokenAsync(first);
        var secondToken = await ReadAccessTokenAsync(second);
        secondToken.Should().NotBeNullOrWhiteSpace();
        firstToken.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// D-15-1 / BUG-01（TC-08 步骤 3）：同键但请求体不一致 → **HTTP 409** + body `code=409`「请勿重复提交」。
    /// 依据 §5.1 错误码分工，`409` 幂等冲突是与「业务异常一律 HTTP 200」并列分立的 HTTP 状态码。
    /// </summary>
    [Fact]
    public async Task Register_WithSameKeyDifferentBody_ReportsConflict()
    {
        await fixture.ResetAsync();
        var key = Guid.NewGuid().ToString();

        await PostAsync(
            "/api/v1/auth/register",
            new
            {
                userName = IntegrationFixture.GenerateUserName(),
                password = IntegrationFixture.ValidPassword,
                confirmPassword = IntegrationFixture.ValidPassword
            },
            key);

        var response = await PostAsync(
            "/api/v1/auth/register",
            new
            {
                userName = IntegrationFixture.GenerateUserName(),
                password = IntegrationFixture.ValidPassword,
                confirmPassword = IntegrationFixture.ValidPassword
            },
            key);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, "幂等冲突的 HTTP 状态码须为 409（§5.1），而非 200（BUG-01）");
        var envelope = await response.Content.ReadFromJsonAsync<IntegrationFixture.Envelope<IntegrationFixture.AuthData>>(IntegrationFixture.JsonOptions);
        envelope!.Code.Should().Be(ErrorCodes.IdempotencyConflict);
        envelope.Message.Should().Be("请勿重复提交");
        envelope.Data.Should().BeNull("冲突响应不得携带凭证");
    }

    /// <summary>D-15-4 登录侧：同键重复登录只产生一次 Login 审计，两次都返回凭证。</summary>
    [Fact]
    public async Task Login_WithSameKey_AuditsOnce()
    {
        await fixture.ResetAsync();
        var userName = IntegrationFixture.GenerateUserName();
        await fixture.RegisterAsync(userName);
        var key = Guid.NewGuid().ToString();
        var body = new { userName, password = IntegrationFixture.ValidPassword };

        await PostAsync("/api/v1/auth/login", body, key);
        var second = await PostAsync("/api/v1/auth/login", body, key);

        (await second.DescribeAsync()).Should().Contain("\"code\":0");

        var loginAuditCount = await fixture.QueryAsync(db => db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS `Value` FROM `AuditLog` WHERE `Module` = 'auth' AND `OperationType` = 'Login'")
            .SingleAsync());
        loginAuditCount.Should().Be(1, "重放路径禁止重复审计（D-15-4）");
    }

    /// <summary>
    /// D-15-1 / BUG-01（TC-17 步骤 3）：登录同键换请求体 → **HTTP 409** + body `code=409`，且不签发凭证。
    /// 与注册侧同构；本用例覆盖认证侧登录链路的 HTTP 状态码（此前仅断言 body，未断言状态码而漏判）。
    /// </summary>
    [Fact]
    public async Task Login_WithSameKeyDifferentBody_Returns409()
    {
        await fixture.ResetAsync();
        var userName = IntegrationFixture.GenerateUserName();
        await fixture.RegisterAsync(userName);
        var key = Guid.NewGuid().ToString();

        await PostAsync("/api/v1/auth/login", new { userName, password = IntegrationFixture.ValidPassword }, key);

        var response = await PostAsync("/api/v1/auth/login", new { userName, password = "Passw0rdz" }, key);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, "幂等冲突的 HTTP 状态码须为 409（§5.1），而非 200（BUG-01）");
        var envelope = await response.Content.ReadFromJsonAsync<IntegrationFixture.Envelope<IntegrationFixture.AuthData>>(IntegrationFixture.JsonOptions);
        envelope!.Code.Should().Be(ErrorCodes.IdempotencyConflict);
        envelope.Message.Should().Be("请勿重复提交");
        envelope.Data.Should().BeNull("冲突响应不得签发凭证");
    }

    /// <summary>
    /// 并发同键注册（双击 / 重试并发）：只建一个用户，无 500；
    /// 非执行者按 D-15-3 得到重放结果或 409，两种结果都不产生第二个用户。
    /// </summary>
    [Fact]
    public async Task Register_WithConcurrentSameKey_CreatesSingleUser()
    {
        await fixture.ResetAsync();
        var userName = IntegrationFixture.GenerateUserName();
        var key = Guid.NewGuid().ToString();
        var body = new
        {
            userName,
            password = IntegrationFixture.ValidPassword,
            confirmPassword = IntegrationFixture.ValidPassword
        };

        var responses = await Task.WhenAll(
            PostAsync("/api/v1/auth/register", body, key),
            PostAsync("/api/v1/auth/register", body, key));

        var userCount = await fixture.QueryAsync(db => db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS `Value` FROM `User` WHERE `UserName` = {0}", userName)
            .SingleAsync());
        userCount.Should().Be(1);

        foreach (var response in responses)
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var code = document.RootElement.GetProperty("code").GetInt32();
            // 执行者 / 重放者 0、或抢占失败者 409（超时未落定）；任何其它码（尤其 500）都不允许
            new[] { 0, ErrorCodes.IdempotencyConflict }.Should().Contain(code);
            // HTTP 状态码必须与业务码配对：0 ↔ 200、409 ↔ 409（§5.1）；BUG-01 修复后 409 不再以 200 返回
            response.StatusCode.Should().Be(
                code == 0 ? HttpStatusCode.OK : HttpStatusCode.Conflict,
                "HTTP 状态码须与业务码配对（§5.1）");
        }
    }

    /// <summary>REV-04 同口径：`Idempotency-Key` 非 UUID → 1002（认证接口与抽奖接口一致）。</summary>
    [Fact]
    public async Task Login_WithMalformedIdempotencyKey_Reports1002()
    {
        await fixture.ResetAsync();
        var userName = IntegrationFixture.GenerateUserName();
        await fixture.RegisterAsync(userName);

        var response = await PostAsync(
            "/api/v1/auth/login",
            new { userName, password = IntegrationFixture.ValidPassword },
            "not-a-uuid");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<IntegrationFixture.Envelope<IntegrationFixture.AuthData>>(IntegrationFixture.JsonOptions);
        envelope!.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    /// <summary>键缺失 = 无幂等、正常处理（D-15-1：不得因缺少请求头报错）。</summary>
    [Fact]
    public async Task Login_WithoutIdempotencyKey_StillSucceeds()
    {
        await fixture.ResetAsync();
        var userName = IntegrationFixture.GenerateUserName();
        await fixture.RegisterAsync(userName);

        var response = await PostAsync("/api/v1/auth/login", new { userName, password = IntegrationFixture.ValidPassword }, key: null);

        (await response.DescribeAsync()).Should().Contain("\"code\":0");
    }

    /// <summary>带可选 `Idempotency-Key` 提交 JSON（认证接口无需 Bearer 头）。</summary>
    private async Task<HttpResponseMessage> PostAsync(string path, object body, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return await fixture.Client.SendAsync(request);
    }

    private static async Task<string?> ReadAccessTokenAsync(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<IntegrationFixture.Envelope<IntegrationFixture.AuthData>>(IntegrationFixture.JsonOptions);
        return envelope?.Data?.AccessToken;
    }
}
