using System.Text.RegularExpressions;
using LuckyDraw.Domain.Exceptions;

namespace LuckyDraw.Application.Common;

/// <summary>
/// `Idempotency-Key` 请求头校验（REV-04）。
/// 契约：API-07 / API-01 / API-02 的请求头为 `Idempotency-Key: {uuid}`（架构 §5.1 提交行），
/// **缺失 = 无幂等、正常处理**；**格式非法 → 1002**（HTTP 200），
/// 不得让非法键落到 `DrawRequest.IdempotencyKey`（varchar(64)）触发 `Data too long`(1406) → 500。
/// </summary>
public static partial class IdempotencyKeyValidator
{
    /// <summary>幂等键最大长度（与 `DrawRequest.IdempotencyKey` 的 varchar(64) 对齐，校验先于落库）。</summary>
    public const int MaxLength = 64;

    /// <summary>
    /// 校验并归一化幂等键：空白视为缺失（返回 null）；长度超限或非 UUID 形态抛 <see cref="BusinessException"/>（1002）。
    /// </summary>
    /// <param name="idempotencyKey">请求头原始值。</param>
    /// <returns>可安全落库 / 作缓存 key 的幂等键；缺失时为 null。</returns>
    public static string? Validate(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        var key = idempotencyKey.Trim();

        // 长度判据先行：即使未来放宽形态规则，也不会再把超长值交给数据库报 1406
        if (key.Length > MaxLength || !UuidPattern().IsMatch(key))
        {
            throw new BusinessException(ErrorCodes.ValidationFailed, "Idempotency-Key 格式非法（应为 UUID）");
        }

        return key;
    }

    /// <summary>UUID 形态（8-4-4-4-12 十六进制，大小写均可）：长度与字符集同时收敛。</summary>
    [GeneratedRegex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    private static partial Regex UuidPattern();
}
