namespace Diet.Application.Features.Meals;

public sealed record MealRecommendationsResponse(decimal RemainingCalories,
    IReadOnlyList<MealSuggestionResponse> RecommendedMeals, string LongevityTip,
    IReadOnlyList<MealCitationResponse> TipCitations, string Disclaimer, bool CalorieCapReached, string SafetyNote);
