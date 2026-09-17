using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using LuckyDraw.Application.Dtos;
using LuckyDraw.Application.Interfaces;
using LuckyDraw.Application.Options;
using LuckyDraw.Application.Services;
using LuckyDraw.Domain.Entities;
using LuckyDraw.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LuckyDraw.UnitTests.Services;

/// <summary>
/// 认证服务业务分支：登录失败路径的账号枚举防护（REV-05 / AC-04）与
/// 注册 / 登录幂等（D-15 / REV-06：作用域、409、重放、仅缓存成功、不重复副作用）。
/// </summary>
public class AuthServiceTests
{
    private const int UserId = 7;
    private const string UserName = "alice_01";
    private const string Password = "Passw0rd";
    private const string PasswordHash = "$2a$11$not-a-real-hash-in-test-double";
    private const string SigningKey = "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk";
    private const string IdempotencyKey = "3f2a1b4c-5d6e-4f70-8a9b-0c1d2e3f4a5b";

    /// <summary>用户不存在：仍执行一次等价成本校验（第二参数为 null 表示用户不存在），失败码与文案不变。</summary>
    [Fact]
    public async Task LoginAsync_WhenUserMissing_StillPerformsEquivalentCostVerify()
    {
        var harness = new Harness();
        harness.Users.GetByUserNameAsync(UserName, Arg.Any<CancellationToken>()).Returns((User?)null);
        harness.Hasher.VerifyOrDummy(Password, null).Returns(false);

        var act = async () => await harness.Service.LoginAsync(LoginRequest(), null, CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<BusinessException>()).Which;
        exception.Code.Should().Be(1102);
        exception.Message.Should().Be("用户名或密码错误");

        // 关键断言：不论用户是否存在都恰好调用一次校验（时序侧信道消除的落点）
        harness.Hasher.Received(1).VerifyOrDummy(Password, null);
        harness.Hasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        await harness.Attempts.Received(1).IncrementAsync(UserName, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    /// <summary>用户存在但密码错误：走真实哈希校验，失败码 / 文案与「用户不存在」逐字一致。</summary>
    [Fact]
    public async Task LoginAsync_WhenPasswordWrong_UsesRealHashAndSameFailureSurface()
    {
        var harness = new Harness();
        harness.Users.GetByUserNameAsync(UserName, Arg.Any<CancellationToken>())
            .Returns(new User { Id = UserId, UserName = UserName, PasswordHash = PasswordHash });
        harness.Hasher.VerifyOrDummy(Password, PasswordHash).Returns(false);

        var act = async () => await harness.Service.LoginAsync(LoginRequest(), null, CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<BusinessException>()).Which;
        exception.Code.Should().Be(1102);
        exception.Message.Should().Be("用户名或密码错误");

        harness.Hasher.Received(1).VerifyOrDummy(Password, PasswordHash);
        await harness.Attempts.Received(1).IncrementAsync(UserName, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    /// <summary>账号已锁定：直接 1103，不再进入密码校验（既有语义不被本次改动影响）。</summary>
    [Fact]
    public async Task LoginAsync_WhenLocked_Throws1103WithoutPasswordCheck()
    {
        var harness = new Harness();
        harness.Attempts.GetAsync(UserName, Arg.Any<CancellationToken>()).Returns(5);

        var act = async () => await harness.Service.LoginAsync(LoginRequest(), null, CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<BusinessException>()).Which;
        exception.Code.Should().Be(1103);
        harness.Hasher.DidNotReceive().VerifyOrDummy(Arg.Any<string>(), Arg.Any<string?>());
    }

    /// <summary>登录成功：清零失败计数、写审计、签发 access token（成功路径未被 REV-05 改动波及）。</summary>
    [Fact]
    public async Task LoginAsync_WhenCredentialsValid_IssuesSession()
    {
        var harness = new Harness();
        harness.Users.GetByUserNameAsync(UserName, Arg.Any<CancellationToken>())
            .Returns(new User { Id = UserId, UserName = UserName, PasswordHash = PasswordHash });
        harness.Hasher.VerifyOrDummy(Password, PasswordHash).Returns(true);

        var response = await harness.Service.LoginAsync(LoginRequest(), null, CancellationToken.None);

        response.AccessToken.Should().Be("access-token");
        response.User.Id.Should().Be(UserId);
        await harness.Attempts.Received(1).ResetAsync(UserName, Arg.Any<CancellationToken>());
        await harness.Audit.Received(1).LogAsync(
            Arg.Is<AuditEntry>(entry => entry.OperationType == "Login"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>D-15-1：键缺失 = 无幂等，正常处理且完全不触碰幂等存储。</summary>
    [Fact]
    public async Task LoginAsync_WithoutIdempotencyKey_SkipsIdempotencyStore()
    {
        var harness = new Harness();
        harness.Users.GetByUserNameAsync(UserName, Arg.Any<CancellationToken>())
            .Returns(new User { Id = UserId, UserName = UserName, PasswordHash = PasswordHash });
        harness.Hasher.VerifyOrDummy(Password, PasswordHash).Returns(true);

        var response = await harness.Service.LoginAsync(LoginRequest(), null, CancellationToken.None);

        response.AccessToken.Should().Be("access-token");
        await harness.AuthIdempotency.DidNotReceive().TryAcquireAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>D-15-4：执行者成功后写入 `{ userId, requestHash, completed = true }`，**不含任何凭证**。</summary>
    [Fact]
    public async Task LoginAsync_OnSuccess_WritesResultWithoutCredentials()
    {
        var harness = new Harness();
        harness.Hasher.VerifyOrDummy(Password, PasswordHash).Returns(true);
        harness.Users.GetByUserNameAsync(UserName, Arg.Any<CancellationToken>())
            .Returns(new User { Id = UserId, UserName = UserName, PasswordHash = PasswordHash });
        harness.AuthIdempotency.TryAcquireAsync("login", IdempotencyKey, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await harness.Service.LoginAsync(LoginRequest(), IdempotencyKey, CancellationToken.None);

        await harness.AuthIdempotency.Received(1).CompleteAsync(
            "login",
            IdempotencyKey,
            Arg.Is<AuthIdempotencyEntry>(entry => entry.UserId == UserId && entry.Completed),
            Arg.Any<CancellationToken>());
        await harness.AuthIdempotency.DidNotReceive().ReleaseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>D-15-5：业务拒绝（1102）不缓存 —— 释放占位、不写结果，且异常原样上抛。</summary>
    [Fact]
    public async Task LoginAsync_OnBusinessFailure_ReleasesPlaceholderWithoutCaching()
    {
        var harness = new Harness();
        harness.Users.GetByUserNameAsync(UserName, Arg.Any<CancellationToken>())
            .Returns(new User { Id = UserId, UserName = UserName, PasswordHash = PasswordHash });
        harness.Hasher.VerifyOrDummy(Password, PasswordHash).Returns(false);
        harness.AuthIdempotency.TryAcquireAsync("login", IdempotencyKey, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var act = async () => await harness.Service.LoginAsync(LoginRequest(), IdempotencyKey, CancellationToken.None);

        (await act.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be(1102);
        await harness.AuthIdempotency.Received(1).ReleaseAsync("login", IdempotencyKey, Arg.Any<CancellationToken>());
        await harness.AuthIdempotency.DidNotReceive().CompleteAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<AuthIdempotencyEntry>(), Arg.Any<CancellationToken>());
    }

    /// <summary>D-15-4：同键重放只重新签发凭证 —— 不重查密码、不重复建用户 / 不重复审计。</summary>
    [Fact]
    public async Task LoginAsync_OnReplay_ReissuesCredentialsWithoutSideEffects()
    {
        var harness = new Harness();
        harness.AuthIdempotency.TryAcquireAsync("login", IdempotencyKey, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        harness.AuthIdempotency.TryGetAsync("login", IdempotencyKey, Arg.Any<CancellationToken>())
            .Returns(new AuthIdempotencyEntry(UserId, Fingerprint("login", UserName, Password), Completed: true));
        harness.Users.GetByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = UserId, UserName = UserName, PasswordHash = PasswordHash });

        var response = await harness.Service.LoginAsync(LoginRequest(), IdempotencyKey, CancellationToken.None);

        response.AccessToken.Should().Be("access-token");
        response.User.Id.Should().Be(UserId);
        harness.Hasher.DidNotReceive().VerifyOrDummy(Arg.Any<string>(), Arg.Any<string?>());
        await harness.Users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await harness.Audit.DidNotReceive().LogAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
        await harness.Attempts.DidNotReceive().IncrementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// D-15-1：同键但请求体不一致（含密码不同）→ 409，不重放、不泄露首次请求信息。
    /// BUG-01：HTTP 状态码须为 409（§5.1 幂等冲突分工），不得落 `BusinessException` 的默认值 200。
    /// </summary>
    [Fact]
    public async Task LoginAsync_OnSameKeyDifferentBody_ThrowsConflict()
    {
        var harness = new Harness();
        harness.AuthIdempotency.TryAcquireAsync("login", IdempotencyKey, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        harness.AuthIdempotency.TryGetAsync("login", IdempotencyKey, Arg.Any<CancellationToken>())
            .Returns(new AuthIdempotencyEntry(UserId, Fingerprint("login", UserName, "OtherPass1"), Completed: true));

        var act = async () => await harness.Service.LoginAsync(LoginRequest(), IdempotencyKey, CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<BusinessException>()).Which;
        exception.Code.Should().Be(409);
        exception.HttpStatusCode.Should().Be(409, "幂等冲突经 ExceptionFilter 映射为 HTTP 409，而非默认 200（BUG-01）");
        exception.Message.Should().Be("请勿重复提交");
        await harness.Users.DidNotReceive().GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// D-15-3：占位在 ≤2s 内始终未落定 → 409「请勿重复提交」（不等成死循环）。
    /// BUG-01：同键异体与在途超时两条抛点都必须带 HTTP 409，避免又一处落回默认 200。
    /// </summary>
    [Fact]
    public async Task LoginAsync_WhenPlaceholderNeverSettles_ThrowsConflictAfterTimeout()
    {
        var harness = new Harness(advancingClock: true);
        harness.AuthIdempotency.TryAcquireAsync("login", IdempotencyKey, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        harness.AuthIdempotency.TryGetAsync("login", IdempotencyKey, Arg.Any<CancellationToken>())
            .Returns((AuthIdempotencyEntry?)null);

        var act = async () => await harness.Service.LoginAsync(LoginRequest(), IdempotencyKey, CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<BusinessException>()).Which;
        exception.Code.Should().Be(409);
        exception.HttpStatusCode.Should().Be(409, "幂等冲突经 ExceptionFilter 映射为 HTTP 409，而非默认 200（BUG-01）");
    }

    /// <summary>注册同键重放：不重复建用户、不重复审计（D-15-4 的注册侧）。</summary>
    [Fact]
    public async Task RegisterAsync_OnReplay_DoesNotCreateUserTwice()
    {
        var harness = new Harness();
        harness.AuthIdempotency.TryAcquireAsync("register", IdempotencyKey, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        harness.AuthIdempotency.TryGetAsync("register", IdempotencyKey, Arg.Any<CancellationToken>())
            .Returns(new AuthIdempotencyEntry(UserId, Fingerprint("register", UserName, Password), Completed: true));
        harness.Users.GetByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = UserId, UserName = UserName, PasswordHash = PasswordHash });

        var response = await harness.Service.RegisterAsync(RegisterRequest(), IdempotencyKey, CancellationToken.None);

        response.AccessToken.Should().Be("access-token");
        await harness.Users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await harness.Audit.DidNotReceive().LogAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    /// <summary>与 `AuthService.ComputeRequestFingerprint` 同算法的测试侧指纹（D-15-1）。</summary>
    private static string Fingerprint(string operation, string userName, string password)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SigningKey));
        var payload = $"{operation}\n{userName.ToLowerInvariant()}\n{password}";
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static LoginRequest LoginRequest() => new() { UserName = UserName, Password = Password };

    private static RegisterRequest RegisterRequest() =>
        new() { UserName = UserName, Password = Password, ConfirmPassword = Password };

    /// <summary>场景夹具：全部依赖为替身，仅被测服务为真实实现。</summary>
    private sealed class Harness
    {
        /// <param name="advancingClock">true = 每次取时间前进 1 秒（用于快速触发在途占位超时）。</param>
        public Harness(bool advancingClock = false)
        {
            Users = Substitute.For<IUserRepository>();
            Hasher = Substitute.For<IPasswordHasher>();
            Tokens = Substitute.For<ITokenService>();
            Refresh = Substitute.For<IRefreshTokenStore>();
            Attempts = Substitute.For<ILoginAttemptStore>();
            AuthIdempotency = Substitute.For<IAuthIdempotencyStore>();
            Audit = Substitute.For<IAuditService>();

            var seconds = 0;
            TimeProvider = Substitute.For<TimeProvider>();
            TimeProvider.GetUtcNow().Returns(_ => new DateTimeOffset(2026, 9, 17, 3, 0, 0, TimeSpan.Zero)
                .AddSeconds(advancingClock ? seconds++ : 0));

            Tokens.CreateRefreshToken(Arg.Any<int>()).Returns(("refresh-token", "jti-1"));
            Tokens.HashRefreshToken(Arg.Any<string>()).Returns("token-hash");
            Tokens.CreateAccessToken(Arg.Any<int>(), Arg.Any<string>()).Returns(("access-token", 7200));

            Service = new AuthService(
                Users,
                Hasher,
                Tokens,
                Refresh,
                Attempts,
                AuthIdempotency,
                Audit,
                Options.Create(new JwtOptions { SigningKey = SigningKey }),
                TimeProvider,
                NullLogger<AuthService>.Instance);
        }

        public IUserRepository Users { get; }

        public IPasswordHasher Hasher { get; }

        public ITokenService Tokens { get; }

        public IRefreshTokenStore Refresh { get; }

        public ILoginAttemptStore Attempts { get; }

        public IAuthIdempotencyStore AuthIdempotency { get; }

        public IAuditService Audit { get; }

        public TimeProvider TimeProvider { get; }

        public AuthService Service { get; }
    }
}
