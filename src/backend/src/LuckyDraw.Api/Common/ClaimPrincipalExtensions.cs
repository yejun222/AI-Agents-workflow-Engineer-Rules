using System.Security.Claims;
using LuckyDraw.Application.Common;
using LuckyDraw.Domain.Exceptions;

namespace LuckyDraw.Api.Common;

/// <summary>当前登录用户解析（受保护接口由 [Authorize] 保证 claims 存在）。</summary>
public static class ClaimPrincipalExtensions
{
    /// <summary>取当前登录用户 Id；缺失或非法时抛会话失效业务异常。</summary>
    public static int GetCurrentUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(ClaimNames.UserId)?.Value;
        if (!int.TryParse(value, out var userId))
        {
            throw new BusinessException(ErrorCodes.SessionInvalid, "登录状态已失效，请重新登录");
        }

        return userId;
    }

    /// <summary>取当前登录用户名（仅供确定性测试配置匹配，不参与业务判定）。</summary>
    public static string GetCurrentUserName(this ClaimsPrincipal principal) =>
        principal.FindFirst(ClaimNames.UserName)?.Value ?? string.Empty;
}
