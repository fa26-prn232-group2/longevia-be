using Longevia.Shared.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Progress.Application.Features.Fasting;

namespace Progress.Api.Endpoints;

public static class FastingEndpoints
{
    public static void MapFastingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/fasting").WithTags("Fasting").RequireAuthorization();

        group.MapPost("/start", async (StartFastingRequest request, FastingUseCases useCases, CancellationToken ct) =>
        {
            var result = await useCases.StartAsync(request, ct);
            return TypedResults.Created($"/api/progress/fasting/current", result);
        })
        .AddEndpointFilter<ValidationFilter<StartFastingRequest>>()
        .WithName("StartFasting")
        .WithSummary("Bắt đầu một phiên nhịn ăn mới");

        group.MapPost("/end", async (EndFastingRequest request, FastingUseCases useCases, CancellationToken ct) =>
        {
            var result = await useCases.EndAsync(request, ct);
            return TypedResults.Ok(result);
        })
        .AddEndpointFilter<ValidationFilter<EndFastingRequest>>()
        .WithName("EndFasting")
        .WithSummary("Kết thúc phiên nhịn ăn hiện tại");

        group.MapGet("/current", async (FastingUseCases useCases, CancellationToken ct) =>
        {
            var result = await useCases.GetCurrentAsync(ct);
            return Results.Json(result);
        })
        .WithName("GetCurrentFasting")
        .WithSummary("Lấy thông tin phiên nhịn ăn đang hoạt động");
    }
}
