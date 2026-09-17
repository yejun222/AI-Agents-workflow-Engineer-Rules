namespace LuckyDraw.Application.Common;

/// <summary>
/// 业务错误码常量：**全部取自 `docs/error-codes.md` 已登记号码**，禁止在此之外编造号码。
/// 新增错误码前必须先查该文件取号并登记，再回填本类。
/// </summary>
public static class ErrorCodes
{
    /// <summary>1001 通用业务：系统繁忙，请稍后重试（限流 / 依赖不可用的降级提示）。</summary>
    public const int SystemBusy = 1001;

    /// <summary>1002 通用业务：参数缺失或格式错误（FluentValidation 业务校验失败）。</summary>
    public const int ValidationFailed = 1002;

    /// <summary>1101 用户：用户名已被占用。</summary>
    public const int UserNameTaken = 1101;

    /// <summary>1102 用户：用户名或密码错误（统一文案，防账号枚举）。</summary>
    public const int InvalidCredentials = 1102;

    /// <summary>1103 用户：账号已锁定，请 15 分钟后再试。</summary>
    public const int AccountLocked = 1103;

    /// <summary>1203 权限：登录状态已失效，请重新登录（refresh 无效 / 被复用）。</summary>
    public const int SessionInvalid = 1203;

    /// <summary>1204 权限：登录状态已过期，请重新登录。</summary>
    public const int SessionExpired = 1204;

    /// <summary>1501 抽奖：今日抽奖次数已用完，明日 0 点重置。</summary>
    public const int QuotaExhausted = 1501;

    /// <summary>1502 抽奖：奖品已抽完，请稍后再来（候选集为空）。</summary>
    public const int NoCandidate = 1502;

    /// <summary>409 幂等冲突：请勿重复提交（与 HTTP 状态码对齐，规范 6.4）。</summary>
    public const int IdempotencyConflict = 409;
}
