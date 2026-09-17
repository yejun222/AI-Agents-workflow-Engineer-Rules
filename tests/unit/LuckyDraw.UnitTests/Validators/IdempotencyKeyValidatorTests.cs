using FluentAssertions;
using LuckyDraw.Application.Common;
using LuckyDraw.Domain.Exceptions;

namespace LuckyDraw.UnitTests.Validators;

/// <summary>
/// `Idempotency-Key` 请求头校验（REV-04）：缺失 = 无幂等（返回 null），非 UUID / 超长 → 1002，
/// 禁止把非法键交给数据库（varchar(64) 溢出会落成 1406 → 500）。
/// </summary>
public class IdempotencyKeyValidatorTests
{
    /// <summary>缺失（null / 空 / 空白）一律按「无幂等」处理，不报错。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenMissing_ReturnsNull(string? key)
    {
        IdempotencyKeyValidator.Validate(key).Should().BeNull();
    }

    /// <summary>标准 UUID v4（小写 / 大写 / 带首尾空格）通过校验并归一化。</summary>
    [Theory]
    [InlineData("3f2a1b4c-5d6e-4f70-8a9b-0c1d2e3f4a5b")]
    [InlineData("3F2A1B4C-5D6E-4F70-8A9B-0C1D2E3F4A5B")]
    public void Validate_WhenUuid_ReturnsKey(string key)
    {
        IdempotencyKeyValidator.Validate(key).Should().Be(key);
        IdempotencyKeyValidator.Validate($"  {key}  ").Should().Be(key);
    }

    /// <summary>非 UUID 形态（任意字符串、缺段、非十六进制）→ 1002，不得落库。</summary>
    [Theory]
    [InlineData("key-1")]
    [InlineData("3f2a1b4c-5d6e-4f70-8a9b")]
    [InlineData("3f2a1b4c-5d6e-4f70-8a9b-0c1d2e3f4a5b-extra")]
    [InlineData("zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz")]
    [InlineData("3f2a1b4c5d6e4f708a9b0c1d2e3f4a5b")]
    public void Validate_WhenNotUuid_Throws1002(string key)
    {
        var act = () => IdempotencyKeyValidator.Validate(key);

        var exception = act.Should().Throw<BusinessException>().Which;
        exception.Code.Should().Be(1002);
        exception.Message.Should().Be("Idempotency-Key 格式非法（应为 UUID）");
    }

    /// <summary>超长键（≥65 字符）→ 1002；列长度 64 的溢出必须在业务层被拦下（对齐 AppDbContext 的 HasMaxLength(64)）。</summary>
    [Fact]
    public void Validate_WhenTooLong_Throws1002()
    {
        var tooLong = new string('a', IdempotencyKeyValidator.MaxLength + 1);
        var act = () => IdempotencyKeyValidator.Validate(tooLong);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(1002);
    }
}
