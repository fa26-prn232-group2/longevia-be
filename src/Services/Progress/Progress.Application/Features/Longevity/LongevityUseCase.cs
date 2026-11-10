using Longevia.Recommendation.Contracts.V1;
using Longevia.Shared.Auth;
using Longevia.Shared.Errors;
using Progress.Application.Abstractions;
using Progress.Application.Common;
using Progress.Domain.Entities;

namespace Progress.Application.Features.Longevity;

public sealed class LongevityUseCase(IFastingRepository fasting, ISleepRepository sleep, IWaterRepository water,
    IBodyMetricRepository metrics, IUserProfileProvider profiles, INutritionAdherenceProvider adherence,
    ILongevityProvider provider, ICurrentUser currentUser, TimeProvider time)
{
    public async Task<LongevityHabitsResponse> ExecuteAsync(int days, CancellationToken ct)
    {
        if (days is < 1 or > 7)
            throw AppException.BadRequest("days phải nằm trong 1..7.", "progress.invalid_days");
        var userId = currentUser.RequiredId();
        var now = time.GetUtcNow();
        var today = Progress.Domain.UtcCalendar.Day(now);
        var from = today.AddDays(-(days - 1));
        var profile = await profiles.GetAsync(userId, ct);
        var weight = await metrics.GetLatestAsync(userId, ct);

        var sessions = await fasting.GetCompletedSessionsInWeekAsync(userId,
            Progress.Domain.UtcCalendar.StartOfDayUtc(from),
            Progress.Domain.UtcCalendar.StartOfDayUtc(today.AddDays(1)), ct);
        var averageFastingHours = sessions.Count == 0 ? 0 : sessions
            .Where(x => x.EndedAt is not null)
            .Select(x => (x.EndedAt!.Value - x.StartedAt).TotalHours)
            .DefaultIfEmpty(0).Average();

        var sleepLogs = await sleep.GetSleepLogsInDateRangeAsync(userId, from, today, ct);
        var averageSleepHours = sleepLogs.Count == 0 ? 0 : sleepLogs.Average(x => x.DurationHours);

        var waterDays = 0;
        for (var date = from; date <= today; date = date.AddDays(1))
        {
            if (await water.GetTotalConsumedMlAsync(userId, date, ct) >= 2500) waterDays++;
        }

        var adherenceResponse = await adherence.GetAsync(userId, from, today, ct);

        var request = new LongevityHabitRequest
        {
            UserId = userId.ToString("D"),
            AverageFastingHours = averageFastingHours,
            AverageSleepHours = averageSleepHours,
            WaterTargetHitDays = waterDays,
            NutritionAdherenceRate = adherenceResponse.AdherenceRate,
            Profile = new UserProfile
            {
                Age = profile.Age,
                Sex = profile.IsMale ? BiologicalSex.Male : BiologicalSex.Female,
                HeightCm = (double)profile.HeightCm,
                WeightKg = weight is null ? 0 : (double)weight.WeightKg,
                ActivityLevel = profile.ActivityLevel,
                DietaryPreference = profile.DietaryPreference,
            },
        };
        request.Profile.Allergies.AddRange(profile.Allergies);
        var result = await provider.EvaluateAsync(request, now.AddSeconds(30), ct);
        if (string.IsNullOrWhiteSpace(result.Disclaimer?.Text))
            throw new AppException("Điểm thói quen thiếu disclaimer an toàn.", 502,
                "Dịch vụ gợi ý trả dữ liệu không hợp lệ.", "progress.ai_integration_failed");
        return LongevityMapper.From(result);
    }
}
