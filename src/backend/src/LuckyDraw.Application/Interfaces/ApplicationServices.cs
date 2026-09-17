using LuckyDraw.Application.Common;
using LuckyDraw.Application.Dtos;

namespace LuckyDraw.Application.Interfaces;

/// <summary>认证与会话服务（MOD-01）。</summary>
public interface IAuthService
{
    /// <summary>注册（唯一性校验 + BCrypt 哈希 + 注册即登录；`Idempotency-Key` 语义见 D-15）。</summary>
    /// <param name="request">注册请求。</param>
    /// <param name="idempotencyKey">幂等键（缺失 = 无幂等，正常处理）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? idempotencyKey, CancellationToken cancellationToken);

    /// <summary>登录（统一失败文案 + 连续 5 次失败锁定 15 分钟；`Idempotency-Key` 语义见 D-15）。</summary>
    /// <param name="request">登录请求。</param>
    /// <param name="idempotencyKey">幂等键（缺失 = 无幂等，正常处理）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<AuthResponse> LoginAsync(LoginRequest request, string? idempotencyKey, CancellationToken cancellationToken);

    /// <summary>刷新（一次性轮换 + 复用检测）。</summary>
    Task<RefreshResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>登出（删除 refresh 会话；对无效凭证幂等返回成功）。</summary>
    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);
}

/// <summary>奖池查询服务（MOD-02）。</summary>
public interface IPrizePoolService
{
    /// <summary>查询启用中的奖池条目（白名单投影，不含权重 / 库存）。</summary>
    Task<PrizePoolResponse> GetPoolAsync(CancellationToken cancellationToken);
}

/// <summary>抽奖服务（MOD-03，事务宿主）。</summary>
public interface IDrawService
{
    /// <summary>查询今日剩余次数与重置时刻。</summary>
    Task<DrawQuotaDto> GetQuotaAsync(int userId, CancellationToken cancellationToken);

    /// <summary>执行一次抽奖（幂等 + 扣次 + 加权随机 + 条件扣库存 + 记录 + 审计，同一事务）。</summary>
    /// <param name="userId">当前登录用户 Id。</param>
    /// <param name="userName">当前登录用户名（仅供确定性测试配置匹配）。</param>
    /// <param name="idempotencyKey">幂等键；缺省 = 无幂等，正常消耗次数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<DrawResponseDto> DrawAsync(int userId, string userName, string? idempotencyKey, CancellationToken cancellationToken);
}

/// <summary>中奖记录服务（MOD-04）。</summary>
public interface IWinningRecordService
{
    /// <summary>分页查询本人中奖记录（恒定当前用户，不接受 userId 参数）。</summary>
    Task<PageResult<WinningRecordDto>> GetMyRecordsAsync(int userId, PageQuery query, CancellationToken cancellationToken);
}
