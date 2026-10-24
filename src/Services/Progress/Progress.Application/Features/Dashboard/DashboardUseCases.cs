using Longevia.Shared.Auth;
using Progress.Application.Abstractions;
using Progress.Application.Common;

namespace Progress.Application.Features.Dashboard;

public sealed class DashboardUseCases(
    IBodyMetricRepository metricRepo,
    IFastingRepository fastingRepo,
    ISleepRepository sleepRepo,
    IActivityContributionRepository activityRepo,
    ICurrentUser currentUser,
    TimeProvider time)
{
    public async Task<DashboardSummaryResponse> GetSummaryAsync(CancellationToken ct)
    {
        var userId = currentUser.RequiredId();
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        // 1. Weight & BMI
        var latestMetric = await metricRepo.GetLatestAsync(userId, ct);
        var earliestMetric = await metricRepo.GetEarliestAsync(userId, ct);

        decimal? currentWeight = latestMetric?.WeightKg;
        decimal? initialWeight = earliestMetric?.WeightKg;
        decimal? currentBmi = latestMetric?.Bmi;

        // Weight delta so với tuần trước (bản ghi gần nhất tại/trước now - 7 days)
        decimal? weightDelta = null;
        if (latestMetric != null)
        {
            var cutoff7DaysAgo = now.AddDays(-7);
            var baselineWeekAgo = await metricRepo.GetLatestBeforeDateAsync(userId, cutoff7DaysAgo, ct);
            if (baselineWeekAgo != null)
            {
                weightDelta = Math.Round(latestMetric.WeightKg - baselineWeekAgo.WeightKg, 2);
            }
        }

        // 2. Streaks (Hợp ngày MealLog và ngày BodyMetricLog)
        var metricDates = await metricRepo.GetActiveMetricDatesAsync(userId, ct);
        var mealDates = await activityRepo.GetMealActiveDatesAsync(userId, ct);

        var activeDates = new HashSet<DateOnly>(metricDates);
        foreach (var mealDate in mealDates)
        {
            activeDates.Add(mealDate);
        }

        var (currentStreak, bestStreak) = StreakCalculator.Calculate(activeDates, today);

        // 3. Fasting this week completed
        // Tuần hiện tại tính từ thứ Hai đầu tuần UTC
        var daysFromMonday = ((int)now.DayOfWeek + 6) % 7;
        var startOfWeekUtc = new DateTimeOffset(
            now.Date.AddDays(-daysFromMonday), TimeSpan.Zero);
        var endOfWeekUtc = startOfWeekUtc.AddDays(7).AddTicks(-1);

        var completedFastingSessions = await fastingRepo.GetCompletedSessionsInWeekAsync(
            userId, startOfWeekUtc, endOfWeekUtc, ct);
        var fastingCount = completedFastingSessions.Count;

        // 4. Average sleep hours trong 7 ngày gần nhất
        var sevenDaysAgoDate = today.AddDays(-6);
        var recentSleepLogs = await sleepRepo.GetSleepLogsInDateRangeAsync(
            userId, sevenDaysAgoDate, today, ct);

        double? averageSleep = null;
        if (recentSleepLogs.Count > 0)
        {
            averageSleep = Math.Round(recentSleepLogs.Average(s => s.DurationHours), 2);
        }

        return new DashboardSummaryResponse(
            currentWeight,
            initialWeight,
            weightDelta,
            currentBmi,
            currentStreak,
            bestStreak,
            fastingCount,
            averageSleep);
    }
}
