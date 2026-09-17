namespace LuckyDraw.Domain.Entities;

/// <summary>用户实体（MOD-01）。用户名全局唯一且不区分大小写（依赖 utf8mb4_0900_ai_ci 排序规则）。</summary>
public class User : AuditableEntity
{
    /// <summary>用户名（4–20 位，仅字母 / 数字 / 下划线）。</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>BCrypt 密码哈希；禁止出现在任何日志、响应与审计 JSON 中。</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>逻辑删除标记（规范红线：核心实体禁止物理删除）。</summary>
    public bool IsDeleted { get; set; }
}
