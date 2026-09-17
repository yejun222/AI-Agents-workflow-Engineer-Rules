using LuckyDraw.Api.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LuckyDraw.Api.Filters;

/// <summary>
/// 全局结果包装过滤器（规范 4.2）：控制器直接返回业务对象，由本过滤器统一包装为 <see cref="ApiResult{T}"/>。
/// 已是 <see cref="IApiResult"/>（错误路径）的结果不再重复包装。
/// </summary>
public class ResultFilter : IResultFilter
{
    /// <inheritdoc />
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult objectResult || objectResult.Value is IApiResult)
        {
            return;
        }

        var statusCode = objectResult.StatusCode ?? StatusCodes.Status200OK;

        objectResult.Value = new ApiResult<object?>
        {
            Code = statusCode == StatusCodes.Status200OK ? 0 : statusCode,
            Message = statusCode == StatusCodes.Status200OK ? string.Empty : "请求失败",
            Data = objectResult.Value
        };

        objectResult.DeclaredType = typeof(ApiResult<object?>);
    }

    /// <inheritdoc />
    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
