using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LuckyDraw.IntegrationTests;

/// <summary>
/// 集成测试宿主（规范 7.1 方案二）：<c>WebApplicationFactory&lt;Program&gt;</c> 直连开发态容器，
/// 以**独立库名 + 独立 Redis db** 隔离。
/// 连接串通过**环境变量**注入（禁止 <c>ConfigureAppConfiguration</c>，否则会绕过启动校验与配置绑定路径）。
/// </summary>
public class LuckyDrawApiFactory : WebApplicationFactory<Program>
{
    /// <summary>测试库（与开发库 `luckydraw_dev` 隔离，可随时清空）。</summary>
    public const string DatabaseName = "luckydraw_test";

    /// <summary>
    /// 测试库连接串（开发态 MySQL 容器映射在宿主 3307）。
    /// **测试侧自带**、不读取 `appsettings.Development.json`（REV-09 后该文件不含任何口令）。
    /// 可用环境变量 `LUCKDRAW_TEST_CONNECTION_STRING` 覆盖（CI 等场景注入自有容器）。
    /// </summary>
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("LUCKDRAW_TEST_CONNECTION_STRING")
        ?? "Server=localhost;Port=3307;Database=" + DatabaseName +
        ";User Id=root;Password=devonly;CharSet=utf8mb4;SslMode=None;AllowPublicKeyRetrieval=True";

    /// <summary>测试用 Redis（独立 db=1，避免与开发数据互相干扰；可用 `LUCKDRAW_TEST_REDIS` 覆盖）。</summary>
    public static string RedisConfiguration =>
        Environment.GetEnvironmentVariable("LUCKDRAW_TEST_REDIS") ?? "localhost:6379,defaultDatabase=1";

    /// <summary>注入环境变量后再启动宿主（环境变量是规范认可的配置覆盖手段）。</summary>
    /// <param name="builder">宿主构建器。</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", ConnectionString);
        Environment.SetEnvironmentVariable("Redis__Configuration", RedisConfiguration);

        // Testing 环境：加载 appsettings.Testing.json（放宽限流、签名密钥），
        // 同时使 D-06 的确定性配置守卫与 Cookie 的 Secure 策略按测试口径生效
        builder.UseEnvironment("Testing");
    }
}
