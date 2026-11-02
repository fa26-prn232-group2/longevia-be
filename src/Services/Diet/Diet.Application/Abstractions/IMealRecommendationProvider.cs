using Diet.Domain.Entities;

namespace Diet.Application.Abstractions;

public sealed record MealProfile(int Age, bool IsMale, decimal HeightCm, decimal WeightKg,
    string ActivityLevel, string DietaryPreference, IReadOnlyList<string> Allergies);
public sealed record MealRecommendationInput(Guid UserId, int MealType, int Goal, MealProfile Profile,
    int ConsumedCalories, int TargetCalories, decimal ProteinRemaining, decimal CarbsRemaining,
    decimal FatRemaining, IReadOnlyList<FoodItem> Foods);
public sealed record MealCitation(int ChapterNumber, string ChapterTitle, int BookPage,
    int PdfPage, string Quote, bool Verified, string SourceFile);
public sealed record MealSuggestion(Guid FoodId, string Name, decimal ServingSize, string ServingUnit,
    decimal Calories, decimal ProteinGrams, decimal CarbsGrams, decimal FatGrams,
    string Description, string Rationale, IReadOnlyList<MealCitation> Citations);
public sealed record MealRecommendationResult(IReadOnlyList<MealSuggestion> Meals, string LongevityTip,
    IReadOnlyList<MealCitation> TipCitations, string Disclaimer, bool CalorieCapReached, string SafetyNote);

public interface IMealRecommendationProvider
{
    Task<MealRecommendationResult> GetAsync(MealRecommendationInput input,
        DateTimeOffset deadline, CancellationToken ct);
}
