namespace LuckyDraw.Application.Common;

/// <summary>
/// JWT 声明名（与令牌签发侧约定一致；JwtBearer 侧关闭入站声明映射后按原名读取）。
/// </summary>
public static class ClaimNames
{
    /// <summary>用户 Id（标准 sub 声明）。</summary>
    public const string UserId = "sub";

    /// <summary>用户名。</summary>
    public const string UserName = "name";
}
