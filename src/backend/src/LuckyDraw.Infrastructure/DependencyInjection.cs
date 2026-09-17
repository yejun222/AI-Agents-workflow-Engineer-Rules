using LuckyDraw.Application.Interfaces;
using LuckyDraw.Infrastructure.Audit;
using LuckyDraw.Infrastructure.Cache;
using LuckyDraw.Infrastructure.Data;
using LuckyDraw.Infrastructure.Random;
using LuckyDraw.Infrastructure.Repositories;
using LuckyDraw.Infrastructure.Security;
using LuckyDraw.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LuckyDraw.Infrastructure;

/// <summary>Infrastructure 层依赖装配（仓储 / 缓存 / 审计 / 随机源 / 安全与令牌服务）。</summary>
public static class DependencyInjection
{
    /// <summary>
    /// 数据库命令超时（秒）。刻意配置为**大于**服务端 <c>innodb_lock_wait_timeout</c>（MySQL 默认 50s，本项目未改动）：
    /// MySqlConnector 默认 30s 会抢先中断锁等待，使 MySQL 的「锁等待超时 1205」这条已被 <c>MySqlErrors</c>
    /// 分类处理的分支在任何超过 30 秒的锁等待上不可达 → 瞬时错误无法分类、整事务重试失效、用户白等后得到未分类的 500
    /// （BUG-05）。命令超时本身仍保留「客户端主动中断、事务状态未知」的保守语义：不纳入瞬时判定、不重试。
    /// </summary>
    private const int CommandTimeoutSeconds = 60;

    /// <summary>注册基础设施服务。</summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">配置（连接串与 Redis 走配置 / 环境变量，禁止硬编码）。</param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("未配置数据库连接串 ConnectionStrings:Default。");

        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));

        // 时钟：BCL TimeProvider（D-05），测试通过替换该注册实现跨日用例
        services.TryAddTimeProvider();

        services.AddHttpContextAccessor();

        services.AddScoped<AuditInterceptor>();
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            options.UseMySql(
                connectionString,
                ResolveServerVersion(connectionString),
                mySqlOptions => mySqlOptions.CommandTimeout(CommandTimeoutSeconds));
            options.AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>());
        });

        // 仓储（Scoped，随 DbContext 生命周期）
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPrizeRepository, PrizeRepository>();
        services.AddScoped<IUserDrawQuotaRepository, UserDrawQuotaRepository>();
        services.AddScoped<IDrawRequestRepository, DrawRequestRepository>();
        services.AddScoped<IWinningRecordRepository, WinningRecordRepository>();

        // 事务宿主（Scoped：与 DbContext 同生命周期）
        services.AddScoped<ITransactionManager, TransactionManager>();

        // 缓存与外部依赖（Singleton：无状态连接复用）
        services.AddSingleton<RedisConnectionProvider>();
        services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
        services.AddSingleton<IAuthIdempotencyStore, RedisAuthIdempotencyStore>();
        services.AddSingleton<IRefreshTokenStore, RedisRefreshTokenStore>();
        services.AddSingleton<ILoginAttemptStore, RedisLoginAttemptStore>();

        // 审计 / 随机源 / 安全与令牌（Singleton：无状态工具类）
        services.AddScoped<IAuditService, AuditService>();
        services.AddSingleton<IRandomSource, SystemRandomSource>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        return services;
    }

    /// <summary>解析 MySQL 服务版本；探测失败时回落 8.4 基线（不阻断启动）。</summary>
    private static ServerVersion ResolveServerVersion(string connectionString)
    {
        try
        {
            return ServerVersion.AutoDetect(connectionString);
        }
        catch (Exception)
        {
            return new MySqlServerVersion(new Version(8, 4, 0));
        }
    }

    private static void TryAddTimeProvider(this IServiceCollection services)
    {
        if (services.All(descriptor => descriptor.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
