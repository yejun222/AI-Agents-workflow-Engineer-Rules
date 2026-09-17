using Asp.Versioning;
using LuckyDraw.Api.Common;
using LuckyDraw.Application.Common;
using LuckyDraw.Application.Dtos;
using LuckyDraw.Application.Interfaces;
using LuckyDraw.Application.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace LuckyDraw.Api.Controllers;

/// <summary>认证与会话接口（API-01…API-04 / MOD-01）。</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController : ControllerBase
{
    /// <summary>refresh token 的 Cookie 名。</summary>
    private const string RefreshTokenCookieName = "refresh_token";

    /// <summary>refresh Cookie 的作用路径（仅刷新与登出接口携带）。</summary>
    private const string RefreshTokenCookiePath = "/api/v1/auth";

    private readonly IAuthService _authService;
    private readonly IOptions<JwtOptions> _jwtOptions;
    private readonly IHostEnvironment _environment;

    /// <summary>构造函数注入。</summary>
    public AuthController(IAuthService authService, IOptions<JwtOptions> jwtOptions, IHostEnvironment environment)
    {
        _authService = authService;
        _jwtOptions = jwtOptions;
        _environment = environment;
    }

    /// <summary>注册（注册成功即登录；`Idempotency-Key` 语义见 D-15）。</summary>
    /// <param name="request">注册请求。</param>
    /// <param name="idempotencyKey">幂等键（UUID；缺失 = 无幂等，格式非法 → 1002）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Register)]
    public async Task<AuthResponse> RegisterAsync(
        [FromBody] RegisterRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var response = await _authService.RegisterAsync(request, IdempotencyKeyValidator.Validate(idempotencyKey), cancellationToken);
        AppendRefreshTokenCookie(response.RefreshToken);
        return response;
    }

    /// <summary>登录（`Idempotency-Key` 语义见 D-15）。</summary>
    /// <param name="request">登录请求。</param>
    /// <param name="idempotencyKey">幂等键（UUID；缺失 = 无幂等，格式非法 → 1002）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<AuthResponse> LoginAsync(
        [FromBody] LoginRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(request, IdempotencyKeyValidator.Validate(idempotencyKey), cancellationToken);
        AppendRefreshTokenCookie(response.RefreshToken);
        return response;
    }

    /// <summary>刷新（一次性轮换 + 复用检测）。</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<RefreshResponse> RefreshAsync(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new Domain.Exceptions.BusinessException(Application.Common.ErrorCodes.SessionExpired, "登录状态已过期，请重新登录");
        }

        var response = await _authService.RefreshAsync(refreshToken, cancellationToken);
        AppendRefreshTokenCookie(response.RefreshToken);
        return response;
    }

    /// <summary>登出（幂等：凭证无效也返回成功，避免前端卡死）。</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<object?> LogoutAsync(CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(Request.Cookies[RefreshTokenCookieName], cancellationToken);
        DeleteRefreshTokenCookie();
        return null;
    }

    /// <summary>写入 refresh token Cookie（httpOnly + Secure + SameSite=Strict，B5）。</summary>
    private void AppendRefreshTokenCookie(string refreshToken)
    {
        Response.Cookies.Append(RefreshTokenCookieName, refreshToken, BuildCookieOptions(_jwtOptions.Value.RefreshTokenDays));
    }

    /// <summary>下发过期 Cookie 完成登出。</summary>
    private void DeleteRefreshTokenCookie()
    {
        Response.Cookies.Append(RefreshTokenCookieName, string.Empty, BuildCookieOptions(0));
    }

    private CookieOptions BuildCookieOptions(int days)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            // 本地开发与集成测试（HTTP）允许关闭 Secure；生产必须为 true（附录 B B5）
            Secure = !(_environment.IsDevelopment() || _environment.IsEnvironment("Testing")),
            SameSite = SameSiteMode.Strict,
            Path = RefreshTokenCookiePath,
            Expires = days > 0 ? DateTimeOffset.UtcNow.AddDays(days) : DateTimeOffset.UtcNow.AddDays(-1)
        };
    }
}
