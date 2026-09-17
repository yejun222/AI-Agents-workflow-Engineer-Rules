using LuckyDraw.Application.Common;
using LuckyDraw.Application.Dtos;
using LuckyDraw.Application.Interfaces;

namespace LuckyDraw.Application.Services;

/// <summary>中奖记录服务（MOD-04）：恒定当前登录用户，天然隔离越权（FR-09-1）。</summary>
public class WinningRecordService : IWinningRecordService
{
    private readonly IWinningRecordRepository _winningRecordRepository;

    /// <summary>构造函数注入。</summary>
    public WinningRecordService(IWinningRecordRepository winningRecordRepository)
    {
        _winningRecordRepository = winningRecordRepository;
    }

    /// <inheritdoc />
    public Task<PageResult<WinningRecordDto>> GetMyRecordsAsync(int userId, PageQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _winningRecordRepository.QueryAsync(userId, query, cancellationToken);
    }
}
