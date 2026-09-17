using LuckyDraw.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LuckyDraw.Infrastructure.Cache;

/// <summary>
/// 登录失败计数（规范 4.3 / D-11）：key <c>draw:auth:loginfail:{userNameLower}</c>，TTL 15 分钟。
/// 按提交的用户名计数（对不存在的用户名同样计数）→ 防账号枚举；
/// Redis 不可用时 **fail-open**（锁定降级，登录本身不阻断）。
/// </summary>
public class RedisLoginAttemptStore : ILoginAttemptStore
{
    /// <summary>key 前缀（A3）。</summary>
    private const string KeyPrefix = "draw:auth:loginfail:";

    private readonly RedisConnectionProvider _connectionProvider;
    private readonly ILogger<RedisLoginAttemptStore> _logger;

    /// <summary>构造函数注入。</summary>
    public RedisLoginAttemptStore(RedisConnectionProvider connectionProvider, ILogger<RedisLoginAttemptStore> logger)
    {
        _connectionProvider = connectionProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> IncrementAsync(string userNameLower, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var database = TryGetDatabase();
        if (database is null)
        {
            return 0;
        }

        try
        {
            var key = BuildKey(userNameLower);
            var count = await database.StringIncrementAsync(key);
            if (count == 1)
            {
                await database.KeyExpireAsync(key, ttl);
            }

            return (int)count;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "登录失败计数不可用，锁定能力降级（fail-open）");
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetAsync(string userNameLower, CancellationToken cancellationToken)
    {
        var database = TryGetDatabase();
        if (database is null)
        {
            return 0;
        }

        try
        {
            var value = await database.StringGetAsync(BuildKey(userNameLower));
            return value.IsNullOrEmpty ? 0 : (int)value;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "读取登录失败计数失败，按未锁定处理");
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task ResetAsync(string userNameLower, CancellationToken cancellationToken)
    {
        var database = TryGetDatabase();
        if (database is null)
        {
            return;
        }

        try
        {
            await database.KeyDeleteAsync(BuildKey(userNameLower));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "清除登录失败计数失败");
        }
    }

    private static string BuildKey(string userNameLower) => $"{KeyPrefix}{userNameLower}";

    private IDatabase? TryGetDatabase() => _connectionProvider.TryGetConnection()?.GetDatabase();
}
