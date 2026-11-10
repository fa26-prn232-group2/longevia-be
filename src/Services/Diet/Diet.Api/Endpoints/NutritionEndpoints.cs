using Diet.Application.Features.Nutrition;

namespace Diet.Api.Endpoints;

public static class NutritionEndpoints
{
    public static void MapNutritionEndpoints(this WebApplication app)
    {
        app.MapGet("/nutrition/nutrient-gaps", async (int? days, NutrientGapUseCase useCase,
            CancellationToken ct) => TypedResults.Ok(await useCase.ExecuteAsync(days ?? 7, ct)))
            .WithTags("Nutrition").RequireAuthorization()
            .WithName("GetNutrientGaps").WithSummary("Phân tích thiếu hụt vi chất 7 ngày");
    }
}
