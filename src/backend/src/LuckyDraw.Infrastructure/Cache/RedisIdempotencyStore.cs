using System.Text.Json;
using LuckyDraw.Application.Dtos;
using LuckyDraw.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LuckyDraw.Infrastructure.Cache;

/// <summary>
/// 幂等结果缓存（MOD-05 加速层）：key <c>draw:idempotency:{userId}:{key}</c>，TTL 24h。
/// 仅缓存成功结果；Redis 不可用时退化为数据库唯一索引路径（正确性不受影响）。
/// </summary>
public class RedisIdempotencyStore : IIdempotencyStore
{
    /// <summary>缓存有效期（24 小时）。</summary>
    public static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    /// <summary>key 前缀（A3：统一前缀 draw）。</summary>
    private const string KeyPrefix = "draw:idempotency:";

    private readonly RedisConnectionProvider _connectionProvider;
    private readonly ILogger<RedisIdempotencyStore> _logger;

    /// <summary>构造函数注入。</summary>
    public RedisIdempotencyStore(RedisConnectionProvider connectionProvider, ILogger<RedisIdempotencyStore> logger)
    {
        _connectionProvider = connectionProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DrawResponseDto?> TryGetAsync(int userId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var database = TryGetDatabase();
        if (database is null)
        {
            return null;
        }

        try
        {
            var value = await database.StringGetAsync(BuildKey(userId, idempotencyKey));
            return value.IsNullOrEmpty
                ? null
                : JsonSerializer.Deserialize<DrawResponseDto>(value.ToString());
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "读取幂等结果缓存失败，退化为数据库幂等路径");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync(int userId, string idempotencyKey, DrawResponseDto result, CancellationToken cancellationToken)
    {
        var database = TryGetDatabase();
        if (database is null)
        {
            return;
        }

        try
        {
            await database.StringSetAsync(
                BuildKey(userId, idempotencyKey),
                JsonSerializer.Serialize(result),
                CacheTtl);
        }
        catch (Exception exception)
        {
            // best effort：缓存写失败仅告警，不回滚已提交的业务事务（D-03）
            _logger.LogWarning(exception, "写入幂等结果缓存失败（不影响业务结果）");
        }
    }

    private static string BuildKey(int userId, string idempotencyKey) => $"{KeyPrefix}{userId}:{idempotencyKey}";

    private IDatabase? TryGetDatabase() => _connectionProvider.TryGetConnection()?.GetDatabase();
}
