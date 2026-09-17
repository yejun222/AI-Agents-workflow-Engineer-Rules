using Asp.Versioning;
using LuckyDraw.Application.Dtos;
using LuckyDraw.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LuckyDraw.Api.Controllers;

/// <summary>奖池查询接口（API-05 / MOD-02）：响应结构上不含权重 / 库存（D-07）。</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/prizes")]
[Authorize]
public class PrizesController : ControllerBase
{
    private readonly IPrizePoolService _prizePoolService;

    /// <summary>构造函数注入。</summary>
    public PrizesController(IPrizePoolService prizePoolService)
    {
        _prizePoolService = prizePoolService;
    }

    /// <summary>查询启用中的奖池条目（按 displayOrder 升序）。</summary>
    [HttpGet]
    public async Task<PrizePoolResponse> GetPoolAsync(CancellationToken cancellationToken)
    {
        return await _prizePoolService.GetPoolAsync(cancellationToken);
    }
}
