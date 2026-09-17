using FluentValidation;
using LuckyDraw.Api.Common;
using LuckyDraw.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LuckyDraw.Api.Filters;

/// <summary>
/// FluentValidation 全局校验过滤器：对动作参数按类型解析 <see cref="IValidator{T}"/>，
/// 校验失败 → HTTP 200 + <c>code=1002</c>（业务层补充校验，docs/error-codes.md）。
/// </summary>
public class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>构造函数注入。</summary>
    public ValidationFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_serviceProvider.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var validationResult = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (!validationResult.IsValid)
            {
                context.Result = new ObjectResult(new ApiResult<object?>
                {
                    Code = ErrorCodes.ValidationFailed,
                    Message = validationResult.Errors[0].ErrorMessage
                })
                {
                    StatusCode = StatusCodes.Status200OK
                };

                return;
            }
        }

        await next();
    }
}
