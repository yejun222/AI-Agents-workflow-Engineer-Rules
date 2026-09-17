using System.Text.Json;
using LuckyDraw.Application.Common;
using LuckyDraw.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LuckyDraw.Infrastructure.Audit;

/// <summary>
/// 实体级审计拦截器（MOD-06）：
/// ① 统一为 <see cref="AuditableEntity"/> 赋 CreateTime / UpdateTime（架构 4.7）；
/// ② 对 User / PrizeItem 的增删改自动落审计（BeforeJson / AfterJson 走**字段白名单**，
///    PasswordHash 等敏感列一律排除，规范第十章 + 8.4）。
/// 注意：<c>ExecuteUpdateAsync</c> 绕过本拦截器，抽奖的审计由 <see cref="AuditService"/> 显式承担（D-08）。
/// </summary>
public class AuditInterceptor : SaveChangesInterceptor
{
    /// <summary>参与自动审计的实体类型。</summary>
    private static readonly HashSet<Type> AuditedEntityTypes = new() { typeof(User), typeof(PrizeItem) };

    private readonly TimeProvider _timeProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>构造函数注入。</summary>
    public AuditInterceptor(TimeProvider timeProvider, IHttpContextAccessor httpContextAccessor)
    {
        _timeProvider = timeProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var operatorId = ResolveOperatorId();
        var auditLogs = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditableEntity auditable)
            {
                ApplyTimestamps(entry, auditable, now);
            }

            if (!AuditedEntityTypes.Contains(entry.Entity.GetType()))
            {
                continue;
            }

            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            auditLogs.Add(new AuditLog
            {
                OperatorId = operatorId,
                OperateTime = now,
                Module = "data",
                OperationType = entry.State.ToString(),
                TargetObject = BuildTargetObject(entry.Entity),
                BeforeJson = entry.State == EntityState.Added ? null : BuildSnapshot(entry.Entity),
                AfterJson = entry.State == EntityState.Deleted ? null : BuildSnapshot(entry.Entity)
            });
        }

        if (auditLogs.Count > 0)
        {
            context.Set<AuditLog>().AddRange(auditLogs);
        }
    }

    private static void ApplyTimestamps(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, AuditableEntity auditable, DateTime now)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                auditable.CreateTime = now;
                auditable.UpdateTime = now;
                break;
            case EntityState.Modified:
                auditable.UpdateTime = now;
                entry.Property(nameof(AuditableEntity.CreateTime)).IsModified = false;
                break;
        }
    }

    /// <summary>字段白名单快照：只输出可审计的非敏感字段。</summary>
    private static string BuildSnapshot(object entity) => entity switch
    {
        User user => JsonSerializer.Serialize(new { user.Id, user.UserName, user.IsDeleted }),
        PrizeItem prize => JsonSerializer.Serialize(new
        {
            prize.Id,
            prize.Code,
            prize.Name,
            prize.Weight,
            prize.Stock,
            prize.DisplayOrder,
            prize.IsEnabled,
            prize.IsDeleted
        }),
        _ => JsonSerializer.Serialize(new { id = BuildTargetObject(entity) })
    };

    private static string? BuildTargetObject(object entity) => entity switch
    {
        AuditableEntity auditable => auditable.Id.ToString(),
        _ => null
    };

    private int? ResolveOperatorId()
    {
        var claimValue = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimNames.UserId)?.Value;
        return int.TryParse(claimValue, out var userId) ? userId : null;
    }
}
