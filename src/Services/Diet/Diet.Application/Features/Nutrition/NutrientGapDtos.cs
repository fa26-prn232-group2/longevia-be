namespace Diet.Application.Features.Nutrition;

using Diet.Application.Features.Meals;

public sealed record NutrientGapItemResponse(string Nutrient, string Unit, double Target,
    double AverageIntake, double Deficit, string Severity);

public sealed record NutrientGapResponse(IReadOnlyList<NutrientGapItemResponse> Gaps,
    IReadOnlyList<string> DeficientNutrients, IReadOnlyList<string> SuggestedFoods,
    string AnalysisSummary, IReadOnlyList<MealCitationResponse> Citations, string Disclaimer,
    IReadOnlyList<string> DataWarnings, int DaysWithData);

internal static class NutrientGapMapper
{
    internal static NutrientGapResponse From(Longevia.Recommendation.Contracts.V1.NutrientGapResponse value,
        IReadOnlyList<string> warnings, int daysWithData) =>
        new(value.Gaps.Select(x => new NutrientGapItemResponse(x.Nutrient, x.Unit, x.Target,
                x.AverageIntake, x.Deficit, x.Severity)).ToArray(),
            value.DeficientNutrients.ToArray(), value.SuggestedFoods.ToArray(), value.AnalysisSummary,
            value.Citations.Select(x => new MealCitationResponse(x.ChapterNumber, x.ChapterTitle,
                x.BookPage, x.PdfPage, x.Quote, x.Verified, x.SourceFile)).ToArray(),
            value.Disclaimer?.Text ?? string.Empty, warnings, daysWithData);
}
