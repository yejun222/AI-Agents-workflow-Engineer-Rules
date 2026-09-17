using FluentValidation;
using LuckyDraw.Application.Dtos;
using LuckyDraw.Application.Interfaces;
using LuckyDraw.Application.Options;
using LuckyDraw.Application.Services;
using LuckyDraw.Application.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LuckyDraw.Application;

/// <summary>Application 层依赖装配。</summary>
public static class DependencyInjection
{
    /// <summary>注册业务服务、校验器与配置强校验（配置绑定在 Api 层完成，避免业务层依赖配置提供程序）。</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // 业务服务（Scoped）
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPrizePoolService, PrizePoolService>();
        services.AddScoped<IDrawService, DrawService>();
        services.AddScoped<IWinningRecordService, WinningRecordService>();

        // FluentValidation 校验器（按 DTO 注册，供全局 ValidationFilter 解析）
        services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();

        // 启动强校验（D-06）：确定性配置泄漏时应用拒绝启动
        services.AddSingleton<IValidateOptions<DrawOptions>, DrawOptionsValidator>();
        services.AddSingleton<IValidateOptions<PrizeOptions>, PrizeOptionsValidator>();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

        return services;
    }
}
