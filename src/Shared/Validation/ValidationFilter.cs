using System.Text.Json;
using FluentValidation;
using Longevia.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Longevia.Shared.Validation;

public sealed class ValidationFilter<TRequest>(
    int statusCode = StatusCodes.Status400BadRequest,
    string errorCode = "validation.failed") : IEndpointFilter
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
                .Select(e => new FieldError(
                    JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName),
                    e.ErrorMessage))
                .ToList();

            var exception = statusCode switch
            {
                StatusCodes.Status400BadRequest => AppException.BadRequest(
                    "Dữ liệu không hợp lệ.", errorCode, errors),
                StatusCodes.Status422UnprocessableEntity => AppException.Unprocessable(
                    "Dữ liệu không hợp lệ.", errorCode, errors),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(statusCode), statusCode, "Validation status must be 400 or 422."),
            };

            throw exception;
        }

        return await next(ctx);
    }
}
