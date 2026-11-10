using Longevia.Recommendation.Contracts.V1;
using Progress.Application.Abstractions;
using Progress.Domain.Entities;

namespace Progress.Application.Features.Longevity;

public sealed record LongevityComponentResponse(string Key, int Score, int Weight, string Reason);
public sealed record LongevityCitationResponse(int ChapterNumber, string ChapterTitle, int BookPage,
    int PdfPage, string Quote, bool Verified, string SourceFile);
public sealed record LongevityHabitsResponse(int LongevityScore,
    IReadOnlyList<string> ActionableInsights, IReadOnlyList<LongevityComponentResponse> ScoreBreakdown,
    IReadOnlyList<LongevityCitationResponse> Citations, string Disclaimer);

internal static class LongevityMapper
{
    internal static LongevityHabitsResponse From(LongevityHabitResponse value) =>
        new(value.LongevityScore, value.ActionableInsights.ToArray(),
            value.ScoreBreakdown.Select(x => new LongevityComponentResponse(x.Key, x.Score, x.Weight,
                x.Reason)).ToArray(),
            value.Citations.Select(x => new LongevityCitationResponse(x.ChapterNumber, x.ChapterTitle,
                x.BookPage, x.PdfPage, x.Quote, x.Verified, x.SourceFile)).ToArray(),
            value.Disclaimer?.Text ?? string.Empty);
}
