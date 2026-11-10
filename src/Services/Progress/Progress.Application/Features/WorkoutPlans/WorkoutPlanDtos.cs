using Longevia.Recommendation.Contracts.V1;

namespace Progress.Application.Features.WorkoutPlans;

public sealed record WorkoutCitationResponse(int ChapterNumber, string ChapterTitle, int BookPage,
    int PdfPage, string Quote, bool Verified, string SourceFile);

public sealed record WorkoutPlanDayResponse(string Date, bool RestDay, string Activity,
    string ActivityLabel, int DurationMinutes, string Intensity, IReadOnlyList<string> EquipmentUsed,
    string Rationale, IReadOnlyList<WorkoutCitationResponse> Citations);

public sealed record WorkoutWarningResponse(string Code, string Message, string Scope);

public sealed record WorkoutPlanResponse(Guid Id, string WeekStartDate, int DaysRequested,
    int SessionsScheduled, int MinutesPerSession, bool UsedFallback, string UsedFallbackReason,
    string Disclaimer, IReadOnlyList<WorkoutWarningResponse> Warnings,
    IReadOnlyList<WorkoutPlanDayResponse> Days, DateTimeOffset GeneratedAt,
    IReadOnlyList<string> ActivityPreference, IReadOnlyList<string> EquipmentAvailable,
    IReadOnlyList<string> Limitations);

public sealed record GenerateWorkoutPlanRequest(string WeekStartDate, string Goal, int Days = 7,
    int SessionsPerWeek = 3, int MinutesPerSession = 30, IReadOnlyList<string>? ActivityPreference = null,
    IReadOnlyList<string>? EquipmentAvailable = null, IReadOnlyList<string>? Limitations = null);

internal static class WorkoutPlanMapper
{
    private static WorkoutCitationResponse Citation(Citation value) => new(value.ChapterNumber,
        value.ChapterTitle, value.BookPage, value.PdfPage, value.Quote, value.Verified, value.SourceFile);

    internal static WorkoutPlanResponse From(Guid id, DateTimeOffset generatedAt,
        GenerateWorkoutPlanResponse value) =>
        new(id, value.WeekStartDate, value.DaysRequested, value.SessionsScheduled,
            value.MinutesPerSession, value.UsedFallback, value.UsedFallbackReason,
            value.Disclaimer?.Text ?? string.Empty,
            value.Warnings.Select(x => new WorkoutWarningResponse(x.Code, x.Message, x.Scope)).ToArray(),
            value.Days.Select(day => new WorkoutPlanDayResponse(day.Date, day.RestDay, day.Activity,
                day.ActivityLabel, day.DurationMinutes, day.Intensity, day.EquipmentUsed.ToArray(),
                day.Rationale, day.Citations.Select(Citation).ToArray())).ToArray(), generatedAt,
            [], [], []);
}
