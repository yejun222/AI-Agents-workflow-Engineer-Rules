namespace LuckyDraw.Domain.Exceptions;

/// <summary>
/// 可重试的瞬时数据异常（MySQL 死锁 1213 / 锁等待超时 1205）。
/// Infrastructure 仓储把数据库瞬时错误翻译为本异常，避免业务层依赖数据库实现；
/// 收到本异常时事务通常已被数据库整体回滚，业务层可安全重试整个事务（D-01 / D-02 重试语义）。
/// </summary>
public class TransientDataException : Exception
{
    /// <summary>构造瞬时数据异常。</summary>
    /// <param name="message">内部诊断信息（不对外展示）。</param>
    /// <param name="innerException">原始数据库异常。</param>
    public TransientDataException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
