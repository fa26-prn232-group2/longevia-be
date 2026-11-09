using Longevia.Recommendation.Contracts.V1;

namespace Diet.Application.Features.MealPlans;

public sealed record MealPlanCitationResponse(int ChapterNumber, string ChapterTitle, int BookPage,
    int PdfPage, string Quote, bool Verified, string SourceFile);

public sealed record MealPlanWarningResponse(string Code, string Message, string Scope);

public sealed record PlannedFoodResponse(string FoodId, string Name, string ServingSize, string ServingUnit,
    int Calories, double ProteinGrams, double CarbsGrams, double FatGrams, double FiberGrams,
    IReadOnlyList<string> Allergens);

public sealed record PlannedMealResponse(string MealType, int TargetCalories, string Rationale,
    IReadOnlyList<PlannedFoodResponse> Foods, IReadOnlyList<MealPlanCitationResponse> Citations);

public sealed record PlanSuggestionResponse(string Text, IReadOnlyList<MealPlanCitationResponse> Citations);

public sealed record MealPlanDayResponse(string Date, int TargetCalories, double TargetProteinGrams,
    double TargetCarbsGrams, double TargetFatGrams, double TargetFiberGrams, int TotalCalories,
    double TotalProteinGrams, double TotalCarbsGrams, double TotalFatGrams, double TotalFiberGrams,
    bool CalorieCapReached, IReadOnlyList<PlannedMealResponse> Meals,
    IReadOnlyList<PlanSuggestionResponse> Suggestions);

public sealed record MealPlanResponse(Guid Id, string WeekStartDate, int DaysRequested, int MealsPerDay,
    int WeeklyTargetCalories, double WeeklyTargetProteinGrams, double WeeklyTargetCarbsGrams,
    double WeeklyTargetFatGrams, double WeeklyTargetFiberGrams, bool UsedFallback, string Disclaimer,
    IReadOnlyList<MealPlanWarningResponse> Warnings, IReadOnlyList<MealPlanDayResponse> Days,
    DateTimeOffset GeneratedAt);

public sealed record GenerateMealPlanRequest(string WeekStartDate, int Days = 7, int MealsPerDay = 4,
    IReadOnlyList<Guid>? ExcludedFoodIds = null);

internal static class MealPlanMapper
{
    internal static MealPlanCitationResponse Citation(Citation value) => new(value.ChapterNumber,
        value.ChapterTitle, value.BookPage, value.PdfPage, value.Quote, value.Verified, value.SourceFile);

    internal static MealPlanResponse From(Guid id, DateTimeOffset generatedAt, int mealsPerDay,
        GenerateMealPlanResponse value) =>
        new(id, value.WeekStartDate, value.DaysRequested, mealsPerDay,
            value.WeeklyTargetCalories, value.WeeklyTargetProteinGrams, value.WeeklyTargetCarbsGrams,
            value.WeeklyTargetFatGrams, value.WeeklyTargetFiberGrams, value.UsedFallback,
            value.Disclaimer?.Text ?? string.Empty,
            value.Warnings.Select(x => new MealPlanWarningResponse(x.Code, x.Message, x.Scope)).ToArray(),
            value.Days.Select(Day).ToArray(), generatedAt);

    private static MealPlanDayResponse Day(MealPlanDay value) => new(value.Date, value.TargetCalories,
        value.TargetProteinGrams, value.TargetCarbsGrams, value.TargetFatGrams, value.TargetFiberGrams,
        value.TotalCalories, value.TotalProteinGrams, value.TotalCarbsGrams, value.TotalFatGrams,
        value.TotalFiberGrams, value.CalorieCapReached,
        value.Meals.Select(Meal).ToArray(),
        value.Suggestions.Select(x => new PlanSuggestionResponse(x.Text,
            x.Citations.Select(Citation).ToArray())).ToArray());

    private static PlannedMealResponse Meal(PlannedMeal value) => new(value.MealType.ToString(),
        value.TargetCalories, value.Rationale,
        value.Foods.Select(x => new PlannedFoodResponse(x.FoodId, x.Name, x.ServingSize, x.ServingUnit,
            x.Calories, x.ProteinGrams, x.CarbsGrams, x.FatGrams, x.FiberGrams, x.Allergens.ToArray())).ToArray(),
        value.Citations.Select(Citation).ToArray());
}
