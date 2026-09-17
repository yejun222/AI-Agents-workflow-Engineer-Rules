using System.Text.Json;
using LuckyDraw.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LuckyDraw.Infrastructure.Cache;

/// <summary>
/// 认证幂等缓存（D-15）：key <c>draw:idempotency:auth:{operation}:{key}</c>，TTL 10 分钟。
/// 只存 `{ userId, requestHash, completed }`，**不含任何凭证**；
/// Redis 不可用时全部按「无幂等」处理（`TryAcquireAsync` 返回 true = 本请求直接执行），不阻断认证链路。
/// </summary>
public class RedisAuthIdempotencyStore : IAuthIdempotencyStore
{
    /// <summary>缓存有效期（10 分钟，D-15-2：覆盖双击 / 网络重试窗口）。</summary>
    public static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    /// <summary>key 前缀（A3 统一前缀 `draw:idempotency:` + 认证命名空间）。</summary>
    private const string KeyPrefix = "draw:idempotency:auth:";

    private readonly RedisConnectionProvider _connectionProvider;
    private readonly ILogger<RedisAuthIdempotencyStore> _logger;

    /// <summary>构造函数注入。</summary>
    public RedisAuthIdempotencyStore(RedisConnectionProvider connectionProvider, ILogger<RedisAuthIdempotencyStore> logger)
    {
        _connectionProvider = connectionProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> TryAcquireAsync(string operation, string idempotencyKey, string requestHash, CancellationToken cancellationToken)
    {
        var database = TryGetDatabase();
        if (database is null)
        {
            // Redis 不可用 = 无幂等：本请求按执行者继续（禁止因缓存不可用阻断登录 / 注册）
            return true;
        }

        try
        {
            var placeholder = new AuthIdempotencyEntry(0, requestHash, Completed: false);
            return await database.StringSetAsync(
                BuildKey(operation, idempotencyKey),
                JsonSerializer.Serialize(placeholder),
                CacheTtl,
                When.NotExists);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "认证幂等占位失败，降级为无幂等执行");
            return true;
        }
    }

    /// <inheritdoc />
    public async Task<AuthIdempotencyEntry?> TryGetAsync(string operation, string idempotencyKey, CancellationToken cancellationToken)
    {
        var database = TryGetDatabase();
        if (database is null)
        {
            return null;
        }

        try
        {
            var value = await database.StringGetAsync(BuildKey(operation, idempotencyKey));
            return value.IsNullOrEmpty
                ? null
                : JsonSerializer.Deserialize<AuthIdempotencyEntry>(value.ToString());
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "读取认证幂等缓存失败，降级为无幂等执行");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task CompleteAsync(string operation, string idempotencyKey, AuthIdempotencyEntry entry, CancellationToken cancellationToken)
    {
        var database = TryGetDatabase();
        if (database is null)
        {
            return;
        }

        try
        {
            await database.StringSetAsync(BuildKey(operation, idempotencyKey), JsonSerializer.Serialize(entry), CacheTtl);
        }
        catch (Exception exception)
        {
            // best effort：写入失败只少一次重放机会，业务结果已经产生，不回滚
            _logger.LogWarning(exception, "写入认证幂等结果失败（不影响本次业务结果）");
        }
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(string operation, string idempotencyKey, CancellationToken cancellationToken)
    {
        var database = TryGetDatabase();
        if (database is null)
        {
            return;
        }

        try
        {
            await database.KeyDeleteAsync(BuildKey(operation, idempotencyKey));
        }
        catch (Exception exception)
        {
            // 释放失败 = 占位保留至 TTL 到期：后续同键请求会拿到 409（更保守方向，可接受）
            _logger.LogWarning(exception, "释放认证幂等占位失败（占位将在 TTL 到期后失效）");
        }
    }

    private static string BuildKey(string operation, string idempotencyKey) =>
        $"{KeyPrefix}{operation}:{idempotencyKey}";

    private IDatabase? TryGetDatabase() => _connectionProvider.TryGetConnection()?.GetDatabase();
}
