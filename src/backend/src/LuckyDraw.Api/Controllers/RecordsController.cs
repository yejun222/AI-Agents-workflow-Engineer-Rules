using Asp.Versioning;
using LuckyDraw.Api.Common;
using LuckyDraw.Application.Common;
using LuckyDraw.Application.Dtos;
using LuckyDraw.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LuckyDraw.Api.Controllers;

/// <summary>我的中奖记录接口（API-08 / MOD-04）：恒定当前登录用户，不接受 userId 参数。</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/records")]
[Authorize]
public class RecordsController : ControllerBase
{
    private readonly IWinningRecordService _winningRecordService;

    /// <summary>构造函数注入。</summary>
    public RecordsController(IWinningRecordService winningRecordService)
    {
        _winningRecordService = winningRecordService;
    }

    /// <summary>分页查询本人中奖记录（按中奖时间倒序，每页默认 10 条）。</summary>
    [HttpGet]
    public async Task<PageResult<WinningRecordDto>> GetMyRecordsAsync(
        [FromQuery] PageQuery query,
        CancellationToken cancellationToken)
    {
        return await _winningRecordService.GetMyRecordsAsync(User.GetCurrentUserId(), query, cancellationToken);
    }
}
