using FluentValidation;
using LuckyDraw.Application.Dtos;

namespace LuckyDraw.Application.Validators;

/// <summary>
/// 注册请求校验（API-01）。**密码规则文本与前端 <c>types/auth.ts</c> 的 Zod Schema 逐字一致**
/// （CR-01 / 附录 A 第 10 条）：8–20 位，须同时包含大写字母、小写字母与数字。
/// </summary>
public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    /// <summary>用户名规则（4–20 位，仅字母 / 数字 / 下划线）。</summary>
    public const string UserNamePattern = "^[A-Za-z0-9_]{4,20}$";

    /// <summary>密码规则（8–20 位，须同时包含大写字母、小写字母与数字）。</summary>
    public const string PasswordPattern = "^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d).{8,20}$";

    /// <summary>用户名提示文案（与原型 reg-username-hint 一致口径）。</summary>
    public const string UserNameMessage = "用户名需 4-20 位，仅字母、数字、下划线";

    /// <summary>密码提示文案（与原型 v3 reg-password-hint 逐字一致）。</summary>
    public const string PasswordMessage = "密码需 8-20 位且同时含大写字母、小写字母与数字";

    /// <summary>确认密码提示文案。</summary>
    public const string ConfirmPasswordMessage = "两次输入不一致";

    /// <summary>构造校验规则。</summary>
    public RegisterRequestValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage(UserNameMessage)
            .Matches(UserNamePattern).WithMessage(UserNameMessage);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(PasswordMessage)
            .Matches(PasswordPattern).WithMessage(PasswordMessage);

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage(ConfirmPasswordMessage)
            .Equal(x => x.Password).WithMessage(ConfirmPasswordMessage);
    }
}

/// <summary>登录请求校验（API-02）：仅做非空校验，失败原因一律由服务层统一为 1102。</summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    /// <summary>构造校验规则。</summary>
    public LoginRequestValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("请输入用户名")
            .MaximumLength(20).WithMessage("用户名长度不能超过 20 位");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("请输入密码")
            .MaximumLength(20).WithMessage("密码长度不能超过 20 位");
    }
}
