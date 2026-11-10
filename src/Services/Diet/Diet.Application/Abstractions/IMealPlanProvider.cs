using Diet.Domain.Entities;
using Longevia.Recommendation.Contracts.V1;

namespace Diet.Application.Abstractions;

public interface IMealPlanProvider
{
    Task<GenerateMealPlanResponse> GenerateAsync(MealPlanInput input, DateTimeOffset deadline,
        CancellationToken ct);
}

public sealed record MealPlanInput(Guid UserId, MealProfile Profile, int TargetCalories,
    double TargetProteinGrams, double TargetCarbsGrams, double TargetFatGrams, double TargetFiberGrams,
    string WeekStartDate, int Days, int MealsPerDay, IReadOnlyList<FoodItem> Foods,
    IReadOnlyList<Guid> ExcludedFoodIds, int ConsumedCalories, string? ConsumedDate);
