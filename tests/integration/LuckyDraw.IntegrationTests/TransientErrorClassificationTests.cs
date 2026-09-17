using FluentAssertions;
using LuckyDraw.Application.Interfaces;
using LuckyDraw.Domain.Entities;
using LuckyDraw.Domain.Exceptions;
using LuckyDraw.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace LuckyDraw.IntegrationTests;

/// <summary>
/// BUG-05 回归：抽奖事务**后半段写入点**（⑥ 中奖记录 / ⑦ 审计 / ⑧ 回填流水）的瞬时错误分类。
///
/// 覆盖边界（如实声明）：本组用例以**真实 MySQL 行锁竞争**在仓储层注入 1205，断言仓储把
/// `MySqlException(1205)` 翻译为 `TransientDataException`（即错误可进入 `DrawService` 的整事务重试链），
/// 并断言**重试后的落库结果**（⑥⑦ 不得因残留实体产生重复行）。
/// 「瞬时错误 → 重试整个事务 → 耗尽后 1001」的编排由 `tests/unit/**` 的 `DrawServiceTests` 覆盖
/// （替身在 ⑥⑦⑧ 抛出 `TransientDataException`）；未构造跨 50 秒的真实锁等待走完整 HTTP 链路
/// —— 生产 `innodb_lock_wait_timeout` 为 50s，用例内不可接受，故以会话级 1s 加速（见 <see cref="ShortLockWaitSession"/>）。
/// </summary>
[Collection(IntegrationCollection.Name)]
public class TransientErrorClassificationTests(IntegrationFixture fixture)
{
    /// <summary>候选奖品（种子数据 `Id = 4` = 幸运奖 · 平台优惠券）。</summary>
    private const int PrizeItemId = 4;

    /// <summary>
    /// ⑥ 中奖记录：锁等待超时（1205）必须翻译为 `TransientDataException`；
    /// 锁释放后重试**只落一行**（残留的 Added 实体必须被脱离跟踪，否则重复插入中奖记录）。
    /// </summary>
    [Fact]
    public async Task WinningRecordAdd_WhenLockWaitTimesOut_IsTranslatedAndRetryLeavesSingleRow()
    {
        await fixture.ResetAsync();
        var userId = await RegisterUserIdAsync();

        await using var session = await ShortLockWaitSession.OpenAsync(fixture);
        await using (await LockHolder.BeginAsync($"SELECT Id FROM `User` WHERE Id = {userId} FOR UPDATE"))
        {
            // 外键父行（User）被另一事务持 X 锁 → INSERT WinningRecord 的外键校验等待至会话超时（1s）
            var repository = session.Resolve<IWinningRecordRepository>();
            var act = async () => await repository.AddAsync(NewRecord(userId), CancellationToken.None);

            var thrown = (await act.Should().ThrowAsync<TransientDataException>(
                "锁等待超时（1205）必须被分类为可重试的瞬时错误，否则整事务重试机制失效（BUG-05）")).Which;
            MySqlErrorNumber(thrown).Should().Be(1205);
        }

        // 锁已释放：重放「回滚 + 整个事务重试」，同一 DbContext 上不得留下重复行
        await using (var transaction = await session.Resolve<ITransactionManager>().BeginAsync(CancellationToken.None))
        {
            await session.Resolve<IWinningRecordRepository>().AddAsync(NewRecord(userId), CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);
        }

        var rows = await fixture.QueryAsync(db => db.WinningRecords.CountAsync(x => x.UserId == userId));
        rows.Should().Be(1, "重试不得插入重复中奖记录（首次失败的实体必须脱离跟踪）");
    }

    /// <summary>
    /// ⑦ 审计：锁等待超时（1205）必须翻译为 `TransientDataException`；重试后**只落一条**审计
    /// （抽奖链路上审计与扣减同生共死，A2）。
    /// </summary>
    [Fact]
    public async Task AuditLog_WhenLockWaitTimesOut_IsTranslatedAndRetryLeavesSingleRow()
    {
        await fixture.ResetAsync();
        var userId = await RegisterUserIdAsync();
        const string target = "transient-audit-target";

        await using var session = await ShortLockWaitSession.OpenAsync(fixture);
        await using (await LockHolder.BeginAsync(
            // AuditLog 无外键可竞争，改以「探针行 + 覆盖至 supremum 的区间锁」阻塞其它连接的 INSERT
            "INSERT INTO `AuditLog` (`OperateTime`, `Module`, `OperationType`) VALUES (NOW(6), 'probe', 'probe')",
            "SELECT Id FROM `AuditLog` WHERE Id > 0 FOR UPDATE"))
        {
            var auditService = session.Resolve<IAuditService>();
            var act = async () => await auditService.LogAsync(
                new AuditEntry(userId, "draw", "Draw", target, "{\"probe\":true}"),
                CancellationToken.None);

            var thrown = (await act.Should().ThrowAsync<TransientDataException>(
                "审计写入的锁等待超时同样必须可分类，否则退化为未分类的 500")).Which;
            MySqlErrorNumber(thrown).Should().Be(1205);
        }

        await using (var transaction = await session.Resolve<ITransactionManager>().BeginAsync(CancellationToken.None))
        {
            await session.Resolve<IAuditService>().LogAsync(
                new AuditEntry(userId, "draw", "Draw", target, "{\"probe\":true}"), CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);
        }

        var rows = await fixture.QueryAsync(db => db.AuditLogs.CountAsync(x => x.TargetObject == target));
        rows.Should().Be(1, "重试不得写入重复审计（首次失败的实体必须脱离跟踪）");
    }

    /// <summary>
    /// ⑧ 回填流水（走 `ExecuteUpdateAsync`、无变更跟踪）：锁等待超时（1205）必须翻译为 `TransientDataException`；
    /// 重试后流水行被正确回填且只有一行。
    /// </summary>
    [Fact]
    public async Task SaveResult_WhenLockWaitTimesOut_IsTranslatedAndRetryBackfillsRow()
    {
        await fixture.ResetAsync();
        var userId = await RegisterUserIdAsync();

        var drawRequestId = await fixture.QueryAsync(async db =>
        {
            var request = new DrawRequest { UserId = userId, IsWin = false, RemainingAttempts = 0 };
            db.DrawRequests.Add(request);
            await db.SaveChangesAsync();
            return request.Id;
        });

        await using var session = await ShortLockWaitSession.OpenAsync(fixture);
        await using (await LockHolder.BeginAsync($"SELECT Id FROM `DrawRequest` WHERE Id = {drawRequestId} FOR UPDATE"))
        {
            var repository = session.Resolve<IDrawRequestRepository>();
            var act = async () => await repository.SaveResultAsync(
                drawRequestId, PrizeItemId, true, 2, DateTime.UtcNow, CancellationToken.None);

            var thrown = (await act.Should().ThrowAsync<TransientDataException>(
                "回填流水的锁等待超时同样必须可分类（该语句绕过变更跟踪，不补分类则直接冒泡为 500）")).Which;
            MySqlErrorNumber(thrown).Should().Be(1205);
        }

        await using (var transaction = await session.Resolve<ITransactionManager>().BeginAsync(CancellationToken.None))
        {
            await session.Resolve<IDrawRequestRepository>().SaveResultAsync(
                drawRequestId, PrizeItemId, true, 2, DateTime.UtcNow, CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);
        }

        var backfilled = await fixture.QueryAsync(db => db.DrawRequests
            .AsNoTracking()
            .Where(x => x.Id == drawRequestId)
            .Select(x => new { x.PrizeItemId, x.IsWin, x.RemainingAttempts })
            .SingleAsync());

        backfilled.PrizeItemId.Should().Be(PrizeItemId);
        backfilled.IsWin.Should().BeTrue();
        backfilled.RemainingAttempts.Should().Be(2);
    }

    /// <summary>注册一名用户并返回其 Id（User 行是 ⑥ 的外键父行，锁注入点）。</summary>
    private async Task<int> RegisterUserIdAsync()
    {
        var (userName, _) = await fixture.RegisterUserAsync();
        return await fixture.QueryAsync(db => db.Users
            .AsNoTracking()
            .Where(x => x.UserName == userName)
            .Select(x => x.Id)
            .SingleAsync());
    }

    /// <summary>新建一条中奖记录（仅取本组用例关心的字段）。</summary>
    private static WinningRecord NewRecord(int userId) => new()
    {
        UserId = userId,
        PrizeItemId = PrizeItemId,
        PrizeName = "幸运奖 · 平台优惠券"
    };

    /// <summary>从异常链中取出 MySQL 错误号（断言确实是 1205 而非其它错误）。</summary>
    private static int? MySqlErrorNumber(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MySqlException sqlException)
            {
                return sqlException.Number;
            }
        }

        return null;
    }

    /// <summary>
    /// 锁注入器：独占一个 MySQL 连接，在事务内执行调用方给定的语句（如 `SELECT … FOR UPDATE`）并**保持不提交**，
    /// 直到 <see cref="DisposeAsync"/>（回滚 + 关闭）。用于制造真实的行锁 / 区间锁竞争。
    /// </summary>
    private sealed class LockHolder : IAsyncDisposable
    {
        private readonly MySqlConnection _connection;
        private readonly MySqlTransaction _transaction;

        private LockHolder(MySqlConnection connection, MySqlTransaction transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        /// <summary>开启持锁事务并依次执行各语句（不提交）。</summary>
        /// <param name="statements">锁语句（按序执行，每条语句的结果被忽略）。</param>
        public static async Task<LockHolder> BeginAsync(params string[] statements)
        {
            var connection = new MySqlConnection(LuckyDrawApiFactory.ConnectionString);
            await connection.OpenAsync();

            var transaction = await connection.BeginTransactionAsync();
            try
            {
                foreach (var statement in statements)
                {
                    await using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = statement;
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch
            {
                await transaction.DisposeAsync();
                await connection.DisposeAsync();
                throw;
            }

            return new LockHolder(connection, transaction);
        }

        public async ValueTask DisposeAsync()
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    /// <summary>
    /// 测试自有的 DbContext 会话：显式打开连接（后续命令复用同一物理连接），
    /// 并把**会话级** `innodb_lock_wait_timeout` 缩短为 1 秒 —— 生产值 50 秒不适合在用例里空等，
    /// 而锁等待超时的**分类路径**与超时长短无关。
    /// Dispose 时先复原会话变量再关闭连接：物理连接会被连接池复用，不得把缩短值带出用例。
    /// </summary>
    private sealed class ShortLockWaitSession : IAsyncDisposable
    {
        private readonly IServiceScope _scope;

        private ShortLockWaitSession(IServiceScope scope, AppDbContext dbContext)
        {
            _scope = scope;
            DbContext = dbContext;
        }

        /// <summary>本会话的 DbContext（连接保持打开，随会话生命周期复用）。</summary>
        public AppDbContext DbContext { get; }

        /// <summary>从本会话作用域按接口解析服务（仓储 / 审计 / 事务宿主与 DbContext 同实例）。</summary>
        /// <typeparam name="T">服务类型。</typeparam>
        public T Resolve<T>()
            where T : notnull =>
            _scope.ServiceProvider.GetRequiredService<T>();

        /// <summary>打开会话并把会话级锁等待超时缩短为 1 秒。</summary>
        /// <param name="fixture">集成测试夹具。</param>
        public static async Task<ShortLockWaitSession> OpenAsync(IntegrationFixture fixture)
        {
            var scope = fixture.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                await dbContext.Database.OpenConnectionAsync();
                await dbContext.Database.ExecuteSqlRawAsync("SET SESSION innodb_lock_wait_timeout = 1");
            }
            catch
            {
                scope.Dispose();
                throw;
            }

            return new ShortLockWaitSession(scope, dbContext);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.Database.ExecuteSqlRawAsync("SET SESSION innodb_lock_wait_timeout = DEFAULT");
            await DbContext.Database.CloseConnectionAsync();
            _scope.Dispose();
        }
    }
}
