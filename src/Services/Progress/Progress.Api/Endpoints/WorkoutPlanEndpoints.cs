using Progress.Application.Features.WorkoutPlans;

namespace Progress.Api.Endpoints;

public static class WorkoutPlanEndpoints
{
    public static void MapWorkoutPlanEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/workout-plans").WithTags("WorkoutPlans").RequireAuthorization();
        group.MapPost("", async (GenerateWorkoutPlanRequest request, WorkoutPlanUseCases useCases,
            CancellationToken ct) => TypedResults.Ok(await useCases.GenerateAsync(request, ct)))
            .WithName("GenerateWorkoutPlan").WithSummary("Sinh và lưu kế hoạch tập theo tuần");
        group.MapGet("/{weekStartDate}", async (DateOnly weekStartDate, WorkoutPlanUseCases useCases,
            CancellationToken ct) => TypedResults.Ok(await useCases.GetAsync(weekStartDate, ct)))
            .WithName("GetWorkoutPlan").WithSummary("Đọc kế hoạch tập đã lưu theo tuần");
    }
}
