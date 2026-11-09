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
            // Chưa có phiên là trạng thái hợp lệ: 204 để client phân biệt với lỗi
            // và không phải parse body rỗng.
            IResult response = result is null ? TypedResults.NoContent() : TypedResults.Ok(result);
            return response;
        })
        .Produces(StatusCodes.Status204NoContent)
        .WithName("GetCurrentFasting")
        .WithSummary("Lấy thông tin phiên nhịn ăn đang hoạt động (204 khi chưa có phiên)");

        group.MapGet("/history", async (int? page, int? pageSize, FastingUseCases useCases,
            CancellationToken ct) =>
            TypedResults.Ok(await useCases.GetHistoryAsync(page ?? 1, pageSize ?? 20, ct)))
            .WithName("GetFastingHistory")
            .WithSummary("Lịch sử các phiên nhịn ăn đã kết thúc (phân trang, mới nhất trước)");
    }
}
