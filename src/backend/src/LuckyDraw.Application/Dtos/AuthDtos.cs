namespace LuckyDraw.Application.Dtos;

/// <summary>注册请求（API-01）；字段与前端 <c>types/auth.ts</c> 逐一对齐。</summary>
public class RegisterRequest
{
    /// <summary>用户名（4–20 位，仅字母 / 数字 / 下划线）。</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>密码（8–20 位，须同时包含大写字母、小写字母与数字，CR-01）。</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>确认密码（必须与密码一致）。</summary>
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>登录请求（API-02）。</summary>
public class LoginRequest
{
    /// <summary>用户名。</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>密码。</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>用户信息。</summary>
public class UserDto
{
    /// <summary>用户 Id。</summary>
    public int Id { get; set; }

    /// <summary>用户名。</summary>
    public string UserName { get; set; } = string.Empty;
}

/// <summary>注册 / 登录响应（API-01 / API-02）：refresh token 走 httpOnly Cookie，不在此结构内。</summary>
public class AuthResponse
{
    /// <summary>访问令牌。</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>访问令牌有效期（秒）。</summary>
    public int ExpiresIn { get; set; }

    /// <summary>用户信息。</summary>
    public UserDto User { get; set; } = new();

    /// <summary>刷新令牌（仅用于写入 httpOnly Cookie，不参与 JSON 序列化）。</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>刷新响应（API-03）。</summary>
public class RefreshResponse
{
    /// <summary>新的访问令牌。</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>访问令牌有效期（秒）。</summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// 用户信息（2026-09-17 **兼容性新增**，v1 不升版）：前端受保护路由静默恢复会话时
    /// 用户信息的唯一来源（§2.8）；禁止在浏览器存储中缓存用户信息。
    /// </summary>
    public UserDto User { get; set; } = new();

    /// <summary>轮换后的刷新令牌（仅用于写入 Cookie，不参与 JSON 序列化）。</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string RefreshToken { get; set; } = string.Empty;
}
