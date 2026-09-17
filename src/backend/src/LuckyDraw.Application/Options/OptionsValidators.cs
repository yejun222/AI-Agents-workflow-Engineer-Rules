using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LuckyDraw.Application.Options;

/// <summary>
/// 确定性配置防泄漏强校验（D-06 / PRD R5 / 附录 B B1）：
/// 宿主环境既不是 Development 也不是 Testing，且存在任一确定性配置（开关或覆盖项）→ 抛异常，应用拒绝启动。
/// </summary>
public class DrawOptionsValidator : IValidateOptions<DrawOptions>
{
    private readonly IHostEnvironment _environment;

    /// <summary>构造函数注入宿主环境。</summary>
    public DrawOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, DrawOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.DailyLimit <= 0)
        {
            failures.Add($"{DrawOptions.SectionName}:DailyLimit 必须大于 0（当前 {options.DailyLimit}）。");
        }

        var hasDeterministicConfig = options.Deterministic.Enabled
            || options.Deterministic.ForcedResults.Count > 0;

        if (hasDeterministicConfig && !IsTestEnvironment(_environment.EnvironmentName))
        {
            failures.Add(
                $"{DrawOptions.SectionName}:Deterministic 仅允许在 Development / Testing 环境使用；" +
                $"当前环境为 {_environment.EnvironmentName}，检测到确定性配置，应用拒绝启动（D-06 / PRD R5）。");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>判定是否为允许确定性配置的环境。</summary>
    internal static bool IsTestEnvironment(string environmentName) =>
        string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
        || string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);
}

/// <summary>权重覆盖的防泄漏强校验（同 D-06 口径）。</summary>
public class PrizeOptionsValidator : IValidateOptions<PrizeOptions>
{
    private readonly IHostEnvironment _environment;

    /// <summary>构造函数注入宿主环境。</summary>
    public PrizeOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, PrizeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.WeightOverrides.Count > 0
            && !DrawOptionsValidator.IsTestEnvironment(_environment.EnvironmentName))
        {
            return ValidateOptionsResult.Fail(
                $"{PrizeOptions.SectionName}:WeightOverrides 仅允许在 Development / Testing 环境使用；" +
                $"当前环境为 {_environment.EnvironmentName}，应用拒绝启动（D-06 / PRD R5）。");
        }

        if (options.WeightOverrides.Any(pair => pair.Value <= 0))
        {
            return ValidateOptionsResult.Fail(
                $"{PrizeOptions.SectionName}:WeightOverrides 权重必须为正整数（D-04 整数权重）。");
        }

        return ValidateOptionsResult.Success;
    }
}

/// <summary>JWT 配置强校验：非 Development / Testing 环境必须显式提供足够长度的签名密钥（B6）。</summary>
public class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    private readonly IHostEnvironment _environment;

    /// <summary>构造函数注入宿主环境。</summary>
    public JwtOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.AccessTokenMinutes <= 0)
        {
            failures.Add($"{JwtOptions.SectionName}:AccessTokenMinutes 必须大于 0。");
        }

        if (options.RefreshTokenDays <= 0)
        {
            failures.Add($"{JwtOptions.SectionName}:RefreshTokenDays 必须大于 0。");
        }

        var requiresStrongKey = !DrawOptionsValidator.IsTestEnvironment(_environment.EnvironmentName);
        if (requiresStrongKey && options.SigningKey.Length < 32)
        {
            failures.Add(
                $"{JwtOptions.SectionName}:SigningKey 必须显式配置且长度 ≥ 32（生产走环境变量 / user-secrets，禁止入库入仓）。");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
