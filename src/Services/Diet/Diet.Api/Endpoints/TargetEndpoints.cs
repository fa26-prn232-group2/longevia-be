using Diet.Application.Features.Targets;
using Longevia.Shared.Validation;

namespace Diet.Api.Endpoints;

public static class TargetEndpoints
{
    public static void MapTargetEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/targets/me").WithTags("Targets").RequireAuthorization();
        group.MapGet("", async (TargetUseCases useCases, CancellationToken ct) =>
            TypedResults.Ok(await useCases.GetAsync(ct)))
            .WithName("GetDietTarget").WithSummary("Lấy mục tiêu dinh dưỡng hiện tại");
        group.MapPost("/calculate", async (UpdateTargetRequest request, TargetUseCases useCases, CancellationToken ct) =>
            TypedResults.Ok(await useCases.CalculateAsync(request, ct)))
            .AddEndpointFilter<ValidationFilter<UpdateTargetRequest>>()
            .WithName("CalculateDietTarget").WithSummary("Tính BMR/TDEE từ hồ sơ và lưu snapshot cùng mục tiêu");
        group.MapPut("", async (UpdateTargetRequest request, TargetUseCases useCases, CancellationToken ct) =>
            TypedResults.Ok(await useCases.UpdateAsync(request, ct)))
            .AddEndpointFilter<ValidationFilter<UpdateTargetRequest>>()
            .WithName("UpdateDietTarget").WithSummary("Lưu mục tiêu calo và macro nhập tay");
    }
}
