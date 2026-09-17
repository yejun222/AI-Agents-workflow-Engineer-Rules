using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using LuckyDraw.Api.Common;
using LuckyDraw.Api.Filters;
using LuckyDraw.Application;
using LuckyDraw.Application.Common;
using LuckyDraw.Application.Options;
using LuckyDraw.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------- 日志（Serilog 结构化日志；SQL 命令日志降到 Warning，避免参数进日志）----------
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ---------- 配置绑定 + 启动强校验（D-06：确定性配置泄漏 → 应用拒绝启动）----------
builder.Services.AddOptions<DrawOptions>()
    .Bind(builder.Configuration.GetSection(DrawOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<PrizeOptions>()
    .Bind(builder.Configuration.GetSection(PrizeOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<RateLimitOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitOptions.SectionName))
    .ValidateOnStart();

// ---------- 分层装配 ----------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---------- MVC + 全局过滤器（规范 4.2：控制器只返回业务对象）----------
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ResultFilter>();
    options.Filters.Add<ExceptionFilter>();
    options.Filters.Add<ValidationFilter>();
})
.ConfigureApiBehaviorOptions(options =>
{
    // 模型绑定 / DataAnnotations 失败：HTTP 400 + code 400（规范 6.4）
    options.InvalidModelStateResponseFactory = _ => new ObjectResult(new ApiResult<object?>
    {
        Code = StatusCodes.Status400BadRequest,
        Message = "参数缺失或格式错误"
    })
    {
        StatusCode = StatusCodes.Status400BadRequest
    };
});

builder.Services.AddScoped<ValidationFilter>();
builder.Services.AddRouting(options => options.LowercaseUrls = true);

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddMvc();

// ---------- 认证与授权（JWT；公开接口显式 [AllowAnonymous]）----------
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var signingKey = string.IsNullOrWhiteSpace(jwtOptions.SigningKey)
    ? new string('0', 32)
    : jwtOptions.SigningKey;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // 关闭入站声明映射：令牌内即 sub / name，读取侧按 ClaimNames 常量取值
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// ---------- 限流（登录 / 注册按 IP，抽奖按用户；规范 8.6）----------
var rateLimitOptions = builder.Configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitPolicies.Register, context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: $"auth-register:{ResolvePartitionKey(context)}",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimitOptions.RegisterPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    options.AddPolicy(RateLimitPolicies.Login, context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: $"auth-login:{ResolvePartitionKey(context)}",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimitOptions.LoginPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    options.AddPolicy(RateLimitPolicies.Draw, context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ResolveDrawPartitionKey(context),
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimitOptions.DrawPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ApiResult<object?>
            {
                Code = ErrorCodes.SystemBusy,
                Message = "系统繁忙，请稍后重试"
            },
            cancellationToken);
    };
});

// ---------- CORS 白名单（禁止 AllowAnyOrigin；refresh 走 Cookie 需 AllowCredentials）----------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

// ---------- 反向代理下的真实客户端 IP 还原（B3：必须排在限流之前）----------
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // 清空可信代理必须用 Clear()：集合初始化器的语义是「不添加任何元素」，不是清空
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddOpenApi();

var app = builder.Build();

// 中间件顺序：还原真实 IP → 安全响应头 → 请求日志 → 路由 → CORS → 认证 → 授权 → 限流
app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none'";
    await next();
});

app.UseSerilogRequestLogging();
app.UseRouting();
app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

/// <summary>按客户端 IP 生成限流分区键（登录接口另叠加服务层的「按用户名失败锁定」）。</summary>
static string ResolvePartitionKey(HttpContext context) =>
    context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

/// <summary>
/// 抽奖限流分区键（REV-03）：按**已认证用户**分区（`draw:user:{userId}`），
/// 避免多用户共享出口 IP（NAT / 办公网 / 反代）互相挤占配额；
/// 未认证（无 `sub` 声明）时回落 IP 分区，保持「限流不可绕过」语义。
/// 限流中间件排在认证中间件之后执行，此处 claims 已就绪（见下方中间件顺序注释）。
/// </summary>
static string ResolveDrawPartitionKey(HttpContext context) =>
    context.User.FindFirst(ClaimNames.UserId)?.Value is { Length: > 0 } userId
        ? $"draw:user:{userId}"
        : $"draw:ip:{ResolvePartitionKey(context)}";

/// <summary>集成测试（WebApplicationFactory）入口点。</summary>
public partial class Program
{
}
