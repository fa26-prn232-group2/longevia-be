using Diet.Application.Features.Targets;
using Longevia.Shared.Validation;

namespace Diet.Api.Endpoints;

public static class TargetEndpoints
{
    public static void MapTargetEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/targets").WithTags("Targets").RequireAuthorization();
        group.MapGet("/me", async (TargetUseCases useCases, CancellationToken ct) =>
            TypedResults.Ok(await useCases.GetAsync(ct)))
            .WithName("GetDietTarget").WithSummary("Lấy mục tiêu dinh dưỡng hiện tại");
        group.MapPost("/me/calculate", async (UpdateTargetRequest request, TargetUseCases useCases, CancellationToken ct) =>
            TypedResults.Ok(await useCases.CalculateAsync(request, ct)))
            .AddEndpointFilter<ValidationFilter<UpdateTargetRequest>>()
            .WithName("CalculateDietTarget").WithSummary("Tính BMR/TDEE từ hồ sơ và lưu snapshot cùng mục tiêu");
        group.MapPost("/me/suggest", async (SuggestTargetsRequest request, SuggestTargetsUseCases useCases,
            CancellationToken ct) => TypedResults.Ok(await useCases.SuggestAsync(request, ct)))
            .WithName("SuggestDietTarget")
            .WithSummary("Gợi ý mục tiêu dinh dưỡng bằng AI (không ghi DB, người dùng xác nhận mới lưu)");
        group.MapPut("/me", async (UpdateTargetRequest request, TargetUseCases useCases, CancellationToken ct) =>
            TypedResults.Ok(await useCases.UpdateAsync(request, ct)))
            .AddEndpointFilter<ValidationFilter<UpdateTargetRequest>>()
            .WithName("UpdateDietTarget").WithSummary("Lưu mục tiêu calo và macro nhập tay");
    }
}
