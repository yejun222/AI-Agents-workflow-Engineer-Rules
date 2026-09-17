using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace LuckyDraw.Infrastructure.Cache;

/// <summary>Redis 配置节。</summary>
public class RedisOptions
{
    /// <summary>配置节名。</summary>
    public const string SectionName = "Redis";

    /// <summary>StackExchange.Redis 连接串（环境变量 / user-secrets 注入，禁止入库入仓）。</summary>
    public string Configuration { get; set; } = string.Empty;
}

/// <summary>
/// Redis 连接提供者：连接失败不抛异常、返回 null，由各存储降级（架构 6.1 降级矩阵）。
/// </summary>
public class RedisConnectionProvider
{
    private readonly Lazy<IConnectionMultiplexer?> _connection;
    private readonly ILogger<RedisConnectionProvider> _logger;

    /// <summary>构造函数注入。</summary>
    public RedisConnectionProvider(IOptions<RedisOptions> options, ILogger<RedisConnectionProvider> logger)
    {
        _logger = logger;
        _connection = new Lazy<IConnectionMultiplexer?>(() => Connect(options.Value.Configuration), isThreadSafe: true);
    }

    /// <summary>获取连接；不可用时返回 null（调用方须走降级路径）。</summary>
    public IConnectionMultiplexer? TryGetConnection()
    {
        try
        {
            return _connection.Value;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Redis 连接不可用，相关能力降级（幂等走数据库兜底 / 登录锁定 fail-open）");
            return null;
        }
    }

    private IConnectionMultiplexer? Connect(string configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration))
        {
            _logger.LogWarning("未配置 Redis__Configuration，Redis 相关能力全部降级");
            return null;
        }

        var options = ConfigurationOptions.Parse(configuration);
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 2000;

        try
        {
            return ConnectionMultiplexer.Connect(options);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Redis 连接失败，相关能力降级");
            return null;
        }
    }
}
