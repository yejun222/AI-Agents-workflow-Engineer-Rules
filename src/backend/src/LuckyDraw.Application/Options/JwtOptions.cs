namespace LuckyDraw.Application.Options;

/// <summary>JWT 与刷新令牌配置（D-11）。签名密钥走 user-secrets / 环境变量，禁止入库入仓。</summary>
public class JwtOptions
{
    /// <summary>配置节名。</summary>
    public const string SectionName = "Jwt";

    /// <summary>签发者。</summary>
    public string Issuer { get; set; } = "LuckyDraw";

    /// <summary>受众。</summary>
    public string Audience { get; set; } = "LuckyDraw.Web";

    /// <summary>签名密钥（非 Development / Testing 环境下必须显式提供且长度 ≥ 32）。</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>access token 有效期（分钟，默认 120）。</summary>
    public int AccessTokenMinutes { get; set; } = 120;

    /// <summary>refresh token 有效期（天，默认 7）。</summary>
    public int RefreshTokenDays { get; set; } = 7;
}
