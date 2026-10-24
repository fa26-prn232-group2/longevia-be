using Longevia.Shared.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Progress.Application.Features.Metrics;

namespace Progress.Api.Endpoints;

public static class MetricEndpoints
{
    public static void MapMetricEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/metrics").WithTags("Metrics").RequireAuthorization();

        group.MapPost("", async (LogBodyMetricRequest request, MetricUseCases useCases, CancellationToken ct) =>
        {
            var result = await useCases.LogAsync(request, ct);
            return TypedResults.Created($"/api/progress/metrics/{result.Id}", result);
        })
        .AddEndpointFilter<ValidationFilter<LogBodyMetricRequest>>()
        .WithName("LogBodyMetric")
        .WithSummary("Ghi nhận chỉ số cơ thể và tính snapshot BMI");

        group.MapGet("/history", async (
            DateOnly? fromDate,
            DateOnly? toDate,
            int? pageNumber,
            int? pageSize,
            MetricUseCases useCases,
            CancellationToken ct) =>
        {
            var result = await useCases.GetHistoryAsync(
                fromDate, toDate, pageNumber ?? 1, pageSize ?? 20, ct);
            return TypedResults.Ok(result);
        })
        .WithName("GetMetricsHistory")
        .WithSummary("Tra cứu lịch sử chỉ số cơ thể có phân trang");
    }
}
