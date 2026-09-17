using System.Security.Cryptography;
using LuckyDraw.Application.Interfaces;

namespace LuckyDraw.Infrastructure.Security;

/// <summary>BCrypt 密码哈希实现（规范 8.1）：禁止明文存储与传输，哈希值禁止出现在日志 / 响应 / 审计。</summary>
public class BcryptPasswordHasher : IPasswordHasher
{
    /// <summary>BCrypt 工作因子（默认 11，登录 / 注册 ≤ 350ms 预算内）。</summary>
    private const int WorkFactor = 11;

    /// <summary>
    /// 等价成本校验用的假哈希（REV-05）：对**随机生成的、无任何账号对应的**口令做一次同工作因子哈希。
    /// 仅在类型初始化时计算一次；其明文由加密随机源生成后即丢弃，不存在可被猜中而误判成功的可能。
    /// </summary>
    private static readonly string DummyPasswordHash =
        BCrypt.Net.BCrypt.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), WorkFactor);

    /// <inheritdoc />
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    /// <inheritdoc />
    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(passwordHash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // 哈希格式非法（历史脏数据）→ 视为校验失败，不暴露内部细节
            return false;
        }
    }

    /// <inheritdoc />
    public bool VerifyOrDummy(string password, string? passwordHash)
    {
        if (string.IsNullOrEmpty(passwordHash))
        {
            // 用户不存在：仍然付出一次同工作因子的 BCrypt 校验成本，失败路径耗时与「密码错误」一致（防账号枚举）
            _ = Verify(password, DummyPasswordHash);
            return false;
        }

        return Verify(password, passwordHash);
    }
}
