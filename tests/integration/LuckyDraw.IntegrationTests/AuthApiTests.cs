using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LuckyDraw.Application.Common;

namespace LuckyDraw.IntegrationTests;

/// <summary>
/// API-01 / API-02 端到端：注册、登录、未登录拦截。
/// 覆盖 AC-01 / AC-02 / AC-03 / AC-04 / AC-05，以及 D-12（登录失败不得用 401）与 CR-01 密码口径。
/// </summary>
[Collection(IntegrationCollection.Name)]
public class AuthApiTests(IntegrationFixture fixture)
{
    /// <summary>AC-01：注册成功即登录，返回 access token 并下发 httpOnly refresh Cookie。</summary>
    [Fact]
    public async Task Register_WithValidInput_ReturnsTokenAndHttpOnlyCookie()
    {
        await fixture.ResetAsync();
        var userName = IntegrationFixture.GenerateUserName();

        var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new
            {
                userName,
                password = IntegrationFixture.ValidPassword,
                confirmPassword = IntegrationFixture.ValidPassword
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<IntegrationFixture.Envelope<IntegrationFixture.AuthData>>(IntegrationFixture.JsonOptions);
        envelope!.Code.ShouldBeSuccess();
        envelope.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();
        envelope.Data.User.UserName.Should().Be(userName);

        // refresh token 只走 Cookie，不进入响应体（D-11）
        (await response.Content.ReadAsStringAsync()).Should().NotContain("refreshToken");

        var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("refresh_token=", StringComparison.Ordinal));
        cookie.Should().Contain("httponly");
        cookie.Should().Contain("path=/api/v1/auth");
        cookie.Should().Contain("samesite=strict");
    }

    /// <summary>FR-01-1 / AC-02：用户名重复（含大小写不同）→ 1101「用户名已被占用」，HTTP 仍为 200。</summary>
    [Fact]
    public async Task Register_WithTakenUserName_Reports1101()
    {
        await fixture.ResetAsync();
        var userName = IntegrationFixture.GenerateUserName();
        await fixture.RegisterAsync(userName);

        var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new
            {
                userName = userName.ToUpperInvariant(),
                password = IntegrationFixture.ValidPassword,
                confirmPassword = IntegrationFixture.ValidPassword
            });

        var body = await response.DescribeAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var envelope = await response.Content.ReadFromJsonAsync<IntegrationFixture.Envelope<IntegrationFixture.AuthData>>(IntegrationFixture.JsonOptions);
        envelope!.Code.Should().Be(ErrorCodes.UserNameTaken, body);
        envelope.Message.Should().Be("用户名已被占用");
    }

    /// <summary>CR-01 / AC-02：缺大写 / 缺小写 / 缺数字三类密码分别被拒（1002 + 冻结文案）。</summary>
    [Theory]
    [InlineData("abcdefg1")]   // 缺大写
    [InlineData("ABCDEFG1")]   // 缺小写
    [InlineData("Abcdefgh")]   // 缺数字
    [InlineData("Abc123")]     // 长度不足
    public async Task Register_WithPasswordViolatingCr01_Reports1002(string password)
    {
        await fixture.ResetAsync();

        var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { userName = IntegrationFixture.GenerateUserName(), password, confirmPassword = password });

        var body = await response.DescribeAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var envelope = await response.Content.ReadFromJsonAsync<IntegrationFixture.Envelope<IntegrationFixture.AuthData>>(IntegrationFixture.JsonOptions);
        envelope!.Code.Should().Be(ErrorCodes.ValidationFailed, body);
        envelope.Message.Should().Be("密码需 8-20 位且同时含大写字母、小写字母与数字");
    }

    /// <summary>AC-02：两次输入不一致 → 1002「两次输入不一致」。</summary>
    [Fact]
    public async Task Register_WithMismatchedConfirmPassword_Reports1002()
    {
        await fixture.ResetAsync();

        var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new
            {
                userName = IntegrationFixture.GenerateUserName(),
                password = IntegrationFixture.ValidPassword,
                confirmPassword = "Passw0rdX"
            });

        var body = await response.DescribeAsync();
        var envelope = await response.Content.ReadFromJsonAsync<IntegrationFixture.Envelope<IntegrationFixture.AuthData>>(IntegrationFixture.JsonOptions);
        envelope!.Code.Should().Be(ErrorCodes.ValidationFailed, body);
        envelope.Message.Should().Be("两次输入不一致");
    }

    /// <summary>AC-03：登录成功返回新 access token。</summary>
    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsToken()
    {
        await fixture.ResetAsync();
        var userName = IntegrationFixture.GenerateUserName();
        await fixture.RegisterAsync(userName);

        var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { userName, password = IntegrationFixture.ValidPassword });

        var body = await response.DescribeAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var envelope = await response.Content.ReadFromJsonAsync<IntegrationFixture.Envelope<IntegrationFixture.AuthData>>(IntegrationFixture.JsonOptions);
        envelope!.Code.ShouldBeSuccess();
        envelope.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>AC-04 / D-12：登录失败（密码错误、用户不存在）一律 HTTP 200 + 1102 统一文案，防账号枚举。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Login_WithWrongCredentials_Reports1102OnHttp200(bool userExists)
    {
        await fixture.ResetAsync();
        var userName = IntegrationFixture.GenerateUserName();
        if (userExists)
        {
            await fixture.RegisterAsync(userName);
        }

        var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { userName, password = "Wrong0rd" });

        var body = await response.DescribeAsync();
        // 关键：不是 401（否则前端会误触发无感刷新并清空会话）
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var envelope = await response.Content.ReadFromJsonAsync<IntegrationFixture.Envelope<IntegrationFixture.AuthData>>(IntegrationFixture.JsonOptions);
        envelope!.Code.Should().Be(ErrorCodes.InvalidCredentials, body);
        envelope.Message.Should().Be("用户名或密码错误");
    }

    /// <summary>AC-05：未登录访问受保护接口返回 401（且响应体不是 ApiResult，前端据此触发无感刷新）。</summary>
    [Theory]
    [InlineData("/api/v1/prizes")]
    [InlineData("/api/v1/draw/quota")]
    [InlineData("/api/v1/records")]
    public async Task ProtectedEndpoints_WithoutToken_Return401(string path)
    {
        await fixture.ResetAsync();

        var response = await fixture.Client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>伪造 / 篡改的令牌同样返回 401（签名校验必须开启）。</summary>
    [Fact]
    public async Task ProtectedEndpoints_WithForgedToken_Return401()
    {
        await fixture.ResetAsync();

        var response = await fixture.SendAsync(HttpMethod.Get, "/api/v1/draw/quota", "not-a-real-token");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>API-04：登出幂等（无 Cookie 也返回成功）并清空 refresh Cookie。</summary>
    [Fact]
    public async Task Logout_WithoutCookie_IsIdempotentAndClearsCookie()
    {
        await fixture.ResetAsync();

        var response = await fixture.Client.PostAsync("/api/v1/auth/logout", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"code\":0");
        response.Headers.GetValues("Set-Cookie").Should().Contain(value => value.StartsWith("refresh_token=;", StringComparison.Ordinal));
    }

    /// <summary>API-03：未携带 refresh Cookie 时刷新失败并给出 1204（前端据此引导重新登录）。</summary>
    [Fact]
    public async Task Refresh_WithoutCookie_Reports1204()
    {
        await fixture.ResetAsync();

        var response = await fixture.Client.PostAsync("/api/v1/auth/refresh", content: null);

        var body = await response.DescribeAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var envelope = await response.Content.ReadFromJsonAsync<IntegrationFixture.Envelope<IntegrationFixture.AuthData>>(IntegrationFixture.JsonOptions);
        envelope!.Code.Should().Be(ErrorCodes.SessionExpired, body);
    }
}
