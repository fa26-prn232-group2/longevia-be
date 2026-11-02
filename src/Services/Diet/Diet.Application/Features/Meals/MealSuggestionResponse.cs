using Diet.Application.Abstractions;

namespace Diet.Application.Features.Meals;

public sealed record MealSuggestionResponse(Guid FoodId, string Name, decimal ServingSize, string ServingUnit,
    decimal Calories, decimal ProteinGrams, decimal CarbsGrams, decimal FatGrams,
    string Description, string Rationale, IReadOnlyList<MealCitationResponse> Citations)
{
    internal static MealSuggestionResponse From(MealSuggestion value) => new(value.FoodId, value.Name,
        value.ServingSize, value.ServingUnit, value.Calories, value.ProteinGrams, value.CarbsGrams, value.FatGrams,
        value.Description, value.Rationale, value.Citations.Select(MealCitationResponse.From).ToArray());
}
