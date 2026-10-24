using Longevia.Shared.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Progress.Api;

public sealed class ProgressExceptionHandler(ILogger<ProgressExceptionHandler> logger,
    IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var error = exception as AppException;
        var status = error?.Status ?? (exception is BadHttpRequestException badRequest ? badRequest.StatusCode : 500);
        if (status >= 500)
        {
            // Exceptions may contain health data or connection details; keep operational logs metadata-only.
            logger.LogError("Progress request failed with trace {TraceId}, type {ExceptionType}",
                context.TraceIdentifier, exception.GetType().Name);
        }
        context.Response.StatusCode = status;
        var code = error?.Code ?? (status == 400 ? "validation.failed" : "system.internal");
        var problem = new ProblemDetails
        {
            Status = status,
            Title = error?.Title ?? (status == 400 ? "Yêu cầu không hợp lệ." : "Lỗi hệ thống."),
            Detail = error?.Detail ?? "Đã xảy ra lỗi khi xử lý yêu cầu.",
            Type = $"https://longevia.local/errors/{code}",
            Instance = context.Request.Path,
        };
        problem.Extensions["code"] = code;
        if (error?.Errors is { Count: > 0 })
        {
            problem.Extensions["errors"] = error.Errors;
        }
        return await problems.TryWriteAsync(new() { HttpContext = context, ProblemDetails = problem });
    }
}
