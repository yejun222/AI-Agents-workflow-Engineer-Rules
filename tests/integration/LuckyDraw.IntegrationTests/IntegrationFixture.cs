using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LuckyDraw.Infrastructure.Cache;
using LuckyDraw.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace LuckyDraw.IntegrationTests;

/// <summary>集成测试集合定义（串行执行，见 <c>AssemblyInfo.cs</c>）。</summary>
[CollectionDefinition(Name)]
public class IntegrationCollection : ICollectionFixture<IntegrationFixture>
{
    /// <summary>集合名。</summary>
    public const string Name = "integration";
}

/// <summary>
/// 集成测试夹具：真实 MySQL（<see cref="LuckyDrawApiFactory.DatabaseName"/>）+ 真实 Redis +
/// 真实 HTTP 管道（<c>WebApplicationFactory</c>）。每例前重置数据，保证互不干扰。
/// Redis 的**实例与库编号一律从 <see cref="LuckyDrawApiFactory.RedisConfiguration"/> 派生**
/// （被测应用与夹具的唯一取值来源，支持 `LUCKDRAW_TEST_REDIS` 覆盖；`51:OBS-12`）。
/// </summary>
public class IntegrationFixture : IAsyncLifetime
{
    /// <summary>统一 JSON 选项（后端 camelCase，大小写不敏感绑定）。</summary>
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// 夹具侧 Redis 配置：由 <see cref="LuckyDrawApiFactory.RedisConfiguration"/>（被测应用读到的
    /// 同一份配置）解析而来，仅额外打开 `allowAdmin` —— `FlushDatabase` / `Keys` 是 admin 命令，
    /// 而应用侧配置不需要 admin 权限，故该标志只留在夹具侧、不回写应用配置。
    /// </summary>
    private static readonly Lazy<ConfigurationOptions> RedisOptions = new(() =>
    {
        var options = ConfigurationOptions.Parse(LuckyDrawApiFactory.RedisConfiguration);
        options.AllowAdmin = true;
        return options;
    });

    /// <summary>夹具连接（连接目标 = 被测应用所用实例；admin 权限仅夹具侧持有）。</summary>
    private static readonly Lazy<ConnectionMultiplexer> Redis =
        new(() => ConnectionMultiplexer.Connect(RedisOptions.Value));

    /// <summary>
    /// 清理目标库 = **被测应用实际使用的库**：与 `Redis__Configuration` 同源解析出的 `defaultDatabase`
    /// （未指定时为 Redis 默认 db0）。不得写字面量库号 —— 覆盖值换库后清理即错靶（`51:OBS-12`）。
    /// </summary>
    private static int RedisDatabase => RedisOptions.Value.DefaultDatabase ?? 0;

    private readonly LuckyDrawApiFactory _factory = new();

    /// <summary>共享 HttpClient（不带 Cookie 依赖：鉴权一律走 Bearer 头）。</summary>
    public HttpClient Client { get; private set; } = null!;

    /// <summary>启动宿主、应用迁移到测试库。</summary>
    public async Task InitializeAsync()
    {
        Client = _factory.CreateClient();
        EnsureRedisTargetMatchesApp();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    /// <summary>释放宿主。</summary>
    public async Task DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// 守护「夹具清理目标 ≡ 被测应用实际使用的 Redis 实例与库」：一侧取宿主 DI 里**已绑定**的
    /// `Redis:Configuration`（应用真正读到的那份），另一侧取夹具**实际生效**的目标
    /// （连接端点 = 本连接连到的端点；库号 = 清理命令实际使用的 <see cref="RedisDatabase"/>），
    /// 不一致即抛 —— 不留静默偏差（`51:OBS-12` 的语义目标）。
    /// **边界（如实声明）**：本守护看不见「库号已派生、但清理语句仍写死字面量」这一形态
    /// （参数值无法自省）—— 该形态由「单一调用点 + 唯一取值来源」约束，运行期样本见 `CHG-20`。
    /// </summary>
    private void EnsureRedisTargetMatchesApp()
    {
        var appOptions = ConfigurationOptions.Parse(
            _factory.Services.GetRequiredService<IOptions<RedisOptions>>().Value.Configuration);
        var appTarget = DescribeTarget(appOptions.EndPoints, appOptions.DefaultDatabase ?? 0);
        var fixtureTarget = DescribeTarget(Redis.Value.GetEndPoints(), RedisDatabase);

        if (!string.Equals(appTarget, fixtureTarget, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"夹具 Redis 清理目标与应用不一致：应用 = {appTarget}；夹具 = {fixtureTarget}。"
                + "夹具必须从 LuckyDrawApiFactory.RedisConfiguration 派生（含 defaultDatabase），不得硬编码实例或库号。");
        }
    }

    /// <summary>「实例集 + 库号」的稳定文本（端点排序后拼接）：用于比对与失败信息，避免集合顺序差异造成假阳性。</summary>
    private static string DescribeTarget(IEnumerable<EndPoint> endpoints, int database) =>
        string.Join(
            ",",
            endpoints.Select(endpoint => endpoint.ToString()).OrderBy(text => text, StringComparer.Ordinal))
        + " / db" + database;

    /// <summary>清空业务数据、复位默认奖池（含权重 / 库存 / 启用状态）、清空测试 Redis db。</summary>
    public async Task ResetAsync()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await db.Database.ExecuteSqlRawAsync("DELETE FROM `WinningRecord`");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM `DrawRequest`");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM `UserDrawQuota`");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM `AuditLog`");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM `User`");
            await db.Database.ExecuteSqlRawAsync(
                """
                UPDATE `PrizeItem` SET
                    `Weight` = CASE `Code`
                        WHEN 'prize-keyboard' THEN 1
                        WHEN 'prize-earbuds' THEN 3
                        WHEN 'prize-mug' THEN 10
                        WHEN 'prize-coupon' THEN 20
                        ELSE 66 END,
                    `Stock` = CASE `Code`
                        WHEN 'prize-keyboard' THEN 3
                        WHEN 'prize-earbuds' THEN 10
                        WHEN 'prize-mug' THEN 50
                        WHEN 'prize-coupon' THEN 200
                        ELSE 0 END,
                    `IsEnabled` = 1,
                    `IsDeleted` = 0
                """);
        }

        // 清理目标 ≡ 被测应用实际使用的实例与库：端点取本连接自身（由同一份配置派生，不写死地址），
        // 库号取配置的 defaultDatabase（不写死 1）
        foreach (var server in Redis.Value.GetServers())
        {
            server.FlushDatabase(RedisDatabase);
        }
    }

    /// <summary>仅清空幂等结果缓存（模拟 Redis 降级），保留会话与失败计数：用于验证数据库唯一索引兜底（D-03）。</summary>
    public async Task ResetIdempotencyCacheAsync()
    {
        var database = Redis.Value.GetDatabase(RedisDatabase);
        foreach (var server in Redis.Value.GetServers())
        {
            foreach (var key in server.Keys(database: RedisDatabase, pattern: "draw:idempotency:*"))
            {
                await database.KeyDeleteAsync(key);
            }
        }
    }

    /// <summary>把键盘权重压到绝对优势、其余条目权重压 0：用于并发零超发的确定性构造（D-01 / AC-13）。</summary>
    public async Task BiasKeyboardWeightAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.ExecuteSqlRawAsync(
            "UPDATE `PrizeItem` SET `Weight` = CASE WHEN `Code` = 'prize-keyboard' THEN 1000 ELSE 0 END");
    }

    /// <summary>注册用户并返回 access token（注册成功即登录，FR-01）。</summary>
    /// <param name="userName">用户名（≤ 20 位，仅字母数字下划线）。</param>
    public async Task<string> RegisterAsync(string? userName = null)
    {
        var name = userName ?? GenerateUserName();
        var response = await Client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { userName = name, password = ValidPassword, confirmPassword = ValidPassword });

        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<AuthData>>(JsonOptions);
        envelope!.Code.ShouldBeSuccess();
        return envelope.Data!.AccessToken;
    }

    /// <summary>注册并返回 (用户名, token)。</summary>
    public async Task<(string UserName, string Token)> RegisterUserAsync()
    {
        var name = GenerateUserName();
        return (name, await RegisterAsync(name));
    }

    /// <summary>携带 Bearer 头执行抽奖（幂等键可空）。</summary>
    /// <param name="token">access token。</param>
    /// <param name="idempotencyKey">幂等键。</param>
    public Task<HttpResponseMessage> DrawAsync(string token, string? idempotencyKey) =>
        SendAsync(HttpMethod.Post, "/api/v1/draw", token, idempotencyKey);

    /// <summary>发送带 Bearer 头的请求。</summary>
    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return Client.SendAsync(request);
    }

    /// <summary>读取抽奖响应为统一信封。</summary>
    public static async Task<Envelope<DrawData>> ReadDrawAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<Envelope<DrawData>>(JsonOptions))!;

    /// <summary>直接查询数据库（集成测试可直连，用于校验落库事实）。</summary>
    public async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(db);
    }

    /// <summary>
    /// 创建测试自有作用域：需要**跨多步复用同一 DbContext 实例（= 同一物理连接）**时使用，
    /// 例如把会话级参数（`SET SESSION`）与随后的仓储调用绑定到同一连接上。调用方负责 dispose。
    /// </summary>
    public IServiceScope CreateScope() => _factory.Services.CreateScope();

    /// <summary>生成合法用户名（4–20 位：u + 12 位十六进制）。</summary>
    public static string GenerateUserName() => $"u{Guid.NewGuid():N}"[..13];

    /// <summary>合规模拟密码（满足 CR-01：8–20 位，同时含大写、小写与数字）。</summary>
    public static string ValidPassword => "Passw0rd";

    /// <summary>统一响应信封（code=0 成功）。</summary>
    public sealed record Envelope<T>(int Code, string Message, T? Data);

    /// <summary>注册 / 登录响应数据。</summary>
    public sealed record AuthData(string AccessToken, int ExpiresIn, UserData User);

    /// <summary>用户数据。</summary>
    public sealed record UserData(int Id, string UserName);

    /// <summary>抽奖响应数据。</summary>
    public sealed record DrawData(int ItemId, bool IsWin, int RemainingAttempts);

    /// <summary>剩余次数响应数据。</summary>
    public sealed record QuotaData(int RemainingAttempts, int DailyLimit, DateTimeOffset ResetAt);
}

/// <summary>断言辅助。</summary>
public static class EnvelopeAssertions
{
    /// <summary>断言业务码为 0（成功）。</summary>
    public static void ShouldBeSuccess(this int code) =>
        Assert.True(code == 0, $"期望业务码 0，实际 {code}");

    /// <summary>
    /// 断言业务码等于期望值。注意：业务异常默认 HTTP 200 + code ≥ 1000（D-12），
    /// 但幂等冲突（409）例外 —— 其 HTTP 状态码同为 409（§5.1），断言时须一并校验状态码。
    /// </summary>
    public static void ShouldBeCode(this int code, int expected) =>
        Assert.True(code == expected, $"期望业务码 {expected}，实际 {code}");

    /// <summary>读取响应正文用于诊断输出。</summary>
    public static async Task<string> DescribeAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return $"HTTP {(int)response.StatusCode} / {body}";
    }

    /// <summary>把当前 UTF-8 字符串按原样输出（便于失败信息可读）。</summary>
    public static string Utf8(this string value) => Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(value));
}
