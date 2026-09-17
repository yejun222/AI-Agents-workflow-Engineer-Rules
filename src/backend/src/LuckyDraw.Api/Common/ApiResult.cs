namespace LuckyDraw.Api.Common;

/// <summary>
/// 统一响应标记接口（规范 6.3）：供 <c>ResultFilter</c> 判断「是否已包装」，
/// 避免错误路径（校验失败 / 业务异常）的 ApiResult 被二次包裹成 <c>data</c>。
/// </summary>
public interface IApiResult
{
}

/// <summary>
/// 统一响应结构（规范 6.3）：<c>code=0</c> 成功；<c>code ≥ 1000</c> 业务异常（HTTP 200）。
/// 由全局 <c>ResultFilter</c> 统一包装，控制器禁止手动构造（规范 4.2）。
/// </summary>
/// <typeparam name="T">业务数据类型。</typeparam>
public class ApiResult<T> : IApiResult
{
    /// <summary>业务码：0=成功，其他见 docs/error-codes.md 与规范 6.4。</summary>
    public int Code { get; set; }

    /// <summary>提示消息（成功时为空串）。</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>业务数据。</summary>
    public T? Data { get; set; }
}

/// <summary>非泛型统一响应（错误路径使用）。</summary>
public class ApiResult : ApiResult<object?>
{
}
