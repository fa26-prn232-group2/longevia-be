namespace Progress.Application.Features.Dashboard;

public sealed record DashboardSummaryResponse(
    decimal? CurrentWeightKg,
    decimal? InitialWeightKg,
    decimal? WeightDeltaKg,
    decimal? CurrentBmi,
    int CurrentStreakDays,
    int BestStreakDays,
    int FastingThisWeekCompleted,
    double? AverageSleepHours);
