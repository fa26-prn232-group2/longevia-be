using Diet.Application.Features.MealPlans;

namespace Diet.Api.Endpoints;

public static class MealPlanEndpoints
{
    public static void MapMealPlanEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/meal-plans").WithTags("MealPlans").RequireAuthorization();
        group.MapPost("", async (GenerateMealPlanRequest request, MealPlanUseCases useCases,
            CancellationToken ct) =>
        {
            var result = await useCases.GenerateAsync(request, ct);
            return TypedResults.Created($"/api/diet/meal-plans/{result.WeekStartDate}", result);
        }).WithName("GenerateMealPlan").WithSummary("Sinh và lưu kế hoạch ăn uống theo tuần");
        group.MapGet("/{weekStartDate}", async (DateOnly weekStartDate, MealPlanUseCases useCases,
            CancellationToken ct) => TypedResults.Ok(await useCases.GetAsync(weekStartDate, ct)))
            .WithName("GetMealPlan").WithSummary("Đọc kế hoạch ăn uống đã lưu theo tuần");
    }
}
