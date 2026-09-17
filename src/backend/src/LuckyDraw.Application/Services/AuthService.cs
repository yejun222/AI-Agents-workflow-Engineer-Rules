using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LuckyDraw.Application.Common;
using LuckyDraw.Application.Dtos;
using LuckyDraw.Application.Interfaces;
using LuckyDraw.Application.Options;
using LuckyDraw.Domain.Entities;
using LuckyDraw.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LuckyDraw.Application.Services;

/// <summary>
/// 认证与会话服务（MOD-01）：注册 / 登录 / 刷新 / 登出。
/// 登录失败一律以业务错误码表达（D-12：401 仅表示 access token 缺失或失效，不得用于登录失败）。
/// </summary>
public class AuthService : IAuthService
{
    /// <summary>触发锁定的连续失败次数（规范 4.3）。</summary>
    private const int MaxLoginFailures = 5;

    /// <summary>锁定与失败计数的有效期。</summary>
    private static readonly TimeSpan LoginFailureTtl = TimeSpan.FromMinutes(15);

    /// <summary>在途占位的最长等待（D-15-3：≤ 2s，超时仍未落定 → 409）。</summary>
    private static readonly TimeSpan PendingWaitTimeout = TimeSpan.FromSeconds(2);

    /// <summary>短轮询间隔（D-15-3）。</summary>
    private static readonly TimeSpan PendingPollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>注册操作名（D-15 作用域 `(operation, key)` 的 operation 取值）。</summary>
    private const string RegisterOperation = "register";

    /// <summary>登录操作名。</summary>
    private const string LoginOperation = "login";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly ILoginAttemptStore _loginAttemptStore;
    private readonly IAuthIdempotencyStore _authIdempotencyStore;
    private readonly IAuditService _auditService;
    private readonly IOptions<JwtOptions> _jwtOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthService> _logger;

    /// <summary>构造函数注入。</summary>
    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IRefreshTokenStore refreshTokenStore,
        ILoginAttemptStore loginAttemptStore,
        IAuthIdempotencyStore authIdempotencyStore,
        IAuditService auditService,
        IOptions<JwtOptions> jwtOptions,
        TimeProvider timeProvider,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _refreshTokenStore = refreshTokenStore;
        _loginAttemptStore = loginAttemptStore;
        _authIdempotencyStore = authIdempotencyStore;
        _auditService = auditService;
        _jwtOptions = jwtOptions;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var userName = request.UserName.Trim();

        // 键缺失 = 无幂等，正常处理（D-15-1，不报错）
        return await ExecuteIdempotentlyAsync(
            RegisterOperation,
            idempotencyKey,
            ComputeRequestFingerprint(RegisterOperation, userName, request.Password),
            async token => await RegisterCoreAsync(request, userName, token),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var userName = request.UserName.Trim();

        return await ExecuteIdempotentlyAsync(
            LoginOperation,
            idempotencyKey,
            ComputeRequestFingerprint(LoginOperation, userName, request.Password),
            async token => await LoginCoreAsync(request, userName, token),
            cancellationToken);
    }

    /// <summary>注册的业务主体（不含幂等编排）：唯一性校验 → BCrypt → 落库 → 审计 → 注册即登录。</summary>
    private async Task<AuthResponse> RegisterCoreAsync(RegisterRequest request, string userName, CancellationToken cancellationToken)
    {
        // 唯一性校验必须 IgnoreQueryFilters()（仓储实现内保证）：软删除不释放唯一性
        if (await _userRepository.ExistsByUserNameAsync(userName, cancellationToken))
        {
            throw new BusinessException(ErrorCodes.UserNameTaken, "用户名已被占用");
        }

        var user = new User
        {
            UserName = userName,
            PasswordHash = _passwordHasher.Hash(request.Password)
        };

        // 并发注册同一用户名由数据库唯一索引兜底，仓储层翻译为 1101
        user = await _userRepository.AddAsync(user, cancellationToken);

        await _auditService.LogAsync(
            new AuditEntry(
                user.Id,
                "auth",
                "Register",
                user.Id.ToString(),
                JsonSerializer.Serialize(new { id = user.Id, userName = user.UserName })),
            cancellationToken);

        _logger.LogInformation("用户注册成功：UserId={UserId}", user.Id);
        return await IssueSessionAsync(user, cancellationToken);
    }

    /// <summary>登录的业务主体（不含幂等编排）：锁定判据 → 密码校验 → 失败计数 / 审计 → 建会话。</summary>
    private async Task<AuthResponse> LoginCoreAsync(LoginRequest request, string userName, CancellationToken cancellationToken)
    {
        var attemptKey = userName.ToLowerInvariant();

        var failures = await _loginAttemptStore.GetAsync(attemptKey, cancellationToken);
        if (failures >= MaxLoginFailures)
        {
            throw new BusinessException(ErrorCodes.AccountLocked, "账号已锁定，请 15 分钟后再试");
        }

        var user = await _userRepository.GetByUserNameAsync(userName, cancellationToken);

        // REV-05：用户不存在时不得短路 BCrypt 校验——对固定假哈希执行一次等价成本校验，
        // 使「用户不存在」与「密码错误」耗时一致，消除账号枚举时序侧信道（返回 false，语义不变）
        var passwordMatched = _passwordHasher.VerifyOrDummy(request.Password, user?.PasswordHash);

        // `user is null` 与 `!passwordMatched` 等价（假哈希恒不匹配），显式写出避免可空抑制
        if (user is null || !passwordMatched)
        {
            // 对不存在的用户名同样计数：否则锁定行为本身会成为账号枚举信道
            var currentFailures = await _loginAttemptStore.IncrementAsync(attemptKey, LoginFailureTtl, cancellationToken);
            if (currentFailures >= MaxLoginFailures)
            {
                await _auditService.LogAsync(
                    new AuditEntry(user?.Id, "auth", "LoginFail", attemptKey),
                    cancellationToken);
                _logger.LogWarning("账号触发登录锁定：UserId={UserId}（已连续失败 {Count} 次）", user?.Id, currentFailures);
            }

            // 统一文案，不区分「用户不存在」与「密码错误」（AC-04 防枚举）
            throw new BusinessException(ErrorCodes.InvalidCredentials, "用户名或密码错误");
        }

        await _loginAttemptStore.ResetAsync(attemptKey, cancellationToken);
        await _auditService.LogAsync(
            new AuditEntry(user.Id, "auth", "Login", user.Id.ToString()),
            cancellationToken);

        return await IssueSessionAsync(user, cancellationToken);
    }

    /// <summary>
    /// 认证幂等编排（D-15）：键缺失 = 直接执行；否则 `SET NX` 抢占执行权 → 执行 → 成功才写结果。
    /// 未抢到 = 已有在途 / 已落定：短轮询（≤2s）取结果重放（重新签发凭证、不重复业务副作用）；
    /// 请求体指纹不一致或超时未落定 → `409`「请勿重复提交」；业务拒绝不缓存（释放占位）。
    /// </summary>
    /// <param name="operation">操作名（register / login）。</param>
    /// <param name="idempotencyKey">幂等键（缺失表示无幂等）。</param>
    /// <param name="requestHash">请求体指纹。</param>
    /// <param name="execute">业务主体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task<AuthResponse> ExecuteIdempotentlyAsync(
        string operation,
        string? idempotencyKey,
        string requestHash,
        Func<CancellationToken, Task<AuthResponse>> execute,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey is null)
        {
            return await execute(cancellationToken);
        }

        var deadline = _timeProvider.GetUtcNow() + PendingWaitTimeout;

        while (true)
        {
            if (await _authIdempotencyStore.TryAcquireAsync(operation, idempotencyKey, requestHash, cancellationToken))
            {
                return await ExecuteAndCompleteAsync(operation, idempotencyKey, requestHash, execute, cancellationToken);
            }

            var entry = await _authIdempotencyStore.TryGetAsync(operation, idempotencyKey, cancellationToken);
            if (entry is not null)
            {
                EnsureSameRequest(entry, requestHash);

                if (entry.Completed)
                {
                    // 重放：只重新签发凭证，不重复审计 / 不重复建用户 / 不重复累计失败计数（D-15-4）
                    return await ReissueAsync(entry.UserId, cancellationToken);
                }
            }

            if (_timeProvider.GetUtcNow() >= deadline)
            {
                // 在途占位在等待窗口内始终未落定（D-15-3）
                // HTTP 状态码须为 409（§5.1：`409` 幂等冲突是与「业务异常一律 200」并列分立的 HTTP 状态码），与抽奖侧同写法
                throw new BusinessException(ErrorCodes.IdempotencyConflict, "请勿重复提交", ErrorCodes.IdempotencyConflict);
            }

            await Task.Delay(PendingPollInterval, cancellationToken);
        }
    }

    /// <summary>执行者路径：执行业务主体，成功写结果、失败释放占位（仅缓存成功，D-15-5）。</summary>
    private async Task<AuthResponse> ExecuteAndCompleteAsync(
        string operation,
        string idempotencyKey,
        string requestHash,
        Func<CancellationToken, Task<AuthResponse>> execute,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await execute(cancellationToken);
            await _authIdempotencyStore.CompleteAsync(
                operation,
                idempotencyKey,
                new AuthIdempotencyEntry(response.User.Id, requestHash, Completed: true),
                cancellationToken);
            return response;
        }
        catch (Exception)
        {
            await _authIdempotencyStore.ReleaseAsync(operation, idempotencyKey, cancellationToken);
            throw;
        }
    }

    /// <summary>同键不同请求体：立即 409，不泄露首次请求的任何信息（D-15-1）。</summary>
    private static void EnsureSameRequest(AuthIdempotencyEntry entry, string requestHash)
    {
        if (!string.Equals(entry.RequestHash, requestHash, StringComparison.Ordinal))
        {
            // 同键异体：HTTP 409（§5.1 幂等冲突分工），与抽奖侧同写法
            throw new BusinessException(ErrorCodes.IdempotencyConflict, "请勿重复提交", ErrorCodes.IdempotencyConflict);
        }
    }

    /// <summary>重放路径：按首次执行的用户重新签发凭证（凭证可再生，不算业务副作用）。</summary>
    private async Task<AuthResponse> ReissueAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.SessionInvalid, "登录状态已失效，请重新登录");

        _logger.LogInformation("认证幂等重放：UserId={UserId}", userId);
        return await IssueSessionAsync(user, cancellationToken);
    }

    /// <summary>
    /// 请求体指纹（D-15-1）：`HMAC-SHA256(服务端密钥, operation + 用户名 + 密码)`。
    /// 用 HMAC 而非裸哈希的原因：登录请求体含密码，裸 SHA-256 落进缓存会成为可离线爆破的密码校验子；
    /// 密钥只在服务端配置中，缓存内容不构成凭证泄露面（D-15-4）。
    /// </summary>
    private string ComputeRequestFingerprint(string operation, string userName, string password)
    {
        // 与 Program.cs 的签名密钥回退口径一致：Development 允许留空（REV-10 范围，不在本轮改动）
        var secret = string.IsNullOrWhiteSpace(_jwtOptions.Value.SigningKey)
            ? new string('0', 32)
            : _jwtOptions.Value.SigningKey;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var payload = $"{operation}\n{userName.ToLowerInvariant()}\n{password}";
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    /// <inheritdoc />
    public async Task<RefreshResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var parsed = _tokenService.TryParseRefreshToken(refreshToken)
            ?? throw new BusinessException(ErrorCodes.SessionExpired, "登录状态已过期，请重新登录");

        var session = await _refreshTokenStore.GetAsync(parsed.UserId, cancellationToken);
        if (session is null)
        {
            throw new BusinessException(ErrorCodes.SessionExpired, "登录状态已过期，请重新登录");
        }

        if (!TokenHashEquals(_tokenService.HashRefreshToken(refreshToken), session.TokenHash))
        {
            // 复用检测：该用户全部会话失效 + 审计告警（D-11）
            await _refreshTokenStore.RemoveAsync(parsed.UserId, cancellationToken);
            await _auditService.LogAsync(
                new AuditEntry(parsed.UserId, "auth", "RefreshReuse", parsed.UserId.ToString()),
                cancellationToken);
            _logger.LogWarning("检测到 refresh token 复用：UserId={UserId}", parsed.UserId);
            throw new BusinessException(ErrorCodes.SessionInvalid, "登录状态已失效，请重新登录");
        }

        var user = await _userRepository.GetByIdAsync(parsed.UserId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.SessionInvalid, "登录状态已失效，请重新登录");

        // 一次性轮换：新 token 覆盖会话，旧 token 立即失效
        var (newRefreshToken, jti) = _tokenService.CreateRefreshToken(user.Id);
        await _refreshTokenStore.SaveAsync(
            user.Id,
            jti,
            _tokenService.HashRefreshToken(newRefreshToken),
            TimeSpan.FromDays(_jwtOptions.Value.RefreshTokenDays),
            cancellationToken);

        var (accessToken, expiresIn) = _tokenService.CreateAccessToken(user.Id, user.UserName);
        return new RefreshResponse
        {
            AccessToken = accessToken,
            ExpiresIn = expiresIn,
            // 兼容性新增字段：前端静默恢复会话依赖该字段补全内存态（§2.8 / API-03）
            User = new UserDto { Id = user.Id, UserName = user.UserName },
            RefreshToken = newRefreshToken
        };
    }

    /// <inheritdoc />
    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        // 幂等：凭证无效或缺失也返回成功，避免「已登出但前端卡死」（API-04）
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var parsed = _tokenService.TryParseRefreshToken(refreshToken);
        if (parsed is not { } session)
        {
            return;
        }

        await _refreshTokenStore.RemoveAsync(session.UserId, cancellationToken);
        await _auditService.LogAsync(
            new AuditEntry(session.UserId, "auth", "Logout", session.UserId.ToString()),
            cancellationToken);
    }

    /// <summary>签发 access token 并建立 / 覆盖 refresh 会话。</summary>
    private async Task<AuthResponse> IssueSessionAsync(User user, CancellationToken cancellationToken)
    {
        var (refreshToken, jti) = _tokenService.CreateRefreshToken(user.Id);
        await _refreshTokenStore.SaveAsync(
            user.Id,
            jti,
            _tokenService.HashRefreshToken(refreshToken),
            TimeSpan.FromDays(_jwtOptions.Value.RefreshTokenDays),
            cancellationToken);

        var (accessToken, expiresIn) = _tokenService.CreateAccessToken(user.Id, user.UserName);
        return new AuthResponse
        {
            AccessToken = accessToken,
            ExpiresIn = expiresIn,
            User = new UserDto { Id = user.Id, UserName = user.UserName },
            RefreshToken = refreshToken
        };
    }

    /// <summary>令牌哈希的固定时间比较，避免时序侧信道。</summary>
    private static bool TokenHashEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left),
            Encoding.UTF8.GetBytes(right));
}
