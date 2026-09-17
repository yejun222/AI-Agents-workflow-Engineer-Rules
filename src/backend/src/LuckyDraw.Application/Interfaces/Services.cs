using LuckyDraw.Application.Dtos;

namespace LuckyDraw.Application.Interfaces;

/// <summary>密码哈希服务（BCrypt）。</summary>
public interface IPasswordHasher
{
    /// <summary>生成密码哈希。</summary>
    string Hash(string password);

    /// <summary>校验密码与哈希是否匹配。</summary>
    bool Verify(string password, string passwordHash);

    /// <summary>
    /// 登录路径专用的等价成本校验（REV-05）：`passwordHash` 为空（用户不存在 / 历史脏数据）时，
    /// 对实现内置的假哈希执行一次**同工作因子**校验并返回 false，使「用户不存在」与「密码错误」
    /// 两条失败路径耗时一致，消除账号枚举时序侧信道；返回值恒为 false，调用方无需区分两种情况。
    /// </summary>
    /// <param name="password">待校验的明文密码。</param>
    /// <param name="passwordHash">用户密码哈希；为空表示用户不存在。</param>
    bool VerifyOrDummy(string password, string? passwordHash);
}

/// <summary>令牌服务（JWT 双 token，D-11）。</summary>
public interface ITokenService
{
    /// <summary>签发 access token（2h）。</summary>
    /// <param name="userId">用户 Id。</param>
    /// <param name="userName">用户名。</param>
    (string AccessToken, int ExpiresInSeconds) CreateAccessToken(int userId, string userName);

    /// <summary>生成不透明 refresh token（内含 userId 与 jti，仅哈希落 Redis）。</summary>
    /// <param name="userId">用户 Id。</param>
    /// <returns>refresh token 明文与 jti。</returns>
    (string RefreshToken, string Jti) CreateRefreshToken(int userId);

    /// <summary>解析 refresh token，取出 userId 与 jti；格式非法返回 null。</summary>
    (int UserId, string Jti)? TryParseRefreshToken(string refreshToken);

    /// <summary>计算 refresh token 的 SHA-256 哈希（Redis 只存哈希，禁止明文缓存）。</summary>
    string HashRefreshToken(string refreshToken);
}

/// <summary>审计入口（MOD-06）：显式记录业务动作（注册 / 登录 / 登出 / 复用检测 / 抽奖）。</summary>
public interface IAuditService
{
    /// <summary>写入一条审计日志；在抽奖链路中与业务事务同生共死（D-08 / A2）。</summary>
    Task LogAsync(AuditEntry entry, CancellationToken cancellationToken);
}

/// <summary>审计条目（IP / UserAgent 由实现从 HttpContext 补齐）。</summary>
/// <param name="OperatorId">操作人 Id。</param>
/// <param name="Module">模块：auth / draw。</param>
/// <param name="OperationType">操作类型：Register / Login / LoginFail / Logout / RefreshReuse / Draw。</param>
/// <param name="TargetObject">目标对象（如 DrawRequest.Id）。</param>
/// <param name="AfterJson">变更后 JSON（已脱敏，禁止含密码 / token）。</param>
/// <param name="BeforeJson">变更前 JSON（已脱敏）。</param>
public record AuditEntry(
    int? OperatorId,
    string Module,
    string OperationType,
    string? TargetObject = null,
    string? AfterJson = null,
    string? BeforeJson = null);

/// <summary>
/// 幂等结果缓存（MOD-05 的加速层）：Redis 不可用时全部降级为数据库唯一索引路径（§6.1 降级矩阵）。
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>读取首次结果；未命中或缓存不可用返回 null。</summary>
    Task<DrawResponseDto?> TryGetAsync(int userId, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>写入首次结果（best effort，失败仅告警不回滚）。</summary>
    Task SetAsync(int userId, string idempotencyKey, DrawResponseDto result, CancellationToken cancellationToken);
}

/// <summary>
/// 认证幂等条目（D-15-4）：**只存 `{ userId, requestHash, completed }`**——
/// 禁止缓存任何凭证（access / refresh token）与密码相关材料；重放时由服务端**重新签发**凭证。
/// </summary>
/// <param name="UserId">首次执行成功后的用户 Id（重放时据此重新签发凭证）。</param>
/// <param name="RequestHash">请求体指纹（HMAC-SHA256，密钥在服务端配置内，不落缓存）。</param>
/// <param name="Completed">false = 在途占位（PENDING）；true = 已落定的成功结果。</param>
public record AuthIdempotencyEntry(int UserId, string RequestHash, bool Completed);

/// <summary>
/// 认证幂等存储（D-15：注册 / 登录的 Redis 结果重放 + `SET NX` 占位，**不新增表**）。
/// 与抽奖的 <see cref="IIdempotencyStore"/> 同属 MOD-05 抽象族、共用同一 Redis 缓存与
/// `draw:idempotency:` 前缀；差异在于认证没有等价的数据库唯一索引可做兜底，
/// 故需要 `SET NX` 占位与短轮询原语（抽奖接口无此需求，故为并列抽象而非同签名泛化）。
/// Redis 不可用时全部按「无幂等」处理（本请求直接执行），与 §6.1 降级矩阵一致。
/// </summary>
public interface IAuthIdempotencyStore
{
    /// <summary>
    /// 尝试占用幂等键（`SET NX` + TTL）：true = 本请求是执行者（须负责写入结果或释放占位）；
    /// false = 已有在途占位或已落定结果（调用方转短轮询）。
    /// </summary>
    /// <param name="operation">操作名（register / login）。</param>
    /// <param name="idempotencyKey">前端幂等键（UUID）。</param>
    /// <param name="requestHash">请求体指纹，写入占位以便并发同键不同体时立即判 409。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<bool> TryAcquireAsync(string operation, string idempotencyKey, string requestHash, CancellationToken cancellationToken);

    /// <summary>读取条目（含在途占位）；键不存在或缓存不可用返回 null。</summary>
    /// <param name="operation">操作名（register / login）。</param>
    /// <param name="idempotencyKey">前端幂等键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<AuthIdempotencyEntry?> TryGetAsync(string operation, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>写入已落定的成功结果（覆盖占位；仅成功路径调用，业务拒绝不缓存，D-15-5）。</summary>
    /// <param name="operation">操作名（register / login）。</param>
    /// <param name="idempotencyKey">前端幂等键。</param>
    /// <param name="entry">已落定条目（不含凭证）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task CompleteAsync(string operation, string idempotencyKey, AuthIdempotencyEntry entry, CancellationToken cancellationToken);

    /// <summary>释放占位（业务失败路径）：不缓存失败结果，使同键重试可重新执行。</summary>
    /// <param name="operation">操作名（register / login）。</param>
    /// <param name="idempotencyKey">前端幂等键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ReleaseAsync(string operation, string idempotencyKey, CancellationToken cancellationToken);
}

/// <summary>refresh token 会话存储（D-11：单会话族 + 哈希存储）。</summary>
public interface IRefreshTokenStore
{
    /// <summary>保存 / 覆盖该用户的会话（轮换即覆盖）。</summary>
    Task SaveAsync(int userId, string jti, string tokenHash, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>读取会话；不存在（过期 / 已登出）返回 null。</summary>
    Task<RefreshSession?> GetAsync(int userId, CancellationToken cancellationToken);

    /// <summary>删除会话（登出 / 复用检测触发全量失效）。</summary>
    Task RemoveAsync(int userId, CancellationToken cancellationToken);
}

/// <summary>refresh 会话内容。</summary>
/// <param name="Jti">令牌唯一标识。</param>
/// <param name="TokenHash">令牌 SHA-256 哈希。</param>
/// <param name="ExpiresAt">过期时刻。</param>
public record RefreshSession(string Jti, string TokenHash, DateTimeOffset ExpiresAt);

/// <summary>登录失败计数（规范 4.3：连续 5 次失败锁定 15 分钟；对不存在的用户名同样计数防枚举）。</summary>
public interface ILoginAttemptStore
{
    /// <summary>失败计数 +1，返回累加后的次数。</summary>
    Task<int> IncrementAsync(string userNameLower, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>读取当前失败次数。</summary>
    Task<int> GetAsync(string userNameLower, CancellationToken cancellationToken);

    /// <summary>清零失败计数（登录成功）。</summary>
    Task ResetAsync(string userNameLower, CancellationToken cancellationToken);
}

/// <summary>随机源（D-04 / D-05）：生产实现为加密级随机，测试可注入确定性实现。</summary>
public interface IRandomSource
{
    /// <summary>返回 [0, exclusiveMax) 区间的均匀随机整数。</summary>
    int NextInt(int exclusiveMax);
}

/// <summary>事务宿主（D-08：事务在业务层 DrawService，隔离级别 READ COMMITTED）。</summary>
public interface ITransactionManager
{
    /// <summary>开启 READ COMMITTED 事务作用域；未提交即释放时回滚。</summary>
    Task<ITransactionScope> BeginAsync(CancellationToken cancellationToken);
}

/// <summary>事务作用域。</summary>
public interface ITransactionScope : IAsyncDisposable
{
    /// <summary>提交事务。</summary>
    Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>回滚事务（幂等，可重复调用）。</summary>
    Task RollbackAsync(CancellationToken cancellationToken);
}
