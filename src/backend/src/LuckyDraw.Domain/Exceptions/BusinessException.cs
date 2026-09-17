namespace LuckyDraw.Domain.Exceptions;

/// <summary>
/// 业务异常：由全局 ExceptionFilter 统一转换为 <c>ApiResult</c>（code ≥ 1000，HTTP 200）。
/// 业务校验失败一律抛本异常，禁止在控制器 try-catch 后手动返回错误结果（规范 4.2）。
/// </summary>
public class BusinessException : Exception
{
    /// <summary>业务错误码（见 docs/error-codes.md，取值 ≥ 1000）。</summary>
    public int Code { get; }

    /// <summary>业务错误码对应的 HTTP 状态码；业务异常默认 200。</summary>
    public int HttpStatusCode { get; }

    /// <summary>构造业务异常。</summary>
    /// <param name="code">业务错误码（必须已登记于 docs/error-codes.md）。</param>
    /// <param name="message">用户可见文案。</param>
    /// <param name="httpStatusCode">HTTP 状态码，默认 200（业务异常统一走 200 + code）。</param>
    public BusinessException(int code, string message, int httpStatusCode = 200)
        : base(message)
    {
        Code = code;
        HttpStatusCode = httpStatusCode;
    }
}
