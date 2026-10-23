using FluentValidation;
using Longevia.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Longevia.Shared.Validation;

public sealed class ValidationFilter<TRequest> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var validator = ctx.HttpContext.RequestServices.GetService<IValidator<TRequest>>();
        if (validator is null)
        {
            return await next(ctx);
        }

        var argument = ctx.Arguments.OfType<TRequest>().FirstOrDefault();
        if (argument is null)
        {
            return await next(ctx);
        }

        var result = await validator.ValidateAsync(argument, ctx.HttpContext.RequestAborted);
        if (!result.IsValid)
        {
            var errors = result.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            throw AppException.BadRequest(
                "Dữ liệu không hợp lệ.",
                "validation.failed",
                errors);
        }

        return await next(ctx);
    }
}
