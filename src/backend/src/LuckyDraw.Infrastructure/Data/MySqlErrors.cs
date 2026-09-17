using MySqlConnector;

namespace LuckyDraw.Infrastructure.Data;

/// <summary>MySQL 错误号判定（唯一键兜底与锁等待超时的统一入口）。</summary>
internal static class MySqlErrors
{
    /// <summary>唯一键冲突。</summary>
    public const int DuplicateEntry = 1062;

    /// <summary>锁等待超时（并发同幂等键等待首个事务提交超时）。</summary>
    public const int LockWaitTimeout = 1205;

    /// <summary>死锁。</summary>
    public const int Deadlock = 1213;

    /// <summary>是否为唯一键冲突（幂等 / 唯一性校验的数据库兜底）。</summary>
    public static bool IsDuplicateKey(Exception exception) =>
        FindMySqlException(exception)?.Number == DuplicateEntry;

    /// <summary>是否为锁等待超时。</summary>
    public static bool IsLockWaitTimeout(Exception exception) =>
        FindMySqlException(exception)?.Number == LockWaitTimeout;

    /// <summary>是否为死锁。</summary>
    public static bool IsDeadlock(Exception exception) =>
        FindMySqlException(exception)?.Number == Deadlock;

    /// <summary>
    /// 是否为可重试的瞬时错误（死锁 / 锁等待超时）。
    /// 死锁场景 InnoDB 会回滚整个牺牲事务，因此只能重试**整个事务**，重试单条语句不安全。
    /// </summary>
    public static bool IsTransient(Exception exception) =>
        IsDeadlock(exception) || IsLockWaitTimeout(exception);

    private static MySqlException? FindMySqlException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MySqlException sqlException)
            {
                return sqlException;
            }
        }

        return null;
    }
}
