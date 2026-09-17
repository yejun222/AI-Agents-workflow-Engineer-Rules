using Asp.Versioning;
using LuckyDraw.Api.Common;
using LuckyDraw.Application.Common;
using LuckyDraw.Application.Dtos;
using LuckyDraw.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LuckyDraw.Api.Controllers;

/// <summary>抽奖接口（API-06 / API-07 / MOD-03）：结果 100% 由后端判定，请求体为空对象。</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/draw")]
[Authorize]
public class DrawController : ControllerBase
{
    private readonly IDrawService _drawService;

    /// <summary>构造函数注入。</summary>
    public DrawController(IDrawService drawService)
    {
        _drawService = drawService;
    }

    /// <summary>查询今日剩余次数与重置时刻。</summary>
    [HttpGet("quota")]
    public async Task<DrawQuotaDto> GetQuotaAsync(CancellationToken cancellationToken)
    {
        return await _drawService.GetQuotaAsync(User.GetCurrentUserId(), cancellationToken);
    }

    /// <summary>执行一次抽奖（携带幂等键；缺失即视为普通新请求，格式非法 → 1002）。</summary>
    /// <param name="idempotencyKey">幂等键（UUID，规范 6.5）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Draw)]
    public async Task<DrawResponseDto> DrawAsync(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        // REV-04：先校验再进业务层——非法键（非 UUID / 超 64 字符）返回 1002，不得落到列长度错误（1406 → 500）
        var validatedKey = IdempotencyKeyValidator.Validate(idempotencyKey);

        return await _drawService.DrawAsync(
            User.GetCurrentUserId(),
            User.GetCurrentUserName(),
            validatedKey,
            cancellationToken);
    }
}
