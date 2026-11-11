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

        group.MapGet("/history", async (int? days, WaterUseCases useCases, CancellationToken ct) =>
            TypedResults.Ok(await useCases.GetHistoryAsync(days ?? 30, ct)))
            .WithName("GetWaterHistory")
            .WithSummary("Lịch sử uống nước theo ngày (1..90 ngày, cũ → mới)");

        group.MapDelete("/{id:guid}", async (Guid id, WaterUseCases useCases, CancellationToken ct) =>
            TypedResults.Ok(await useCases.UndoAsync(id, ct)))
            .WithName("UndoWaterLog")
            .WithSummary("Hoàn tác một lần ghi nước của chính người dùng");
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

        group.MapGet("/today", async (SleepUseCases useCases, CancellationToken ct) =>
        {
            var result = await useCases.GetTodayAsync(ct);
            IResult response = result is null ? TypedResults.NoContent() : TypedResults.Ok(result);
            return response;
        })
        .Produces(StatusCodes.Status204NoContent)
        .WithName("GetTodaySleep")
        .WithSummary("Lấy giấc ngủ đêm qua (ngày thức dậy hôm nay; 204 nếu chưa có)");

        group.MapPut("/{id:guid}", async (Guid id, LogSleepRequest request, SleepUseCases useCases,
            CancellationToken ct) =>
            TypedResults.Ok(await useCases.UpdateAsync(id, request, ct)))
        .AddEndpointFilter<ValidationFilter<LogSleepRequest>>()
        .WithName("UpdateSleepLog")
        .WithSummary("Sửa bản ghi giấc ngủ của bạn (404 nếu không sở hữu)");

        group.MapDelete("/{id:guid}", async (Guid id, SleepUseCases useCases, CancellationToken ct) =>
        {
            await useCases.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        })
        .WithName("DeleteSleepLog")
        .WithSummary("Xoá bản ghi giấc ngủ của bạn");

        group.MapGet("/history", async (int? page, int? pageSize, SleepUseCases useCases,
            CancellationToken ct) =>
            TypedResults.Ok(await useCases.GetHistoryAsync(page ?? 1, pageSize ?? 10, ct)))
            .WithName("GetSleepHistory")
            .WithSummary("Lịch sử giấc ngủ (phân trang, mới nhất trước)");
    }
}
