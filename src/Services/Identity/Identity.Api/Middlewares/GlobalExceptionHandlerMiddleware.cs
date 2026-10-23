using System.Text.Json;
using Longevia.Shared.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Middlewares;

public sealed class GlobalExceptionHandlerMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlerMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            await HandleAppExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception occurred during request execution.");
            await HandleGenericExceptionAsync(context);
        }
    }

    private static async Task HandleAppExceptionAsync(HttpContext context, AppException ex)
    {
        context.Response.StatusCode = ex.Status;
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Status = ex.Status,
            Title = ex.Title,
            Detail = ex.Message,
            Instance = context.Request.Path,
            Type = $"https://longevia.local/errors/{ex.Code.Replace('.', '-')}",
        };

        problemDetails.Extensions["code"] = ex.Code;

        if (ex.Errors is { Count: > 0 })
        {
            problemDetails.Extensions["errors"] = ex.Errors;
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, JsonOptions));
    }

    private static async Task HandleGenericExceptionAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Lỗi hệ thống",
            Detail = "Đã xảy ra lỗi hệ thống không mong muốn.",
            Instance = context.Request.Path,
            Type = "https://longevia.local/errors/system-internal",
        };

        problemDetails.Extensions["code"] = "system.internal";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, JsonOptions));
    }
}
