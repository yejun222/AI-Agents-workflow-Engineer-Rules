using System.Security.Cryptography;
using System.Text;
using LuckyDraw.Application.Common;
using LuckyDraw.Application.Interfaces;
using LuckyDraw.Application.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LuckyDraw.Infrastructure.Tokens;

/// <summary>
/// JWT 双 token 实现（D-11）：access token 自包含（userId + userName）；refresh token 为不透明随机串
/// （内含 userId 与 jti），服务端只保存其 SHA-256 哈希。
/// </summary>
public class JwtTokenService : ITokenService
{
    /// <summary>refresh token 组成部分分隔符。</summary>
    private const char TokenSeparator = '.';

    /// <summary>refresh token 随机段字节数。</summary>
    private const int RefreshSecretBytes = 32;

    private readonly JwtOptions _options;

    /// <summary>构造函数注入。</summary>
    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public (string AccessToken, int ExpiresInSeconds) CreateAccessToken(int userId, string userName)
    {
        // access token 的有效期以系统 UTC 时钟签发与校验（框架校验同样使用系统时钟），
        // 不接 TimeProvider，避免测试伪造业务时钟后签发出「未生效 / 已过期」的令牌
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        // 显式使用标准声明的原名（sub / name）：令牌处理器不做声明类型映射，
        // 读取侧（Api / 审计拦截器）按 ClaimNames 常量取值
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Claims = new Dictionary<string, object>
            {
                [ClaimNames.UserId] = userId.ToString(),
                [ClaimNames.UserName] = userName
            },
            NotBefore = now,
            Expires = expires,
            SigningCredentials = credentials
        };

        var accessToken = new JsonWebTokenHandler().CreateToken(descriptor);
        return (accessToken, (int)(expires - now).TotalSeconds);
    }

    /// <inheritdoc />
    public (string RefreshToken, string Jti) CreateRefreshToken(int userId)
    {
        var jti = Guid.NewGuid().ToString("N");
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(RefreshSecretBytes));
        var refreshToken = string.Join(TokenSeparator, userId.ToString(), jti, secret);
        return (refreshToken, jti);
    }

    /// <inheritdoc />
    public (int UserId, string Jti)? TryParseRefreshToken(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var parts = refreshToken.Split(TokenSeparator);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var userId))
        {
            return null;
        }

        return (userId, parts[1]);
    }

    /// <inheritdoc />
    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
