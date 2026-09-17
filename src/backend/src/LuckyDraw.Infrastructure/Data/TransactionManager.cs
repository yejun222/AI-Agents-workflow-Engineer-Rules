using System.Data;
using LuckyDraw.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LuckyDraw.Infrastructure.Data;

/// <summary>
/// 事务宿主实现（D-08）：显式 READ COMMITTED —— 重抽与重试读必须看到最新已提交的库存，
/// MySQL 默认 RR 的快照复用会导致「重抽仍选到刚被抽空的奖品」。
/// </summary>
public class TransactionManager : ITransactionManager
{
    private readonly AppDbContext _dbContext;

    /// <summary>构造函数注入。</summary>
    public TransactionManager(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<ITransactionScope> BeginAsync(CancellationToken cancellationToken)
    {
        var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        return new EfTransactionScope(transaction);
    }

    /// <summary>EF Core 事务作用域适配：未提交即释放时自动回滚。</summary>
    private sealed class EfTransactionScope : ITransactionScope
    {
        private readonly IDbContextTransaction _transaction;
        private bool _completed;

        public EfTransactionScope(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            await _transaction.CommitAsync(cancellationToken);
            _completed = true;
        }

        public async Task RollbackAsync(CancellationToken cancellationToken)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;

            try
            {
                await _transaction.RollbackAsync(cancellationToken);
            }
            catch (Exception)
            {
                // 连接已断开等场景下回滚失败无需上抛：事务已由数据库侧终止
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_completed)
            {
                await RollbackAsync(CancellationToken.None);
            }

            await _transaction.DisposeAsync();
        }
    }
}
