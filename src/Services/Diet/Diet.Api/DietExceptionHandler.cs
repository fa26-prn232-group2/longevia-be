using Longevia.Shared.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Diet.Api;

public sealed class DietExceptionHandler(ILogger<DietExceptionHandler> logger,
    IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var error = exception as AppException;
        var status = error?.Status ?? (exception is BadHttpRequestException badRequest ? badRequest.StatusCode : 500);
        if (status >= 500)
        {
            logger.LogError(exception, "Diet request failed with trace {TraceId}", context.TraceIdentifier);
        }

        context.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = error?.Title ?? (status == 400 ? "Yêu cầu không hợp lệ." : "Lỗi hệ thống."),
            Detail = error?.Detail ?? (status == 400 ? "Dữ liệu không hợp lệ." : "Đã xảy ra lỗi hệ thống."),
            Type = $"https://longevia.local/errors/{error?.Code ?? (status == 400 ? "validation.failed" : "system.internal")}",
            Instance = context.Request.Path,
        };
        problem.Extensions["code"] = error?.Code ?? (status == 400 ? "validation.failed" : "system.internal");
        if (error?.Errors is { Count: > 0 })
        {
            problem.Extensions["errors"] = error.Errors;
        }
        return await problems.TryWriteAsync(new() { HttpContext = context, ProblemDetails = problem });
    }
}
