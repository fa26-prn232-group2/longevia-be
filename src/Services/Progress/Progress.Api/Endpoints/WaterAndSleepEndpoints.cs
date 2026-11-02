using Longevia.Shared.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Progress.Application.Features.Sleep;
using Progress.Application.Features.Water;

namespace Progress.Api.Endpoints;

public static class WaterEndpoints
{
    public static void MapWaterEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/water").WithTags("Water").RequireAuthorization();

        group.MapPost("/log", async (LogWaterRequest request, WaterUseCases useCases, CancellationToken ct) =>
        {
            var result = await useCases.LogAsync(request, ct);
            return TypedResults.Ok(result);
        })
        .AddEndpointFilter<ValidationFilter<LogWaterRequest>>()
        .WithName("LogWater")
        .WithSummary("Ghi nhận lượng nước uống vào");

        group.MapGet("/today", async (WaterUseCases useCases, CancellationToken ct) =>
        {
            var result = await useCases.GetTodayAsync(ct);
            return TypedResults.Ok(result);
        })
        .WithName("GetWaterToday")
        .WithSummary("Lấy tổng lượng nước uống hôm nay");
    }
}

public static class SleepEndpoints
{
    public static void MapSleepEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/sleep").WithTags("Sleep").RequireAuthorization();

        group.MapPost("/log", async (LogSleepRequest request, SleepUseCases useCases, CancellationToken ct) =>
        {
            var result = await useCases.LogAsync(request, ct);
            return TypedResults.Created($"/api/progress/sleep/{result.Id}", result);
        })
        .AddEndpointFilter<ValidationFilter<LogSleepRequest>>()
        .WithName("LogSleep")
        .WithSummary("Ghi nhận thông tin giấc ngủ");
    }
}
