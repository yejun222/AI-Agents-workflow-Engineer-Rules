using FluentAssertions;
using LuckyDraw.Application.Dtos;
using LuckyDraw.Application.Validators;

namespace LuckyDraw.UnitTests.Validators;

/// <summary>
/// CR-01 密码规则：8–20 位且**必须同时包含大写字母、小写字母与数字**（不是宽松的「字母 + 数字」）。
/// 规则文本与前端 `types/auth.ts` 的 Zod Schema 逐字一致，本测试同时锁定该文本。
/// </summary>
public class RequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    private static RegisterRequest Request(string password, string? confirm = null, string userName = "alice_01") =>
        new()
        {
            UserName = userName,
            Password = password,
            ConfirmPassword = confirm ?? password
        };

    /// <summary>合规密码（大小写字母 + 数字，长度 8–20）通过校验。</summary>
    [Theory]
    [InlineData("Abcd1234")]
    [InlineData("Passw0rd")]
    [InlineData("aB1aaaaa")]
    [InlineData("Aa1bcdefghijklmnopqr")]   // 20 位，含大写 / 小写 / 数字
    public void Register_WithValidPassword_Passes(string password)
    {
        _validator.Validate(Request(password)).IsValid.Should().BeTrue();
    }

    /// <summary>缺数字：即使同时含大小写字母也必须失败（CR-01 收紧点）。</summary>
    [Theory]
    [InlineData("Abcdefgh")]     // 只有大小写字母
    [InlineData("ABCDEFGH")]     // 全大写
    public void Register_WithPasswordMissingDigit_Fails(string password)
    {
        var result = _validator.Validate(Request(password));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == RegisterRequestValidator.PasswordMessage);
    }

    /// <summary>缺小写字母失败。</summary>
    [Theory]
    [InlineData("ABCD1234")]
    [InlineData("ABC12345")]
    public void Register_WithPasswordMissingLowercase_Fails(string password)
    {
        _validator.Validate(Request(password)).IsValid.Should().BeFalse();
    }

    /// <summary>缺大写字母失败。</summary>
    [Theory]
    [InlineData("abcd1234")]
    [InlineData("abc12345")]
    public void Register_WithPasswordMissingUppercase_Fails(string password)
    {
        _validator.Validate(Request(password)).IsValid.Should().BeFalse();
    }

    /// <summary>长度越界（&lt; 8 或 &gt; 20）失败。</summary>
    [Theory]
    [InlineData("Abc123")]                      // 6 位
    [InlineData("Abc1234")]                     // 7 位
    [InlineData("Aa1bcdefghijklmnopqrs")]       // 21 位
    public void Register_WithPasswordOutOfLengthRange_Fails(string password)
    {
        _validator.Validate(Request(password)).IsValid.Should().BeFalse();
    }

    /// <summary>确认密码不一致 → 「两次输入不一致」（与原型文案一致）。</summary>
    [Fact]
    public void Register_WithMismatchedConfirmPassword_ReportsFrozenMessage()
    {
        var result = _validator.Validate(Request("Abcd1234", "Abcd12345"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == RegisterRequestValidator.ConfirmPasswordMessage);
    }

    /// <summary>用户名规则：4–20 位，仅字母 / 数字 / 下划线。</summary>
    [Theory]
    [InlineData("abc")]           // 3 位
    [InlineData("ab-cd")]         // 连字符
    [InlineData("张三")]          // 非 ASCII
    [InlineData("abcdefghijklmnopqrstu")] // 21 位
    public void Register_WithInvalidUserName_Fails(string userName)
    {
        var result = _validator.Validate(Request("Abcd1234", userName: userName));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == RegisterRequestValidator.UserNameMessage);
    }

    /// <summary>冻结文案与正则常量逐字锁定（防止后续被改回宽松规则或改写提示）。</summary>
    [Fact]
    public void Register_FrozenRuleTextAndPattern_AreStable()
    {
        RegisterRequestValidator.PasswordMessage.Should().Be("密码需 8-20 位且同时含大写字母、小写字母与数字");
        RegisterRequestValidator.ConfirmPasswordMessage.Should().Be("两次输入不一致");
        RegisterRequestValidator.UserNamePattern.Should().Be("^[A-Za-z0-9_]{4,20}$");
        RegisterRequestValidator.PasswordPattern.Should().Be("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d).{8,20}$");
    }

    /// <summary>登录校验只做非空与长度上限，不套用注册的复杂度规则（登录失败统一 1102）。</summary>
    [Fact]
    public void Login_WithWrongComplexity_IsStillValidLocally()
    {
        var result = new LoginRequestValidator().Validate(new LoginRequest { UserName = "alice_01", Password = "whatever" });

        result.IsValid.Should().BeTrue();
    }

    /// <summary>登录请求缺少用户名 / 密码时拦截。</summary>
    [Fact]
    public void Login_WithEmptyFields_IsInvalid()
    {
        var result = new LoginRequestValidator().Validate(new LoginRequest { UserName = "", Password = "" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }
}
