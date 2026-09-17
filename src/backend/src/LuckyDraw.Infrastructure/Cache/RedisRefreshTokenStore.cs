using System.Text.Json;
using LuckyDraw.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LuckyDraw.Infrastructure.Cache;

/// <summary>
/// refresh 会话存储（D-11）：key <c>draw:auth:refresh:{userId}</c>，值只存 **令牌哈希**（禁止明文缓存），
/// 单会话族 + 一次性轮换 + 复用检测；Redis 不可用 = 会话缺失 → 引导重新登录（可接受的降级）。
/// </summary>
public class RedisRefreshTokenStore : IRefreshTokenStore
{
    /// <summary>key 前缀（A3）。</summary>
    private const string KeyPrefix = "draw:auth:refresh:";

    private readonly RedisConnectionProvider _connectionProvider;
    private readonly ILogger<RedisRefreshTokenStore> _logger;

    /// <summary>构造函数注入。</summary>
    public RedisRefreshTokenStore(RedisConnectionProvider connectionProvider, ILogger<RedisRefreshTokenStore> logger)
    {
        _connectionProvider = connectionProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SaveAsync(int userId, string jti, string tokenHash, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var database = TryGetDatabase();
        if (database is null)
        {
            return;
        }

        var session = new RefreshSession(jti, tokenHash, DateTimeOffset.UtcNow.Add(ttl));

        try
        {
            await database.StringSetAsync(BuildKey(userId), JsonSerializer.Serialize(session), ttl);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "写入 refresh 会话失败（用户可能需要重新登录）");
        }
    }

    /// <inheritdoc />
    public async Task<RefreshSession?> GetAsync(int userId, CancellationToken cancellationToken)
    {
        var database = TryGetDatabase();
        if (database is null)
        {
            return null;
        }

        try
        {
            var value = await database.StringGetAsync(BuildKey(userId));
            return value.IsNullOrEmpty
                ? null
                : JsonSerializer.Deserialize<RefreshSession>(value.ToString());
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "读取 refresh 会话失败，按会话过期处理");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(int userId, CancellationToken cancellationToken)
    {
        var database = TryGetDatabase();
        if (database is null)
        {
            return;
        }

        try
        {
            await database.KeyDeleteAsync(BuildKey(userId));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "删除 refresh 会话失败");
        }
    }

    private static string BuildKey(int userId) => $"{KeyPrefix}{userId}";

    private IDatabase? TryGetDatabase() => _connectionProvider.TryGetConnection()?.GetDatabase();
}
