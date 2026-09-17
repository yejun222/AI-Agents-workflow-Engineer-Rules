using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LuckyDraw.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
/// 集成测试夹具：真实 MySQL（<see cref="LuckyDrawApiFactory.DatabaseName"/>）+ 真实 Redis db=1 +
/// 真实 HTTP 管道（<c>WebApplicationFactory</c>）。每例前重置数据，保证互不干扰。
/// </summary>
public class IntegrationFixture : IAsyncLifetime
{
    /// <summary>统一 JSON 选项（后端 camelCase，大小写不敏感绑定）。</summary>
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<ConnectionMultiplexer> Redis =
        new(() => ConnectionMultiplexer.Connect("localhost:6379,allowAdmin=true"));

    private readonly LuckyDrawApiFactory _factory = new();

    /// <summary>共享 HttpClient（不带 Cookie 依赖：鉴权一律走 Bearer 头）。</summary>
    public HttpClient Client { get; private set; } = null!;

    /// <summary>启动宿主、应用迁移到测试库。</summary>
    public async Task InitializeAsync()
    {
        Client = _factory.CreateClient();

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

        Redis.Value.GetServer("localhost:6379").FlushDatabase(1);
    }

    /// <summary>仅清空幂等结果缓存（模拟 Redis 降级），保留会话与失败计数：用于验证数据库唯一索引兜底（D-03）。</summary>
    public async Task ResetIdempotencyCacheAsync()
    {
        var database = Redis.Value.GetDatabase(1);
        foreach (var key in Redis.Value.GetServer("localhost:6379").Keys(database: 1, pattern: "draw:idempotency:*"))
        {
            await database.KeyDeleteAsync(key);
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
