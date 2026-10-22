namespace Longevia.Worker.Templates;

/// <summary>
/// Pre-aggregated weekly numbers produced by the tracking service and carried
/// in the event payload. The Worker renders these values only — it MUST NOT
/// compute calories, fasting hours, or streaks itself.
/// </summary>
/// <param name="WeekLabel">Human-readable week label, e.g. "20/10 – 26/10".</param>
/// <param name="AverageCalories">Average kcal per day, pre-computed by the producer.</param>
/// <param name="FastingHours">Total fasting hours in the week, pre-computed by the producer.</param>
/// <param name="StreakDays">Current streak length in days, pre-computed by the producer.</param>
public sealed record WeeklyDigestData(
    string WeekLabel,
    double AverageCalories,
    double FastingHours,
    int StreakDays);
