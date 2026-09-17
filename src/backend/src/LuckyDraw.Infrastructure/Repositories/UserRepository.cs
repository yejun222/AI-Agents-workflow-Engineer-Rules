using LuckyDraw.Application.Common;
using LuckyDraw.Application.Interfaces;
using LuckyDraw.Domain.Entities;
using LuckyDraw.Domain.Exceptions;
using LuckyDraw.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LuckyDraw.Infrastructure.Repositories;

/// <summary>用户仓储实现（MOD-01）。</summary>
public class UserRepository : IUserRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>构造函数注入。</summary>
    public UserRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken)
    {
        // 列级 collation utf8mb4_0900_ai_ci 使等值比较大小写不敏感（FR-01-1）
        return await _dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserName == userName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByUserNameAsync(string userName, CancellationToken cancellationToken)
    {
        // 规范 4.4：唯一索引建在物理列上，软删除不释放唯一性 → 校验必须 IgnoreQueryFilters()
        return await _dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(x => x.UserName == userName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User> AddAsync(User user, CancellationToken cancellationToken)
    {
        _dbContext.Users.Add(user);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (MySqlErrors.IsDuplicateKey(exception))
        {
            // 并发注册同一用户名的兜底：唯一索引冲突翻译为业务错误 1101
            _dbContext.Entry(user).State = EntityState.Detached;
            throw new BusinessException(ErrorCodes.UserNameTaken, "用户名已被占用");
        }

        return user;
    }
}
