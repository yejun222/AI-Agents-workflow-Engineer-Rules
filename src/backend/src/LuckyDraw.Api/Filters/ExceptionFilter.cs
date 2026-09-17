using LuckyDraw.Api.Common;
using LuckyDraw.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LuckyDraw.Api.Filters;

/// <summary>
/// 全局异常过滤器（规范 4.2 / 6.4）：
/// 业务异常 → HTTP 200 + 业务码（D-12）；系统异常 → HTTP 500 + 统一文案「系统内部错误」，
/// 完整堆栈只进日志，绝不外泄。
/// </summary>
public class ExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ExceptionFilter> _logger;

    /// <summary>构造函数注入。</summary>
    public ExceptionFilter(ILogger<ExceptionFilter> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is BusinessException businessException)
        {
            // 只记错误码与路径，不记用户输入（避免密码等敏感信息进日志）
            _logger.LogWarning(
                "业务异常：Code={Code} Path={Path}",
                businessException.Code,
                context.HttpContext.Request.Path.Value);

            context.Result = new ObjectResult(new ApiResult<object?>
            {
                Code = businessException.Code,
                Message = businessException.Message
            })
            {
                StatusCode = businessException.HttpStatusCode
            };

            context.ExceptionHandled = true;
            return;
        }

        _logger.LogError(
            context.Exception,
            "未处理异常：Path={Path} Method={Method}",
            context.HttpContext.Request.Path.Value,
            context.HttpContext.Request.Method);

        context.Result = new ObjectResult(new ApiResult<object?>
        {
            Code = StatusCodes.Status500InternalServerError,
            Message = "系统内部错误"
        })
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };

        context.ExceptionHandled = true;
    }
}
