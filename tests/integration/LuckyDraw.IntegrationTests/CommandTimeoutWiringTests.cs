using FluentAssertions;
using LuckyDraw.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace LuckyDraw.IntegrationTests;

/// <summary>
/// BUG-05 守护：抽奖事务的**命令超时**必须显式接线，且严格大于服务端 <c>innodb_lock_wait_timeout</c>。
///
/// 为什么需要这条：<see cref="TransientErrorClassificationTests"/> 为把锁等待压到用例可接受的长度，
/// 以**会话级** <c>innodb_lock_wait_timeout = 1</c> 加速 —— 在该口径下无论命令超时是 30s 还是 60s，
/// MySQL 的「锁等待超时 1205」都会先触发，因此**那组用例发现不了**下面两种回归：
/// ① `AddInfrastructure` 内 <c>mySqlOptions.CommandTimeout(...)</c> 这行接线被删（回落 MySqlConnector 默认 30s）；
/// ② 常量被下调到 ≤ <c>innodb_lock_wait_timeout</c>（默认 50s），命令超时重新抢在 1205 之前（BUG-05 静默复活）。
///
/// 本组用例**读回 DI 容器中实际生效的命令超时**（而非源码常量，故不是同义反复），
/// 并与服务端**实际取值**比较，把上述两种回归钉死。
/// </summary>
[Collection(IntegrationCollection.Name)]
public class CommandTimeoutWiringTests(IntegrationFixture fixture)
{
    /// <summary>服务端锁等待超时变量名（与 <c>DependencyInjection.CommandTimeoutSeconds</c> 的注释口径一致）。</summary>
    private const string LockWaitVariable = "innodb_lock_wait_timeout";

    /// <summary>
    /// 实际生效的命令超时存在（接线未丢）且 &gt; 服务端 <c>innodb_lock_wait_timeout</c>（1205 能先触发）。
    /// </summary>
    [Fact]
    public async Task CommandTimeout_IsWiredAndExceedsServerLockWaitTimeout()
    {
        using var scope = fixture.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 从已构建的 DbContextOptions 读回实际生效值（接线被删时回落连接器默认值或 null）
        var effective = dbContext.Database.GetCommandTimeout();
        var serverLockWait = await ReadServerLockWaitTimeoutSecondsAsync();

        effective.Should().NotBeNull(
            "必须显式接线 CommandTimeout（DependencyInjection.cs 的 mySqlOptions.CommandTimeout(...)）：" +
            "回落 MySqlConnector 默认 30s 会抢在 MySQL 的 1205 之前中断锁等待，" +
            "使瞬时错误无法分类、整事务重试失效（BUG-05 复活）");

        effective!.Value.Should().BeGreaterThan(
            serverLockWait,
            $"命令超时（{effective.Value}s）必须严格大于服务端 {LockWaitVariable}（{serverLockWait}s），" +
            "否则锁等待永远由客户端命令超时先中断，1205 不可达 → 抽奖事务的整事务重试链失效（BUG-05）");
    }

    /// <summary>读取服务端实际生效的 <c>innodb_lock_wait_timeout</c>（秒）。</summary>
    private static async Task<int> ReadServerLockWaitTimeoutSecondsAsync()
    {
        await using var connection = new MySqlConnection(LuckyDrawApiFactory.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"SHOW VARIABLES LIKE '{LockWaitVariable}'";

        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue($"测试库应能读到 {LockWaitVariable}");

        return Convert.ToInt32(reader.GetValue(1));
    }
}
