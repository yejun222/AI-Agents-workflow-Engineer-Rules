using LuckyDraw.Application.Interfaces;
using LuckyDraw.Domain.Entities;
using LuckyDraw.Domain.Exceptions;
using LuckyDraw.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LuckyDraw.Infrastructure.Audit;

/// <summary>
/// 显式审计实现（MOD-06）：记录注册 / 登录 / 登录失败 / 登出 / 复用检测 / 每次抽奖。
/// 在抽奖链路中随业务事务提交：审计写失败 = 抽奖失败（D-08 / A2，宁可拒绝也不留无痕操作）。
/// </summary>
public class AuditService : IAuditService
{
    /// <summary>UserAgent 截断长度（列宽 200）。</summary>
    private const int UserAgentMaxLength = 200;

    private readonly AppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeProvider _timeProvider;

    /// <summary>构造函数注入。</summary>
    public AuditService(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task LogAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var httpContext = _httpContextAccessor.HttpContext;

        var auditLog = new AuditLog
        {
            OperatorId = entry.OperatorId,
            OperateTime = _timeProvider.GetUtcNow().UtcDateTime,
            Module = entry.Module,
            OperationType = entry.OperationType,
            TargetObject = entry.TargetObject,
            BeforeJson = entry.BeforeJson,
            AfterJson = entry.AfterJson,
            IpAddress = ResolveIpAddress(httpContext),
            UserAgent = Truncate(httpContext?.Request.Headers.UserAgent.ToString())
        };

        _dbContext.AuditLogs.Add(auditLog);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (MySqlErrors.IsTransient(exception))
        {
            // 死锁（1213）/ 锁等待超时（1205）：脱离跟踪后交由业务层重试整个事务（D-08）。
            // 抽奖链路上本写入与扣减同生共死（A2），瞬时错误必须可分类，否则退化为未分类的 500。
            _dbContext.Entry(auditLog).State = EntityState.Detached;
            throw new TransientDataException("审计写入遇数据库瞬时错误", exception);
        }
    }

    private static string? ResolveIpAddress(HttpContext? httpContext)
    {
        var remoteIp = httpContext?.Connection.RemoteIpAddress;
        return remoteIp?.ToString();
    }

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return value.Length <= UserAgentMaxLength ? value : value[..UserAgentMaxLength];
    }
}
