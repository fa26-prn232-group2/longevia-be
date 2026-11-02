using Diet.Application.Features.Meals;
using Longevia.Shared.Validation;

namespace Diet.Api.Endpoints;

public static class MealEndpoints
{
    public static void MapMealEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/meals").WithTags("Meals").RequireAuthorization();
        group.MapPost("", async (LogMealRequest request, MealUseCases useCases, CancellationToken ct) =>
        {
            var result = await useCases.LogAsync(request, ct);
            return TypedResults.Created($"/api/diet/meals/daily-summary?date={result.Date:yyyy-MM-dd}", result);
        }).AddEndpointFilter<ValidationFilter<LogMealRequest>>()
            .WithName("LogMeal").WithSummary("Ghi nhật ký và snapshot dinh dưỡng");
        group.MapGet("/ai-recommendations", async (string mealType, string goal,
            MealRecommendationsUseCase useCase, CancellationToken ct) =>
            TypedResults.Ok(await useCase.ExecuteAsync(mealType, goal, ct)))
            .WithName("GetMealRecommendations").WithSummary("Gợi ý bữa ăn từ profile và phần dinh dưỡng còn lại hôm nay");
        group.MapGet("/daily-summary", async (DateOnly? date, DailySummaryUseCase useCase, CancellationToken ct) =>
            TypedResults.Ok(await useCase.ExecuteAsync(date, ct)))
            .WithName("GetDailyDietSummary").WithSummary("Tổng hợp dinh dưỡng theo ngày, mặc định UTC");
    }
}
