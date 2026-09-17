using LuckyDraw.Application.Common;
using LuckyDraw.Application.Dtos;
using LuckyDraw.Application.Interfaces;
using LuckyDraw.Domain.Entities;
using LuckyDraw.Domain.Exceptions;
using LuckyDraw.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LuckyDraw.Infrastructure.Repositories;

/// <summary>中奖记录仓储实现（MOD-04）：只增（无更新 / 删除路径）。</summary>
public class WinningRecordRepository : IWinningRecordRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>构造函数注入。</summary>
    public WinningRecordRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AddAsync(WinningRecord record, CancellationToken cancellationToken)
    {
        _dbContext.WinningRecords.Add(record);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (MySqlErrors.IsTransient(exception))
        {
            // 死锁（1213）/ 锁等待超时（1205）：InnoDB 已回滚整个牺牲事务（1205 已回滚当前语句），
            // 交由业务层重试整个事务（D-08）。脱离跟踪是必须的：重试会新建实体，
            // 残留的 Added 实体会被下一次 SaveChanges 一并插入，产生重复中奖记录。
            _dbContext.Entry(record).State = EntityState.Detached;
            throw new TransientDataException("中奖记录写入遇数据库瞬时错误", exception);
        }
    }

    /// <inheritdoc />
    public async Task<PageResult<WinningRecordDto>> QueryAsync(int userId, PageQuery query, CancellationToken cancellationToken)
    {
        var pageIndex = query.NormalizedPageIndex;
        var pageSize = query.NormalizedPageSize;

        // 排序固定为 CreateTime 倒序（开放排序字段会让 (UserId, CreateTime) 索引失效，API-08）
        var baseQuery = _dbContext.WinningRecords
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .OrderByDescending(x => x.CreateTime)
            .ThenByDescending(x => x.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new WinningRecordDto
            {
                Id = x.Id,
                PrizeName = x.PrizeName,
                // 库内时间即 UTC，但 MySQL DATETIME 读回为 Kind=Unspecified，须显式标注 UTC，
                // 否则序列化会丢掉时区标识（BUG-02）。仅作用于本 DTO 的 winTime，不触碰全局时间映射。
                WinTime = new DateTimeOffset(DateTime.SpecifyKind(x.CreateTime, DateTimeKind.Utc))
            })
            .ToListAsync(cancellationToken);

        return new PageResult<WinningRecordDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }
}
